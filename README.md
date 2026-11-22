# ArtGrader

Ein lokales Werkzeug für Lehrpersonen im Bildnerischen Gestalten: Eine KI schlägt anhand des Beurteilungsbogens eine
Bewertung für fotografierte Schülerarbeiten vor. Die Lehrperson prüft jeden Vorschlag, korrigiert ihn und entscheidet
über die Note. Die KI ist als zweite Meinung gedacht, damit die Beurteilung objektiver wird.

Als KI dient Claude von Anthropic (Cloud) oder ein Modell auf dem eigenen Computer (Ollama). Läuft auf **Windows**
und **macOS** (Intel und Apple Silicon). Alle Daten bleiben auf dem eigenen Computer.

Für die Schulleitung: Die Übersicht «ArtGrader für die Schulleitung» beschreibt ohne Technik, welche Daten wohin
gehen, warum die Lehrperson entscheidet und was die Schule klären sollte. Sie öffnet sich in ArtGrader unter
**Einstellungen → Datenschutz → Übersicht für die Schulleitung**.

## So funktioniert es

1. **Prüfung erstellen** – Titel, Klasse, Datum.
2. **Aufgabe & Kriterien**
   - Aufgabenblatt hochladen (Word, PowerPoint, PDF oder Text) oder eintippen. Optional das Unterrichtsmaterial
     dazu, zum Beispiel die PowerPoint-Präsentation zum Thema; so weiss die KI, was behandelt wurde.
   - ArtGrader zeigt jedes Dokument so, wie die KI es liest. Was auf die Schule hinweist (Schulhaus, Ort, Namen von
     Lehrpersonen, Logo), lässt sich entfernen: im Text direkt löschen, PDF-Seiten zuschneiden oder mit Balken
     abdecken. ArtGrader schlägt Stellen vor, siehe «Schwärzen» unten.
   - Beurteilungsbogen hochladen, prüfen und schwärzen, dann «Kriterien auslesen». Die KI überträgt Kategorien und
     Kriterien. Kriterien, die man auf einem Foto nicht sehen kann (z. B. «Prozess und Engagement»), werden als «nur
     Lehrperson» markiert. Alles lässt sich anpassen: Wortlaut, Reihenfolge, Gewicht.
   - «Nur lokal bewerten» legt fest, dass für diese Prüfung nichts in die Cloud geht.
3. **Arbeiten erfassen** – Fotos hineinziehen. Jedes Foto wird eine Arbeit mit einem Zufallsnamen wie «Fuchs
   Indigo»; echte Namen trägt die Lehrperson selbst ein, sie bleiben auf dem Computer. Mehrere Fotos einer Arbeit
   lassen sich zusammenführen und einzelne Fotos wieder als eigene Arbeit abtrennen. Name oder Signatur auf dem Werk
   lassen sich mit einem Balken abdecken, das Foto lässt sich zuschneiden; die KI sieht nur das bearbeitete Foto.
4. **Arbeiten bewerten** – Mit «… Arbeiten bewerten» schlägt die KI pro Kriterium eine Stufe mit Begründung vor
   (Claude: ca. 30–60 s pro Arbeit, drei gleichzeitig). Dann jede Arbeit prüfen: Foto links, Kriterien rechts. Alles
   von der KI ist blau mit «✦ KI» markiert, farbig gefüllt = gilt. Ein Klick übersteuert den Vorschlag. Unsichere
   Einschätzungen sind gelb markiert. «Abschliessen & weiter» springt zur nächsten offenen Arbeit.
5. **Noten** – Alle Arbeiten mit ihrer Note auf einen Blick. Oben die Notenberechnung: Punkte pro Stufe und
   Rundung; jede Änderung gilt sofort für alle Noten.
6. **Übersicht & Export** – Notenliste als Excel, Beurteilungsbögen als PDF mit Foto der Arbeit, Note und
   Rückmeldung (einzeln oder für die ganze Klasse). Das PDF vermerkt, dass die Einschätzung mit KI-Unterstützung
   entstand und ob die Lehrperson sie abgeschlossen hat.

