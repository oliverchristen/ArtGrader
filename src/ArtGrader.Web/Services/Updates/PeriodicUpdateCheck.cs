namespace ArtGrader.Web.Services.Updates;

/// <summary>
/// Removes old update downloads, then looks for a new version at start and every hour while ArtGrader runs, unless the
/// automatic check is switched off. Files that the update script of the previous version still holds stay until the
/// next start.
/// </summary>
public sealed class PeriodicUpdateCheck(Updater updater, TimeProvider clock) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        updater.CleanUp();
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            await updater.CheckAutomaticallyAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
