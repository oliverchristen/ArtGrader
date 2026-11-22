using ArtGrader.Web.Components.Shared;

namespace ArtGrader.Web.Tests;

public class RedactionTests
{
    [Fact]
    public void Selected_text_is_replaced_by_a_marker()
    {
        const string text = "Oberstufe Musterdorf, Frau Beispiel\nAufgabe: Zeichne eine Stadt von oben.";

        var redacted = RedactableText.Redact(text, 0, text.IndexOf('\n'));

        Assert.Equal("[geschwärzt]\nAufgabe: Zeichne eine Stadt von oben.", redacted);
    }
}
