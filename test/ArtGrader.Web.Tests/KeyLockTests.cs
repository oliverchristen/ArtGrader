using System.Security.Cryptography;
using ArtGrader.Core.Ai;
using ArtGrader.Core.Storage;
using ArtGrader.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace ArtGrader.Web.Tests;

public sealed class KeyLockTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"artgrader-keys-{Guid.NewGuid():N}");
    private readonly AppPaths paths;

    public KeyLockTests() => paths = new AppPaths(root);

    public void Dispose() => Directory.Delete(root, recursive: true);

    private sealed class FixedSecret(byte[] secret) : IUserSecretStore
    {
        public byte[] GetOrCreate() => secret;
    }

    private ServiceProvider Services(Action<IDataProtectionBuilder>? lockKeys = null, IUserSecretStore? store = null)
    {
        var services = new ServiceCollection();
        if (store is not null)
        {
            services.AddSingleton(store);
        }
        var builder = services.AddDataProtection().SetApplicationName("ArtGrader").PersistKeysToFileSystem(new DirectoryInfo(paths.Keys));
        lockKeys?.Invoke(builder);
        return services.BuildServiceProvider();
    }

    private SettingsStore Settings(IServiceProvider services) => new(paths, services.GetRequiredService<IDataProtectionProvider>());

    private void StoreApiKeyWithPlaintextKey()
    {
        using var before = Services();
        Settings(before).Save(new SettingsUpdate(AiProvider.Anthropic, "sk-ant-geheim", AiSettings.DefaultModel, "high", AiSettings.DefaultOllamaUrl, null));
        Assert.Contains(Directory.GetFiles(paths.Keys), KeyLock.IsPlaintext);
    }

    [Fact]
    public void Plaintext_key_is_replaced_by_a_locked_key_and_the_api_key_survives()
    {
        StoreApiKeyWithPlaintextKey();
        var secret = new FixedSecret(RandomNumberGenerator.GetBytes(32));

        using (var locked = Services(b => b.LockKeysWith(), secret))
        {
            var settings = Settings(locked);
            Assert.Equal(1, KeyLock.RemovePlaintextKeys(locked, paths.Keys, settings.ReprotectApiKey));
            Assert.Equal("sk-ant-geheim", settings.StoredApiKey());
        }

        Assert.DoesNotContain(Directory.GetFiles(paths.Keys), KeyLock.IsPlaintext);
        using var restarted = Services(b => b.LockKeysWith(), secret);
        Assert.Equal("sk-ant-geheim", Settings(restarted).StoredApiKey());
        Assert.Equal(0, KeyLock.RemovePlaintextKeys(restarted, paths.Keys, () => { }));
    }

    [Fact]
    public void A_copied_data_folder_cannot_reveal_the_api_key_without_the_users_secret()
    {
        StoreApiKeyWithPlaintextKey();
        using (var locked = Services(b => b.LockKeysWith(), new FixedSecret(RandomNumberGenerator.GetBytes(32))))
        {
            KeyLock.RemovePlaintextKeys(locked, paths.Keys, Settings(locked).ReprotectApiKey);
        }

        using var otherUser = Services(b => b.LockKeysWith(), new FixedSecret(RandomNumberGenerator.GetBytes(32)));

        Assert.Null(Settings(otherUser).StoredApiKey());
    }

    [Fact]
    public void On_windows_the_key_is_locked_to_the_windows_account()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        StoreApiKeyWithPlaintextKey();

        using var locked = Services(b => b.LockKeysToUser());
        var settings = Settings(locked);
        KeyLock.RemovePlaintextKeys(locked, paths.Keys, settings.ReprotectApiKey);

        var files = Directory.GetFiles(paths.Keys);
        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.Contains("DpapiXmlDecryptor", File.ReadAllText(f)));
        Assert.Equal("sk-ant-geheim", settings.StoredApiKey());
    }
}
