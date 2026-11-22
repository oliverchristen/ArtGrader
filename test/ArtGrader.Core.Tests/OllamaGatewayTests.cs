using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ArtGrader.Core.Ai;

namespace ArtGrader.Core.Tests;

public class OllamaGatewayTests
{
    private sealed class FakeOllama(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        private int next;

        public List<JsonObject> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                Requests.Add(JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken))!.AsObject());
            }
            var (status, body) = responses[Math.Min(next++, responses.Length - 1)];
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Settings(string effort = "high", string? model = "qwen3-vl:32b") : IAiSettingsSource
    {
        public AiSettings Current() => new(AiProvider.Ollama, null, AiSettings.DefaultModel, effort, "http://localhost:11434", model);
    }

    private const string Ok = """{"message":{"role":"assistant","content":"{\"ok\":true}"},"done_reason":"stop","prompt_eval_count":1200,"eval_count":300}""";

    private static ModelJsonRequest Request(params ContentPart[] content) =>
        new("System", [new TextPart("Aufgabe")], content, new JsonObject { ["type"] = "object" });

    [Fact]
    public async Task Sends_images_schema_and_thinking_and_returns_content()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));
        var gateway = new OllamaGateway(new HttpClient(fake), new Settings());

        var response = await gateway.CompleteJsonAsync(Request(new TextPart("Bild 1 von 1:"), new ImagePart([1, 2, 3])), default);

        var body = fake.Requests.Single();
        Assert.Equal("qwen3-vl:32b", body["model"]!.GetValue<string>());
        Assert.False(body["stream"]!.GetValue<bool>());
        Assert.True(body["think"]!.GetValue<bool>());
        Assert.Equal("object", body["format"]!["type"]!.GetValue<string>());
        var user = body["messages"]![1]!;
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), user["images"]![0]!.GetValue<string>());
        Assert.Contains("Aufgabe", user["content"]!.GetValue<string>());
        Assert.Equal("""{"ok":true}""", response.Json);
        Assert.Equal(1200, response.Usage.InputTokens);
    }

    [Fact]
    public async Task Fast_mode_does_not_ask_for_thinking()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));

        await new OllamaGateway(new HttpClient(fake), new Settings("medium")).CompleteJsonAsync(Request(), default);

        Assert.False(fake.Requests.Single()["think"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Quick_requests_switch_thinking_off_even_on_high()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));

        await new OllamaGateway(new HttpClient(fake), new Settings("high")).CompleteJsonAsync(Request() with { Quick = true }, default);

        Assert.False(fake.Requests.Single()["think"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Fast_mode_uses_the_instruct_variant_of_a_thinking_only_model()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));

        var response = await new OllamaGateway(new HttpClient(fake), new Settings("medium")).CompleteJsonAsync(Request(), default);

        Assert.Equal("qwen3-vl:32b-instruct", fake.Requests.Single()["model"]!.GetValue<string>());
        Assert.Equal("qwen3-vl:32b-instruct", response.Model);
    }

    [Fact]
    public async Task Thinking_mode_keeps_the_selected_model()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));

        await new OllamaGateway(new HttpClient(fake), new Settings("high")).CompleteJsonAsync(Request(), default);

        Assert.Equal("qwen3-vl:32b", fake.Requests.Single()["model"]!.GetValue<string>());
    }

    [Fact]
    public async Task Fast_mode_falls_back_to_the_selected_model_when_the_instruct_variant_is_missing()
    {
        var fake = new FakeOllama(
            (HttpStatusCode.NotFound, """{"error":"model 'qwen3-vl:32b-instruct' not found"}"""),
            (HttpStatusCode.OK, Ok));

        var response = await new OllamaGateway(new HttpClient(fake), new Settings("medium")).CompleteJsonAsync(Request(), default);

        Assert.Equal("qwen3-vl:32b", fake.Requests[1]["model"]!.GetValue<string>());
        Assert.Equal("""{"ok":true}""", response.Json);
    }

    [Fact]
    public async Task Models_that_can_switch_thinking_off_are_not_replaced()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));

        await new OllamaGateway(new HttpClient(fake), new Settings("medium", "qwen3.8:27b")).CompleteJsonAsync(Request(), default);

        Assert.Equal("qwen3.8:27b", fake.Requests.Single()["model"]!.GetValue<string>());
    }

    [Fact]
    public async Task Retries_without_thinking_when_the_model_cannot_think()
    {
        var fake = new FakeOllama(
            (HttpStatusCode.BadRequest, """{"error":"\"gemma3:27b\" does not support thinking"}"""),
            (HttpStatusCode.OK, Ok));

        var response = await new OllamaGateway(new HttpClient(fake), new Settings()).CompleteJsonAsync(Request(), default);

        Assert.Equal(2, fake.Requests.Count);
        Assert.Null(fake.Requests[1]["think"]);
        Assert.Equal("""{"ok":true}""", response.Json);
    }

    [Fact]
    public async Task Missing_model_gives_a_pull_hint()
    {
        var fake = new FakeOllama((HttpStatusCode.NotFound, """{"error":"model 'qwen3-vl:32b' not found"}"""));

        var ex = await Assert.ThrowsAsync<ModelException>(() =>
            new OllamaGateway(new HttpClient(fake), new Settings()).CompleteJsonAsync(Request(), default));

        Assert.Contains("ollama pull", ex.Message);
    }

    [Fact]
    public async Task No_model_selected_is_a_configuration_error()
    {
        await Assert.ThrowsAsync<ModelNotConfiguredException>(() =>
            new OllamaGateway(new HttpClient(new FakeOllama()), new Settings(model: null)).CompleteJsonAsync(Request(), default));
    }

    [Fact]
    public async Task Truncated_answer_is_reported()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, """{"message":{"content":"{\"a\":"},"done_reason":"length"}"""));

        await Assert.ThrowsAsync<ModelException>(() =>
            new OllamaGateway(new HttpClient(fake), new Settings()).CompleteJsonAsync(Request(), default));
    }
}
