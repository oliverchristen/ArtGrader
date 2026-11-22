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

        public int Calls { get; private set; }

        public List<JsonObject> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
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

    [Theory]
    [InlineData("http://localhost:11434", true)]
    [InlineData("http://LOCALHOST:11434/", true)]
    [InlineData("http://127.0.0.1:11434", true)]
    [InlineData("http://[::1]:11434", true)]
    [InlineData("http://ollama.localhost:11434", true)]
    [InlineData("http://192.168.1.20:11434", false)]
    [InlineData("https://ollama.example.com", false)]
    [InlineData("http://localhost.example.com:11434", false)]
    [InlineData("http://localhost@example.com:11434", false)]
    [InlineData("http://127.0.0.1.example.com", false)]
    [InlineData("file:///C:/temp", false)]
    [InlineData("localhost:11434", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_addresses_on_this_computer_count_as_local(string? url, bool local) =>
        Assert.Equal(local, OllamaGateway.IsThisComputer(url));

    private sealed class RemoteSettings : IAiSettingsSource
    {
        public AiSettings Current() => new(AiProvider.Ollama, null, AiSettings.DefaultModel, "high", "http://192.168.1.20:11434", "qwen3-vl:32b");
    }

    [Fact]
    public async Task Nothing_is_sent_to_an_address_on_another_computer()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));
        var gateway = new OllamaGateway(new HttpClient(fake), new RemoteSettings());

        await Assert.ThrowsAsync<ModelNotConfiguredException>(() => gateway.CompleteJsonAsync(Request(new ImagePart([1, 2, 3])), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ModelNotConfiguredException>(() => gateway.ListModelsAsync("http://192.168.1.20:11434", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ModelNotConfiguredException>(() =>
            gateway.PullAsync("http://192.168.1.20:11434", "qwen3-vl:32b", new Progress<PullProgress>(), TestContext.Current.CancellationToken));

        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Sends_images_schema_and_thinking_and_returns_content()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));
        var gateway = new OllamaGateway(new HttpClient(fake), new Settings());

        var response = await gateway.CompleteJsonAsync(Request(new TextPart("Bild 1 von 1:"), new ImagePart([1, 2, 3])), TestContext.Current.CancellationToken);

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

        await new OllamaGateway(new HttpClient(fake), new Settings("medium")).CompleteJsonAsync(Request(), TestContext.Current.CancellationToken);

        Assert.False(fake.Requests.Single()["think"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Quick_requests_switch_thinking_off_even_on_high()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));

        await new OllamaGateway(new HttpClient(fake), new Settings("high")).CompleteJsonAsync(Request() with { Quick = true }, TestContext.Current.CancellationToken);

        Assert.False(fake.Requests.Single()["think"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Fast_mode_uses_the_instruct_variant_of_a_thinking_only_model()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));

        var response = await new OllamaGateway(new HttpClient(fake), new Settings("medium")).CompleteJsonAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("qwen3-vl:32b-instruct", fake.Requests.Single()["model"]!.GetValue<string>());
        Assert.Equal("qwen3-vl:32b-instruct", response.Model);
    }

    [Fact]
    public async Task Thinking_mode_keeps_the_selected_model()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));

        await new OllamaGateway(new HttpClient(fake), new Settings("high")).CompleteJsonAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("qwen3-vl:32b", fake.Requests.Single()["model"]!.GetValue<string>());
    }

    [Fact]
    public async Task Fast_mode_falls_back_to_the_selected_model_when_the_instruct_variant_is_missing()
    {
        var fake = new FakeOllama(
            (HttpStatusCode.NotFound, """{"error":"model 'qwen3-vl:32b-instruct' not found"}"""),
            (HttpStatusCode.OK, Ok));

        var response = await new OllamaGateway(new HttpClient(fake), new Settings("medium")).CompleteJsonAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("qwen3-vl:32b", fake.Requests[1]["model"]!.GetValue<string>());
        Assert.Equal("""{"ok":true}""", response.Json);
    }

    [Fact]
    public async Task Models_that_can_switch_thinking_off_are_not_replaced()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));

        await new OllamaGateway(new HttpClient(fake), new Settings("medium", "qwen3.8:27b")).CompleteJsonAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal("qwen3.8:27b", fake.Requests.Single()["model"]!.GetValue<string>());
    }

    [Fact]
    public async Task Retries_without_thinking_when_the_model_cannot_think()
    {
        var fake = new FakeOllama(
            (HttpStatusCode.BadRequest, """{"error":"\"gemma3:27b\" does not support thinking"}"""),
            (HttpStatusCode.OK, Ok));

        var response = await new OllamaGateway(new HttpClient(fake), new Settings()).CompleteJsonAsync(Request(), TestContext.Current.CancellationToken);

        Assert.Equal(2, fake.Requests.Count);
        Assert.Null(fake.Requests[1]["think"]);
        Assert.Equal("""{"ok":true}""", response.Json);
    }

    [Fact]
    public async Task Missing_model_gives_a_pull_hint()
    {
        var fake = new FakeOllama((HttpStatusCode.NotFound, """{"error":"model 'qwen3-vl:32b' not found"}"""));

        var ex = await Assert.ThrowsAsync<ModelException>(() =>
            new OllamaGateway(new HttpClient(fake), new Settings()).CompleteJsonAsync(Request(), TestContext.Current.CancellationToken));

        Assert.Contains("ollama pull", ex.Message);
    }

    [Fact]
    public async Task No_model_selected_is_a_configuration_error()
    {
        await Assert.ThrowsAsync<ModelNotConfiguredException>(() =>
            new OllamaGateway(new HttpClient(new FakeOllama()), new Settings(model: null)).CompleteJsonAsync(Request(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Truncated_answer_is_reported()
    {
        var fake = new FakeOllama((HttpStatusCode.OK, """{"message":{"content":"{\"a\":"},"done_reason":"length"}"""));

        await Assert.ThrowsAsync<ModelException>(() =>
            new OllamaGateway(new HttpClient(fake), new Settings()).CompleteJsonAsync(Request(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cloud_models_are_flagged_in_the_model_list()
    {
        const string tags = """
            {"models":[
              {"name":"qwen3.8:27b","size":17700000000},
              {"name":"gemma4:31b-cloud","size":0},
              {"name":"kimi-k3.1-pro:latest","size":384,"remote_host":"https://ollama.com:443","remote_model":"kimi-k3.1-pro"}
            ]}
            """;
        var gateway = new OllamaGateway(new HttpClient(new FakeOllama((HttpStatusCode.OK, tags))), new Settings());

        var models = await gateway.ListModelsAsync("http://localhost:11434", TestContext.Current.CancellationToken);

        Assert.Equal(["gemma4:31b-cloud", "kimi-k3.1-pro:latest"], models.Where(m => m.Cloud).Select(m => m.Name));
        Assert.False(models.Single(m => m.Name == "qwen3.8:27b").Cloud);
    }

    [Theory]
    [InlineData("gemma4:cloud")]
    [InlineData("gemma4:31b-cloud")]
    public async Task A_cloud_model_is_refused_before_anything_is_sent(string model)
    {
        var fake = new FakeOllama((HttpStatusCode.OK, Ok));

        var ex = await Assert.ThrowsAsync<ModelNotConfiguredException>(() =>
            new OllamaGateway(new HttpClient(fake), new Settings(model: model)).CompleteJsonAsync(Request(), TestContext.Current.CancellationToken));

        Assert.Contains("Cloud-Modell", ex.Message);
        Assert.Empty(fake.Requests);
    }
}
