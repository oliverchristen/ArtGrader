using ArtGrader.Core.Domain;
using ArtGrader.Core.Grading;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ArtGrader.Web.Export;

public static class FeedbackPdf
{
    private const string Ink = "#262019";
    private const string InkSoft = "#6a6157";
    private const string LineColor = "#e2d6c2";
    private const string Paper = "#f4ede1";
    private const string Terracotta = "#c0533a";
    private const float PhotoColumnWidth = 160;

    public const string AiNotice =
        "Die Einschätzung wurde mit Unterstützung einer KI vorbereitet. Die Lehrperson hat jedes Kriterium geprüft und die Note festgelegt.";

    public const string AiDraftNotice =
        "Entwurf: Die Einschätzung wurde mit Unterstützung einer KI vorbereitet. Die Lehrperson hat sie noch nicht abgeschlossen.";

    public static string? AiNoticeFor(Submission submission) =>
        submission.ModelId is null ? null
        : submission.Status == SubmissionStatus.Abgeschlossen ? AiNotice
        : AiDraftNotice;

    public static byte[] Create(
        Exam exam,
        IEnumerable<Submission> submissions,
        bool includeJustifications,
        IReadOnlyDictionary<Guid, byte[]>? images = null) =>
        Build(exam, submissions, includeJustifications, images).GeneratePdf();

    public static IDocument Build(
        Exam exam,
        IEnumerable<Submission> submissions,
        bool includeJustifications,
        IReadOnlyDictionary<Guid, byte[]>? images = null) =>
        Document.Create(container =>
        {
            foreach (var submission in submissions)
            {
                container.Page(page => ComposePage(page, exam, submission, includeJustifications, images));
            }
        });

