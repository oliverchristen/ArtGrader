using static ArtGrader.Core.Localization.Texts;

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
            Level.Uebertroffen => T("Exceeded"),
            Level.Erfuellt => T("Met"),
            Level.Teilweise => T("Partly met"),
            Level.Nicht => T("Not met"),
            _ => level.ToString(),
        };

    public static string Short(Level level) =>
        level switch
        {
            Level.Uebertroffen => T("very good"),
            Level.Erfuellt => T("good"),
            Level.Teilweise => T("sufficient"),
            Level.Nicht => T("insufficient"),
            _ => level.ToString(),
        };

    public static string Label(Confidence confidence) =>
        confidence switch
        {
            Confidence.Tief => T("unsure"),
            Confidence.Mittel => T("fairly sure"),
            Confidence.Hoch => T("sure"),
            _ => confidence.ToString(),
        };

    public static string Label(SubmissionStatus status) =>
        status switch
        {
            SubmissionStatus.Neu => T("New"),
            SubmissionStatus.InWarteschlange => T("Queued"),
            SubmissionStatus.WirdBewertet => T("Being assessed"),
            SubmissionStatus.Vorschlag => T("Suggested"),
            SubmissionStatus.Abgeschlossen => T("Completed"),
            SubmissionStatus.Fehler => T("Error"),
            _ => status.ToString(),
        };
}

public enum DocumentKind
{
    Aufgabe = 0,
    Beurteilung = 1,
    Unterricht = 2,
}
