using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArtGrader.Core.Ai;

public sealed class LocalRedactionFinder(OllamaGateway ollama, IAiSettingsSource settings)
{
    public const int MaxCharacters = 20000;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
    private (DateTimeOffset At, string? Model)? cached;

    public async Task<string?> AvailableModelAsync(CancellationToken cancellationToken)
    {
        if (cached is { } hit && DateTimeOffset.UtcNow - hit.At < CacheDuration)
        {
            return hit.Model;
        }
        var current = settings.Current();
        string? model;
        try
        {
            var installed = (await ollama.ListModelsAsync(current.OllamaUrl, cancellationToken)).Where(m => !m.Cloud).Select(m => m.Name).ToList();
            model = installed.FirstOrDefault(m => m == current.OllamaModel)
                ?? LocalModelCatalog.Recommended.Select(r => r.Name).FirstOrDefault(installed.Contains)
                ?? installed.FirstOrDefault();
        }
        catch (ModelException)
        {
            model = null;
        }
        cached = (DateTimeOffset.UtcNow, model);
        return model;
    }

    private const string System = """
        Du hilfst einer Lehrperson, ein Dokument für den Unterricht zu anonymisieren, bevor es an eine KI geht.
        Finde alle Stellen, die auf die Schule oder auf Personen hinweisen: Name der Schule oder des Schulhauses,
        Ort oder Gemeinde der Schule, Namen und Kürzel von Lehrpersonen, Namen von Schülerinnen und Schülern,
        Klassenbezeichnungen (zum Beispiel «2b»), Adressen, E-Mail-Adressen, Telefonnummern und Webadressen.

        Keine Treffer sind: Inhalte der Aufgabe, Fachbegriffe, Beurteilungskriterien, Jahreszahlen, Namen und Kürzel
        von Schulfächern (zum Beispiel «BG» für Bildnerisches Gestalten, «TTG», «NMG») und allgemeine Wörter wie
        «Schülerin», «Lehrperson», «Klasse» oder «Name».

        Gib jede Stelle genau so zurück, wie sie im Text steht, mit gleicher Schreibweise und ohne zusätzliche Wörter.
        Nenne zu jeder Stelle kurz den Grund, zum Beispiel «Schulhaus», «Ort», «Lehrperson».
        Wenn es keine solchen Stellen gibt, gib eine leere Liste zurück.
        """;

    public async Task<IReadOnlyList<(string Text, string Reason)>> FindAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }
        var model = await AvailableModelAsync(cancellationToken)
            ?? throw new ModelNotConfiguredException("Es ist kein lokales Modell installiert oder Ollama läuft nicht.");
        var document = text.Length > MaxCharacters ? text[..MaxCharacters] : text;
        var response = await ollama.CompleteJsonAsync(
            new ModelJsonRequest(
                System,
                [],
                [new TextPart("<dokument>\n" + document + "\n</dokument>")],
                Schema(),
                MaxTokens: 2000,
                Quick: true,
                LocalOnly: true,
                Model: model),
            cancellationToken);

        try
        {
            var passages = JsonNode.Parse(response.Json)?["stellen"]?.AsArray() ?? [];
            return passages
                .Select(p => ((string?)p?["text"] ?? "", (string?)p?["grund"] ?? ""))
                .Where(p => p.Item1.Trim().Length >= 2 && text.Contains(p.Item1.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(p => (p.Item1.Trim(), p.Item2.Trim()))
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new ModelException("Das lokale Modell hat keine lesbare Antwort geliefert. Bitte nochmals versuchen.", ex);
        }
    }

    private static JsonObject Schema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["stellen"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["text"] = new JsonObject { ["type"] = "string" },
                        ["grund"] = new JsonObject { ["type"] = "string" },
                    },
                    ["required"] = new JsonArray("text", "grund"),
                    ["additionalProperties"] = false,
                },
            },
        },
        ["required"] = new JsonArray("stellen"),
        ["additionalProperties"] = false,
    };
}
