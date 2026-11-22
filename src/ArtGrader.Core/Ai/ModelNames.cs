using ArtGrader.Core.Domain;
using static ArtGrader.Core.Localization.Texts;

namespace ArtGrader.Core.Ai;

/// <summary>Names the AI model that assessed a work, so the teacher sees whether Claude or a local model made the suggestion.</summary>
public static class ModelNames
{
    /// <summary>The provider stored with the work; works assessed before it was stored are told apart by the model id.</summary>
    public static AiProvider? ProviderOf(Submission submission) =>
        string.IsNullOrEmpty(submission.ModelId) ? null
        : submission.ModelProvider ?? (submission.ModelId.StartsWith("claude", StringComparison.OrdinalIgnoreCase) ? AiProvider.Anthropic : AiProvider.Ollama);

    /// <summary>«Claude, model claude-opus-5-5» or «local model qwen3.8:27b»; null if no AI assessed the work.</summary>
    public static string? Describe(Submission submission) =>
        ProviderOf(submission) switch
        {
            AiProvider.Anthropic => T("Claude, model {0}", submission.ModelId!),
            AiProvider.Ollama => T("local model {0}", submission.ModelId!),
            _ => null,
        };

    /// <summary>Where the model ran, for the line under the suggestion.</summary>
    public static string? WhereItRan(Submission submission) =>
        ProviderOf(submission) switch
        {
            AiProvider.Anthropic => T("in the cloud at Anthropic"),
            AiProvider.Ollama => T("on this computer"),
            _ => null,
        };

    /// <summary>The models that assessed the works, with how many works each, most used first.</summary>
    public static IReadOnlyList<(string Model, int Works)> Used(IEnumerable<Submission> submissions) =>
        submissions
            .Select(Describe)
            .OfType<string>()
            .GroupBy(m => m)
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(m => m.Item2)
            .ThenBy(m => m.Key, StringComparer.Ordinal)
            .ToList();
}
