using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Options;

namespace ArtGrader.Web.Services;

public static class KeyLock
{
    public static IDataProtectionBuilder LockKeysToUser(this IDataProtectionBuilder builder)
    {
        if (OperatingSystem.IsWindows())
        {
            builder.ProtectKeysWithDpapi();
        }
        else if (OperatingSystem.IsMacOS())
        {
            builder.Services.AddSingleton<IUserSecretStore, MacKeychainSecretStore>();
            builder.LockKeysWith();
        }
        return builder;
    }

    public static IDataProtectionBuilder LockKeysWith(this IDataProtectionBuilder builder)
    {
        builder.Services.AddOptions<KeyManagementOptions>()
            .Configure<IUserSecretStore>((options, store) => options.XmlEncryptor = new UserSecretXmlEncryptor(store));
        return builder;
    }

    public static int RemovePlaintextKeys(IServiceProvider services, string keysFolder, Action reprotectSecrets)
    {
        var options = services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        if (options.XmlEncryptor is null || !Directory.Exists(keysFolder))
        {
            return 0;
        }
        var plaintext = Directory.GetFiles(keysFolder, "key-*.xml").Where(IsPlaintext).ToList();
        if (plaintext.Count == 0)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        services.GetRequiredService<IKeyManager>().CreateNewKey(now, now.AddDays(90));
        reprotectSecrets();
        foreach (var file in plaintext)
        {
            File.Delete(file);
        }
        return plaintext.Count;
    }

    public static bool IsPlaintext(string keyFile) =>
        XDocument.Load(keyFile).Descendants("masterKey").Any();
}

public interface IUserSecretStore
{
    byte[] GetOrCreate();
}

public sealed class UserSecretXmlEncryptor(IUserSecretStore store) : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        var plain = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var cipher = new byte[plain.Length];
        using (var aes = new AesGcm(store.GetOrCreate(), tag.Length))
        {
            aes.Encrypt(nonce, plain, cipher, tag);
        }
        var element = new XElement("userLockedKey", new XElement("value", Convert.ToBase64String([.. nonce, .. tag, .. cipher])));
        return new EncryptedXmlInfo(element, typeof(UserSecretXmlDecryptor));
    }
}

public sealed class UserSecretXmlDecryptor(IServiceProvider services) : IXmlDecryptor
{
    public XElement Decrypt(XElement encryptedElement)
    {
        var data = Convert.FromBase64String((string?)encryptedElement.Element("value") ?? "");
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;
        var plain = new byte[data.Length - nonceSize - tagSize];
        using (var aes = new AesGcm(services.GetRequiredService<IUserSecretStore>().GetOrCreate(), tagSize))
        {
            aes.Decrypt(data.AsSpan(0, nonceSize), data.AsSpan(nonceSize + tagSize), data.AsSpan(nonceSize, tagSize), plain);
        }
        return XElement.Parse(Encoding.UTF8.GetString(plain));
    }
}

public sealed class MacKeychainSecretStore : IUserSecretStore
{
    private const string Service = "ArtGrader";
    private const string Account = "data-protection";
    private readonly Lazy<byte[]> secret = new(Load);

    public byte[] GetOrCreate() => secret.Value;

    private static byte[] Load()
    {
        var (found, value) = Security("find-generic-password", "-s", Service, "-a", Account, "-w");
        if (found)
        {
            return Convert.FromBase64String(value.Trim());
        }
        var created = RandomNumberGenerator.GetBytes(32);
        var (added, error) = Security("add-generic-password", "-s", Service, "-a", Account, "-U", "-w", Convert.ToBase64String(created));
        if (!added)
        {
            throw new InvalidOperationException("Der Schlüssel konnte nicht im macOS-Schlüsselbund gespeichert werden: " + error);
        }
        return created;
    }

    private static (bool Ok, string Output) Security(params string[] arguments)
    {
        var start = new ProcessStartInfo("/usr/bin/security")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode == 0, process.ExitCode == 0 ? output : error);
    }
}
