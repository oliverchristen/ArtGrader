using ArtGrader.Core.Documents;
using ArtGrader.Core.Domain;
using ArtGrader.Web.Data;

namespace ArtGrader.Web.Services;

public static class DocumentBackfill
{
    public static async Task RunAsync(ExamRepository repository, FileStore files, ILogger logger)
    {
        foreach (var summary in await repository.ListAsync())
        {
            var exam = await repository.GetAsync(summary.Id);
            if (exam is null)
            {
                continue;
            }
            await FillAsync(exam, DocumentKind.Aufgabe, exam.ExerciseFile, exam.ExerciseText, repository, files, logger);
            await FillAsync(exam, DocumentKind.Beurteilung, exam.RubricFile, exam.RubricText, repository, files, logger);
        }
    }

    private static async Task FillAsync(
        Exam exam, DocumentKind kind, StoredFile? file, string text, ExamRepository repository, FileStore files, ILogger logger)
    {
        if (file is null || !string.IsNullOrWhiteSpace(text) || exam.PagesOf(kind).Any() || files.TryResolveExisting(file.RelativePath) is null)
        {
            return;
        }
        try
        {
            var content = DocumentContent.Read(file.OriginalName, await files.ReadAsync(file.RelativePath));
            var paths = new List<string>();
            foreach (var page in content.Pages)
            {
                paths.Add(await files.SavePageAsync(exam.Id, page));
            }
            await repository.SetDocumentAsync(exam.Id, kind, file, content.Text, paths);
        }
        catch (Exception ex) when (ex is UnsupportedDocumentException or InvalidDataException or IOException
            or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            logger.LogWarning(ex, "Could not read document {File} afterwards.", file.OriginalName);
        }
    }
}
