using System.Text;
using ArtGrader.Core.Ai;
using ArtGrader.Core.Documents;
using ArtGrader.Core.Domain;
using ArtGrader.Core.Images;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using SkiaSharp;
using ImagePart = ArtGrader.Core.Ai.ImagePart;
using A = DocumentFormat.OpenXml.Drawing;

namespace ArtGrader.Core.Tests;

public class DocumentTests
{
    private static byte[] Presentation(params (string Text, string? Notes)[] slides)
    {
        var stream = new MemoryStream();
        using (var document = PresentationDocument.Create(stream, DocumentFormat.OpenXml.PresentationDocumentType.Presentation))
        {
            var presentation = document.AddPresentationPart();
            presentation.Presentation = new Presentation(new SlideIdList());
            uint id = 256;
            foreach (var (text, notes) in slides)
            {
                var slide = presentation.AddNewPart<SlidePart>();
                slide.Slide = new Slide(new CommonSlideData(new ShapeTree(Shape(text))));
                if (notes is not null)
                {
                    var notesPart = slide.AddNewPart<NotesSlidePart>();
                    notesPart.NotesSlide = new NotesSlide(new CommonSlideData(new ShapeTree(Shape(notes))));
                }
                presentation.Presentation.SlideIdList!.Append(new SlideId { Id = id++, RelationshipId = presentation.GetIdOfPart(slide) });
            }
        }
        return stream.ToArray();
    }

    private static Shape Shape(string text) =>
        new(new TextBody(new A.BodyProperties(), new A.Paragraph(new A.Run(new A.Text(text)))));

    [Fact]
    public void PowerPoint_text_and_notes_are_read_slide_by_slide()
    {
        var pptx = Presentation(("Zentralperspektive", "Fluchtpunkt zeigen"), ("Leere Folie ohne Notizen", null));

        var content = DocumentContent.Read("Unterricht.pptx", pptx);

        Assert.Empty(content.Pages);
        Assert.Equal("Folie 1:\nZentralperspektive\nNotizen: Fluchtpunkt zeigen\n\nFolie 2:\nLeere Folie ohne Notizen", content.Text.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Pdf_pages_become_images()
    {
        var pdf = Encoding.ASCII.GetBytes(
            "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>endobj\n"
                + "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]>>endobj\n4 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 100 200]>>endobj\n"
                + "trailer<</Root 1 0 R>>\n%%EOF");

        var content = DocumentContent.Read("Aufgabe.pdf", pdf);

        Assert.Equal("", content.Text);
        Assert.Equal(2, content.Pages.Count);
        using var first = SKBitmap.Decode(content.Pages[0]);
        Assert.True(first.Width > first.Height);
    }

    [Fact]
    public void Crop_keeps_only_the_chosen_area_and_covers_stay_black()
    {
        var photo = ImagePreprocessorTests.Image(400, 200, SKEncodedImageFormat.Jpeg);

        var edited = ImageCover.Apply(
            photo,
            [new CoverArea { X = 0.5, Y = 0, Width = 0.25, Height = 1 }],
            new CoverArea { X = 0.25, Y = 0, Width = 0.5, Height = 1 });

        using var bitmap = SKBitmap.Decode(edited);
        Assert.Equal(200, bitmap.Width);
        Assert.Equal(200, bitmap.Height);
        Assert.True(bitmap.GetPixel(50, 100).Red > 235);
        Assert.True(bitmap.GetPixel(150, 100).Red < 20);
    }

    [Fact]
    public async Task Exercise_pages_and_lesson_material_are_sent_as_background()
    {
        var gateway = new RecordingGateway();
        var exam = ImageCoverTests.OneCriterion(new Exam { ExerciseText = "Zeichne eine Stadt.", LessonText = "Folie 1:\nZentralperspektive" });
        var page = ImagePreprocessorTests.Image(100, 140, SKEncodedImageFormat.Jpeg);

        await new SubmissionGradingService(gateway).GradeAsync(GradingContext.From(exam, [page]), [ImagePreprocessorTests.Image(20, 20)], TestContext.Current.CancellationToken);

        var shared = gateway.LastRequest!.SharedContext;
        Assert.Equal("Aufgabenblatt, Seite 1 von 1:", ((TextPart)shared[0]).Text);
        Assert.Same(page, ((ImagePart)shared[1]).Jpeg);
        var texts = shared.OfType<TextPart>().Select(t => t.Text).ToList();
        Assert.Contains(texts, t => t.Contains("<unterrichtsmaterial>\nFolie 1:\nZentralperspektive\n</unterrichtsmaterial>"));
        Assert.IsType<TextPart>(shared[^1]);
        Assert.Contains("<kriterien>", ((TextPart)shared[^1]).Text);
    }

    [Fact]
    public async Task Lesson_pages_from_a_pdf_are_sent_with_a_note()
    {
        var gateway = new RecordingGateway();
        var slide = ImagePreprocessorTests.Image(160, 90, SKEncodedImageFormat.Jpeg);

        await new SubmissionGradingService(gateway).GradeAsync(
            GradingContext.From(ImageCoverTests.OneCriterion(new Exam()), null, [slide]), [ImagePreprocessorTests.Image(20, 20)], TestContext.Current.CancellationToken);

        var shared = gateway.LastRequest!.SharedContext;
        Assert.Contains(shared, p => p is TextPart { Text: "Unterrichtsmaterial, Seite 1 von 1:" });
        Assert.Contains(shared, p => p is ImagePart i && ReferenceEquals(i.Jpeg, slide));
        Assert.Contains(shared, p => p is TextPart t && t.Text.StartsWith("Das Unterrichtsmaterial zeigt"));
    }

    [Fact]
    public async Task Long_lesson_material_is_shortened()
    {
        var gateway = new RecordingGateway();
        var exam = ImageCoverTests.OneCriterion(new Exam { LessonText = new string('x', GradingContext.MaxLessonCharacters + 500) });

        await new SubmissionGradingService(gateway).GradeAsync(GradingContext.From(exam, null), [ImagePreprocessorTests.Image(20, 20)], TestContext.Current.CancellationToken);

        var lesson = gateway.LastRequest!.SharedContext.OfType<TextPart>().Single(t => t.Text.StartsWith("<unterrichtsmaterial>")).Text;
        Assert.Equal(GradingContext.MaxLessonCharacters, lesson.Count(c => c == 'x'));
    }

    [Fact]
    public async Task Rubric_pages_are_sent_as_images_for_the_import()
    {
        var gateway = new RecordingGateway();
        var page = ImagePreprocessorTests.Image(100, 140, SKEncodedImageFormat.Jpeg);

        await Assert.ThrowsAnyAsync<ModelException>(() => new RubricImportService(gateway).ImportAsync("", [page], null, localOnly: false, TestContext.Current.CancellationToken));

        Assert.Same(page, gateway.LastRequest!.SharedContext.OfType<ImagePart>().Single().Jpeg);
    }

    [Fact]
    public async Task Empty_rubric_is_not_sent()
    {
        var gateway = new RecordingGateway();

        await Assert.ThrowsAsync<ModelException>(() => new RubricImportService(gateway).ImportAsync(" ", [], null, localOnly: false, TestContext.Current.CancellationToken));

        Assert.Null(gateway.LastRequest);
    }
}
