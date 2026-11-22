using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Drawing = DocumentFormat.OpenXml.Drawing;

namespace ArtGrader.Core.Documents;

public static class PptxTextExtractor
{
    public static string Extract(Stream pptx)
    {
        using var document = PresentationDocument.Open(pptx, false);
        var presentation = document.PresentationPart;
        var slideIds = presentation?.Presentation?.SlideIdList?.ChildElements
            .OfType<DocumentFormat.OpenXml.Presentation.SlideId>()
            .ToList() ?? [];

        var builder = new StringBuilder();
        var number = 0;
        foreach (var slideId in slideIds)
        {
            if (slideId.RelationshipId?.Value is not { } relationship || presentation!.GetPartById(relationship) is not SlidePart slide)
            {
                continue;
            }
            number++;
            var text = Paragraphs(slide.Slide);
            var notes = slide.NotesSlidePart?.NotesSlide is { } notesSlide ? Paragraphs(notesSlide) : "";
            if (text.Length == 0 && notes.Length == 0)
            {
                continue;
            }
            builder.Append("Folie ").Append(number).AppendLine(":");
            if (text.Length > 0)
            {
                builder.AppendLine(text);
            }
            if (notes.Length > 0)
            {
                builder.Append("Notizen: ").AppendLine(notes);
            }
            builder.AppendLine();
        }
        return builder.ToString().Trim();
    }

    private static string Paragraphs(DocumentFormat.OpenXml.OpenXmlElement? root)
    {
        if (root is null)
        {
            return "";
        }
        var lines = root.Descendants<Drawing.Paragraph>()
            .Select(p => string.Concat(p.Descendants<Drawing.Text>().Select(t => t.Text)).Trim())
            .Where(line => line.Length > 0 && !int.TryParse(line, out _));
        return string.Join("\n", lines);
    }
}
