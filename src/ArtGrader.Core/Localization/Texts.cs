using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;

namespace ArtGrader.Core.Localization;

/// <summary>
/// Looks up user-facing texts. The German text is written in the code and serves as the key;
/// the English translations live in Localization/en/*.json (one file per area), embedded into this assembly.
/// A missing translation falls back to German, and a test makes sure none is missing.
/// </summary>
public static class Texts
{
    private static readonly Lazy<FrozenDictionary<string, string>> English = new(LoadEnglish);

    /// <summary>The text in the current language.</summary>
    public static string T(string german) =>
        Language.IsEnglish && English.Value.TryGetValue(german, out var english) ? english : german;

    /// <summary>The text in the current language with {0}, {1} … filled in, formatted for the current language.</summary>
    public static string T(string german, params object?[] args) => string.Format(Language.Culture, T(german), args);

    /// <summary>All English translations, keyed by the German text. For tests.</summary>
    public static IReadOnlyDictionary<string, string> EnglishTexts => English.Value;

    /// <summary>Translation files whose keys appear in more than one file with a different English text. For tests.</summary>
    public static IReadOnlyList<string> Conflicts { get; private set; } = [];

    private static FrozenDictionary<string, string> LoadEnglish()
    {
        var assembly = typeof(Texts).Assembly;
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        var conflicts = new List<string>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.Contains(".Localization.en.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
            foreach (var (german, english) in entries)
            {
                if (merged.TryGetValue(german, out var existing))
                {
                    if (existing != english)
                    {
                        conflicts.Add($"{name}: «{german}»");
                    }
                    continue;
                }
                merged[german] = english;
            }
        }
        Conflicts = conflicts;
        return merged.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
