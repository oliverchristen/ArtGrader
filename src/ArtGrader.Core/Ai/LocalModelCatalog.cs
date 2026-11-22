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
            T("Empfohlen"),
            T("Im Vergleich am nächsten an Claude: ordnet die Arbeiten am ähnlichsten ein. Neueres Modell, etwas kleiner."),
            17.7,
            32,
            T("mehr als 2"),
            "2"),
        new(
            "qwen3-vl:32b",
            T("Genau"),
            T("Das stärkste Bildmodell, das auf einem Mac mit 48 GB läuft. Liest alle Bildteile mit voller Rechenleistung."),
            20.9,
            32,
            "9–10",
            "4–5",
            "qwen3-vl:32b-instruct"),
        new(
            "qwen3-vl:30b",
            T("Flink"),
            T("Gleich gross, aber pro Wort rechnet nur ein kleiner Teil des Modells mit (Mixture of Experts). Deutlich schneller, etwas ungenauer."),
            19.6,
            32,
            "2–3",
            T("unter 1"),
            "qwen3-vl:30b-a3b-instruct"),
    ];

    public static LocalModel? Find(string? name) => Recommended.FirstOrDefault(m => m.Name == name);

    public static string? FastVariantOf(string model) => Find(model)?.FastVariant;

    public static bool IsKnown(string name) => Recommended.Any(m => m.Name == name || m.FastVariant == name);
}
