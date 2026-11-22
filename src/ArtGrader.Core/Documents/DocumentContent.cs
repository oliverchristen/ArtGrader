namespace ArtGrader.Core.Documents;

public sealed record DocumentContent(string Text, IReadOnlyList<byte[]> Pages)
{
    public static readonly IReadOnlyList<string> SupportedExtensions = [".docx", ".pptx", ".pdf", ".txt", ".md"];

    public static DocumentContent Read(string fileName, byte[] bytes)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".docx" => new DocumentContent(DocxTextExtractor.Extract(new MemoryStream(bytes)), []),
            ".pptx" => new DocumentContent(PptxTextExtractor.Extract(new MemoryStream(bytes)), []),
            ".pdf" when OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() =>
                new DocumentContent("", PdfPages.Render(bytes)),
            ".txt" or ".md" => new DocumentContent(System.Text.Encoding.UTF8.GetString(bytes).Trim(), []),
            _ => throw new UnsupportedDocumentException(
                $"Das Format «{extension}» wird nicht unterstützt. Bitte Word (.docx), PowerPoint (.pptx), PDF oder Text hochladen."),
        };
    }
}

public sealed class UnsupportedDocumentException(string message) : Exception(message);
