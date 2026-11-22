using MudBlazor;
using static ArtGrader.Core.Localization.Texts;

namespace ArtGrader.Web.Services;

public enum AppTheme
{
    /// <summary>Bright primary colours on white paper; the original look.</summary>
    Atelier,

    /// <summary>Black spray lines on grey concrete, in homage to Harald Naegeli, the «Sprayer von Zürich».</summary>
    Naegeli,
}

/// <summary>
/// The looks of ArtGrader. A theme is the value of <c>data-theme</c> on the html element, which swaps the colour variables
/// in app.css, plus the matching MudBlazor palette for its components.
/// </summary>
public static class Themes
{
    private static readonly string[] Sans = ["DM Sans", "Segoe UI", "-apple-system", "BlinkMacSystemFont", "Helvetica Neue", "Arial", "sans-serif"];

    /// <summary>All themes in the order the settings show them, with name, description and three colours for the preview.</summary>
    public static IReadOnlyList<(AppTheme Theme, string Name, string Description, string[] Swatches)> All =>
    [
        (AppTheme.Atelier, T("Atelier"), T("Bright colours on white paper."), ["#ffffff", "#1f4fe0", "#ffc31f", "#ec3b2c"]),
        (AppTheme.Naegeli, "Harald Naegeli", T("Black spray lines on grey concrete, in homage to the Sprayer of Zurich."), ["#c9c7c1", "#0d0d0d", "#ecebe7", "#d1301f"]),
    ];

    /// <summary>The value of the data-theme attribute.</summary>
    public static string Attribute(AppTheme theme) => theme.ToString().ToLowerInvariant();

    public static MudTheme MudThemeFor(AppTheme theme) => theme == AppTheme.Naegeli ? Naegeli : Atelier;

    private static Typography Typography => new()
    {
        Default = new DefaultTypography { FontFamily = Sans },
        H4 = new H4Typography { FontFamily = Sans, FontWeight = "800" },
        H5 = new H5Typography { FontFamily = Sans, FontWeight = "800" },
        H6 = new H6Typography { FontFamily = Sans, FontWeight = "800" },
        Button = new ButtonTypography { FontFamily = Sans, FontWeight = "600", TextTransform = "none" },
    };

    private static readonly MudTheme Atelier = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1f4fe0",
            Secondary = "#ec3b2c",
            Tertiary = "#ffc31f",
            Success = "#0f9a63",
            Warning = "#c98a00",
            Error = "#d42a1c",
            Info = "#1f4fe0",
            Background = "#ffffff",
            Surface = "#ffffff",
            TextPrimary = "#141414",
            TextSecondary = "#55534f",
            LinesInputs = "#c4c1b9",
            Divider = "#e4e2dc",
            ActionDefault = "#55534f",
        },
        Typography = Typography,
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "10px" },
    };

    private static readonly MudTheme Naegeli = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0d0d0d",
            Secondary = "#d1301f",
            Tertiary = "#8f8c86",
            Success = "#0f8a58",
            Warning = "#b07800",
            Error = "#c4281a",
            Info = "#1f4fe0",
            Background = "#c9c7c1",
            Surface = "#ecebe7",
            TextPrimary = "#0d0d0d",
            TextSecondary = "#3d3c39",
            LinesInputs = "#8f8c86",
            Divider = "#b3b0a9",
            ActionDefault = "#3d3c39",
        },
        Typography = Typography,
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "3px" },
    };
}
