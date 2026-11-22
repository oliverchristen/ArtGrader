namespace ArtGrader.Core.Domain;

public enum Level
{
    Nicht = 0,
    Teilweise = 1,
    Erfuellt = 2,
    Uebertroffen = 3,
}

public enum AssessedBy
{
    Ai = 0,
    Teacher = 1,
}

public enum Confidence
{
    Tief = 0,
    Mittel = 1,
    Hoch = 2,
}

public enum SubmissionStatus
{
    Neu = 0,
    InWarteschlange = 1,
    WirdBewertet = 2,
    Vorschlag = 3,
    Abgeschlossen = 4,
    Fehler = 5,
}

public static class LevelText
{
    public static IReadOnlyList<Level> AllDescending { get; } =
        [Level.Uebertroffen, Level.Erfuellt, Level.Teilweise, Level.Nicht];

    public static string Label(Level level) =>
        level switch
        {
            Level.Uebertroffen => "Übertroffen",
            Level.Erfuellt => "Erfüllt",
            Level.Teilweise => "Teilweise erfüllt",
            Level.Nicht => "Nicht erfüllt",
            _ => level.ToString(),
        };

    public static string Short(Level level) =>
        level switch
        {
            Level.Uebertroffen => "sehr gut",
            Level.Erfuellt => "gut",
            Level.Teilweise => "genügend",
            Level.Nicht => "ungenügend",
            _ => level.ToString(),
        };

    public static string Label(Confidence confidence) =>
        confidence switch
        {
            Confidence.Tief => "unsicher",
            Confidence.Mittel => "mittel",
            Confidence.Hoch => "sicher",
            _ => confidence.ToString(),
        };

    public static string Label(SubmissionStatus status) =>
        status switch
        {
            SubmissionStatus.Neu => "Neu",
            SubmissionStatus.InWarteschlange => "In Warteschlange",
            SubmissionStatus.WirdBewertet => "Wird bewertet",
            SubmissionStatus.Vorschlag => "Vorschlag",
            SubmissionStatus.Abgeschlossen => "Abgeschlossen",
            SubmissionStatus.Fehler => "Fehler",
            _ => status.ToString(),
        };
}

public enum DocumentKind
{
    Aufgabe = 0,
    Beurteilung = 1,
    Unterricht = 2,
}
