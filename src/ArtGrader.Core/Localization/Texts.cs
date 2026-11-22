using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;

namespace ArtGrader.Core.Localization;

/// <summary>
/// Looks up user-facing texts. The English text is written in the code and serves as the key;
/// the German translations live in Localization/de/*.json (one file per area), embedded into this assembly.
/// A missing translation falls back to English, and a test makes sure none is missing.
/// </summary>
public static class Texts
{
    private static readonly Lazy<FrozenDictionary<string, string>> German = new(LoadGerman);

    /// <summary>The text in the current language.</summary>
    public static string T(string english) =>
        !Language.IsEnglish && German.Value.TryGetValue(english, out var german) ? german : english;

    /// <summary>The text in the current language with {0}, {1} … filled in, formatted for the current language.</summary>
    public static string T(string english, params object?[] args) => string.Format(Language.Culture, T(english), args);

    /// <summary>All German translations, keyed by the English text. For tests.</summary>
    public static IReadOnlyDictionary<string, string> GermanTexts => German.Value;

    /// <summary>Translation files whose keys appear in more than one file with a different German text. For tests.</summary>
    public static IReadOnlyList<string> Conflicts { get; private set; } = [];

    private static FrozenDictionary<string, string> LoadGerman()
    {
        var assembly = typeof(Texts).Assembly;
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new List<string>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.Contains(".Localization.de.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
            foreach (var (english, german) in entries)
            {
                if (merged.TryGetValue(english, out var existing))
                {
                    if (existing != german)
                    {
                        conflicts.Add($"{name}: «{english}»");
                    }
                    continue;
                }
                merged[english] = german;
            }
        }
        Conflicts = conflicts;
        return merged.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
