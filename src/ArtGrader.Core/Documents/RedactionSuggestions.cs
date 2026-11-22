using System.Text.RegularExpressions;
using static ArtGrader.Core.Localization.Texts;

namespace ArtGrader.Core.Documents;

public sealed record RedactionHit(int Start, int Length, string Text, string Reason);

public static partial class RedactionSuggestions
{
    public const string Marker = "[geschwärzt]";

    public static IReadOnlyList<RedactionHit> Find(string text, IEnumerable<string> terms, IEnumerable<(string Text, string Reason)>? modelPassages = null)
    {
        var hits = new List<RedactionHit>();
        foreach (var term in terms.Select(t => t.Trim()).Where(t => t.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            hits.AddRange(Occurrences(text, term, T("Your list")));
        }
        foreach (var (passage, reason) in modelPassages ?? [])
        {
            hits.AddRange(Occurrences(text, passage.Trim(), string.IsNullOrWhiteSpace(reason) ? T("local model") : reason));
        }
        hits.AddRange(Email().Matches(text).Select(m => new RedactionHit(m.Index, m.Length, m.Value, T("Email"))));
        hits.AddRange(Phone().Matches(text).Select(m => new RedactionHit(m.Index, m.Length, m.Value.Trim(), T("Phone"))));
        hits.AddRange(Web().Matches(text).Select(m => new RedactionHit(m.Index, m.Length, m.Value.TrimEnd('.', ',', ')'), T("Web address"))));
        return Merge(hits);
    }

    public static string Apply(string text, IEnumerable<RedactionHit> hits)
    {
        foreach (var hit in hits.OrderByDescending(h => h.Start))
        {
            text = text[..hit.Start] + Marker + text[(hit.Start + hit.Length)..];
        }
        return text;
    }

    private static IEnumerable<RedactionHit> Occurrences(string text, string term, string reason)
    {
        if (term.Length < 2 || term == Marker)
        {
            yield break;
        }
        var pattern = new Regex(@"(?<![\p{L}\p{N}])" + Regex.Escape(term) + @"(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        foreach (Match match in pattern.Matches(text))
        {
            yield return new RedactionHit(match.Index, match.Length, match.Value, reason);
        }
    }

    private static List<RedactionHit> Merge(List<RedactionHit> hits)
    {
        var merged = new List<RedactionHit>();
        foreach (var hit in hits.OrderBy(h => h.Start).ThenByDescending(h => h.Length))
        {
            if (merged.Count > 0 && hit.Start < merged[^1].Start + merged[^1].Length)
            {
                var last = merged[^1];
                var end = Math.Max(last.Start + last.Length, hit.Start + hit.Length);
                if (end > last.Start + last.Length)
                {
                    merged[^1] = last with { Length = end - last.Start };
                }
                continue;
            }
            merged.Add(hit);
        }
        return merged;
    }

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")]
    private static partial Regex Email();

    [GeneratedRegex(@"(?<!\d)(\+41|0041|0)\s?\d{2}\s?\d{3}\s?\d{2}\s?\d{2}(?!\d)")]
    private static partial Regex Phone();

    [GeneratedRegex(@"\b(https?://|www\.)[^\s<>""]+", RegexOptions.IgnoreCase)]
    private static partial Regex Web();
}
