using System.Text.Json;
using ArtGrader.Core.Domain;

namespace ArtGrader.Core.Ai;

public static class GradingResponseValidator
{
    public static GradingSuggestion Parse(string json, IReadOnlyDictionary<string, Guid> keys)
    {
        GradingDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<GradingDto>(json, JsonOptions.Web);
        }
        catch (JsonException ex)
        {
            throw new ModelException("Die Antwort der KI konnte nicht gelesen werden. Bitte nochmals bewerten.", ex);
        }
        if (dto?.Criteria is null)
        {
            throw new ModelException("Die Antwort der KI enthält keine Kriterien. Bitte nochmals bewerten.");
        }

        var suggestions = new List<CriterionSuggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in dto.Criteria)
        {
            var key = (item.CriterionId ?? "").Trim().ToUpperInvariant();
            if (!keys.TryGetValue(key, out var criterionId))
            {
                throw new ModelException(
                    $"Die KI hat ein unbekanntes Kriterium «{item.CriterionId}» bewertet. Bitte nochmals bewerten.");
            }
            if (!seen.Add(key))
            {
                throw new ModelException($"Die KI hat das Kriterium {key} doppelt bewertet. Bitte nochmals bewerten.");
            }
            suggestions.Add(
                new CriterionSuggestion(
                    criterionId,
                    ParseLevel(item.Level),
                    (item.Justification ?? "").Trim(),
                    ParseConfidence(item.Confidence)));
        }

        var missing = keys.Keys.Where(k => !seen.Contains(k)).ToList();
        if (missing.Count > 0)
        {
            throw new ModelException(
                $"Die KI hat nicht alle Kriterien bewertet (fehlend: {string.Join(", ", missing)}). Bitte nochmals bewerten.");
        }

        return new GradingSuggestion(
            (dto.Beschreibung ?? "").Trim(),
            suggestions,
            (dto.OverallComment ?? "").Trim(),
            Clean(dto.Strengths),
            Clean(dto.NextSteps),
            "",
            new ModelUsage(0, 0, 0, 0));
    }

    private static Level ParseLevel(string? level) =>
        level?.Trim().ToLowerInvariant() switch
        {
            "uebertroffen" => Level.Uebertroffen,
            "erfuellt" => Level.Erfuellt,
            "teilweise" => Level.Teilweise,
            "nicht" => Level.Nicht,
            _ => throw new ModelException($"Die KI hat eine ungültige Stufe «{level}» geliefert. Bitte nochmals bewerten."),
        };

    private static Confidence ParseConfidence(string? confidence) =>
        confidence?.Trim().ToLowerInvariant() switch
        {
            "hoch" => Confidence.Hoch,
            "tief" => Confidence.Tief,
            _ => Confidence.Mittel,
        };

    private static List<string> Clean(List<string>? items) =>
        (items ?? []).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

    private sealed record GradingDto(
        string? Beschreibung,
        List<CriterionDto>? Criteria,
        string? OverallComment,
        List<string>? Strengths,
        List<string>? NextSteps);

    private sealed record CriterionDto(string? CriterionId, string? Level, string? Justification, string? Confidence);
}
