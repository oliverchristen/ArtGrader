namespace ArtGrader.Core.Domain;

public sealed class GradingSettings
{
    public int PointsUebertroffen { get; set; } = 6;
    public int PointsErfuellt { get; set; } = 5;
    public int PointsTeilweise { get; set; } = 4;
    public int PointsNicht { get; set; } = 3;
    public decimal RoundingStep { get; set; } = 0.25m;
    public decimal MinGrade { get; set; } = 1m;
    public decimal MaxGrade { get; set; } = 6m;

    public int PointsFor(Level level) =>
        level switch
        {
            Level.Uebertroffen => PointsUebertroffen,
            Level.Erfuellt => PointsErfuellt,
            Level.Teilweise => PointsTeilweise,
            _ => PointsNicht,
        };

    public int MaxLevelPoints => new[] { PointsUebertroffen, PointsErfuellt, PointsTeilweise, PointsNicht }.Max();
}
