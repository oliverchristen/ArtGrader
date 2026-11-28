using System.Diagnostics;

namespace ArtGrader.Web.Services.Updates;

/// <summary>
/// Makes ArtGrader easy to find on the Mac, as build/install.sh does: a link in the general Applications folder, when the
/// user may write there, and a Dock entry. ArtGrader adds the Dock entry only once (marker file dock-entry in the data
/// folder), so an entry that the user removed stays removed. <paramref name="run"/> runs a command and returns its
/// output, or null when it fails.
/// </summary>
public sealed class MacIntegration(Func<IReadOnlyList<string>, string?> run)
{
    public const string DockMarker = "dock-entry";

    public static MacIntegration ForThisComputer() => new(RunTool);

    /// <summary>True when the link is in place. A real ArtGrader.app in the folder stays untouched.</summary>
    public bool LinkIntoApplications(string bundle, string applications)
    {
        if (!Directory.Exists(applications)
            || Path.GetFullPath(bundle).StartsWith(Path.GetFullPath(applications) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return false;
        }
        var shared = new DirectoryInfo(Path.Combine(applications, "ArtGrader.app"));
        try
        {
            if (shared.LinkTarget is not null)
            {
                shared.Delete();
            }
            else if (shared.Exists)
            {
                return false;
            }
            Directory.CreateSymbolicLink(shared.FullName, bundle);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>True when this call added the entry.</summary>
    public bool AddToDockOnce(string bundle, string dataFolder)
    {
        var marker = Path.Combine(dataFolder, DockMarker);
        if (File.Exists(marker))
        {
            return false;
        }
        var dock = run(["defaults", "read", "com.apple.dock", "persistent-apps"]);
        if (dock is null)
        {
            return false;
        }
        if (dock.Contains("/ArtGrader.app", StringComparison.Ordinal))
        {
            File.WriteAllText(marker, "");
            return false;
        }
        var url = new Uri(Path.TrimEndingDirectorySeparator(bundle) + "/").AbsoluteUri;
        var tile = "<dict><key>tile-data</key><dict><key>file-data</key><dict><key>_CFURLString</key><string>"
            + System.Security.SecurityElement.Escape(url)
            + "</string><key>_CFURLStringType</key><integer>15</integer></dict></dict></dict>";
        if (run(["defaults", "write", "com.apple.dock", "persistent-apps", "-array-add", tile]) is null)
        {
            return false;
        }
        run(["killall", "Dock"]);
        File.WriteAllText(marker, "");
        return true;
    }

    private static string? RunTool(IReadOnlyList<string> command)
    {
        var start = new ProcessStartInfo("/usr/bin/" + command[0]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in command.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
