using System.Threading.Channels;
using ArtGrader.Core.Ai;
using ArtGrader.Core.Domain;
using ArtGrader.Core.Images;
using ArtGrader.Web.Data;
using Microsoft.EntityFrameworkCore;
using static ArtGrader.Core.Localization.Texts;

namespace ArtGrader.Web.Services;

public sealed class GradingEvents
{
    public event Action<Guid, Guid>? SubmissionChanged;

    public void Raise(Guid examId, Guid submissionId) => SubmissionChanged?.Invoke(examId, submissionId);
}

public sealed class GradingQueue(
    IDbContextFactory<AppDbContext> dbFactory,
    ExamRepository repository,
    FileStore files,
    SubmissionGradingService grading,
    GradingEvents events,
    IAiSettingsSource settings,
    ILogger<GradingQueue> logger) : BackgroundService
{
    private const int Parallelism = 3;
    private readonly Channel<(Guid ExamId, Guid SubmissionId)> channel = Channel.CreateUnbounded<(Guid, Guid)>();
    private readonly SemaphoreSlim localModelGate = new(1, 1);
    private int active;

    public bool IsBusy => channel.Reader.Count > 0 || Volatile.Read(ref active) > 0;

    public async Task EnqueueAsync(Guid examId, IEnumerable<Guid> submissionIds)
    {
        foreach (var id in submissionIds)
        {
            await repository.SetStatusAsync(id, SubmissionStatus.InWarteschlange);
            events.Raise(examId, id);
            await channel.Writer.WriteAsync((examId, id));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await repository.ResetInterruptedAsync();
        var workers = Enumerable.Range(0, Parallelism).Select(_ => WorkAsync(stoppingToken));
        await Task.WhenAll(workers);
    }

    private async Task WorkAsync(CancellationToken stoppingToken)
    {
        await foreach (var (examId, submissionId) in channel.Reader.ReadAllAsync(stoppingToken))
        {
            Interlocked.Increment(ref active);
            try
            {
                await GradeAsync(examId, submissionId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is ModelException or UnsupportedImageException or IOException)
            {
                logger.LogWarning(ex, "Grading of submission {SubmissionId} failed", submissionId);
                await repository.SetStatusAsync(submissionId, SubmissionStatus.Fehler, ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while grading submission {SubmissionId}", submissionId);
                await repository.SetStatusAsync(submissionId, SubmissionStatus.Fehler, T("Unexpected error: {0}", ex.Message));
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
            events.Raise(examId, submissionId);
        }
    }

    private async Task GradeAsync(Guid examId, Guid submissionId, CancellationToken cancellationToken)
    {
        var exam = await repository.GetAsync(examId);
        var submission = exam?.Submissions.FirstOrDefault(s => s.Id == submissionId);
        if (exam is null || submission is null || submission.Status != SubmissionStatus.InWarteschlange)
        {
            return;
        }

        var local = RoutingModelGateway.RunsLocally(settings.Current(), exam.LocalOnly);
        if (local)
        {
            await localModelGate.WaitAsync(cancellationToken);
        }
        try
        {
            await GradeLockedAsync(exam, submission, cancellationToken);
        }
        finally
        {
            if (local)
            {
                localModelGate.Release();
            }
        }
    }

    private async Task GradeLockedAsync(Exam exam, Submission submission, CancellationToken cancellationToken)
    {
        var examId = exam.Id;
        var submissionId = submission.Id;
        await repository.SetStatusAsync(submissionId, SubmissionStatus.WirdBewertet);
        events.Raise(examId, submissionId);

        var images = new List<byte[]>();
        foreach (var image in submission.Images)
        {
            images.Add(ImageCover.Apply(await files.ReadAsync(image.RelativePath, cancellationToken), image.Covers, image.Crop));
        }

        var exercisePages = new List<byte[]>();
        foreach (var page in exam.PagesOf(DocumentKind.Aufgabe))
        {
            exercisePages.Add(ImageCover.Apply(await files.ReadAsync(page.RelativePath, cancellationToken), page.Covers, page.Crop));
        }

        var lessonPages = new List<byte[]>();
        foreach (var page in exam.PagesOf(DocumentKind.Unterricht))
        {
            lessonPages.Add(ImageCover.Apply(await files.ReadAsync(page.RelativePath, cancellationToken), page.Covers, page.Crop));
        }

        var suggestion = await grading.GradeAsync(GradingContext.From(exam, exercisePages, lessonPages), images, cancellationToken);
        await StoreAsync(submissionId, suggestion);
    }

    private async Task StoreAsync(Guid submissionId, GradingSuggestion suggestion)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var submission = await db.Submissions.Include(s => s.Assessments).FirstAsync(s => s.Id == submissionId);
        foreach (var item in suggestion.Criteria)
        {
            var assessment = submission.Assessments.FirstOrDefault(a => a.CriterionId == item.CriterionId);
            if (assessment is null)
            {
                assessment = new CriterionAssessment { SubmissionId = submissionId, CriterionId = item.CriterionId };
                db.Assessments.Add(assessment);
            }
            assessment.AiLevel = item.Level;
            assessment.AiJustification = item.Justification;
            assessment.AiConfidence = item.Confidence;
        }
        submission.AiDescription = suggestion.Description;
        submission.AiOverallComment = suggestion.OverallComment;
        submission.AiStrengths = string.Join("\n", suggestion.Strengths);
        submission.AiNextSteps = string.Join("\n", suggestion.NextSteps);
        submission.ModelId = suggestion.Model;
        submission.GradedAt = DateTimeOffset.Now;
        submission.Status = SubmissionStatus.Vorschlag;
        submission.ErrorMessage = null;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Graded submission {SubmissionId} with {Model}: input {Input}, output {Output}, cache read {CacheRead}, cache write {CacheWrite}",
            submissionId,
            suggestion.Model,
            suggestion.Usage.InputTokens,
            suggestion.Usage.OutputTokens,
            suggestion.Usage.CacheReadTokens,
            suggestion.Usage.CacheWriteTokens);
    }
}
