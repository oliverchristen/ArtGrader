using System.Text.Json;
using ArtGrader.Core.Ai;
using ArtGrader.Core.Domain;
using SkiaSharp;

namespace ArtGrader.Core.Tests;

public class GradingResponseValidatorTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Dictionary<string, Guid> Keys = new() { ["K1"] = A, ["K2"] = B };

    private static string Json(params object[] criteria) =>
        JsonSerializer.Serialize(
            new
            {
                beschreibung = "Eine Stadt.",
                criteria,
                overallComment = "Gut gemacht.",
                strengths = new[] { "Tiefe", " " },
                nextSteps = new[] { "Sorgfalt" },
            });

    private static object C(string id, string level = "erfuellt", string confidence = "hoch") =>
        new { criterionId = id, level, justification = " Begründung ", confidence };

    [Fact]
    public void Valid_response_is_mapped_to_criterion_ids()
    {
        var result = GradingResponseValidator.Parse(Json(C("K1", "uebertroffen"), C("k2", "nicht", "tief")), Keys);

        Assert.Collection(
            result.Criteria,
            a =>
            {
                Assert.Equal(A, a.CriterionId);
                Assert.Equal(Level.Uebertroffen, a.Level);
                Assert.Equal("Begründung", a.Justification);
            },
            b =>
            {
                Assert.Equal(B, b.CriterionId);
                Assert.Equal(Level.Nicht, b.Level);
                Assert.Equal(Confidence.Tief, b.Confidence);
            });
        Assert.Equal(["Tiefe"], result.Strengths);
    }

    [Fact]
    public void Missing_criterion_is_rejected()
    {
        var ex = Assert.Throws<ModelException>(() => GradingResponseValidator.Parse(Json(C("K1")), Keys));
        Assert.Contains("K2", ex.Message);
    }

    [Fact]
    public void Duplicate_criterion_is_rejected()
    {
        Assert.Throws<ModelException>(() => GradingResponseValidator.Parse(Json(C("K1"), C("K1"), C("K2")), Keys));
    }

    [Fact]
    public void Unknown_criterion_is_rejected()
    {
        Assert.Throws<ModelException>(() => GradingResponseValidator.Parse(Json(C("K1"), C("K2"), C("K9")), Keys));
    }

    [Fact]
    public void Invalid_level_is_rejected()
    {
        Assert.Throws<ModelException>(() => GradingResponseValidator.Parse(Json(C("K1", "super"), C("K2")), Keys));
    }

    [Fact]
    public void Broken_json_is_rejected()
    {
        Assert.Throws<ModelException>(() => GradingResponseValidator.Parse("{nope", Keys));
    }
}

public class SubmissionGradingServiceTests
{
    private sealed class FakeClaude : IModelGateway
    {
        public List<ModelJsonRequest> Requests { get; } = [];

        public Task<ModelJsonResponse> CompleteJsonAsync(ModelJsonRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var text = string.Join("\n", request.SharedContext.OfType<TextPart>().Select(p => p.Text));
            var keys = Enumerable.Range(1, 20).Select(i => $"K{i}").Where(k => text.Contains($"- {k}:")).ToList();
            var json = JsonSerializer.Serialize(
                new
                {
                    beschreibung = "x",
                    criteria = keys.Select(k => new { criterionId = k, level = "erfuellt", justification = "j", confidence = "mittel" }),
                    overallComment = "o",
                    strengths = Array.Empty<string>(),
                    nextSteps = Array.Empty<string>(),
                });
            return Task.FromResult(new ModelJsonResponse(json, "claude-opus-5-5", new ModelUsage(1, 2, 3, 4)));
        }
    }

    private static Exam ExampleExam()
    {
        var exam = new Exam { Title = "Stadt von oben", ExerciseText = "Zeichne eine Stadt von oben." };
        var perspective = new RubricCategory { Name = "Perspektivisches Zeichnen", Order = 0 };
        perspective.Criteria.Add(new RubricCriterion { Text = "Perspektivisch korrekt.", Order = 0 });
        perspective.Criteria.Add(new RubricCriterion { Text = "Realistisch.", Order = 1 });
        var process = new RubricCategory { Name = "Prozess und Engagement", Order = 1 };
        process.Criteria.Add(new RubricCriterion { Text = "Mit Ausdauer gearbeitet.", AssessedBy = AssessedBy.Teacher });
        exam.Categories.AddRange([perspective, process]);
        return exam;
    }

    private static byte[] Photo() => ImagePreprocessorTests.Image(3000, 4000, SKEncodedImageFormat.Jpeg);

