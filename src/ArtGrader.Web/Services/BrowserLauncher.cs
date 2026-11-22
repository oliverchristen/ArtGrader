using System.Diagnostics;

namespace ArtGrader.Web.Services;

public static class BrowserLauncher
{
    public static void Open(string url, ILogger? logger)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger?.LogWarning(ex, "Could not open the browser. Open {Url} manually.", url);
        }
    }
}
