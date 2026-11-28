using System.Diagnostics;
using System.Globalization;
using System.Text;
using ArtGrader.Core.Storage;

namespace ArtGrader.Web.Services.Updates;

public interface IUpdateLauncher
{
    void Launch(PreparedUpdate update);
}

/// <summary>
/// A running program cannot replace its own files. So a small script waits until ArtGrader has stopped, copies the new
/// version over the old one and starts it again. ArtGrader stops right after it has started the script.
/// </summary>
public sealed class UpdateLauncher(AppPaths paths, IHostApplicationLifetime lifetime) : IUpdateLauncher
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public void Launch(PreparedUpdate update)
    {
        Directory.CreateDirectory(paths.Updates);
        var processId = Environment.ProcessId;
        if (OperatingSystem.IsWindows())
        {
            var script = Path.Combine(paths.Updates, "install.cmd");
            Directory.CreateDirectory(paths.Logs);
            File.WriteAllText(script, WindowsScript(processId, update, Path.Combine(paths.Logs, "update.log")), Utf8);
            Process.Start(new ProcessStartInfo(script) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = paths.Updates });
        }
        else
        {
            var script = Path.Combine(paths.Updates, "install.sh");
            File.WriteAllText(script, MacScript, Utf8);
            var start = new ProcessStartInfo("/bin/bash") { UseShellExecute = false, WorkingDirectory = paths.Updates };
            start.ArgumentList.Add(script);
            start.ArgumentList.Add(processId.ToString(CultureInfo.InvariantCulture));
            start.ArgumentList.Add(update.NewContent);
            start.ArgumentList.Add(update.InstallPath);
            start.ArgumentList.Add(paths.Root);
            start.ArgumentList.Add("/Applications");
            Process.Start(start);
        }
        lifetime.StopApplication();
    }

    /// <summary>
    /// cmd.exe does not reliably take paths with spaces and special characters as arguments, so the paths are written into
    /// the script. The Windows tools are called with their full path: a PATH with Git or other Unix tools has its own
    /// "find", and then the script would not wait. robocopy retries while Windows still holds the old ArtGrader.exe.
    /// </summary>
    public static string WindowsScript(int processId, PreparedUpdate update, string logFile) =>
        $"""
        @echo off
        chcp 65001 >NUL
        set "SYS=%SystemRoot%\System32"
        set "SOURCE={Escape(Path.GetFullPath(update.NewContent))}"
        set "TARGET={Escape(Path.GetFullPath(update.InstallPath))}"
        set "LOG={Escape(Path.GetFullPath(logFile))}"
        :wait
        "%SYS%\tasklist.exe" /FI "PID eq {processId}" /NH 2>NUL | "%SYS%\find.exe" " {processId} " >NUL
        if not errorlevel 1 (
            "%SYS%\ping.exe" -n 2 127.0.0.1 >NUL
            goto wait
        )
        "%SYS%\robocopy.exe" "%SOURCE%" "%TARGET%" /E /R:60 /W:1 /NP /UNILOG:"%LOG%" >NUL
        start "" "%TARGET%\ArtGrader.exe"

        """.ReplaceLineEndings("\r\n");

    private static string Escape(string path) => path.Replace("%", "%%", StringComparison.Ordinal);

    /// <summary>
    /// Arguments: process id, new bundle, installed bundle, data folder, applications folder. The old bundle stays until the
    /// new one is in place, so a failed move leaves the old version working. Like build/install.sh, the script links the
    /// app into the applications folder when the user may write there, and adds it to the Dock. It adds the Dock entry
    /// only once (marker file dock-entry in the data folder), so an app the user removed from the Dock stays removed.
    /// </summary>
    public const string MacScript = """
        #!/bin/bash
        trap '' HUP
        pid="$1"
        source="$2"
        target="$3"
        data="$4"
        applications="${5:-/Applications}"
        while kill -0 "$pid" 2>/dev/null; do sleep 0.5; done
        rm -rf "$target.old"
        if mv "$target" "$target.old"; then
            if mv "$source" "$target"; then
                rm -rf "$target.old"
            else
                mv "$target.old" "$target"
            fi
        fi
        xattr -dr com.apple.quarantine "$target" 2>/dev/null || true
        chmod +x "$target/Contents/MacOS/ArtGrader" "$target/Contents/Resources/app/ArtGrader"
        codesign --force --sign - "$target/Contents/Resources/app/ArtGrader" >/dev/null 2>&1 || true

        shared="$applications/ArtGrader.app"
        if [[ "$target" != "$applications/"* ]] && [[ -L "$shared" || ! -e "$shared" ]] && [[ -w "$applications" ]]; then
            ln -sfn "$target" "$shared" 2>/dev/null || true
        fi

        marker="$data/dock-entry"
        if [[ ! -e "$marker" ]]; then
            dock_apps="$(defaults read com.apple.dock persistent-apps 2>/dev/null || true)"
            if [[ "$dock_apps" == *"/ArtGrader.app"* ]]; then
                touch "$marker"
            else
                url="file://$(printf '%s' "$target/" | sed 's/ /%20/g')"
                tile="<dict><key>tile-data</key><dict><key>file-data</key><dict><key>_CFURLString</key><string>$url</string><key>_CFURLStringType</key><integer>15</integer></dict></dict></dict>"
                if defaults write com.apple.dock persistent-apps -array-add "$tile" 2>/dev/null; then
                    killall Dock 2>/dev/null || true
                    touch "$marker"
                fi
            fi
        fi

        open "$target"

        """;
}
