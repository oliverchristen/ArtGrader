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
            Level.Uebertroffen => T("Übertroffen"),
            Level.Erfuellt => T("Erfüllt"),
            Level.Teilweise => T("Teilweise erfüllt"),
            Level.Nicht => T("Nicht erfüllt"),
            _ => level.ToString(),
        };

    public static string Short(Level level) =>
        level switch
        {
            Level.Uebertroffen => T("sehr gut"),
            Level.Erfuellt => T("gut"),
            Level.Teilweise => T("genügend"),
            Level.Nicht => T("ungenügend"),
            _ => level.ToString(),
        };

    public static string Label(Confidence confidence) =>
        confidence switch
        {
            Confidence.Tief => T("unsicher"),
            Confidence.Mittel => T("mittel"),
            Confidence.Hoch => T("sicher"),
            _ => confidence.ToString(),
        };

    public static string Label(SubmissionStatus status) =>
        status switch
        {
            SubmissionStatus.Neu => T("Neu"),
            SubmissionStatus.InWarteschlange => T("In Warteschlange"),
            SubmissionStatus.WirdBewertet => T("Wird bewertet"),
            SubmissionStatus.Vorschlag => T("Vorschlag"),
            SubmissionStatus.Abgeschlossen => T("Abgeschlossen"),
            SubmissionStatus.Fehler => T("Fehler"),
            _ => status.ToString(),
        };
}

public enum DocumentKind
{
    Aufgabe = 0,
    Beurteilung = 1,
    Unterricht = 2,
}