Alle Eingaben werden etwa eine Sekunde nach der letzten Änderung automatisch gespeichert. Jeder Schritt führt
von oben nach unten; unten geht es mit einem Knopf zum nächsten Schritt.

Nach Ende des Semesters (ab 1. Februar und ab 1. August) erinnert die Startseite an Prüfungen, die noch Fotos von Schülerarbeiten
enthalten, und löscht die Fotos auf Knopfdruck. Noten und Beurteilungen bleiben.

### Notenberechnung

Übertroffen = 6, Erfüllt = 5, Teilweise = 4, Nicht erfüllt = 3 Punkte (anpassbar), jedes Kriterium mit seinem
Gewicht. **Note = Punkte / Maximum × 5 + 1**, gerundet auf Viertelnoten (oder halbe Noten/Zehntel). Die Note berechnet das
Programm, nicht die KI. Eine Note kann pro Arbeit auch manuell gesetzt werden.

### Schwärzen

ArtGrader schlägt in Aufgabe, Beurteilungsbogen und Unterrichtsmaterial Stellen zum Schwärzen vor. Geschwärzt wird
erst, wenn die Lehrperson bestätigt.

- **Eigene Liste:** Unter Einstellungen «Immer schwärzen» einmal Schulhaus, Ort, eigenen Namen und Kürzel eintragen.
- **Kontaktangaben:** E-Mail-Adressen, Telefonnummern und Webadressen erkennt ArtGrader selbst.
- **Mit lokalem Modell prüfen:** Ist Ollama mit einem lokalen Modell installiert, sucht dieses weitere Namen und
  Orte, auch wenn für die Bewertung Claude gewählt ist. Der Text verlässt dafür den Computer nicht.

Die Vorschläge gelten für Text. PDF-Seiten deckt die Lehrperson von Hand ab.

## Installation (ohne Programmierkenntnisse)

1. Die ZIP-Datei für dein System herunterladen und entpacken:
   - Windows: `ArtGrader-win-x64.zip`
   - Mac mit Apple-Chip (M1/M2/M3/…): `ArtGrader-osx-arm64.zip`
   - Mac mit Intel-Prozessor: `ArtGrader-osx-x64.zip`
2. Starten:
   - **Windows:** Vor dem Entpacken die ZIP-Datei mit der rechten Maustaste anklicken → *Eigenschaften* → unten
     *Zulassen* ankreuzen → *OK*. Sonst erscheint beim Start «Der Computer wurde durch Windows geschützt», weil
     ArtGrader nicht signiert ist. Dann `ArtGrader.exe` doppelklicken. Erscheint die Meldung trotzdem:
     *Weitere Informationen* → *Trotzdem ausführen*. Fehlt diese Schaltfläche (oft auf verwalteten Schul- oder
     Firmencomputern), muss die Informatik ArtGrader freigeben.
   - **Mac:** Einmalig im Programm «Terminal» `bash ` eintippen (mit Leerzeichen), die Datei `install.sh` aus dem
     entpackten Ordner ins Terminalfenster ziehen und Enter drücken. Das Skript kopiert `ArtGrader.app` nach
     «Programme» im Benutzerordner, gibt es für macOS frei und startet es. Danach startet ArtGrader per Doppelklick
     auf `ArtGrader.app` (Tipp: ins Dock ziehen).
3. Der Browser öffnet sich mit http://localhost:5080. Es erscheint kein weiteres Fenster. Ein zweiter Doppelklick
   öffnet ArtGrader im Browser, auch wenn es schon läuft.
4. **Beenden:** in ArtGrader oben rechts auf ⏻. Ist 30 Minuten lang kein ArtGrader-Tab offen und läuft keine
   Bewertung, beendet sich ArtGrader von selbst.
5. Unter **Einstellungen** Claude oder ein lokales Modell wählen (siehe unten). Für Claude einen API-Schlüssel
   eintragen.

### Aktualisieren

