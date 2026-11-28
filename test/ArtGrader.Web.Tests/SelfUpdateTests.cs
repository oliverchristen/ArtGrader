using ArtGrader.Web.Services.Updates;

namespace ArtGrader.Web.Tests;

/// <summary>ArtGrader replaces its own files, without a script. See AppReplacer and MacIntegration.</summary>
public sealed class SelfUpdateTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "artgrader-selfupdate-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private string Install => Path.Combine(folder, "install");

    private string New => Path.Combine(folder, "new");

    [Fact]
    public void On_windows_the_files_are_replaced_and_the_old_ones_kept_until_cleanup()
    {
        Write(Install, "ArtGrader.exe", "old exe");
        Write(Install, "wwwroot/app.css", "old css");
        Write(Install, "notes.txt", "kept");
        Write(New, "ArtGrader.exe", "new exe");
        Write(New, "wwwroot/app.css", "new css");
        Write(New, "wwwroot/new.js", "added");

        AppReplacer.ReplaceFolder(New, Install);

        Assert.Equal("new exe", Read(Install, "ArtGrader.exe"));
        Assert.Equal("new css", Read(Install, "wwwroot/app.css"));
        Assert.Equal("added", Read(Install, "wwwroot/new.js"));
        Assert.Equal("kept", Read(Install, "notes.txt"));
        Assert.Equal("old exe", Read(Install, "ArtGrader.exe.old"));

        AppReplacer.RemoveLeftovers(Install, bundle: false);

        Assert.Empty(Directory.EnumerateFiles(Install, "*.old", SearchOption.AllDirectories));
        Assert.Equal("new exe", Read(Install, "ArtGrader.exe"));
    }

    [Fact]
    public void When_a_file_cannot_be_copied_the_old_version_is_restored()
    {
        Write(Install, "ArtGrader.exe", "old exe");
        Write(Install, "wwwroot/app.css", "old css");
        Write(New, "ArtGrader.exe", "new exe");
        Write(New, "wwwroot/app.css", "new css");
        Write(New, "blocked/file.txt", "new");
        // A file where the new version needs a folder makes the copy fail after the first files.
        Write(Install, "blocked", "a file, not a folder");

        Assert.ThrowsAny<IOException>(() => AppReplacer.ReplaceFolder(New, Install));

        Assert.Equal("old exe", Read(Install, "ArtGrader.exe"));
        Assert.Equal("old css", Read(Install, "wwwroot/app.css"));
        Assert.Empty(Directory.EnumerateFiles(Install, "*.old", SearchOption.AllDirectories));
    }

    [Fact]
    public void A_file_that_another_program_holds_stops_the_update_and_changes_nothing()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Only Windows locks open files against renaming.");
        }
        Write(Install, "ArtGrader.exe", "old exe");
        Write(Install, "appsettings.json", "old settings");
        Write(New, "ArtGrader.exe", "new exe");
        Write(New, "appsettings.json", "new settings");

        using (File.Open(Path.Combine(Install, "appsettings.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsAny<IOException>(() => AppReplacer.ReplaceFolder(New, Install));
        }

        Assert.Equal("old exe", Read(Install, "ArtGrader.exe"));
        Assert.Equal("old settings", Read(Install, "appsettings.json"));
        Assert.Empty(Directory.EnumerateFiles(Install, "*.old", SearchOption.AllDirectories));
    }

    [Fact]
    public void On_the_mac_the_bundle_is_replaced_and_the_old_one_kept_until_cleanup()
    {
        var bundle = Bundle(Path.Combine(Install, "ArtGrader.app"), "1");
        var update = Bundle(Path.Combine(New, "ArtGrader.app"), "2");

        AppReplacer.ReplaceBundle(update, bundle);

        Assert.Equal("2", Read(bundle, "Contents/version.txt"));
        Assert.Equal("1", Read(bundle + ".old", "Contents/version.txt"));
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(File.GetUnixFileMode(Path.Combine(bundle, "Contents", "Resources", "app", "ArtGrader")).HasFlag(UnixFileMode.UserExecute));
            Assert.True(File.GetUnixFileMode(Path.Combine(bundle, "Contents", "MacOS", "ArtGrader")).HasFlag(UnixFileMode.UserExecute));
        }

        AppReplacer.RemoveLeftovers(bundle, bundle: true);

        Assert.False(Directory.Exists(bundle + ".old"));
    }

    [Fact]
    public void When_the_new_bundle_cannot_be_moved_the_old_one_is_restored()
    {
        var bundle = Bundle(Path.Combine(Install, "ArtGrader.app"), "1");

        Assert.ThrowsAny<IOException>(() => AppReplacer.ReplaceBundle(Path.Combine(New, "missing.app"), bundle));

        Assert.Equal("1", Read(bundle, "Contents/version.txt"));
        Assert.False(Directory.Exists(bundle + ".old"));
    }

    [Fact]
    public void The_previous_process_is_read_from_the_arguments()
    {
        Assert.Equal(1234, AfterUpdate.PreviousProcessId(["--after-update", "1234"]));
        Assert.Null(AfterUpdate.PreviousProcessId([]));
        Assert.Null(AfterUpdate.PreviousProcessId(["--after-update"]));
        Assert.Null(AfterUpdate.PreviousProcessId(["--after-update", "abc"]));
    }

    [Fact]
    public async Task A_previous_process_id_that_now_belongs_to_a_protected_process_does_not_stop_the_start()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Process 4 is the protected System process only on Windows.");
        }

        var wait = AfterUpdate.WaitForPreviousAsync(["--after-update", "4"]);

        Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
        await wait;
    }

    [Fact]
    public async Task A_previous_process_that_has_ended_does_not_delay_the_start()
    {
        var wait = AfterUpdate.WaitForPreviousAsync(["--after-update", int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)]);

        Assert.Same(wait, await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)));
        await wait;
    }

    [Fact]
    public void The_dock_entry_is_added_once_and_an_entry_the_user_removed_stays_removed()
    {
        var dock = new List<string>();
        var calls = new List<string>();
        var mac = new MacIntegration(command =>
        {
            calls.Add(string.Join(' ', command.Take(2)));
            return (command[0], command[1]) switch
            {
                ("defaults", "read") => string.Join("\n", dock),
                ("defaults", "write") => Add(dock, command[^1]),
                _ => "",
            };
        });
        var bundle = Path.Combine(folder, "home", "Applications", "ArtGrader.app");
        var data = Path.Combine(folder, "data");
        Directory.CreateDirectory(data);

        Assert.True(mac.AddToDockOnce(bundle, data));
        Assert.Single(dock);
        Assert.Contains("ArtGrader.app", dock[0]);
        Assert.Contains("killall Dock", calls);

        dock.Clear();
        Assert.False(mac.AddToDockOnce(bundle, data));
        Assert.Empty(dock);
    }

    [Fact]
    public void An_entry_that_is_already_in_the_dock_is_not_added_twice()
    {
        var writes = 0;
        var mac = new MacIntegration(command => (command[0], command[1]) switch
        {
            ("defaults", "read") => "\"_CFURLString\" = \"file:///Users/x/Applications/ArtGrader.app/\";",
            ("defaults", "write") => (++writes).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => "",
        });
        var data = Path.Combine(folder, "data");
        Directory.CreateDirectory(data);

        Assert.False(mac.AddToDockOnce(Path.Combine(folder, "ArtGrader.app"), data));
        Assert.Equal(0, writes);
        Assert.True(File.Exists(Path.Combine(data, "dock-entry")));
    }

    [Fact]
    public void A_failing_dock_tool_leaves_no_marker_so_the_next_update_tries_again()
    {
        var mac = new MacIntegration(_ => null);
        var data = Path.Combine(folder, "data");
        Directory.CreateDirectory(data);

        Assert.False(mac.AddToDockOnce(Path.Combine(folder, "ArtGrader.app"), data));
        Assert.False(File.Exists(Path.Combine(data, "dock-entry")));
    }

    [Fact]
    public void The_app_is_linked_into_the_applications_folder()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Symbolic links need extra rights on Windows; the link is for the Mac.");
        }
        var bundle = Bundle(Path.Combine(folder, "home", "Applications", "ArtGrader.app"), "1");
        var applications = Path.Combine(folder, "Applications");
        Directory.CreateDirectory(applications);
        var mac = new MacIntegration(_ => "");

        Assert.True(mac.LinkIntoApplications(bundle, applications));
        Assert.Equal("1", Read(Path.Combine(applications, "ArtGrader.app"), "Contents/version.txt"));
        Assert.True(mac.LinkIntoApplications(bundle, applications));
    }

    [Fact]
    public void A_real_app_in_the_applications_folder_and_a_missing_folder_are_left_alone()
    {
        var bundle = Path.Combine(folder, "home", "Applications", "ArtGrader.app");
        var applications = Path.Combine(folder, "Applications");
        var mac = new MacIntegration(_ => "");

        Assert.False(mac.LinkIntoApplications(bundle, applications));
        Assert.False(Directory.Exists(applications));

        var real = Bundle(Path.Combine(applications, "ArtGrader.app"), "own copy");
        Assert.False(mac.LinkIntoApplications(bundle, applications));
        Assert.Equal("own copy", Read(real, "Contents/version.txt"));
    }

    private static string Add(List<string> dock, string entry)
    {
        dock.Add(entry);
        return "";
    }

    private static string Bundle(string bundle, string version)
    {
        Write(bundle, "Contents/MacOS/ArtGrader", "#!/bin/bash");
        Write(bundle, "Contents/Resources/app/ArtGrader", "program");
        Write(bundle, "Contents/version.txt", version);
        return bundle;
    }

    private static void Write(string root, string relative, string text)
    {
        var file = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
    }

    private static string Read(string root, string relative) => File.ReadAllText(Path.Combine(root, relative));
}
