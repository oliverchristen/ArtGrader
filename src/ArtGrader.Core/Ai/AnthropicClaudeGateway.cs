using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;

namespace ArtGrader.Core.Ai;

public sealed class AnthropicClaudeGateway(IAiSettingsSource settingsSource) : IModelGateway
{
    public async Task<ModelJsonResponse> CompleteJsonAsync(ModelJsonRequest request, CancellationToken cancellationToken)
    {
        var settings = settingsSource.Current();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new ModelNotConfiguredException(
                "Es ist kein Anthropic API-Schlüssel hinterlegt. Bitte unter «Einstellungen» eintragen oder das lokale Modell wählen.");
        }

        var client = new AnthropicClient { ApiKey = settings.ApiKey, MaxRetries = 3 };

        var content = new List<BetaContentBlockParam>();
        content.AddRange(request.SharedContext.Select(ToBlock));
        if (content.Count > 0 && content[^1].TryPickText(out var lastShared))
        {
            content[^1] = lastShared with { CacheControl = new BetaCacheControlEphemeral() };
        }
        content.AddRange(request.Content.Select(ToBlock));

        var parameters = new MessageCreateParams
        {
            Model = settings.Model,
            MaxTokens = request.MaxTokens,
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            Thinking = new BetaThinkingConfigAdaptive(),
            OutputConfig = new BetaOutputConfig
            {
                Effort = ParseEffort(settings.Effort),
                Format = new BetaJsonOutputFormat
                {
                    Schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.Schema.ToJsonString())!,
                },
            },
            System = new List<BetaTextBlockParam>
            {
                new() { Text = request.SystemPrompt, CacheControl = new BetaCacheControlEphemeral() },
            },
            Messages = [new BetaUserMessageParam { Content = content }],
        };

        BetaMessage response;
        try
        {
            response = await client.Beta.Messages.Create(parameters, cancellationToken);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new ModelException("Zu viele Anfragen an Claude (Rate Limit). Bitte später nochmals versuchen.", ex);
        }
        catch (AnthropicBadRequestException ex)
        {
            throw new ModelException($"Claude hat die Anfrage abgelehnt: {ex.Message}", ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw new ModelException("Claude ist gerade nicht erreichbar (Serverfehler). Bitte später nochmals versuchen.", ex);
        }
        catch (AnthropicApiException ex)
        {
            throw new ModelException($"Fehler bei der Anfrage an Claude: {ex.Message}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ModelException("Keine Verbindung zu Claude. Bitte die Internetverbindung prüfen.", ex);
        }

        var stopReason = response.StopReason?.ToString();
        if (stopReason is "refusal")
        {
            throw new ModelException("Claude hat diese Bewertung abgelehnt. Bitte die Arbeit selbst beurteilen.");
        }
        if (stopReason is "max_tokens")
        {
            throw new ModelException("Die Antwort von Claude wurde abgeschnitten. Bitte nochmals versuchen.");
        }

        var text = new StringBuilder();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
            {
                text.Append(textBlock.Text);
            }
        }

        var usage = response.Usage;
        return new ModelJsonResponse(
            text.ToString(),
            response.Model.ToString(),
            new ModelUsage(
                usage.InputTokens,
                usage.OutputTokens,
                usage.CacheReadInputTokens ?? 0,
                usage.CacheCreationInputTokens ?? 0));
    }

    private static BetaContentBlockParam ToBlock(ContentPart part) =>
        part switch
        {
            TextPart text => new BetaTextBlockParam { Text = text.Text },
            ImagePart image => new BetaImageBlockParam
            {
                Source = new BetaBase64ImageSource { Data = Convert.ToBase64String(image.Jpeg), MediaType = MediaType.ImageJpeg },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(part)),
        };

    private static Effort ParseEffort(string effort) =>
        effort.ToLowerInvariant() switch
        {
            "low" => Effort.Low,
            "medium" => Effort.Medium,
            "max" => Effort.Max,
            _ => Effort.High,
        };
}
