using System.Collections.Frozen;
using System.Text.Json;

namespace ArtGrader.Core.Localization;

/// <summary>
/// Looks up user-facing texts. The English text is written in the code and serves as the key; the translations live in
/// Localization/{de,fr,it,rm}/*.json (one file per area), embedded into this assembly. A missing translation falls back
/// to English, and a test makes sure none is missing.
/// </summary>
public static class Texts
{
    private static readonly FrozenDictionary<AppLanguage, Lazy<FrozenDictionary<string, string>>> Tables =
        Enum.GetValues<AppLanguage>()
            .Where(l => l != AppLanguage.En)
            .ToFrozenDictionary(l => l, l => new Lazy<FrozenDictionary<string, string>>(() => Load(l)));

    private static readonly Dictionary<AppLanguage, IReadOnlyList<string>> conflicts = [];

    /// <summary>The text in the current language.</summary>
    public static string T(string english) =>
        Tables.TryGetValue(Language.Current, out var table) && table.Value.TryGetValue(english, out var translated) ? translated : english;

    /// <summary>The text in the current language with {0}, {1} … filled in, formatted for the current language.</summary>
    public static string T(string english, params object?[] args) => string.Format(Language.Culture, T(english), args);

    /// <summary>The languages that have translation files: all but English.</summary>
    public static IEnumerable<AppLanguage> TranslatedLanguages => Tables.Keys.Order();

    /// <summary>All translations of one language, keyed by the English text. For tests.</summary>
    public static IReadOnlyDictionary<string, string> TranslationsOf(AppLanguage language) => Tables[language].Value;

    /// <summary>All German translations, keyed by the English text. For tests.</summary>
    public static IReadOnlyDictionary<string, string> GermanTexts => TranslationsOf(AppLanguage.De);

    /// <summary>Keys that appear in more than one file of a language with different translations. For tests.</summary>
    public static IReadOnlyList<string> ConflictsOf(AppLanguage language)
    {
        _ = Tables[language].Value;
        lock (conflicts)
        {
            return conflicts.GetValueOrDefault(language, []);
        }
    }

    private static FrozenDictionary<string, string> Load(AppLanguage language)
    {
        var assembly = typeof(Texts).Assembly;
        var folder = $".Localization.{Language.Code(language)}.";
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        var found = new List<string>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.Contains(folder, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)).Order())
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
            foreach (var (english, translated) in entries)
            {
                if (merged.TryGetValue(english, out var existing))
                {
                    if (existing != translated)
                    {
                        found.Add($"{name}: «{english}»");
                    }
                    continue;
                }
                merged[english] = translated;
            }
        }
        lock (conflicts)
        {
            conflicts[language] = found;
        }
        return merged.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
