using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ArtGrader.Core.Documents;

public static class DocxTextExtractor
{
    public static string Extract(Stream docx)
    {
        using var document = WordprocessingDocument.Open(docx, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return "";
        }

        var builder = new StringBuilder();
        foreach (var element in body.ChildElements)
        {
            switch (element)
            {
                case Paragraph paragraph:
                    AppendLine(builder, ParagraphText(paragraph));
                    break;
                case Table table:
                    AppendTable(builder, table);
                    break;
            }
        }

        return Normalize(builder.ToString());
    }

    private static void AppendTable(StringBuilder builder, Table table)
    {
        builder.AppendLine();
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>()
                .Select(cell => string.Join(" ", cell.Elements<Paragraph>().Select(ParagraphText).Where(t => t.Length > 0)))
                .ToList();
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }
            builder.AppendLine("| " + string.Join(" | ", cells.Select(c => c.Trim())) + " |");
        }
        builder.AppendLine();
    }

    private static string ParagraphText(Paragraph paragraph)
    {
        var builder = new StringBuilder();
        foreach (var node in paragraph.Descendants())
        {
            switch (node)
            {
                case Text text:
                    builder.Append(text.Text);
                    break;
                case TabChar:
                    builder.Append(' ');
                    break;
                case Break:
                    builder.Append(' ');
                    break;
            }
        }
        return builder.ToString().Trim();
    }

    private static void AppendLine(StringBuilder builder, string line) => builder.AppendLine(line);

    private static string Normalize(string text)
    {
        var lines = text.Replace("\r", "").Split('\n').Select(l => l.TrimEnd());
        var result = new StringBuilder();
        var blank = 0;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                blank++;
                if (blank > 1)
                {
                    continue;
                }
            }
            else
            {
                blank = 0;
            }
            result.Append(line).Append('\n');
        }
        return result.ToString().Trim();
    }
}
