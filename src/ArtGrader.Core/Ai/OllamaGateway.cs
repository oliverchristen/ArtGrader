using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using static ArtGrader.Core.Localization.Texts;

namespace ArtGrader.Core.Ai;

public sealed class OllamaGateway(HttpClient http, IAiSettingsSource settingsSource) : IModelGateway
{
    private const int ContextTokens = 32768;
    private readonly SemaphoreSlim oneAtATime = new(1, 1);

    public async Task<ModelJsonResponse> CompleteJsonAsync(ModelJsonRequest request, CancellationToken cancellationToken)
    {
        var settings = settingsSource.Current();
        var model = request.Model ?? settings.OllamaModel;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ModelNotConfiguredException(T("Es ist kein lokales Modell gewählt. Bitte unter «Einstellungen» ein Ollama-Modell auswählen."));
        }
        if (IsCloudName(model))
        {
            throw new ModelNotConfiguredException(
                T("«{0}» ist ein Cloud-Modell von Ollama. Damit würden die Fotos den Computer verlassen. Bitte unter «Einstellungen» ein lokales Modell wählen.", model));
        }

        var text = new StringBuilder();
        var images = new List<string>();
        foreach (var part in request.SharedContext.Concat(request.Content))
        {
            switch (part)
            {
                case TextPart t:
                    text.Append(t.Text).Append("\n\n");
                    break;
                case ImagePart image:
                    images.Add(Convert.ToBase64String(image.Jpeg));
                    text.Append($"[Bild {images.Count} ist angehängt]\n\n");
                    break;
            }
        }

