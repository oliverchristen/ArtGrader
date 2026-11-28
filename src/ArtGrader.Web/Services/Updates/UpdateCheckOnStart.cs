namespace ArtGrader.Web.Services.Updates;

/// <summary>
/// Removes old update downloads and, unless switched off, looks for a new version. Files that the update script of the
/// previous version still holds stay until the next start.
/// </summary>
public sealed class UpdateCheckOnStart(Updater updater) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        updater.CleanUp();
        await updater.CheckOnStartAsync(stoppingToken);
    }
}
