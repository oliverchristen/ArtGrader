using System.Reflection;
using System.Runtime.InteropServices;

namespace ArtGrader.Web.Services.Updates;

/// <summary>
/// The running ArtGrader: its version, the package it needs and where it is installed. <see cref="InstallPath"/> is the
/// folder with ArtGrader.exe on Windows and ArtGrader.app on the Mac. It is null when ArtGrader runs from the IDE or as a
/// plain build, because then there is nothing to replace.
/// </summary>
public sealed record UpdateTarget(Version Current, string? RuntimeId, string? InstallPath)
{
    public bool CanInstall => RuntimeId is not null && InstallPath is not null;

    public static UpdateTarget ThisApp()
    {
        var version = ReleaseFeed.Normalize(typeof(UpdateTarget).Assembly.GetName().Version ?? new Version(0, 0, 0));
        var runtimeId = RuntimeIdFor(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS(), RuntimeInformation.OSArchitecture);
        return new UpdateTarget(version, runtimeId, runtimeId is null ? null : InstalledAt());
    }

    public static string? RuntimeIdFor(bool windows, bool mac, Architecture architecture) =>
        (windows, mac, architecture) switch
        {
            (true, _, Architecture.X64) => "win-x64",
            (_, true, Architecture.Arm64) => "osx-arm64",
            (_, true, Architecture.X64) => "osx-x64",
            _ => null,
        };

    /// <summary>The ArtGrader.app around Contents/Resources/app, where the Mac package puts the program.</summary>
    public static string? AppBundleOf(string baseDirectory)
    {
        var app = new DirectoryInfo(Path.TrimEndingDirectorySeparator(baseDirectory));
        var bundle = app.Parent?.Parent?.Parent;
        return app.Name == "app" && app.Parent?.Name == "Resources" && app.Parent.Parent?.Name == "Contents"
            && bundle is not null && bundle.Name.EndsWith(".app", StringComparison.Ordinal)
            ? bundle.FullName
            : null;
    }

    private static string? InstalledAt()
    {
        // Only the published single-file program can replace itself. A plain build has an assembly file.
#pragma warning disable IL3000
        var singleFile = string.IsNullOrEmpty(Assembly.GetEntryAssembly()?.Location);
#pragma warning restore IL3000
        if (!singleFile || Environment.ProcessPath is null)
        {
            return null;
        }
        return OperatingSystem.IsMacOS() ? AppBundleOf(AppContext.BaseDirectory) : Path.GetDirectoryName(Environment.ProcessPath);
    }
}
