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
    public static IReadOnlyList<LocalModel> Recommended { get; } =
    [
        new(
            "qwen3.8:27b",
            "Empfohlen",
            "Im Vergleich am nächsten an Claude: ordnet die Arbeiten am ähnlichsten ein. Neueres Modell, etwas kleiner.",
            17.7,
            32,
            "mehr als 2",
            "2"),
        new(
            "qwen3-vl:32b",
            "Genau",
            "Das stärkste Bildmodell, das auf einem Mac mit 48 GB läuft. Liest alle Bildteile mit voller Rechenleistung.",
            20.9,
            32,
            "9–10",
            "4–5",
            "qwen3-vl:32b-instruct"),
        new(
            "qwen3-vl:30b",
            "Flink",
            "Gleich gross, aber pro Wort rechnet nur ein kleiner Teil des Modells mit (Mixture of Experts). Deutlich schneller, etwas ungenauer.",
            19.6,
            32,
            "2–3",
            "unter 1",
            "qwen3-vl:30b-a3b-instruct"),
    ];

    public static LocalModel? Find(string? name) => Recommended.FirstOrDefault(m => m.Name == name);

    public static string? FastVariantOf(string model) => Find(model)?.FastVariant;

    public static bool IsKnown(string name) => Recommended.Any(m => m.Name == name || m.FastVariant == name);
}
