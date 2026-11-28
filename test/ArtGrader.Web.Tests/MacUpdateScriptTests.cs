using System.Diagnostics;
using ArtGrader.Web.Services.Updates;

namespace ArtGrader.Web.Tests;

/// <summary>
/// Runs the Mac update script in bash, with stand-ins for the macOS tools (defaults, killall, open, codesign, xattr).
/// On Windows it uses the bash of Git for Windows; without bash the tests are skipped.
/// </summary>
public sealed class MacUpdateScriptTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "artgrader-macscript-" + Guid.NewGuid().ToString("N"));

    public MacUpdateScriptTests()
    {
        Directory.CreateDirectory(Tools);
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Applications);
        Fake("defaults", """
            if [[ "$1" == read ]]; then cat "$FAKE_DOCK" 2>/dev/null; else echo "$5" >> "$FAKE_DOCK"; fi
            """);
        Fake("open", """echo "$1" >> "$FAKE_OPENED" """);
        Fake("killall", "exit 0");
        Fake("codesign", "exit 0");
        Fake("xattr", "exit 0");
        Bundle(Target, "1");
    }

    private string Tools => Path.Combine(folder, "tools");

    private string Data => Path.Combine(folder, "data");

    private string Applications => Path.Combine(folder, "Applications");

    private string Target => Path.Combine(folder, "home", "Applications", "ArtGrader.app");

    private string DockFile => Path.Combine(folder, "dock.txt");

    private string OpenedFile => Path.Combine(folder, "opened.txt");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task The_update_replaces_the_app_adds_it_to_the_dock_once_and_starts_it()
    {
        await UpdateAsync("2");
        await UpdateAsync("3");

        Assert.Equal("3", File.ReadAllText(Path.Combine(Target, "Contents", "version.txt")).Trim());
        var entries = File.ReadAllLines(DockFile);
        Assert.Single(entries);
        Assert.Contains("ArtGrader.app", entries[0]);
        Assert.Equal(2, File.ReadAllLines(OpenedFile).Length);
    }

    [Fact]
    public async Task The_update_puts_a_link_into_the_applications_folder()
    {
        await UpdateAsync("2");

        Assert.True(Directory.Exists(Path.Combine(Applications, "ArtGrader.app", "Contents")));
    }

    [Fact]
    public async Task An_app_the_user_removed_from_the_dock_is_not_added_again()
    {
        await UpdateAsync("2");
        File.Delete(DockFile);

        await UpdateAsync("3");

        Assert.False(File.Exists(DockFile));
    }

    [Fact]
    public async Task Without_an_applications_folder_the_update_still_works()
    {
        Directory.Delete(Applications);

        await UpdateAsync("2");

        Assert.False(Directory.Exists(Applications));
        Assert.Equal("2", File.ReadAllText(Path.Combine(Target, "Contents", "version.txt")).Trim());
    }

    private async Task UpdateAsync(string version)
    {
        var bash = Bash() ?? throw new InvalidOperationException();
        var source = Path.Combine(folder, "updates", version, "ArtGrader.app");
        Bundle(source, version);
        var script = Path.Combine(folder, "install.sh");
        await File.WriteAllTextAsync(script, UpdateLauncher.MacScript, TestContext.Current.CancellationToken);

        var start = new ProcessStartInfo(bash) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("""PATH="$1:$PATH"; shift; exec bash "$@" """);
        start.ArgumentList.Add("_");
        foreach (var argument in new[] { Tools, script, "999999", source, Target, Data, Applications })
        {
            start.ArgumentList.Add(argument == "999999" ? argument : ForBash(argument));
        }
        start.Environment["FAKE_DOCK"] = ForBash(DockFile);
        start.Environment["FAKE_OPENED"] = ForBash(OpenedFile);
        using var process = Process.Start(start)!;
        var errors = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(process.ExitCode == 0, errors);
    }

    private static string? Bash()
    {
        var bash = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe")
            : "/bin/bash";
        if (!File.Exists(bash))
        {
            Assert.Skip("bash is not installed.");
        }
        return bash;
    }

    /// <summary>Git bash on Windows wants /c/folder instead of C:\folder.</summary>
    private static string ForBash(string path) =>
        OperatingSystem.IsWindows() && path.Length > 2 && path[1] == ':'
            ? "/" + char.ToLowerInvariant(path[0]) + path[2..].Replace('\\', '/')
            : path;

    private void Fake(string name, string body) =>
        File.WriteAllText(Path.Combine(Tools, name), "#!/bin/bash\n" + body.Trim() + "\n");

    private static void Bundle(string bundle, string version)
    {
        Directory.CreateDirectory(Path.Combine(bundle, "Contents", "MacOS"));
        Directory.CreateDirectory(Path.Combine(bundle, "Contents", "Resources", "app"));
        File.WriteAllText(Path.Combine(bundle, "Contents", "MacOS", "ArtGrader"), "#!/bin/bash\n");
        File.WriteAllText(Path.Combine(bundle, "Contents", "Resources", "app", "ArtGrader"), "");
        File.WriteAllText(Path.Combine(bundle, "Contents", "version.txt"), version);
    }
}
