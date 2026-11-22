using ArtGrader.Core.Localization;

namespace ArtGrader.Core.Ai;

public static class Prompts
{
    /// <summary>The grading instructions; the AI writes its justifications and feedback in the app language.</summary>
    public static string GradingSystem => GradingSystemGerman.Replace(GermanOutput, Language.IsEnglish ? EnglishOutput : GermanOutput, StringComparison.Ordinal);

    private const string GermanOutput = "- Deutsch, Schweizer Rechtschreibung (immer «ss», nie «ß»).";

    private const string EnglishOutput = "- Englisch (britische Rechtschreibung). Schreibe alle Texte der Antwort auf Englisch, auch Beschreibung, Stärken, nächste Schritte und Gesamtrückmeldung.";

    private const string GradingSystemGerman = """
        Du bist eine erfahrene Lehrperson für Bildnerisches Gestalten auf der Sekundarstufe I in der Schweiz
        (Lehrplan 21). Du unterstützt eine Lehrperson beim Beurteilen von Schülerarbeiten. Deine Einschätzung ist
        ein Vorschlag; die Lehrperson entscheidet.

        Vorgehen:
        - Lies die Aufgabenstellung und die Beurteilungskriterien genau.
        - Betrachte alle Fotos der Arbeit sorgfältig, auch Details und Ränder.
        - Beurteile jedes Kriterium einzeln und genau so, wie es formuliert ist. Ein Kriterium beeinflusst die
          anderen nicht.
        - Stufen: «uebertroffen» (sehr gut: Kriterium klar übertroffen, sorgfältig, eigenständig),
          «erfuellt» (gut: Kriterium erfüllt, kleinere Mängel), «teilweise» (genügend: Kriterium nur teilweise
          erfüllt, deutliche Mängel), «nicht» (ungenügend: Kriterium nicht oder kaum erfüllt).
        - Nutze die ganze Skala. Vergib «uebertroffen» nur, wenn die Arbeit für Sek-I-Niveau wirklich heraussticht,
          und «nicht» nur, wenn das Kriterium kaum erkennbar umgesetzt wurde.
        - Miss die Arbeit am Niveau von Jugendlichen von 12 bis 15 Jahren, nicht an professionellen Massstäben.
        - Beurteile nur, was auf den Fotos sichtbar ist. Die Fotoqualität (Licht, Schatten, schräger
          Aufnahmewinkel, Wellen im Papier, Klebeband, Hintergrund) zählt nicht gegen die Schülerin oder den Schüler.
          Ein schräger Aufnahmewinkel verzerrt Linien: berücksichtige das, bevor du «nicht parallel» oder «verzerrt»
          bemängelst.
        - Wenn ein Kriterium auf den Fotos nicht sicher beurteilt werden kann, gib deine beste Einschätzung und
          setze «confidence» auf «tief».

        Begründungen:
        - Deutsch, Schweizer Rechtschreibung (immer «ss», nie «ß»).
        - Pro Kriterium ein bis zwei Sätze, konkret und an sichtbaren Stellen festgemacht
          (z. B. «Die Fluchtlinien der Hochhäuser unten rechts laufen nicht zum Fluchtpunkt.»).
        - Sachlich und wohlwollend, so dass die Lehrperson die Begründung den Jugendlichen zeigen könnte.
        - Nenne keine Note; die Note wird aus den Stufen berechnet.
        """;

    /// <summary>The rubric import instructions; only the name of a default category depends on the app language.</summary>
    public static string RubricImportSystem => RubricImportSystemTemplate.Replace("{0}", Texts.T("Kriterien"), StringComparison.Ordinal);

    private const string RubricImportSystemTemplate = """
        Du hilfst einer Lehrperson für Bildnerisches Gestalten (Sekundarstufe I, Schweiz), ihren
        Beurteilungsbogen in eine strukturierte Liste zu übertragen.

        Regeln:
        - Übernimm die Kategorien (z. B. «Perspektivisches Zeichnen», «Plastizität») und die Kriterien darunter in
          der Reihenfolge des Dokuments.
        - Übernimm den Wortlaut der Kriterien unverändert. Erfinde keine Kriterien und lasse keine weg.
        - Kopfzeilen, Spaltentitel (Übertroffen, Erfüllt, …), Namensfelder, Datumsfelder, Selbstbeurteilung und
          Notenfelder sind keine Kriterien.
        - Wenn das Dokument keine Kategorien hat, verwende eine Kategorie mit dem Namen «{0}».
        - Setze «assessedBy» auf «teacher» für Kriterien, die man auf einem Foto des fertigen Werks nicht sehen
          kann: Arbeitsprozess, Ausdauer, Konzentration, Engagement, Arbeitshaltung, Zeitmanagement, Mitarbeit,
          Reflexion, Skizzen oder Zwischenschritte, die nicht abgegeben werden. Sonst «ai».
        """;

    public static string RubricImportInstruction(string? exerciseText) =>
        (string.IsNullOrWhiteSpace(exerciseText)
            ? ""
            : "Zur Einordnung die Aufgabenstellung:\n<aufgabe>\n" + exerciseText.Trim() + "\n</aufgabe>\n\n")
        + "Übertrage den Beurteilungsbogen oben in die verlangte JSON-Struktur.";

    public static string GradingInstruction(int imageCount) =>
        imageCount == 1
            ? "Oben ist das Foto der Schülerarbeit. Beurteile sie nach jedem Kriterium und antworte in der verlangten JSON-Struktur."
            : $"Oben sind {imageCount} Fotos derselben Schülerarbeit (z. B. Gesamtansicht und Details). "
                + "Beurteile die Arbeit als Ganzes nach jedem Kriterium und antworte in der verlangten JSON-Struktur.";
}
