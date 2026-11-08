namespace ArtGrader.Core.Documents;

public sealed record DocumentContent(string? Text, byte[]? Pdf)
{
    public static readonly IReadOnlyList<string> SupportedExtensions = [".docx", ".pdf", ".txt", ".md"];

    public static DocumentContent Read(string fileName, byte[] bytes)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".docx" => new DocumentContent(DocxTextExtractor.Extract(new MemoryStream(bytes)), null),
            ".pdf" => new DocumentContent(null, bytes),
            ".txt" or ".md" => new DocumentContent(System.Text.Encoding.UTF8.GetString(bytes).Trim(), null),
            _ => throw new UnsupportedDocumentException(
                $"Das Format «{extension}» wird nicht unterstützt. Bitte Word (.docx), PDF oder Text hochladen."),
        };
    }
}

public sealed class UnsupportedDocumentException(string message) : Exception(message);
