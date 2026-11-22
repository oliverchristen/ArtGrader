using static ArtGrader.Core.Localization.Texts;

namespace ArtGrader.Core.Ai;

public sealed record LocalModel(
    string Name,
    string Title,
    string Description,
    double DownloadGb,
    int MinMemoryGb,
    string MinutesWithThinking,
    string MinutesFast,
    string? FastVariant = null);

public static class LocalModelCatalog
{
    // A property, not a stored list, so the titles and descriptions follow the current language.
    public static IReadOnlyList<LocalModel> Recommended =>
    [
        new(
            "qwen3.8:27b",
            T("Recommended"),
            T("Closest to Claude in our comparison: places the works most similarly. Newer model, slightly smaller."),
            17.7,
            32,
            T("more than 2"),
            "2"),
        new(
            "qwen3-vl:32b",
            T("Accurate"),
            T("The strongest image model that runs on a Mac with 48 GB. Reads every part of the image with full computing power."),
            20.9,
            32,
            "9–10",
            "4–5",
            "qwen3-vl:32b-instruct"),
        new(
            "qwen3-vl:30b",
            T("Quick"),
            T("Same size, but only a small part of the model works on each word (mixture of experts). Much faster, slightly less accurate."),
            19.6,
            32,
            "2–3",
            T("under 1"),
            "qwen3-vl:30b-a3b-instruct"),
    ];

    public static LocalModel? Find(string? name) => Recommended.FirstOrDefault(m => m.Name == name);

    public static string? FastVariantOf(string model) => Find(model)?.FastVariant;

    public static bool IsKnown(string name) => Recommended.Any(m => m.Name == name || m.FastVariant == name);
}
