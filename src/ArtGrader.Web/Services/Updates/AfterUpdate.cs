using System.Diagnostics;
using System.Globalization;

namespace ArtGrader.Web.Services.Updates;

/// <summary>
/// The old ArtGrader starts the new one with "--after-update &lt;process id&gt;" once its web server has stopped. The new
/// one waits until the old process has ended, so that it can remove the old files.
/// </summary>
public static class AfterUpdate
{
    public const string Argument = "--after-update";

    private static readonly TimeSpan MaximumWait = TimeSpan.FromSeconds(30);

    public static int? PreviousProcessId(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (args[i] == Argument && int.TryParse(args[i + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                return id;
            }
        }
        return null;
    }

    public static async Task WaitForPreviousAsync(IReadOnlyList<string> args)
    {
        if (PreviousProcessId(args) is not { } id)
        {
            return;
        }
        try
        {
            using var previous = Process.GetProcessById(id);
            using var timeout = new CancellationTokenSource(MaximumWait);
            await previous.WaitForExitAsync(timeout.Token);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OperationCanceledException
            or System.ComponentModel.Win32Exception)
        {
            // Already ended, it takes too long, or the id now belongs to a process of another user that this one may not
            // watch: start anyway. Old files that are still in use stay until the next start.
        }
    }
}
