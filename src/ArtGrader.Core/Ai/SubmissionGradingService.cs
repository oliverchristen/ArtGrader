using System.Text;
using ArtGrader.Core.Domain;
using ArtGrader.Core.Images;

namespace ArtGrader.Core.Ai;

public sealed record GradingCriterion(Guid Id, string Category, string Text);

public sealed record GradingContext(string ExerciseText, byte[]? ExercisePdf, IReadOnlyList<GradingCriterion> Criteria)
{
    public static GradingContext From(Exam exam, byte[]? exercisePdf) =>
        new(
            exam.ExerciseText,
            exercisePdf,
            exam.Categories.OrderBy(c => c.Order)
                .SelectMany(c =>
                    c.Criteria.OrderBy(k => k.Order)
                        .Where(k => k.AssessedBy == AssessedBy.Ai)
                        .Select(k => new GradingCriterion(k.Id, c.Name, k.Text)))
                .ToList());
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
            throw new ModelException("Diese Prüfung hat keine Kriterien, die von der KI beurteilt werden.");
        }
        if (images.Count == 0)
        {
            throw new ModelException("Für diese Arbeit ist kein Foto vorhanden.");
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
            new ModelJsonRequest(Prompts.GradingSystem, SharedContext(context, keys), content, Schemas.Grading()),
            cancellationToken);

        var parsed = GradingResponseValidator.Parse(response.Json, keys);
        return parsed with { Model = response.Model, Usage = response.Usage };
    }

    public static IReadOnlyDictionary<string, Guid> CriterionKeys(IReadOnlyList<GradingCriterion> criteria) =>
        criteria.Select((c, i) => (Key: $"K{i + 1}", c.Id)).ToDictionary(x => x.Key, x => x.Id);

    public static IReadOnlyList<ContentPart> SharedContext(GradingContext context, IReadOnlyDictionary<string, Guid> keys)
    {
        var parts = new List<ContentPart>();
        if (context.ExercisePdf is not null && string.IsNullOrWhiteSpace(context.ExerciseText))
        {
            parts.Add(new PdfPart(context.ExercisePdf, "Aufgabenstellung"));
        }
        else
        {
            parts.Add(new TextPart("<aufgabe>\n" + context.ExerciseText.Trim() + "\n</aufgabe>"));
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
