namespace ArtGrader.Web.Services.Updates;

/// <summary>
/// Replaces the running ArtGrader with a new version, without a script. Windows does not let a program overwrite its
/// running .exe, but it lets it rename it. So each file that the new version replaces is first renamed to *.old, then
/// the new file is copied in. macOS lets a program move its own bundle, so the bundle is renamed to *.app.old and the new
/// bundle takes its place. If a step fails, the old version is restored, and ArtGrader keeps running. The *.old files
/// stay until the next start, because the running program still uses them.
/// </summary>
public static class AppReplacer
{
    public const string OldSuffix = ".old";

    public static void ReplaceFolder(string newFolder, string installFolder)
    {
        var relatives = Directory.EnumerateFiles(newFolder, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(newFolder, f))
            .ToList();
        var renamed = new List<string>();
        var copied = new List<string>();
        try
        {
            foreach (var target in relatives.Select(r => Path.Combine(installFolder, r)).Where(File.Exists))
            {
                var old = target + OldSuffix;
                File.Delete(old);
                File.Move(target, old);
                renamed.Add(target);
            }
            foreach (var relative in relatives)
            {
                var target = Path.Combine(installFolder, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(newFolder, relative), target);
                copied.Add(target);
            }
        }
        catch
        {
            foreach (var target in copied)
            {
                File.Delete(target);
            }
            foreach (var target in renamed)
            {
                File.Move(target + OldSuffix, target, overwrite: true);
            }
            throw;
        }
    }

    public static void ReplaceBundle(string newBundle, string bundle)
    {
        var old = bundle + OldSuffix;
        if (Directory.Exists(old))
        {
            Directory.Delete(old, recursive: true);
        }
        Directory.Move(bundle, old);
        try
        {
            MoveDirectory(newBundle, bundle);
        }
        catch
        {
            if (Directory.Exists(bundle))
            {
                Directory.Delete(bundle, recursive: true);
            }
            Directory.Move(old, bundle);
            throw;
        }
        if (!OperatingSystem.IsWindows())
        {
            // The package may lose the execute rights on the way; without them macOS cannot start ArtGrader.
            const UnixFileMode Executable = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(Path.Combine(bundle, "Contents", "MacOS", "ArtGrader"), Executable);
            File.SetUnixFileMode(Path.Combine(bundle, "Contents", "Resources", "app", "ArtGrader"), Executable);
        }
    }

    /// <summary>Deletes what an update left behind. Files that are still in use stay until the next start.</summary>
    public static void RemoveLeftovers(string installPath, bool bundle)
    {
        if (bundle)
        {
            TryDeleteDirectory(installPath + OldSuffix);
            return;
        }
        if (!Directory.Exists(installPath))
        {
            return;
        }
        foreach (var file in Directory.EnumerateFiles(installPath, "*" + OldSuffix, SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Still in use; the next start removes it.
            }
        }
    }

    /// <summary>Moves a folder, also to another volume, where a plain move does not work.</summary>
    private static void MoveDirectory(string source, string target)
    {
        try
        {
            Directory.Move(source, target);
        }
        catch (IOException) when (Directory.Exists(source) && !Directory.Exists(target))
        {
            CopyDirectory(source, target);
            Directory.Delete(source, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still in use; the next start removes it.
        }
    }
}
