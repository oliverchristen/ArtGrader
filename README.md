# ArtGrader

Ein lokales Werkzeug für Lehrpersonen im Bildnerischen Gestalten: Die KI (Claude von Anthropic) schlägt anhand des
Beurteilungsbogens eine Bewertung für fotografierte Schülerarbeiten vor. Die Lehrperson prüft jeden Vorschlag,
korrigiert ihn und entscheidet über die Note.

Läuft auf **Windows** und **macOS** (Intel und Apple Silicon). Alle Daten bleiben auf dem eigenen Computer.

## So funktioniert es

1. **Prüfung erstellen** – Titel, Klasse, Datum.
2. **Aufgabe & Kriterien** – Aufgabenblatt (Word/PDF) und Beurteilungsbogen (Word/PDF) hochladen. Die KI liest die
   Kategorien und Kriterien aus. Kriterien, die man auf einem Foto nicht sehen kann (z. B. «Prozess und Engagement»),
   werden als «nur Lehrperson» markiert. Alles lässt sich anpassen: Wortlaut, Reihenfolge, Gewicht.
3. **Arbeiten & Bewertung** – Fotos hineinziehen. Jedes Foto wird eine Arbeit; mehrere Fotos einer Arbeit lassen
   sich zusammenführen. Mit «Alle neuen bewerten» schlägt die KI pro Kriterium eine Stufe mit Begründung vor
   (ca. 30–60 s pro Arbeit, drei gleichzeitig).
4. **Prüfen** – Pro Arbeit: Foto links, Kriterien rechts. Gestrichelt = Vorschlag der KI, gefüllt = gilt. Ein Klick
   übersteuert den Vorschlag. Unsichere Einschätzungen sind gelb markiert. «Abschliessen & weiter» springt zur
   nächsten offenen Arbeit.
5. **Übersicht & Export** – Notenliste als Excel, Beurteilungsbögen als PDF mit Foto der Arbeit, Note und
   Rückmeldung (einzeln oder für die ganze Klasse).

### Notenberechnung

Übertroffen = 3, Erfüllt = 2, Teilweise = 1, Nicht erfüllt = 0 Punkte (anpassbar), jedes Kriterium mit seinem
Gewicht. **Note = Punkte / Maximum × 5 + 1**, gerundet auf halbe Noten (oder Viertel/Zehntel). Die Note berechnet das
Programm, nicht die KI. Eine Note kann pro Arbeit auch manuell gesetzt werden.

## Installation (ohne Programmierkenntnisse)

1. Die ZIP-Datei für dein System herunterladen und entpacken:
   - Windows: `ArtGrader-win-x64.zip`
   - Mac mit Apple-Chip (M1/M2/M3/…): `ArtGrader-osx-arm64.zip`
   - Mac mit Intel-Prozessor: `ArtGrader-osx-x64.zip`
2. Starten:
   - **Windows:** `ArtGrader.exe` doppelklicken. Erscheint «Der Computer wurde durch Windows geschützt»:
     *Weitere Informationen* → *Trotzdem ausführen*.
   - **Mac:** Im Programm «Terminal» einmal `bash ` eintippen (mit Leerzeichen), die Datei `install.sh` aus dem
     entpackten Ordner ins Terminalfenster ziehen und Enter drücken. Das Skript kopiert ArtGrader nach
     `~/Applications/ArtGrader` und gibt es für macOS frei. Danach dort `ArtGrader.command` doppelklicken.
3. Der Browser öffnet sich mit http://localhost:5080. Das schwarze Fenster (Terminal) offen lassen, solange du
   arbeitest; Schliessen beendet ArtGrader.
4. Unter **Einstellungen** den Anthropic API-Schlüssel eintragen (console.anthropic.com → API Keys).

### Aktualisieren

ArtGrader beenden (Terminalfenster schliessen), die neue Version wie oben installieren – auf dem Mac wieder mit
`install.sh`. Prüfungen, Fotos, Noten und der API-Schlüssel liegen im Datenordner und bleiben erhalten. Nötige
Änderungen an der Datenbank macht ArtGrader beim ersten Start selbst. Vorher den Datenordner zu kopieren schadet nie.

### Kosten

Mit Claude Opus 5.5 und Gründlichkeit «Hoch» kostet eine Arbeit etwa 7–10 Rappen (abgerechnet über das
Anthropic-Konto). In den Einstellungen kann das günstigere Claude Sonnet 5.5 gewählt werden.

### Lokales Modell statt Cloud (Ollama)

