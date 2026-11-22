using ArtGrader.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtGrader.Web.Data;

public sealed class ExamRepository(IDbContextFactory<AppDbContext> factory)
{
    public async Task<List<ExamSummary>> ListAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var exams = await db.Exams.AsNoTracking()
            .Select(e => new ExamSummary(
                e.Id,
                e.Title,
                e.SchoolClass,
                e.Date,
                e.Submissions.Count,
                e.Submissions.Count(s => s.Status == SubmissionStatus.Abgeschlossen),
                e.Submissions.SelectMany(s => s.Images).Count(),
                new List<string>()))
            .ToListAsync();

        var examIds = exams.Select(e => e.Id).ToList();
        var covers = await db.Images.AsNoTracking()
            .Join(db.Submissions, i => i.SubmissionId, s => s.Id, (i, s) => new { s.ExamId, s.CreatedAt, i.Order, i.RelativePath })
            .Where(x => examIds.Contains(x.ExamId) && x.Order == 0)
            .ToListAsync();
        foreach (var exam in exams)
        {
            exam.Covers.AddRange(covers.Where(c => c.ExamId == exam.Id).OrderBy(c => c.CreatedAt).Take(3).Select(c => c.RelativePath));
        }
        return exams.OrderByDescending(e => e.Date).ThenBy(e => e.Title).ToList();
    }

    public async Task<Exam?> GetAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var exam = await db.Exams.AsNoTracking()
            .Include(e => e.Categories).ThenInclude(c => c.Criteria)
            .Include(e => e.Submissions).ThenInclude(s => s.Images)
            .Include(e => e.Submissions).ThenInclude(s => s.Assessments)
            .Include(e => e.Pages)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id);
        if (exam is not null)
        {
            exam.Categories = exam.Categories.OrderBy(c => c.Order).ToList();
            foreach (var category in exam.Categories)
            {
                category.Criteria = category.Criteria.OrderBy(k => k.Order).ToList();
            }
            exam.Pages = exam.Pages.OrderBy(p => p.Kind).ThenBy(p => p.Order).ToList();
            exam.Submissions = exam.Submissions.OrderBy(s => s.StudentName, StringComparer.CurrentCultureIgnoreCase).ToList();
            foreach (var submission in exam.Submissions)
            {
                submission.Images = submission.Images.OrderBy(i => i.Order).ToList();
            }
        }
        return exam;
    }

    public async Task<Exam> CreateAsync(string title, string schoolClass, DateOnly date)
    {
        await using var db = await factory.CreateDbContextAsync();
        var exam = new Exam { Title = title.Trim(), SchoolClass = schoolClass.Trim(), Date = date };
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        return exam;
    }

    /// <summary>Saves title, class and date from the page header. An empty title keeps the current one.</summary>
    public async Task<string> SaveExamHeaderAsync(Guid examId, string title, string schoolClass, DateOnly date)
    {
        await using var db = await factory.CreateDbContextAsync();
        var exam = await db.Exams.FirstAsync(e => e.Id == examId);
        if (!string.IsNullOrWhiteSpace(title))
        {
            exam.Title = title.Trim();
        }
        exam.SchoolClass = schoolClass.Trim();
        exam.Date = date;
        await db.SaveChangesAsync();
        return exam.Title;
    }

    public async Task UpdateGradingAsync(Guid examId, bool localOnly, GradingSettings settings)
    {
        await using var db = await factory.CreateDbContextAsync();
        var exam = await db.Exams.FirstAsync(e => e.Id == examId);
        exam.LocalOnly = localOnly;
        exam.Settings = new GradingSettings
        {
            PointsUebertroffen = settings.PointsUebertroffen,
            PointsErfuellt = settings.PointsErfuellt,
            PointsTeilweise = settings.PointsTeilweise,
            PointsNicht = settings.PointsNicht,
            RoundingStep = settings.RoundingStep,
            MinGrade = settings.MinGrade,
            MaxGrade = settings.MaxGrade,
        };
        await db.SaveChangesAsync();
    }

    public async Task SaveRubricAsync(Guid examId, IReadOnlyList<RubricCategory> categories)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Categories.Include(c => c.Criteria).Where(c => c.ExamId == examId).ToListAsync();

        var keepCategoryIds = categories.Select(c => c.Id).ToHashSet();
        var keepCriterionIds = categories.SelectMany(c => c.Criteria).Select(k => k.Id).ToHashSet();
        db.Criteria.RemoveRange(existing.SelectMany(c => c.Criteria).Where(k => !keepCriterionIds.Contains(k.Id)));
        db.Categories.RemoveRange(existing.Where(c => !keepCategoryIds.Contains(c.Id)));

        var existingCategories = existing.ToDictionary(c => c.Id);
        var existingCriteria = existing.SelectMany(c => c.Criteria).ToDictionary(k => k.Id);
        for (var i = 0; i < categories.Count; i++)
        {
            var source = categories[i];
            if (!existingCategories.TryGetValue(source.Id, out var category))
            {
                category = new RubricCategory { Id = source.Id, ExamId = examId };
                db.Categories.Add(category);
            }
            category.Name = source.Name.Trim();
            category.Order = i;

            for (var j = 0; j < source.Criteria.Count; j++)
            {
                var sourceCriterion = source.Criteria[j];
                if (!existingCriteria.TryGetValue(sourceCriterion.Id, out var criterion))
                {
                    criterion = new RubricCriterion { Id = sourceCriterion.Id };
                    db.Criteria.Add(criterion);
                }
                criterion.CategoryId = category.Id;
                criterion.Text = sourceCriterion.Text.Trim();
                criterion.Weight = sourceCriterion.Weight;
                criterion.AssessedBy = sourceCriterion.AssessedBy;
                criterion.Order = j;
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<string>> DeletePhotosAsync(IReadOnlyCollection<Guid> examIds)
    {
        await using var db = await factory.CreateDbContextAsync();
        var images = await db.Images
            .Where(i => db.Submissions.Any(s => s.Id == i.SubmissionId && examIds.Contains(s.ExamId)))
            .ToListAsync();
        db.Images.RemoveRange(images);
        await db.SaveChangesAsync();
        return images.Select(i => i.RelativePath).ToList();
    }

    public async Task DeleteExamAsync(Guid id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Exams.Where(e => e.Id == id).ExecuteDeleteAsync();
    }

    public async Task<Submission> AddSubmissionAsync(Guid examId, string? studentName, IReadOnlyList<SubmissionImage> images)
    {
        await using var db = await factory.CreateDbContextAsync();
        var submission = new Submission { ExamId = examId };
        await ApplyNameAsync(db, submission, studentName);
        foreach (var (image, index) in images.Select((img, i) => (img, i)))
        {
            image.SubmissionId = submission.Id;
            image.Order = index;
            submission.Images.Add(image);
        }
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();
        return submission;
    }

    public async Task AddImagesAsync(Guid submissionId, IReadOnlyList<SubmissionImage> images)
    {
        await using var db = await factory.CreateDbContextAsync();
        var next = await db.Images.Where(i => i.SubmissionId == submissionId).Select(i => (int?)i.Order).MaxAsync() ?? -1;
        foreach (var image in images)
        {
            image.SubmissionId = submissionId;
            image.Order = ++next;
            db.Images.Add(image);
        }
        await db.SaveChangesAsync();
    }

    public async Task<SubmissionImage?> GetImageAsync(Guid imageId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Images.AsNoTracking().FirstOrDefaultAsync(i => i.Id == imageId);
    }

    public async Task SetImageEditsAsync(Guid imageId, IReadOnlyList<CoverArea> covers, CoverArea? crop)
    {
        await using var db = await factory.CreateDbContextAsync();
        var image = await db.Images.FirstAsync(i => i.Id == imageId);
        image.Covers = covers.Select(Copy).ToList();
        image.Crop = crop is null ? null : Copy(crop);
        await db.SaveChangesAsync();
    }

    public async Task SetPageEditsAsync(Guid pageId, IReadOnlyList<CoverArea> covers, CoverArea? crop)
    {
        await using var db = await factory.CreateDbContextAsync();
        var page = await db.Pages.FirstAsync(p => p.Id == pageId);
        page.Covers = covers.Select(Copy).ToList();
        page.Crop = crop is null ? null : Copy(crop);
        await db.SaveChangesAsync();
    }

    public async Task<DocumentPage?> GetPageAsync(Guid pageId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pageId);
    }

    public async Task<List<string>> SetDocumentAsync(Guid examId, DocumentKind kind, StoredFile? file, string text, IReadOnlyList<string> pagePaths)
    {
        await using var db = await factory.CreateDbContextAsync();
        var exam = await db.Exams.FirstAsync(e => e.Id == examId);
        switch (kind)
        {
            case DocumentKind.Aufgabe:
                exam.ExerciseFile = file;
                exam.ExerciseText = text;
                break;
            case DocumentKind.Beurteilung:
                exam.RubricFile = file;
                exam.RubricText = text;
                break;
            default:
                exam.LessonFile = file;
                exam.LessonText = text;
                break;
        }
        var old = await db.Pages.Where(p => p.ExamId == examId && p.Kind == kind).ToListAsync();
        db.Pages.RemoveRange(old);
        db.Pages.AddRange(pagePaths.Select((path, index) => new DocumentPage { ExamId = examId, Kind = kind, Order = index, RelativePath = path }));
        await db.SaveChangesAsync();
        return old.Select(p => p.RelativePath).ToList();
    }

    public async Task SetDocumentTextAsync(Guid examId, DocumentKind kind, string text)
    {
        await using var db = await factory.CreateDbContextAsync();
        var exam = await db.Exams.FirstAsync(e => e.Id == examId);
        switch (kind)
        {
            case DocumentKind.Aufgabe:
                exam.ExerciseText = text;
                break;
            case DocumentKind.Beurteilung:
                exam.RubricText = text;
                break;
            default:
                exam.LessonText = text;
                break;
        }
        await db.SaveChangesAsync();
    }

    private static CoverArea Copy(CoverArea area) => new() { X = area.X, Y = area.Y, Width = area.Width, Height = area.Height };

    public async Task<List<SubmissionImage>> RemoveImageAsync(Guid imageId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var image = await db.Images.FirstOrDefaultAsync(i => i.Id == imageId);
        if (image is null)
        {
            return [];
        }
        db.Images.Remove(image);
        await db.SaveChangesAsync();
        return [image];
    }

    public async Task MergeSubmissionsAsync(Guid targetId, IReadOnlyList<Guid> sourceIds)
    {
        await using var db = await factory.CreateDbContextAsync();
        var target = await db.Submissions.Include(s => s.Images).FirstAsync(s => s.Id == targetId);
        var sources = await db.Submissions.Include(s => s.Images)
            .Where(s => sourceIds.Contains(s.Id) && s.Id != targetId)
            .ToListAsync();
        var next = target.Images.Count == 0 ? -1 : target.Images.Max(i => i.Order);
        foreach (var source in sources.OrderBy(s => s.CreatedAt))
        {
            foreach (var image in source.Images.OrderBy(i => i.Order))
            {
                image.SubmissionId = target.Id;
                image.Order = ++next;
            }
        }
        await db.SaveChangesAsync();
        db.Submissions.RemoveRange(sources);
        await db.SaveChangesAsync();
    }

    public async Task<(string Name, bool Placeholder)> RenameSubmissionAsync(Guid submissionId, string? name)
    {
        await using var db = await factory.CreateDbContextAsync();
        var submission = await db.Submissions.FirstAsync(s => s.Id == submissionId);
        await ApplyNameAsync(db, submission, name);
        await db.SaveChangesAsync();
        return (submission.StudentName, submission.HasPlaceholderName);
    }

    public async Task<int> ReplaceGeneratedNamesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var candidates = await db.Submissions.Include(s => s.Images).Where(s => !s.HasPlaceholderName).ToListAsync();
        var replaced = 0;
        foreach (var submission in candidates.Where(s => Pseudonyms.LooksGenerated(s.StudentName, s.Images.Select(i => i.OriginalName))))
        {
            await ApplyNameAsync(db, submission, null);
            replaced++;
        }
        await db.SaveChangesAsync();
        return replaced;
    }

    private static async Task ApplyNameAsync(AppDbContext db, Submission submission, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            submission.StudentName = name.Trim();
            submission.HasPlaceholderName = false;
            return;
        }
        var taken = await db.Submissions.Where(s => s.ExamId == submission.ExamId && s.Id != submission.Id).Select(s => s.StudentName).ToListAsync();
        taken.AddRange(db.Submissions.Local.Where(s => s.ExamId == submission.ExamId && s.Id != submission.Id).Select(s => s.StudentName));
        submission.StudentName = Pseudonyms.Next(taken);
        submission.HasPlaceholderName = true;
    }

    public async Task<List<SubmissionImage>> DeleteSubmissionAsync(Guid submissionId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var submission = await db.Submissions.Include(s => s.Images).FirstOrDefaultAsync(s => s.Id == submissionId);
        if (submission is null)
        {
            return [];
        }
        db.Submissions.Remove(submission);
        await db.SaveChangesAsync();
        return submission.Images;
    }

    public async Task SaveReviewAsync(Submission source, bool complete)
    {
        await using var db = await factory.CreateDbContextAsync();
        var submission = await db.Submissions.Include(s => s.Assessments).FirstAsync(s => s.Id == source.Id);
        submission.TeacherComment = source.TeacherComment;
        submission.FinalGradeOverride = source.FinalGradeOverride;
        if (source.StudentName.Trim() != submission.StudentName || string.IsNullOrWhiteSpace(source.StudentName))
        {
            await ApplyNameAsync(db, submission, source.StudentName);
            source.StudentName = submission.StudentName;
            source.HasPlaceholderName = submission.HasPlaceholderName;
        }
        foreach (var sourceAssessment in source.Assessments)
        {
            var assessment = submission.Assessments.FirstOrDefault(a => a.CriterionId == sourceAssessment.CriterionId);
            if (assessment is null)
            {
                assessment = new CriterionAssessment { SubmissionId = submission.Id, CriterionId = sourceAssessment.CriterionId };
                db.Assessments.Add(assessment);
            }
            assessment.TeacherLevel = sourceAssessment.TeacherLevel;
        }
        if (complete)
        {
            submission.Status = SubmissionStatus.Abgeschlossen;
        }
        else if (submission.Status == SubmissionStatus.Abgeschlossen)
        {
            submission.Status = SubmissionStatus.Vorschlag;
        }
        await db.SaveChangesAsync();
    }

    public async Task SetStatusAsync(Guid submissionId, SubmissionStatus status, string? error = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Submissions.Where(s => s.Id == submissionId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status).SetProperty(x => x.ErrorMessage, error));
    }

    public async Task ResetInterruptedAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Submissions
            .Where(s => s.Status == SubmissionStatus.InWarteschlange || s.Status == SubmissionStatus.WirdBewertet)
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.Status,
                x => x.GradedAt == null ? SubmissionStatus.Neu : SubmissionStatus.Vorschlag));
    }
}

public sealed record ExamSummary(
    Guid Id,
    string Title,
    string SchoolClass,
    DateOnly Date,
    int SubmissionCount,
    int CompletedCount,
    int PhotoCount,
    List<string> Covers);