        var think = !request.Quick && UsesThinking(settings.Effort);
        var fastVariant = think || request.Model is not null ? null : LocalModelCatalog.FastVariantOf(model);
        var body = new JsonObject
        {
            ["model"] = fastVariant ?? model,
            ["stream"] = false,
            ["format"] = request.Schema.DeepClone(),
            ["keep_alive"] = "15m",
            ["options"] = new JsonObject { ["num_ctx"] = ContextTokens, ["temperature"] = 0.2 },
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = text.ToString().Trim(),
                    ["images"] = new JsonArray(images.Select(i => (JsonNode)JsonValue.Create(i)!).ToArray()),
                }),
        };
        body["think"] = think;

        await oneAtATime.WaitAsync(cancellationToken);
        try
        {
            var response = await SendAsync(settings.OllamaUrl, body, cancellationToken);
            if (fastVariant is not null && response.NotFound)
            {
                body["model"] = model;
                response = await SendAsync(settings.OllamaUrl, body, cancellationToken);
            }
            if (response.Error?.Contains("does not support thinking", StringComparison.OrdinalIgnoreCase) == true)
            {
                body.Remove("think");
                response = await SendAsync(settings.OllamaUrl, body, cancellationToken);
            }
            if (response.Error is not null)
            {
                throw new ModelException(T("Das lokale Modell meldet einen Fehler: {0}", response.Error));
            }
            if (response.DoneReason == "length")
            {
                throw new ModelException(T("Die Antwort des lokalen Modells wurde abgeschnitten. Bitte nochmals versuchen."));
            }
            return new ModelJsonResponse(
                response.Message?.Content ?? "",
                body["model"]!.GetValue<string>(),
                new ModelUsage(response.PromptEvalCount, response.EvalCount, 0, 0));
        }
        finally
        {
            oneAtATime.Release();
        }
    }

    public static bool UsesThinking(string effort) => effort is "high" or "max";

    public async Task<IReadOnlyList<OllamaModelInfo>> ListModelsAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            var tags = await http.GetFromJsonAsync<TagsResponse>(new Uri(new Uri(baseUrl), "/api/tags"), cancellationToken);
            return (tags?.Models ?? [])
                .Select(m => new OllamaModelInfo(m.Name, m.Size, !string.IsNullOrEmpty(m.RemoteHost) || !string.IsNullOrEmpty(m.RemoteModel) || IsCloudName(m.Name)))
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            throw new ModelException(T("Ollama ist unter {0} nicht erreichbar. Läuft die Ollama-App?", baseUrl), ex);
        }
    }

    public async Task PullAsync(string baseUrl, string model, IProgress<PullProgress> progress, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(baseUrl), "/api/pull"))
        {
            Content = new StringContent(new JsonObject { ["model"] = model, ["stream"] = true }.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ModelException(T("Ollama ist unter {0} nicht erreichbar. Läuft die Ollama-App?", baseUrl), ex);
        }

        using (response)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                PullLine? update;
                try
                {
                    update = JsonSerializer.Deserialize<PullLine>(line);
                }
                catch (JsonException)
                {
                    continue;
                }
                if (update?.Error is { } error)
                {
                    throw new ModelException(T("Download fehlgeschlagen: {0}", error));
                }
                if (update is null)
                {
                    continue;
                }
                progress.Report(new PullProgress(update.Status ?? "", update.Completed, update.Total));
                if (update.Status == "success")
                {
                    return;
                }
            }
        }
        throw new ModelException(T("Der Download wurde unterbrochen. Bitte nochmals starten – er setzt dort fort, wo er aufgehört hat."));
    }

    private sealed record PullLine(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("completed")] long Completed,
        [property: JsonPropertyName("total")] long Total,
        [property: JsonPropertyName("error")] string? Error);

    private async Task<ChatResponse> SendAsync(string baseUrl, JsonObject body, CancellationToken cancellationToken)
    {
        HttpResponseMessage message;
        try
        {
            message = await http.PostAsync(
                new Uri(new Uri(baseUrl), "/api/chat"),
                new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ModelException(T("Ollama ist unter {0} nicht erreichbar. Läuft die Ollama-App?", baseUrl), ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ModelException(T("Das lokale Modell hat zu lange nicht geantwortet (Zeitüberschreitung)."), ex);
        }

        using (message)
        {
            var raw = await message.Content.ReadAsStringAsync(cancellationToken);
            ChatResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<ChatResponse>(raw);
            }
            catch (JsonException)
            {
                parsed = null;
            }
            if (message.StatusCode == HttpStatusCode.NotFound && parsed?.Error is not null)
            {
                return parsed with { Error = T("Das Modell ist nicht installiert ({0}). Im Terminal: ollama pull <modell>", parsed.Error), NotFound = true };
            }
            if (!message.IsSuccessStatusCode)
            {
                return new ChatResponse { Error = parsed?.Error ?? $"HTTP {(int)message.StatusCode}" };
            }
            return parsed ?? new ChatResponse { Error = T("Unverständliche Antwort von Ollama.") };
        }
    }

    private sealed record ChatResponse
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; init; }
        [JsonPropertyName("done_reason")] public string? DoneReason { get; init; }
        [JsonIgnore] public bool NotFound { get; init; }
        [JsonPropertyName("prompt_eval_count")] public long PromptEvalCount { get; init; }
        [JsonPropertyName("eval_count")] public long EvalCount { get; init; }
        [JsonPropertyName("error")] public string? Error { get; init; }
    }

    private sealed record ChatMessage([property: JsonPropertyName("content")] string? Content);

    private sealed record TagsResponse([property: JsonPropertyName("models")] List<TagModel>? Models);

    private sealed record TagModel(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("remote_host")] string? RemoteHost = null,
        [property: JsonPropertyName("remote_model")] string? RemoteModel = null);

    public static bool IsCloudName(string model)
    {
        var tag = model.Contains(':') ? model[(model.IndexOf(':') + 1)..] : "";
        return tag.Equals("cloud", StringComparison.OrdinalIgnoreCase) || tag.EndsWith("-cloud", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record OllamaModelInfo(string Name, long SizeBytes, bool Cloud = false);

public sealed record PullProgress(string Status, long Completed, long Total)
{
    public double? Fraction => Total > 0 ? (double)Completed / Total : null;
}

public sealed class RoutingModelGateway(
    IAiSettingsSource settings,
    AnthropicClaudeGateway anthropic,
    OllamaGateway ollama) : IModelGateway
{
    public static string LocalOnlyWithoutModel =>
        T("Diese Prüfung darf nur lokal bewertet werden, es ist aber kein lokales Modell eingerichtet. Bitte unter «Einstellungen» ein lokales Modell wählen.");

    public static bool RunsLocally(AiSettings current, bool localOnly) => current.Provider == AiProvider.Ollama || localOnly;

    public Task<ModelJsonResponse> CompleteJsonAsync(ModelJsonRequest request, CancellationToken cancellationToken)
    {
        var current = settings.Current();
        if (!RunsLocally(current, request.LocalOnly))
        {
            return anthropic.CompleteJsonAsync(request, cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(current.OllamaModel))
        {
            throw new ModelNotConfiguredException(LocalOnlyWithoutModel);
        }
        return ollama.CompleteJsonAsync(request, cancellationToken);
    }
}
