namespace ArtGrader.Core.Ai;

public sealed record LocalModel(
    string Name,
    string Title,
    string Description,
    double DownloadGb,
    int MinMemoryGb,
    string MinutesWithThinking,
    string MinutesFast);

public static class LocalModelCatalog
{
    public static IReadOnlyList<LocalModel> Recommended { get; } =
    [
        new(
            "qwen3-vl:32b",
            "Genau",
            "Das stärkste Bildmodell, das auf einem Mac mit 48 GB läuft. Liest alle Bildteile mit voller Rechenleistung.",
            20.9,
            32,
            "6–7",
            "2–3"),
        new(
            "qwen3-vl:30b",
            "Schnell",
            "Gleich gross, aber pro Wort rechnet nur ein kleiner Teil des Modells mit (Mixture of Experts). Deutlich schneller, etwas ungenauer.",
            19.6,
            32,
            "1–2",
            "unter 1"),
    ];

    public static LocalModel? Find(string? name) => Recommended.FirstOrDefault(m => m.Name == name);
}
