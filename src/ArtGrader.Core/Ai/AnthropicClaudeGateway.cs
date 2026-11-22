using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using static ArtGrader.Core.Localization.Texts;

namespace ArtGrader.Core.Ai;

public sealed class AnthropicClaudeGateway(IAiSettingsSource settingsSource) : IModelGateway
{
    public async Task<ModelJsonResponse> CompleteJsonAsync(ModelJsonRequest request, CancellationToken cancellationToken)
    {
        var settings = settingsSource.Current();
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new ModelNotConfiguredException(
                T("No Anthropic API key has been saved. Please enter one in ‘Settings’ or choose the local model."));
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
            throw new ModelException(T("Too many requests to Claude (rate limit). Please try again later."), ex);
        }
        catch (AnthropicBadRequestException ex)
        {
            throw new ModelException(T("Claude rejected the request: {0}", ex.Message), ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw new ModelException(T("Claude cannot be reached right now (server error). Please try again later."), ex);
        }
        catch (AnthropicApiException ex)
        {
            throw new ModelException(T("Error in the request to Claude: {0}", ex.Message), ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ModelException(T("No connection to Claude. Please check the internet connection."), ex);
        }

        var stopReason = response.StopReason?.ToString();
        if (stopReason is "refusal")
        {
            throw new ModelException(T("Claude declined to assess this. Please assess the work yourself."));
        }
        if (stopReason is "max_tokens")
        {
            throw new ModelException(T("Claude's answer was cut off. Please try again."));
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
