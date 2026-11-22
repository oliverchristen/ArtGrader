using System.Globalization;

namespace ArtGrader.Core.Localization;

public enum AppLanguage
{
    De,
    En,
}

/// <summary>
/// The language of the whole app: interface, exports and the text the AI writes. ArtGrader runs for one teacher on one
/// computer, so the language is a process-wide setting; the web app sets it at start and when the teacher changes it.
/// </summary>
public static class Language
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-CH");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

    public static AppLanguage Current { get; set; } = AppLanguage.De;

    public static bool IsEnglish => Current == AppLanguage.En;

    /// <summary>Formats dates and numbers: «22. November 2026» in German, «22 November 2026» in English.</summary>
    public static CultureInfo Culture => IsEnglish ? English : German;

    /// <summary>The value for the HTML lang attribute.</summary>
    public static string HtmlLang => IsEnglish ? "en" : "de-CH";
}
