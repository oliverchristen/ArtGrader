using System.Text;
using ArtGrader.Core.Ai;
using ArtGrader.Core.Storage;
using ArtGrader.Web.Components;
using ArtGrader.Web.Data;
using ArtGrader.Web.Export;
using ArtGrader.Web.Services;
using ArtGrader.Web.Services.Updates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using static ArtGrader.Core.Localization.Texts;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

var url = builder.Configuration["ArtGrader:Url"] ?? "http://localhost:5080";
var paths = new AppPaths();
var accessKey = AccessGate.LoadOrCreateKey(paths.AccessKeyFile);
var gate = new AccessGate(accessKey, TimeProvider.System);
if (await SingleInstance.IsRunningAsync(url))
{
    Console.WriteLine($"ArtGrader is already running on {url}.");
    if (Environment.GetEnvironmentVariable("ARTGRADER_NO_BROWSER") is null)
    {
        // The running ArtGrader hands out a ticket only to someone who can read this user's access key.
        var ticket = await SingleInstance.TicketAsync(url, accessKey);
        BrowserLauncher.Open(ticket is null ? url : AccessGate.Link(url, ticket), null);
    }
    return;
}
builder.WebHost.UseUrls(url);

builder.Logging.AddProvider(new FileLoggerProvider(paths.Logs));
builder.Logging.AddFilter<FileLoggerProvider>("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter<FileLoggerProvider>("System", LogLevel.Warning);
builder.Services.AddSingleton(paths);
builder.Services.AddSingleton(gate);
builder.Services.AddDataProtection().SetApplicationName("ArtGrader").PersistKeysToFileSystem(new DirectoryInfo(paths.Keys)).LockKeysToUser();
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite($"Data Source={paths.Database}"));
builder.Services.AddSingleton<ExamRepository>();
builder.Services.AddSingleton<FileStore>();
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<IAiSettingsSource>(sp => sp.GetRequiredService<SettingsStore>());
builder.Services.AddSingleton<AnthropicClaudeGateway>();
builder.Services.AddSingleton(sp => new OllamaGateway(
    new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromMinutes(30) },
    sp.GetRequiredService<IAiSettingsSource>()));
builder.Services.AddSingleton<IModelGateway, RoutingModelGateway>();
builder.Services.AddSingleton<ModelDownloads>();
builder.Services.AddSingleton<RubricImportService>();
builder.Services.AddSingleton<LocalRedactionFinder>();
builder.Services.AddSingleton<SubmissionGradingService>();
builder.Services.AddSingleton<GradingEvents>();
builder.Services.AddScoped<CurrentExam>();
builder.Services.AddSingleton<GradingQueue>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GradingQueue>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ActiveTabs>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, TabCircuitHandler>();
builder.Services.AddHostedService<IdleShutdown>();
builder.Services.AddSingleton<IUpdatePreferences>(sp => sp.GetRequiredService<SettingsStore>());
builder.Services.AddSingleton(UpdateTarget.ThisApp());
builder.Services.AddSingleton<IUpdateLauncher, UpdateLauncher>();
builder.Services.AddSingleton(sp => new Updater(
    new HttpClient { Timeout = Timeout.InfiniteTimeSpan },
    sp.GetRequiredService<IUpdatePreferences>(),
    paths,
    sp.GetRequiredService<UpdateTarget>(),
    sp.GetRequiredService<ILogger<Updater>>(),
    sp.GetRequiredService<IUpdateLauncher>()));
builder.Services.AddHostedService<PeriodicUpdateCheck>();

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
    await DocumentBackfill.RunAsync(scope.ServiceProvider.GetRequiredService<ExamRepository>(), scope.ServiceProvider.GetRequiredService<FileStore>(), app.Logger);
}

try
{
    var settings = app.Services.GetRequiredService<SettingsStore>();
    var removed = KeyLock.RemovePlaintextKeys(app.Services, paths.Keys, settings.ReprotectApiKey);
    if (removed > 0)
    {
        app.Logger.LogInformation("Bound the keys to the user account and removed {Count} unprotected key file(s).", removed);
    }
}
catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or InvalidOperationException or IOException)
{
    app.Logger.LogWarning(ex, "Could not bind the keys to the user account.");
}

app.Use(gate.InvokeAsync);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet(SingleInstance.PingPath, () => SingleInstance.PingAnswer);

app.MapPost(AccessGate.TicketPath, () => gate.NewTicket());

// The overview for school management (docs/schulleitung.html), built into the app.
app.MapGet("/schulleitung.html", () =>
    Results.Stream(typeof(Program).Assembly.GetManifestResourceStream("schulleitung.html")!, "text/html; charset=utf-8"));

app.MapPost("/beenden", (HttpContext context, IHostApplicationLifetime lifetime) =>
{
    var site = context.Request.Headers["Sec-Fetch-Site"].ToString();
    if (site.Length > 0 && site is not ("same-origin" or "none"))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
    _ = Task.Run(async () =>
    {
        await Task.Delay(500);
        lifetime.StopApplication();
    });
    return Results.Content(StoppedPage.Html(), "text/html; charset=utf-8");
}).DisableAntiforgery();

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
    return Results.File(ArtGrader.Core.Images.ImageCover.Apply(bytes, image.Covers, image.Crop), "image/jpeg");
});

app.MapGet("/seite/{pageId:guid}", async (Guid pageId, ExamRepository repository, FileStore files, CancellationToken ct) =>
{
    var page = await repository.GetPageAsync(pageId);
    if (page is null || files.TryResolveExisting(page.RelativePath) is null)
    {
        return Results.NotFound();
    }
    var bytes = await files.ReadAsync(page.RelativePath, ct);
    return Results.File(ArtGrader.Core.Images.ImageCover.Apply(bytes, page.Covers, page.Crop), "image/jpeg");
});

app.MapGet("/export/{examId:guid}/noten.xlsx", async (Guid examId, ExamRepository repository) =>
{
    var exam = await repository.GetAsync(examId);
    return exam is null
        ? Results.NotFound()
        : Results.File(
            ExcelExporter.Create(exam),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName(exam.Title, T("Grades"), "xlsx"));
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
                var covered = ArtGrader.Core.Images.ImageCover.Apply(await files.ReadAsync(image.RelativePath, ct), image.Covers, image.Crop);
                await using var original = new MemoryStream(covered);
                images[image.Id] = await ArtGrader.Core.Images.ImagePreprocessor.ToJpegAsync(original, 1200, ct);
            }
        }
        var name = submission is null ? T("Assessments") : selected[0].StudentName;
        return Results.File(
            FeedbackPdf.Create(exam, selected, begruendungen ?? false, images),
            "application/pdf",
            FileName(exam.Title, name, "pdf"));
    });

app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("ArtGrader is running on {Url}. Data: {Data}", url, paths.Root);
    if (app.Environment.IsDevelopment())
    {
        // Only on the console, never in the log file: this link lets a browser in.
        Console.WriteLine($"Open in the browser (valid for 10 minutes): {AccessGate.Link(url, gate.NewTicket(TimeSpan.FromMinutes(10)))}");
    }
    else if (Environment.GetEnvironmentVariable("ARTGRADER_NO_BROWSER") is null)
    {
        BrowserLauncher.Open(AccessGate.Link(url, gate.NewTicket()), logger);
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
