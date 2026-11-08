using ArtGrader.Core.Documents;

namespace ArtGrader.Core.Tests;

public class DocxTextExtractorTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void Exercise_text_is_extracted_without_drawing_offsets()
    {
        using var stream = File.OpenRead(Fixture("aufgabe-stadt-von-oben.docx"));

        var text = DocxTextExtractor.Extract(stream);

        Assert.Contains("Zeichne eine Grossstadt von oben", text);
        Assert.Contains("Zentralperspektive", text);
        Assert.DoesNotContain("276542535687000", text);
    }

    [Theory]
    [InlineData("Perspektivisches Zeichnen")]
    [InlineData("Plastizität")]
    [InlineData("Kreative Umsetzung")]
    [InlineData("Technische Umsetzung")]
    [InlineData("Prozess und Engagement")]
    [InlineData("Die Standflächen der Häuser sind sinnvoll nebeneinander gezeichnet.")]
    [InlineData("Die lineare Ausarbeitung wirkt spannend und lebendig.")]
    public void Rubric_table_rows_are_extracted(string expected)
    {
        using var stream = File.OpenRead(Fixture("beurteilung-stadt-von-oben.docx"));

        var text = DocxTextExtractor.Extract(stream);

        Assert.Contains(expected, text);
    }

    [Fact]
    public void Unsupported_format_is_rejected_with_a_german_message()
    {
        var ex = Assert.Throws<UnsupportedDocumentException>(() => DocumentContent.Read("x.pages", []));
        Assert.Contains(".pages", ex.Message);
    }
}
