using System.Text;
using ArtGrader.Core.Domain;
using ArtGrader.Core.Images;
using static ArtGrader.Core.Localization.Texts;

namespace ArtGrader.Core.Ai;

public sealed record GradingCriterion(Guid Id, string Category, string Text);

public sealed record GradingContext(
    string ExerciseText,
    IReadOnlyList<byte[]> ExercisePages,
    IReadOnlyList<GradingCriterion> Criteria,
    bool LocalOnly = false,
    string LessonText = "",
    IReadOnlyList<byte[]>? LessonPages = null)
{
    public const int MaxLessonCharacters = 20000;

    public static GradingContext From(Exam exam, IReadOnlyList<byte[]>? exercisePages, IReadOnlyList<byte[]>? lessonPages = null) =>
        new(
            exam.ExerciseText,
            exercisePages ?? [],
            exam.Categories.OrderBy(c => c.Order)
                .SelectMany(c =>
                    c.Criteria.OrderBy(k => k.Order)
                        .Where(k => k.AssessedBy == AssessedBy.Ai)
                        .Select(k => new GradingCriterion(k.Id, c.Name, k.Text)))
                .ToList(),
            exam.LocalOnly,
            exam.LessonText,
            lessonPages);
}

public sealed record CriterionSuggestion(Guid CriterionId, Level Level, string Justification, Confidence Confidence);

public sealed record GradingSuggestion(
    string Description,
    IReadOnlyList<CriterionSuggestion> Criteria,
    string OverallComment,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> NextSteps,
    string Model,
    ModelUsage Usage);

public sealed class SubmissionGradingService(IModelGateway claude)
{
    public async Task<GradingSuggestion> GradeAsync(
        GradingContext context,
        IReadOnlyList<byte[]> images,
        CancellationToken cancellationToken)
    {
        if (context.Criteria.Count == 0)
        {
            throw new ModelException(T("Diese Prüfung hat keine Kriterien, die von der KI beurteilt werden."));
        }
        if (images.Count == 0)
        {
            throw new ModelException(T("Für diese Arbeit ist kein Foto vorhanden."));
        }

        var keys = CriterionKeys(context.Criteria);
        var content = new List<ContentPart>();
        for (var i = 0; i < images.Count; i++)
        {
            var jpeg = await ImagePreprocessor.ToJpegAsync(
                new MemoryStream(images[i]),
                ImagePreprocessor.ModelMaxSide,
                cancellationToken);
            content.Add(new TextPart($"Bild {i + 1} von {images.Count}:"));
            content.Add(new ImagePart(jpeg));
        }
        content.Add(new TextPart(Prompts.GradingInstruction(images.Count)));

        var response = await claude.CompleteJsonAsync(
            new ModelJsonRequest(Prompts.GradingSystem, SharedContext(context, keys), content, Schemas.Grading(), LocalOnly: context.LocalOnly),
            cancellationToken);

        var parsed = GradingResponseValidator.Parse(response.Json, keys);
        return parsed with { Model = response.Model, Usage = response.Usage };
    }

    private const string LessonNote =
        "Das Unterrichtsmaterial zeigt, was im Unterricht behandelt wurde. Nutze es nur als Hintergrund, "
            + "um die Kriterien richtig zu verstehen. Beurteile ausschliesslich nach den Kriterien.";

    public static IReadOnlyDictionary<string, Guid> CriterionKeys(IReadOnlyList<GradingCriterion> criteria) =>
        criteria.Select((c, i) => (Key: $"K{i + 1}", c.Id)).ToDictionary(x => x.Key, x => x.Id);

    public static IReadOnlyList<ContentPart> SharedContext(GradingContext context, IReadOnlyDictionary<string, Guid> keys)
    {
        var parts = new List<ContentPart>();
        for (var i = 0; i < context.ExercisePages.Count; i++)
        {
            parts.Add(new TextPart($"Aufgabenblatt, Seite {i + 1} von {context.ExercisePages.Count}:"));
            parts.Add(new ImagePart(context.ExercisePages[i]));
        }
        if (!string.IsNullOrWhiteSpace(context.ExerciseText))
        {
            parts.Add(new TextPart("<aufgabe>\n" + context.ExerciseText.Trim() + "\n</aufgabe>"));
        }
        var lessonPages = context.LessonPages ?? [];
        for (var i = 0; i < lessonPages.Count; i++)
        {
            parts.Add(new TextPart($"Unterrichtsmaterial, Seite {i + 1} von {lessonPages.Count}:"));
            parts.Add(new ImagePart(lessonPages[i]));
        }
        if (lessonPages.Count > 0 && string.IsNullOrWhiteSpace(context.LessonText))
        {
            parts.Add(new TextPart(LessonNote));
        }
        if (!string.IsNullOrWhiteSpace(context.LessonText))
        {
            var lesson = context.LessonText.Trim();
            if (lesson.Length > GradingContext.MaxLessonCharacters)
            {
                lesson = lesson[..GradingContext.MaxLessonCharacters];
            }
            parts.Add(new TextPart("<unterrichtsmaterial>\n" + lesson + "\n</unterrichtsmaterial>\n" + LessonNote));
        }

        var byId = context.Criteria.ToDictionary(c => c.Id);
        var rubric = new StringBuilder("<kriterien>\n");
        foreach (var group in keys.Select(k => (k.Key, Criterion: byId[k.Value])).GroupBy(x => x.Criterion.Category))
        {
            rubric.Append("Kategorie: ").Append(group.Key).Append('\n');
            foreach (var (key, criterion) in group)
            {
                rubric.Append("- ").Append(key).Append(": ").Append(criterion.Text).Append('\n');
            }
        }
        rubric.Append("</kriterien>\n");
        rubric.Append("Gib für jedes Kriterium (").Append(string.Join(", ", keys.Keys)).Append(") genau einen Eintrag zurück. ");
        rubric.Append("Verwende die Kennung (z. B. K1) als criterionId.");
        parts.Add(new TextPart(rubric.ToString()));
        return parts;
    }
}
