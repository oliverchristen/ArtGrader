using System.Text.Json.Nodes;

namespace ArtGrader.Core.Ai;

public abstract record ContentPart;

public sealed record TextPart(string Text) : ContentPart;

public sealed record ImagePart(byte[] Jpeg) : ContentPart;

public sealed record PdfPart(byte[] Pdf, string Title) : ContentPart;

public sealed record ModelJsonRequest(
    string SystemPrompt,
    IReadOnlyList<ContentPart> SharedContext,
    IReadOnlyList<ContentPart> Content,
    JsonObject Schema,
    int MaxTokens = 16000,
    bool Quick = false);

public sealed record ModelJsonResponse(string Json, string Model, ModelUsage Usage);

public sealed record ModelUsage(long InputTokens, long OutputTokens, long CacheReadTokens, long CacheWriteTokens);

public interface IModelGateway
{
    Task<ModelJsonResponse> CompleteJsonAsync(ModelJsonRequest request, CancellationToken cancellationToken);
}

public enum AiProvider
{
    Anthropic = 0,
    Ollama = 1,
}

public sealed record AiSettings(
    AiProvider Provider,
    string? ApiKey,
    string Model,
    string Effort,
    string OllamaUrl,
    string? OllamaModel)
{
    public const string DefaultModel = "claude-opus-5-5";
    public const string DefaultEffort = "high";
    public const string DefaultOllamaUrl = "http://localhost:11434";

    public string ActiveModel => Provider == AiProvider.Ollama ? OllamaModel ?? "" : Model;
}

public interface IAiSettingsSource
{
    AiSettings Current();
}

public class ModelException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class ModelNotConfiguredException(string message) : ModelException(message);