ArtGrader beenden (⏻ oben rechts), die neue Version wie oben installieren – auf dem Mac wieder mit `install.sh`, das
eine laufende Version selbst beendet. Prüfungen, Fotos, Noten und der API-Schlüssel liegen im Datenordner und bleiben erhalten, ebenso die
lokalen Modelle von Ollama. Nötige Änderungen an der Datenbank macht ArtGrader beim ersten Start selbst. Vorher den
Datenordner zu kopieren schadet nie.

### Claude (Cloud) und Kosten

ArtGrader braucht einen API-Schlüssel der Claude Developer Platform (platform.claude.com → API Keys). Die Nutzung
wird dort nach Verbrauch abgerechnet; ein Claude-Abo (Pro, Max) gilt dafür nicht. Mit Claude Opus 5.5 und
Gründlichkeit «Hoch» kostet eine Arbeit etwa 7–10 Rappen. In den Einstellungen kann das günstigere Claude Sonnet 5.5
gewählt werden.

### Lokales Modell statt Cloud (Ollama)

Unter **Einstellungen → Lokal (Ollama)** bewertet ein Modell auf dem eigenen Computer. Dann verlässt nichts den
Computer, und es entstehen keine Kosten. Dafür ist es langsamer und weniger genau.

1. [Ollama](https://ollama.com/download) installieren und starten.
2. In ArtGrader unter Einstellungen «Lokal (Ollama)» wählen und ein Modell herunterladen. Empfohlen ist
   `qwen3.8:27b` (18 GB, ab 32 GB Arbeitsspeicher) im Modus «Schnell». Andere Bildmodelle lassen sich im Terminal
   mit `ollama pull <name>` laden.
3. Das Modell auswählen. Einstellungen werden automatisch gespeichert.

Die Modelle werden nur auf Knopfdruck heruntergeladen und liegen im Ordner von Ollama. Die Arbeiten werden
nacheinander bewertet. PDF-Seiten gehen als Bilder an das Modell. Cloud-Modelle von Ollama (Namen mit `cloud`)
blendet ArtGrader aus und verwendet sie nie, weil sie auf fremden Servern rechnen.

### Datenschutz

- Gespeichert wird lokal: Windows `%LOCALAPPDATA%\ArtGrader`, macOS `~/Library/Application Support/ArtGrader`.
  Im Unterordner `logs` liegen Protokolle der letzten 14 Tage, falls etwas nicht funktioniert.
- Mit Claude gehen nur Aufgabe, Unterrichtsmaterial, Kriterien und Fotos an Anthropic – **keine Namen** und keine
  Dateinamen. Fotos werden neu gespeichert, Metadaten (Kamera, Ort, Datum) fallen weg. Mit dem lokalen Modell
  verlässt nichts den Computer.
- Namen oder Signaturen auf den Werken abdecken (oder auf der Rückseite signieren lassen), Hinweise auf die Schule in
  den Dokumenten schwärzen.
- Der API-Schlüssel wird verschlüsselt gespeichert. Der Schlüssel dazu ist an das Benutzerkonto gebunden (Windows:
  Benutzerkonto, macOS: Schlüsselbund). Eine Kopie des Datenordners verrät den API-Schlüssel nicht; auf einem neuen
  Computer muss er neu eingetragen werden.
- Datenbank und Fotos sind nicht separat verschlüsselt. Den Computer mit BitLocker (Windows) bzw. FileVault (macOS)
  verschlüsseln.
- Grundlagen im Kanton Solothurn:
  [Handreichung zu KI-Anwendungen](https://so.ch/fileadmin/internet/dbk/dbk-vsa/Schulbetrieb_und_Unterricht/Informatische_Bildung/Handreichung_KI-Anwendungen_und_Kriterienkatalog/Handreichung_KI-Anwendungen.pdf)
  (Kapitel 4.4: KI als Unterstützung bei der Beurteilung) und
  [Merkblatt Datenschutz](https://so.ch/fileadmin/internet/dbk/dbk-vsa/Rechtsetzende_Erlasse/Merkblatt_Datenschutz_VSA_August_2019.pdf)
  des Volksschulamts. Die Nutzung mit der Schulleitung abklären.

### Grenzen

- Die KI sieht nur das Foto. Prozess, Skizzen und Arbeitshaltung beurteilt die Lehrperson.
- Fotos möglichst gerade, gut beleuchtet und formatfüllend aufnehmen; Details als zusätzliche Fotos.
- iPhone-Fotos im HEIC-Format werden nicht angenommen (Kamera-Einstellung «Maximale Kompatibilität» oder als JPEG
  exportieren).
- PDFs mit nicht eingebetteten Schriften erscheinen als leere Seiten. Exporte aus Word und PowerPoint sind in
  Ordnung; was ArtGrader anzeigt, sieht auch die KI.
- Die Vorschläge sind ein Hilfsmittel. Zwei Bewertungen derselben Arbeit können um eine Stufe abweichen.

## Für Entwickler

- .NET 11, Blazor Web App (Interactive Server), MudBlazor, EF Core + SQLite, Anthropic C# SDK, SkiaSharp,
  PDFtoImage (PDFium), DocumentFormat.OpenXml, ClosedXML, QuestPDF.
- `src/ArtGrader.Core` – Domäne, Notenberechnung, Dokument- und Bildaufbereitung, Schwärzen, KI-Anbindung
  (`IModelGateway` mit `AnthropicClaudeGateway` und `OllamaGateway`, Auswahl über `RoutingModelGateway`;
  `LocalRedactionFinder` nutzt immer Ollama).
- `src/ArtGrader.Web` – Oberfläche, Speicherung, Warteschlange, Exporte.
- Schrift: DM Sans liegt unter `wwwroot/fonts` (SIL Open Font License), damit die App offline läuft.
- `test/` – xUnit-Tests (ohne echte API-Aufrufe).

```shell
dotnet tool restore
dotnet test ArtGrader.slnx
dotnet run --project src/ArtGrader.Web            # http://localhost:5080
./publish.sh osx-arm64 osx-x64 win-x64             # macOS/Linux: ZIPs in artifacts/
./publish.ps1                                      # Windows; Mac-Ordner danach auf dem Mac mit install.sh einrichten
```

- `ANTHROPIC_API_KEY` übersteuert den gespeicherten Schlüssel, `ARTGRADER_DATA` den Datenordner,
  `ARTGRADER_NO_BROWSER=1` verhindert das Öffnen des Browsers. `ArtGrader:IdleMinutes` (Standard 30, in Development
  0 = aus) steuert das automatische Beenden.
- Die Windows-Version ist eine Windows-App ohne Konsolenfenster (`WinExe`). Die Mac-Version ist ein App-Bundle ohne
  Dock-Symbol (`LSUIElement`): `Contents/MacOS/ArtGrader` ist ein Startskript, das das Programm aus
  `Contents/Resources/app` im Hintergrund startet. Läuft ArtGrader schon (`/api/ping`), öffnet ein zweiter Start nur
  den Browser. Beenden über `POST /beenden`.
- Schemaänderungen: `dotnet ef migrations add <Name> --project src/ArtGrader.Web --output-dir Data/Migrations`.
  Beim Start werden Migrationen automatisch angewendet.
- Die Schlüssel für den API-Schlüssel (ASP.NET Data Protection) sind unter Windows mit DPAPI an den Benutzer
  gebunden, unter macOS über einen Schlüssel im Schlüsselbund (`KeyLock`). Beim ersten Start ersetzt ArtGrader
  ungeschützte Schlüsseldateien.
- Unter Windows gebaute Mac-Versionen sind weder ausführbar markiert noch signiert; `build/install.sh` holt das auf
  dem Mac nach (Ausführungsrecht, Quarantäne entfernen, Ad-hoc-Signatur des Programms in `Resources/app`). Die ZIPs aus dem CI-Job `macos-latest`
  sind bereits signiert.

### Lizenzen

QuestPDF läuft unter der Community-Lizenz (gratis für Einzelpersonen, Non-Profits und Organisationen unter
1 Mio. USD Jahresumsatz). Alle anderen Bibliotheken stehen unter MIT-, Apache- oder BSD-Lizenzen.