Unter **Einstellungen → Lokal (Ollama)** bewertet ein Modell auf dem eigenen Computer. Dann verlässt nichts den
Computer, und es entstehen keine Kosten. Dafür ist es langsamer und weniger genau.

1. [Ollama](https://ollama.com/download) installieren und starten.
2. Im Terminal ein Bildmodell laden, für einen Mac mit 48 GB Arbeitsspeicher z. B.:
   `ollama pull qwen3-vl:32b` (21 GB Download).
3. In ArtGrader unter Einstellungen «Lokal (Ollama)» wählen, das Modell auswählen und speichern.

Das lokale Modell kann keine PDF-Dateien lesen: Aufgabe und Beurteilungsbogen als Word hochladen oder den Text
einfügen. Die Arbeiten werden nacheinander bewertet.

### Datenschutz

- Gespeichert wird lokal: Windows `%LOCALAPPDATA%\ArtGrader`, macOS `~/Library/Application Support/ArtGrader`.
- Mit Claude gehen nur Aufgabentext, Kriterien und Fotos an Anthropic – **keine Namen**. Auf den Fotos sollten
  keine Namen sichtbar sein. Mit dem lokalen Modell verlässt nichts den Computer.
- Der API-Schlüssel wird verschlüsselt gespeichert. Der Schlüssel dazu ist an das Benutzerkonto gebunden (Windows:
  Benutzerkonto, macOS: Schlüsselbund). Eine Kopie des Datenordners verrät den API-Schlüssel nicht; auf einem neuen
  Computer muss er neu eingetragen werden.
- Datenbank und Fotos sind nicht separat verschlüsselt. Den Computer mit BitLocker (Windows) bzw. FileVault (macOS)
  verschlüsseln.
- Die Nutzung mit der Schulleitung abklären (Datenschutzrichtlinien des Kantons).

### Grenzen

- Die KI sieht nur das Foto. Prozess, Skizzen und Arbeitshaltung beurteilt die Lehrperson.
- Fotos möglichst gerade, gut beleuchtet und formatfüllend aufnehmen; Details als zusätzliche Fotos.
- iPhone-Fotos im HEIC-Format werden nicht angenommen (Kamera-Einstellung «Maximale Kompatibilität» oder als JPEG
  exportieren).
- Die Vorschläge sind ein Hilfsmittel. Zwei Bewertungen derselben Arbeit können um eine Stufe abweichen.

## Für Entwickler

- .NET 11, Blazor Web App (Interactive Server), MudBlazor, EF Core + SQLite, Anthropic C# SDK, SkiaSharp,
  DocumentFormat.OpenXml, ClosedXML, QuestPDF.
- `src/ArtGrader.Core` – Domäne, Notenberechnung, Dokument- und Bildaufbereitung, KI-Anbindung
  (`IModelGateway` mit `AnthropicClaudeGateway` und `OllamaGateway`, Auswahl über `RoutingModelGateway`).
- Schrift: DM Sans liegt unter `wwwroot/fonts` (SIL Open Font License), damit die App offline läuft.
- `src/ArtGrader.Web` – Oberfläche, Speicherung, Warteschlange, Exporte.
- `test/` – xUnit-Tests (ohne echte API-Aufrufe).

```shell
dotnet test ArtGrader.slnx
dotnet run --project src/ArtGrader.Web            # http://localhost:5080
./publish.sh osx-arm64 osx-x64 win-x64             # macOS/Linux: ZIPs in artifacts/
./publish.ps1                                      # Windows; Mac-Ordner danach auf dem Mac mit install.sh einrichten
```

- `ANTHROPIC_API_KEY` übersteuert den gespeicherten Schlüssel, `ARTGRADER_DATA` den Datenordner,
  `ARTGRADER_NO_BROWSER=1` verhindert das Öffnen des Browsers.
- Schemaänderungen: `dotnet ef migrations add <Name> --project src/ArtGrader.Web --output-dir Data/Migrations`.
  Beim Start werden Migrationen automatisch angewendet.
- Unter Windows gebaute Mac-Versionen sind weder ausführbar markiert noch signiert; `build/install.sh` holt das auf
  dem Mac nach (Ausführungsrecht, Quarantäne entfernen, Ad-hoc-Signatur). Die ZIPs aus dem CI-Job `macos-latest`
  sind bereits signiert.

### Lizenzen

QuestPDF läuft unter der Community-Lizenz (gratis für Einzelpersonen, Non-Profits und Organisationen unter
1 Mio. USD Jahresumsatz). Alle anderen Bibliotheken sind MIT/Apache-lizenziert.