    private static void ComposePage(
        PageDescriptor page,
        Exam exam,
        Submission submission,
        bool includeJustifications,
        IReadOnlyDictionary<Guid, byte[]>? images)
    {
        page.Size(PageSizes.A4);
        page.Margin(1.2f, Unit.Centimetre);
        page.DefaultTextStyle(t => t.FontSize(9f).FontColor(Ink));

        page.Header().PaddingBottom(10).BorderBottom(1).BorderColor(LineColor).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("BEURTEILUNG").FontSize(7.5f).Bold().LetterSpacing(0.15f).FontColor(Terracotta);
                column.Item().Text(exam.Title).FontSize(17).Bold();
            });
            row.AutoItem().AlignBottom().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontColor(InkSoft));
                if (!string.IsNullOrWhiteSpace(exam.SchoolClass))
                {
                    t.Span($"Klasse {exam.SchoolClass}  ·  ");
                }
                t.Span(exam.Date.ToString("dd.MM.yyyy"));
            });
        });

        page.Content().PaddingTop(12).Column(column =>
        {
            column.Spacing(12);
            column.Item().Row(row =>
            {
                var photos = submission.Images
                    .OrderBy(i => i.Order)
                    .Select(i => images is not null && images.TryGetValue(i.Id, out var bytes) ? bytes : null)
                    .OfType<byte[]>()
                    .ToList();
                if (photos.Count > 0)
                {
                    row.ConstantItem(PhotoColumnWidth).Column(photoColumn => ComposePhotos(photoColumn, photos));
                    row.ConstantItem(16);
                }
                row.RelativeItem().Column(summary => ComposeSummary(summary, exam, submission));
            });

            column.Item().Element(c => ComposeTable(c, exam, submission, includeJustifications));
        });

        page.Footer().Column(footer =>
        {
            if (AiNoticeFor(submission) is { } notice)
            {
                footer.Item().PaddingBottom(4).AlignCenter().Text(notice).FontSize(7.5f).FontColor(InkSoft);
            }
            footer.Item().AlignCenter().Text("Bildnerisches Gestalten").FontSize(7).FontColor(InkSoft);
        });
    }

    private static void ComposePhotos(ColumnDescriptor column, IReadOnlyList<byte[]> photos)
    {
        column.Spacing(6);
        column.Item().Background("#ffffff").Border(0.5f).BorderColor(LineColor).Padding(4)
            .MaxHeight(210).Image(photos[0]).FitArea();
        if (photos.Count > 1)
        {
            column.Item().Row(row =>
            {
                row.Spacing(4);
                foreach (var photo in photos.Skip(1).Take(3))
                {
                    row.RelativeItem().Border(0.5f).BorderColor(LineColor).Padding(2).Height(48).Image(photo).FitArea();
                }
                for (var i = photos.Count - 1; i < 3; i++)
                {
                    row.RelativeItem();
                }
            });
        }
    }

    private static void ComposeSummary(ColumnDescriptor column, Exam exam, Submission submission)
    {
        var result = GradeCalculator.Calculate(exam, submission);
        var grade = GradeCalculator.FinalGrade(exam, submission);

        column.Spacing(8);
        column.Item().Row(row =>
        {
            row.RelativeItem().Column(name =>
            {
                name.Item().Text("Name").FontSize(7.5f).FontColor(InkSoft);
                name.Item().Text(submission.StudentName).FontSize(16).Bold().Italic();
                name.Item().PaddingTop(4).Text($"{result.Points:0.##} von {result.MaxPoints:0.##} Punkten").FontColor(InkSoft);
            });
            row.ConstantItem(78).Height(78).Border(2.5f).BorderColor(Terracotta).CornerRadius(39)
                .AlignCenter().AlignMiddle().Column(stamp =>
                {
                    stamp.Item().AlignCenter().Text(grade is null ? "–" : grade.Value.ToString("0.0#"))
                        .FontSize(26).Bold().FontColor(Terracotta);
                    stamp.Item().AlignCenter().Text("NOTE").FontSize(6.5f).Bold().LetterSpacing(0.15f).FontColor(Terracotta);
                });
        });

        var comment = string.IsNullOrWhiteSpace(submission.TeacherComment) ? submission.AiOverallComment : submission.TeacherComment;
        if (!string.IsNullOrWhiteSpace(comment))
        {
            column.Item().Background(Paper).CornerRadius(6).Padding(10).Column(feedback =>
            {
                feedback.Item().Text("Rückmeldung").Bold().FontSize(9.5f);
                feedback.Item().PaddingTop(3).Text(comment).LineHeight(1.35f);
            });
        }
    }

    private static void ComposeTable(IContainer container, Exam exam, Submission submission, bool includeJustifications)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(6);
                foreach (var _ in LevelText.AllDescending)
                {
                    columns.RelativeColumn(1.1f);
                }
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).AlignBottom().Text("Kriterium").Bold();
                foreach (var level in LevelText.AllDescending)
                {
                    header.Cell().Element(HeaderCell).Column(cell =>
                    {
                        cell.Item().AlignCenter().Width(22).Height(4).Background(LevelColor(level)).CornerRadius(2);
                        cell.Item().PaddingTop(3).AlignCenter().Text(LevelText.Label(level)).FontSize(7.5f).Bold();
                        cell.Item().AlignCenter().Text(LevelText.Short(level)).FontSize(6.5f).FontColor(InkSoft);
                    });
                }
            });

            foreach (var category in exam.Categories.OrderBy(c => c.Order))
            {
                table.Cell().ColumnSpan(5).PaddingTop(6).PaddingBottom(3).PaddingHorizontal(4)
                    .Text(category.Name).Bold().Italic().FontSize(10).FontColor(Terracotta);
                foreach (var criterion in category.Criteria.OrderBy(k => k.Order))
                {
                    var assessment = submission.Assessments.FirstOrDefault(a => a.CriterionId == criterion.Id);
                    table.Cell().Element(BodyCell).Column(cell =>
                    {
                        cell.Item().Text(criterion.Text);
                        if (includeJustifications
                            && assessment is { AiJustification.Length: > 0 }
                            && (assessment.TeacherLevel is null || assessment.TeacherLevel == assessment.AiLevel))
                        {
                            cell.Item().PaddingTop(1).Text(assessment.AiJustification).FontSize(7.5f).Italic().FontColor(InkSoft);
                        }
                    });
                    foreach (var level in LevelText.AllDescending)
                    {
                        var chosen = assessment?.EffectiveLevel == level;
                        var cell = table.Cell().Element(BodyCell).AlignCenter().AlignMiddle();
                        if (chosen)
                        {
                            cell.Width(18).Height(18).Background(LevelColor(level)).CornerRadius(9)
                                .AlignCenter().AlignMiddle().Text("X").FontSize(9).Bold().FontColor("#ffffff");
                        }
                    }
                }
            }
        });
    }

    private static string LevelColor(Level level) =>
        level switch
        {
            Level.Uebertroffen => "#3f7353",
            Level.Erfuellt => "#86ab7f",
            Level.Teilweise => "#dca94a",
            _ => "#c9614a",
        };

    private static IContainer HeaderCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Ink).PaddingVertical(4).PaddingHorizontal(4);

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(LineColor).PaddingVertical(4).PaddingHorizontal(4);
}
