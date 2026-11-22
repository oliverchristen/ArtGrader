using System.Text.Json;
using ArtGrader.Core.Documents;
using ArtGrader.Core.Domain;

namespace ArtGrader.Core.Ai;

public sealed class RubricImportService(IModelGateway claude)
{
    public async Task<List<RubricCategory>> ImportAsync(
        string rubricText,
        IReadOnlyList<byte[]> rubricPages,
        string? exerciseText,
        bool localOnly,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rubricText) && rubricPages.Count == 0)
        {
            throw new ModelException("Der Beurteilungsbogen ist leer.");
        }
        var shared = new List<ContentPart>();
        for (var i = 0; i < rubricPages.Count; i++)
        {
            shared.Add(new TextPart($"Beurteilungsbogen, Seite {i + 1} von {rubricPages.Count}:"));
            shared.Add(new ImagePart(rubricPages[i]));
        }
        if (!string.IsNullOrWhiteSpace(rubricText))
        {
            shared.Add(new TextPart("<beurteilungsbogen>\n" + rubricText.Trim() + "\n</beurteilungsbogen>"));
        }

        var response = await claude.CompleteJsonAsync(
            new ModelJsonRequest(
                Prompts.RubricImportSystem,
                shared,
                [new TextPart(Prompts.RubricImportInstruction(exerciseText))],
                Schemas.Rubric(),
                Quick: true,
                LocalOnly: localOnly),
            cancellationToken);

        return Parse(response.Json);
    }

    public static List<RubricCategory> Parse(string json)
    {
        RubricDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<RubricDto>(json, JsonOptions.Web);
        }
        catch (JsonException ex)
        {
            throw new ModelException("Die Antwort der KI konnte nicht gelesen werden. Bitte nochmals versuchen.", ex);
        }

        var categories = new List<RubricCategory>();
        var categoryIndex = 0;
        foreach (var category in dto?.Categories ?? [])
        {
            var entity = new RubricCategory { Name = (category.Name ?? "").Trim(), Order = categoryIndex };
            var criterionIndex = 0;
            foreach (var criterion in category.Criteria ?? [])
            {
                if (string.IsNullOrWhiteSpace(criterion.Text))
                {
                    continue;
                }
                entity.Criteria.Add(
                    new RubricCriterion
                    {
                        CategoryId = entity.Id,
                        Text = criterion.Text.Trim(),
                        Order = criterionIndex++,
                        AssessedBy = criterion.AssessedBy == "teacher" ? AssessedBy.Teacher : AssessedBy.Ai,
                    });
            }
            if (entity.Criteria.Count > 0)
            {
                categories.Add(entity);
                categoryIndex++;
            }
        }

        if (categories.Count == 0)
        {
            throw new ModelException("Im Dokument wurden keine Beurteilungskriterien gefunden.");
        }
        return categories;
    }

    private sealed record RubricDto(List<CategoryDto>? Categories);

    private sealed record CategoryDto(string? Name, List<CriterionDto>? Criteria);

    private sealed record CriterionDto(string? Text, string? AssessedBy);
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
