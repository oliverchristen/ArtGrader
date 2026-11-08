namespace ArtGrader.Core.Storage;

public sealed class AppPaths
{
    public const string AppFolderName = "ArtGrader";

    public AppPaths(string? overrideRoot = null)
    {
        Root = overrideRoot ?? DefaultRoot();
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Files);
        Directory.CreateDirectory(Keys);
    }

    public string Root { get; }

    public string Database => Path.Combine(Root, "artgrader.db");

    public string Files => Path.Combine(Root, "files");

    public string Keys => Path.Combine(Root, "keys");

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public static string DefaultRoot()
    {
        var overridden = Environment.GetEnvironmentVariable("ARTGRADER_DATA");
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return overridden;
        }
        return Path.Combine(PlatformBaseFolder(), AppFolderName);
    }

    public static string PlatformBaseFolder()
    {
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }
}
