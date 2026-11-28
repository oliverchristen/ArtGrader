using System.Diagnostics;
using System.Globalization;
using ArtGrader.Core.Storage;

namespace ArtGrader.Web.Services.Updates;

public interface IUpdateLauncher
{
    void Launch(PreparedUpdate update);
}

/// <summary>
/// Replaces the running ArtGrader with the new version (see <see cref="AppReplacer"/>) and stops it. The new version
/// starts only after the web server of this one has stopped, so that it finds the port free and does not take itself for
/// a second start. It gets "--after-update" with this process id and waits until this process has ended.
/// </summary>
public sealed class UpdateLauncher(
    AppPaths paths,
    IHostApplicationLifetime lifetime,
    ILogger<UpdateLauncher> logger) : IUpdateLauncher
{
    public void Launch(PreparedUpdate update)
    {
        string program;
        if (OperatingSystem.IsMacOS())
        {
            AppReplacer.ReplaceBundle(update.NewContent, update.InstallPath);
            var mac = MacIntegration.ForThisComputer();
            mac.LinkIntoApplications(update.InstallPath, "/Applications");
            mac.AddToDockOnce(update.InstallPath, paths.Root);
            program = Path.Combine(update.InstallPath, "Contents", "Resources", "app", "ArtGrader");
        }
        else
        {
            AppReplacer.ReplaceFolder(update.NewContent, update.InstallPath);
            program = Path.Combine(update.InstallPath, "ArtGrader.exe");
        }

        var start = new ProcessStartInfo(program) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(program)! };
        start.ArgumentList.Add(AfterUpdate.Argument);
        start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        lifetime.ApplicationStopped.Register(() =>
        {
            try
            {
                Process.Start(start);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                logger.LogError(ex, "The new ArtGrader is installed but did not start. Start it again by hand.");
            }
        });
        lifetime.StopApplication();
    }
}
