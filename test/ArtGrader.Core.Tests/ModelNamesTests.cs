using ArtGrader.Core.Ai;
using ArtGrader.Core.Domain;
using ArtGrader.Core.Localization;

namespace ArtGrader.Core.Tests;

[Collection(nameof(LanguageSwitching))]
public class ModelNamesTests
{
    [Fact]
    public void Names_claude_and_local_models_and_tells_older_works_apart_by_the_model_id()
    {
        Language.Current = AppLanguage.En;
        try
        {
            Assert.Equal("Claude, model claude-opus-5-5", ModelNames.Describe(new Submission { ModelId = "claude-opus-5-5", ModelProvider = AiProvider.Anthropic }));
            Assert.Equal("local model qwen3.8:27b", ModelNames.Describe(new Submission { ModelId = "qwen3.8:27b", ModelProvider = AiProvider.Ollama }));
            Assert.Equal(AiProvider.Anthropic, ModelNames.ProviderOf(new Submission { ModelId = "claude-sonnet-5-5" }));
            Assert.Equal(AiProvider.Ollama, ModelNames.ProviderOf(new Submission { ModelId = "gemma3:27b" }));
            Assert.Null(ModelNames.Describe(new Submission()));
        }
        finally
        {
            Language.Current = AppLanguage.De;
        }
    }

    [Fact]
    public void Lists_the_models_used_most_used_first()
    {
        Language.Current = AppLanguage.En;
        try
        {
            Submission Claude() => new() { ModelId = "claude-opus-5-5", ModelProvider = AiProvider.Anthropic };
            var works = new[] { new Submission { ModelId = "qwen3.8:27b", ModelProvider = AiProvider.Ollama }, Claude(), Claude(), new Submission() };

            Assert.Equal([("Claude, model claude-opus-5-5", 2), ("local model qwen3.8:27b", 1)], ModelNames.Used(works));
            Assert.Single(ModelNames.Used([Claude(), Claude()]));
        }
        finally
        {
            Language.Current = AppLanguage.De;
        }
    }
}
