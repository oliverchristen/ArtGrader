using System.Text;
using ArtGrader.Core.Ai;
using ArtGrader.Core.Storage;
using ArtGrader.Web.Components;
using ArtGrader.Web.Data;
using ArtGrader.Web.Export;
using ArtGrader.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

var url = builder.Configuration["ArtGrader:Url"] ?? "http://localhost:5080";
builder.WebHost.UseUrls(url);

var paths = new AppPaths();
builder.Services.AddSingleton(paths);
builder.Services.AddDataProtection().SetApplicationName("ArtGrader").PersistKeysToFileSystem(new DirectoryInfo(paths.Keys)).LockKeysToUser();
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite($"Data Source={paths.Database}"));
builder.Services.AddSingleton<ExamRepository>();
builder.Services.AddSingleton<FileStore>();
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<IAiSettingsSource>(sp => sp.GetRequiredService<SettingsStore>());
builder.Services.AddSingleton<AnthropicClaudeGateway>();
builder.Services.AddSingleton(sp => new OllamaGateway(
    new HttpClient { Timeout = TimeSpan.FromMinutes(30) },
    sp.GetRequiredService<IAiSettingsSource>()));
builder.Services.AddSingleton<IModelGateway, RoutingModelGateway>();
builder.Services.AddSingleton<ModelDownloads>();
builder.Services.AddSingleton<RubricImportService>();
builder.Services.AddSingleton<SubmissionGradingService>();
builder.Services.AddSingleton<GradingEvents>();
builder.Services.AddScoped<CurrentExam>();
builder.Services.AddSingleton<GradingQueue>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GradingQueue>());

builder.Services.AddMudServices();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 1024 * 1024);

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ExamRepository>().ReplaceGeneratedNamesAsync();
}

try
{
    var settings = app.Services.GetRequiredService<SettingsStore>();
    var removed = KeyLock.RemovePlaintextKeys(app.Services, paths.Keys, settings.ReprotectApiKey);
    if (removed > 0)
    {
        app.Logger.LogInformation("Schlüssel an das Benutzerkonto gebunden, {Count} ungeschützte Schlüsseldatei(en) entfernt.", removed);
    }
}
catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or InvalidOperationException or IOException)
{
    app.Logger.LogWarning(ex, "Die Schlüssel konnten nicht an das Benutzerkonto gebunden werden.");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet("/files/{**path}", (string path, FileStore files) =>
{
    var full = files.TryResolveExisting(path);
    if (full is null)
    {
        return Results.NotFound();
    }
    var contentType = Path.GetExtension(full).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream",
    };
    return Results.File(full, contentType, enableRangeProcessing: true);
});

app.MapGet("/bild/{imageId:guid}", async (Guid imageId, ExamRepository repository, FileStore files, CancellationToken ct) =>
{
    var image = await repository.GetImageAsync(imageId);
    if (image is null || files.TryResolveExisting(image.RelativePath) is null)
    {
        return Results.NotFound();
    }
    var bytes = await files.ReadAsync(image.RelativePath, ct);
    return Results.File(ArtGrader.Core.Images.ImageCover.Apply(bytes, image.Covers), "image/jpeg");
});

app.MapGet("/export/{examId:guid}/noten.xlsx", async (Guid examId, ExamRepository repository) =>
{
    var exam = await repository.GetAsync(examId);
    return exam is null
        ? Results.NotFound()
        : Results.File(
            ExcelExporter.Create(exam),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName(exam.Title, "Noten", "xlsx"));
});

app.MapGet(
    "/export/{examId:guid}/beurteilungen.pdf",
    async (Guid examId, bool? begruendungen, Guid? submission, ExamRepository repository, FileStore files, CancellationToken ct) =>
    {
        var exam = await repository.GetAsync(examId);
        if (exam is null)
        {
            return Results.NotFound();
        }
        var selected = submission is null ? exam.Submissions : exam.Submissions.Where(s => s.Id == submission).ToList();
        if (selected.Count == 0)
        {
            return Results.NotFound();
        }
        var images = new Dictionary<Guid, byte[]>();
        foreach (var image in selected.SelectMany(s => s.Images.OrderBy(i => i.Order).Take(4)))
        {
            if (files.TryResolveExisting(image.RelativePath) is not null)
            {
                var covered = ArtGrader.Core.Images.ImageCover.Apply(await files.ReadAsync(image.RelativePath, ct), image.Covers);
                await using var original = new MemoryStream(covered);
                images[image.Id] = await ArtGrader.Core.Images.ImagePreprocessor.ToJpegAsync(original, 1200, ct);
            }
        }
        var name = submission is null ? "Beurteilungen" : selected[0].StudentName;
        return Results.File(
            FeedbackPdf.Create(exam, selected, begruendungen ?? false, images),
            "application/pdf",
            FileName(exam.Title, name, "pdf"));
    });

app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("ArtGrader läuft auf {Url} – dieses Fenster offen lassen. Daten: {Data}", url, paths.Root);
    if (!app.Environment.IsDevelopment() && Environment.GetEnvironmentVariable("ARTGRADER_NO_BROWSER") is null)
    {
        BrowserLauncher.Open(url, logger);
    }
});

app.Run();

static string FileName(string examTitle, string name, string extension)
{
    var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
    var builder = new StringBuilder();
    foreach (var c in $"{examTitle} - {name}.{extension}")
    {
        builder.Append(invalid.Contains(c) ? '_' : c);
    }
    return builder.ToString();
}
