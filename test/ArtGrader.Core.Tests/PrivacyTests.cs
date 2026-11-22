using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ArtGrader.Core.Ai;
using ArtGrader.Core.Domain;
using ArtGrader.Core.Images;
using SkiaSharp;

namespace ArtGrader.Core.Tests;

public class ImageCoverTests
{
    [Fact]
    public void Covered_area_is_black_and_the_rest_is_unchanged()
    {
        var photo = ImagePreprocessorTests.Image(400, 200, SKEncodedImageFormat.Jpeg);

        var covered = ImageCover.Apply(photo, [new CoverArea { X = 0.75, Y = 0.5, Width = 0.25, Height = 0.5 }]);

        using var bitmap = SKBitmap.Decode(covered);
        Assert.True(bitmap.GetPixel(350, 150).Red < 20);
        Assert.True(bitmap.GetPixel(50, 50).Red > 235);
    }

    [Fact]
    public void Without_covers_the_image_is_returned_as_is()
    {
        var photo = ImagePreprocessorTests.Image(40, 20, SKEncodedImageFormat.Jpeg);

        Assert.Same(photo, ImageCover.Apply(photo, []));
    }

    [Fact]
    public void Dragging_in_any_direction_gives_a_rectangle_inside_the_image()
    {
        var area = ImageCover.Normalize(1.2, 0.8, 0.6, -0.1);

        Assert.Equal(0.6, area.X, 6);
        Assert.Equal(0, area.Y, 6);
        Assert.Equal(0.4, area.Width, 6);
        Assert.Equal(0.8, area.Height, 6);
    }

    [Fact]
    public async Task Covered_area_stays_black_in_the_image_sent_to_the_model()
    {
        var gateway = new RecordingGateway();
        var photo = ImagePreprocessorTests.Image(400, 200, SKEncodedImageFormat.Jpeg);
        var covered = ImageCover.Apply(photo, [new CoverArea { X = 0, Y = 0, Width = 1, Height = 1 }]);

        await new SubmissionGradingService(gateway).GradeAsync(GradingContext.From(OneCriterion(new Exam()), null), [covered], default);

        using var sent = SKBitmap.Decode(gateway.LastRequest!.Content.OfType<ImagePart>().Single().Jpeg);
        Assert.True(sent.GetPixel(sent.Width / 2, sent.Height / 2).Red < 20);
    }

    internal static Exam OneCriterion(Exam exam)
    {
        var category = new RubricCategory { Name = "Gestaltung" };
        category.Criteria.Add(new RubricCriterion { Text = "Sorgfältig." });
        exam.Categories.Add(category);
        return exam;
    }
}

internal sealed class RecordingGateway : IModelGateway
{
    public ModelJsonRequest? LastRequest { get; private set; }

    public Task<ModelJsonResponse> CompleteJsonAsync(ModelJsonRequest request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        const string json = """{"beschreibung":"x","criteria":[{"criterionId":"K1","level":"erfuellt","justification":"j","confidence":"hoch"}],"overallComment":"o","strengths":[],"nextSteps":[]}""";
        return Task.FromResult(new ModelJsonResponse(json, "m", new ModelUsage(0, 0, 0, 0)));
    }
}

public class LocalOnlyRoutingTests
{
    private sealed class FakeOllama : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            const string body = """{"model":"qwen3.8:27b","message":{"role":"assistant","content":"{}"},"done_reason":"stop"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class Settings(string? ollamaModel) : IAiSettingsSource
    {
        public AiSettings Current() =>
            new(AiProvider.Anthropic, "sk-test", AiSettings.DefaultModel, "medium", AiSettings.DefaultOllamaUrl, ollamaModel);
    }

    private static ModelJsonRequest Request(bool localOnly) =>
        new("System", [new TextPart("Aufgabe")], [new TextPart("Bild")], new JsonObject { ["type"] = "object" }, LocalOnly: localOnly);

    private static (RoutingModelGateway Gateway, FakeOllama Ollama) Create(string? ollamaModel)
    {
        var settings = new Settings(ollamaModel);
        var ollama = new FakeOllama();
        return (new RoutingModelGateway(settings, new AnthropicClaudeGateway(settings), new OllamaGateway(new HttpClient(ollama), settings)), ollama);
    }

    [Fact]
    public async Task Local_only_exam_uses_the_local_model_even_when_claude_is_selected()
    {
        var (gateway, ollama) = Create("qwen3.8:27b");

        var response = await gateway.CompleteJsonAsync(Request(localOnly: true), default);

        Assert.Equal(1, ollama.Calls);
        Assert.Equal("qwen3.8:27b", response.Model);
    }

    [Fact]
    public async Task Local_only_exam_without_a_local_model_is_refused_and_nothing_is_sent()
    {
        var (gateway, ollama) = Create(null);

        var ex = await Assert.ThrowsAsync<ModelNotConfiguredException>(() => gateway.CompleteJsonAsync(Request(localOnly: true), default));

        Assert.Equal(RoutingModelGateway.LocalOnlyWithoutModel, ex.Message);
        Assert.Equal(0, ollama.Calls);
    }

    [Fact]
    public async Task Grading_request_of_a_local_only_exam_is_marked_local_only()
    {
        var gateway = new RecordingGateway();
        var exam = ImageCoverTests.OneCriterion(new Exam { LocalOnly = true });

        await new SubmissionGradingService(gateway).GradeAsync(GradingContext.From(exam, null), [ImagePreprocessorTests.Image(20, 20)], default);

        Assert.True(gateway.LastRequest!.LocalOnly);
    }

    [Fact]
    public async Task Rubric_import_of_a_local_only_exam_is_marked_local_only()
    {
        var gateway = new RecordingGateway();

        await Assert.ThrowsAnyAsync<ModelException>(() =>
            new RubricImportService(gateway).ImportAsync("Raster", [], null, localOnly: true, default));

        Assert.True(gateway.LastRequest!.LocalOnly);
    }
}
