using System.Globalization;

namespace ArtGrader.Core.Localization;

public enum AppLanguage
{
    De,
    En,
    Fr,
    It,
    Rm,
}

/// <summary>
/// The language of the whole app: interface, exports and the text the AI writes. ArtGrader runs for one teacher on one
/// computer, so the language is a process-wide setting; the web app sets it at start and when the teacher changes it.
/// </summary>
public static class Language
{
    /// <summary>All languages in the order the settings show them, with their own name.</summary>
    public static IReadOnlyList<(AppLanguage Language, string Name)> All { get; } =
    [
        (AppLanguage.De, "Deutsch"),
        (AppLanguage.Fr, "Français"),
        (AppLanguage.It, "Italiano"),
        (AppLanguage.Rm, "Rumantsch"),
        (AppLanguage.En, "English"),
    ];

    public static AppLanguage Current { get; set; } = AppLanguage.De;

    public static bool IsEnglish => Current == AppLanguage.En;

    /// <summary>Formats dates and numbers the Swiss way of each language; English uses British conventions.</summary>
    public static CultureInfo Culture => CultureOf(Current);

    /// <summary>The value for the HTML lang attribute.</summary>
    public static string HtmlLang => Current == AppLanguage.En ? "en" : CultureOf(Current).Name;

    /// <summary>The folder of the translations: Localization/de, fr, it, rm. English has none, it is the text in the code.</summary>
    public static string Code(AppLanguage language) => language.ToString().ToLowerInvariant();

    public static CultureInfo CultureOf(AppLanguage language) =>
        CultureInfo.GetCultureInfo(language switch
        {
            AppLanguage.En => "en-GB",
            AppLanguage.Fr => "fr-CH",
            AppLanguage.It => "it-CH",
            AppLanguage.Rm => "rm-CH",
            _ => "de-CH",
        });
}
