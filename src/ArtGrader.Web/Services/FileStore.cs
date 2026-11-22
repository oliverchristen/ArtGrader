using ArtGrader.Core.Images;
using ArtGrader.Core.Storage;

namespace ArtGrader.Web.Services;

public sealed class FileStore(AppPaths paths)
{
    public async Task<string> SaveDocumentAsync(Guid examId, string kind, string originalName, byte[] bytes)
    {
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        var relative = Path.Combine(examId.ToString("N"), $"{kind}-{Guid.NewGuid():N}{extension}");
        await WriteAsync(relative, bytes);
        return ToUrlPath(relative);
    }

    public async Task<string> SaveSubmissionImageAsync(Guid examId, Stream original, CancellationToken cancellationToken)
    {
        var jpeg = await ImagePreprocessor.ToJpegAsync(original, ImagePreprocessor.StorageMaxSide, cancellationToken);
        var relative = Path.Combine(examId.ToString("N"), "arbeiten", $"{Guid.NewGuid():N}.jpg");
        await WriteAsync(relative, jpeg);
        return ToUrlPath(relative);
    }

    public async Task<string> SavePageAsync(Guid examId, byte[] jpeg)
    {
        var relative = Path.Combine(examId.ToString("N"), "seiten", $"{Guid.NewGuid():N}.jpg");
        await WriteAsync(relative, jpeg);
        return ToUrlPath(relative);
    }

    public Task<byte[]> ReadAsync(string relativePath, CancellationToken cancellationToken = default) =>
        File.ReadAllBytesAsync(Resolve(relativePath), cancellationToken);

    public string? TryResolveExisting(string relativePath)
    {
        try
        {
            var full = Resolve(relativePath);
            return File.Exists(full) ? full : null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return;
        }
        var full = TryResolveExisting(relativePath);
        if (full is not null)
        {
            File.Delete(full);
        }
    }

    public void DeleteExamFolder(Guid examId)
    {
        var folder = Path.Combine(paths.Files, examId.ToString("N"));
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private async Task WriteAsync(string relative, byte[] bytes)
    {
        var full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, bytes);
    }

    private string Resolve(string relativePath)
    {
        var root = Path.GetFullPath(paths.Files) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Pfad ausserhalb des Datenordners.");
        }
        return full;
    }

    private static string ToUrlPath(string relative) => relative.Replace(Path.DirectorySeparatorChar, '/');
}
