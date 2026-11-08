using ArtGrader.Core.Storage;

namespace ArtGrader.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void Base_folder_follows_platform_conventions()
    {
        var folder = AppPaths.PlatformBaseFolder();

        if (OperatingSystem.IsMacOS())
        {
            Assert.EndsWith(Path.Combine("Library", "Application Support"), folder);
        }
        else if (OperatingSystem.IsWindows())
        {
            Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), folder);
        }
    }

    [Fact]
    public void Override_root_creates_all_folders()
    {
        var root = Path.Combine(Path.GetTempPath(), "artgrader-test-" + Guid.NewGuid());
        try
        {
            var paths = new AppPaths(root);

            Assert.True(Directory.Exists(paths.Files));
            Assert.True(Directory.Exists(paths.Keys));
            Assert.Equal(Path.Combine(root, "artgrader.db"), paths.Database);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
