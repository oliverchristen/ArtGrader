using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ArtGrader.Core.Ai;
using ArtGrader.Core.Documents;

namespace ArtGrader.Core.Tests;

public class RedactionSuggestionTests
{
    private const string Exercise = "Aufgabe Zentralperspektive   BG // S. Flückiger // Frohheim Olten // 2024\n"
        + "Fragen an s.flueckiger@schule-olten.ch oder 062 311 22 33, Infos auf www.frohheim.ch.\n"
        + "Zeichne eine Stadt von oben. Holtenbach ist kein Treffer.";

    [Fact]
    public void Terms_from_the_list_are_found_as_whole_words_ignoring_case()
    {
        var hits = RedactionSuggestions.Find(Exercise, ["Olten", "frohheim", "S. Flückiger", " ", "x"]);

        Assert.Equal(["S. Flückiger", "Frohheim", "Olten"], hits.Where(h => h.Reason == "Liste").Select(h => h.Text));
    }

    [Fact]
    public void Email_phone_and_web_addresses_are_found_without_a_list()
    {
        var hits = RedactionSuggestions.Find(Exercise, []);

        Assert.Contains(hits, h => h is { Reason: "E-Mail", Text: "s.flueckiger@schule-olten.ch" });
        Assert.Contains(hits, h => h is { Reason: "Telefon", Text: "062 311 22 33" });
        Assert.Contains(hits, h => h is { Reason: "Webadresse", Text: "www.frohheim.ch" });
        Assert.DoesNotContain(hits, h => h.Text == "2024");
    }

    [Fact]
    public void Overlapping_hits_are_merged_and_applied_once()
    {
        var hits = RedactionSuggestions.Find("Schulhaus Frohheim Olten", ["Frohheim Olten", "Olten"]);

        var hit = Assert.Single(hits);
        Assert.Equal("Schulhaus [geschwärzt]", RedactionSuggestions.Apply("Schulhaus Frohheim Olten", hits));
        Assert.Equal(10, hit.Start);
    }

    [Fact]
    public void Redacted_markers_are_not_suggested_again()
    {
        var text = RedactionSuggestions.Apply(Exercise, RedactionSuggestions.Find(Exercise, ["Olten", "Frohheim", "S. Flückiger"]));

        Assert.Empty(RedactionSuggestions.Find(text, ["Olten", "Frohheim", "S. Flückiger"]));
        Assert.DoesNotContain("Olten", text.Replace("Holtenbach", ""));
    }

    private sealed class FakeOllama(string content) : HttpMessageHandler
    {
        public JsonObject? Request { get; private set; }

        public bool Reachable { get; init; } = true;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!Reachable)
            {
                throw new HttpRequestException("connection refused");
            }
            if (request.RequestUri!.AbsolutePath == "/api/tags")
            {
                const string tags = """{"models":[{"name":"gemma4:31b-cloud","size":0},{"name":"llama3:8b","size":4000000000},{"name":"qwen3.8:27b","size":17700000000}]}""";
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(tags, Encoding.UTF8, "application/json") };
            }
            Request = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
            var body = new JsonObject
            {
                ["model"] = "qwen3.8:27b",
                ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = content },
                ["done_reason"] = "stop",
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        }
    }

    private sealed class ClaudeSelected : IAiSettingsSource
    {
        public AiSettings Current() =>
            new(AiProvider.Anthropic, "sk-test", AiSettings.DefaultModel, "high", AiSettings.DefaultOllamaUrl, null);
    }

    [Fact]
    public async Task Local_model_runs_on_ollama_even_when_claude_is_selected_and_only_real_passages_are_kept()
    {
        var fake = new FakeOllama("""{"stellen":[{"text":"S. Flückiger","grund":"Lehrperson"},{"text":"Schule Erfunden","grund":"Schulhaus"},{"text":"Frohheim Olten","grund":"Schulhaus"}]}""");

        var settings = new ClaudeSelected();
        var found = await new LocalRedactionFinder(new OllamaGateway(new HttpClient(fake), settings), settings).FindAsync(Exercise, TestContext.Current.CancellationToken);

        Assert.Equal([("S. Flückiger", "Lehrperson"), ("Frohheim Olten", "Schulhaus")], found);
        Assert.False((bool)fake.Request!["think"]!);
        Assert.Equal("qwen3.8:27b", (string)fake.Request["model"]!);
        Assert.Contains("Frohheim Olten", (string)fake.Request["messages"]![1]!["content"]!);
        var hits = RedactionSuggestions.Find(Exercise, [], found);
        Assert.Contains(hits, h => h is { Text: "S. Flückiger", Reason: "Lehrperson" });
    }

    [Fact]
    public async Task No_local_model_is_offered_when_ollama_is_not_running()
    {
        var settings = new ClaudeSelected();
        var finder = new LocalRedactionFinder(new OllamaGateway(new HttpClient(new FakeOllama("{}") { Reachable = false }), settings), settings);

        Assert.Null(await finder.AvailableModelAsync(TestContext.Current.CancellationToken));
    }
}