    [Fact]
    public async Task Teacher_only_criteria_are_not_sent_and_result_maps_back()
    {
        var claude = new FakeClaude();
        var exam = ExampleExam();

        var result = await new SubmissionGradingService(claude).GradeAsync(GradingContext.From(exam, null), [Photo()], default);

        var rubric = string.Join("\n", claude.Requests[0].SharedContext.OfType<TextPart>().Select(p => p.Text));
        Assert.DoesNotContain("Ausdauer", rubric);
        Assert.Equal(2, result.Criteria.Count);
        Assert.Equal(exam.OrderedCriteria().Take(2).Select(c => c.Id), result.Criteria.Select(c => c.CriterionId));
        Assert.Equal("claude-opus-5-5", result.Model);
    }

    [Fact]
    public async Task Student_name_is_never_sent_and_shared_prefix_is_identical_across_students()
    {
        var claude = new FakeClaude();
        var exam = ExampleExam();
        exam.Submissions.Add(new Submission { StudentName = "Lena Muster" });
        exam.Submissions.Add(new Submission { StudentName = "Noah Beispiel" });
        var service = new SubmissionGradingService(claude);

        foreach (var _ in exam.Submissions)
        {
            await service.GradeAsync(GradingContext.From(exam, null), [Photo(), Photo()], default);
        }

        var all = claude.Requests.SelectMany(r => r.SharedContext.Concat(r.Content)).OfType<TextPart>().Select(p => p.Text).ToList();
        all.AddRange(claude.Requests.Select(r => r.SystemPrompt));
        Assert.DoesNotContain(all, t => t.Contains("Lena") || t.Contains("Noah"));

        var first = claude.Requests[0].SharedContext.Select(Describe);
        var second = claude.Requests[1].SharedContext.Select(Describe);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Names_in_image_metadata_are_not_sent()
    {
        var claude = new FakeClaude();
        var photo = WithMetadata(Photo(), "Lena Muster");
        Assert.True(Contains(photo, "Lena Muster"));

        await new SubmissionGradingService(claude).GradeAsync(GradingContext.From(ExampleExam(), null), [photo], default);

        var sent = claude.Requests[0].Content.OfType<ImagePart>().Single().Jpeg;
        Assert.False(Contains(sent, "Lena Muster"));
    }

    private static byte[] WithMetadata(byte[] jpeg, string text)
    {
        var payload = System.Text.Encoding.ASCII.GetBytes(text);
        var exif = System.Text.Encoding.ASCII.GetBytes("Exif\0\0").Concat(payload).ToArray();
        static byte[] Segment(byte marker, byte[] data) =>
            [0xFF, marker, (byte)((data.Length + 2) >> 8), (byte)((data.Length + 2) & 0xFF), .. data];
        return [.. jpeg[..2], .. Segment(0xE1, exif), .. Segment(0xFE, payload), .. jpeg[2..]];
    }

    private static bool Contains(byte[] haystack, string text) =>
        haystack.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(text)) >= 0;

    [Fact]
    public async Task Images_are_downscaled_and_labelled()
    {
        var claude = new FakeClaude();

        await new SubmissionGradingService(claude).GradeAsync(GradingContext.From(ExampleExam(), null), [Photo(), Photo()], default);

        var content = claude.Requests[0].Content;
        Assert.Equal("Bild 1 von 2:", ((TextPart)content[0]).Text);
        using var codec = SKCodec.Create(new MemoryStream(((ImagePart)content[1]).Jpeg));
        Assert.Equal(1568, Math.Max(codec.Info.Width, codec.Info.Height));
        Assert.Equal(2, content.OfType<ImagePart>().Count());
    }

    private static string Describe(ContentPart part) =>
        part switch
        {
            TextPart t => "T:" + t.Text,
            ImagePart i => "I:" + i.Jpeg.Length,
            _ => part.GetType().Name,
        };
}

public class RubricImportParseTests
{
    [Fact]
    public void Categories_and_criteria_keep_order_and_assessor()
    {
        var json = """
            {"categories":[
              {"name":"Perspektivisches Zeichnen","criteria":[{"text":"A","assessedBy":"ai"},{"text":"B","assessedBy":"ai"}]},
              {"name":"Leer","criteria":[]},
              {"name":"Prozess und Engagement","criteria":[{"text":"Ausdauer","assessedBy":"teacher"}]}
            ]}
            """;

        var categories = RubricImportService.Parse(json);

        Assert.Equal(["Perspektivisches Zeichnen", "Prozess und Engagement"], categories.Select(c => c.Name));
        Assert.Equal([0, 1], categories.Select(c => c.Order));
        Assert.Equal(AssessedBy.Teacher, categories[1].Criteria[0].AssessedBy);
        Assert.Equal(["A", "B"], categories[0].Criteria.Select(c => c.Text));
    }

    [Fact]
    public void Empty_rubric_is_rejected()
    {
        Assert.Throws<ModelException>(() => RubricImportService.Parse("""{"categories":[]}"""));
    }
}
