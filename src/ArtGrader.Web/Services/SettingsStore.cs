using System.Text.Json;
using System.Text.Json.Serialization;
using ArtGrader.Core.Ai;
using ArtGrader.Core.Localization;
using ArtGrader.Core.Storage;
using ArtGrader.Web.Services.Updates;
using Microsoft.AspNetCore.DataProtection;

namespace ArtGrader.Web.Services;

public sealed class SettingsStore : IAiSettingsSource, IUpdatePreferences
{
    private const string EnvironmentKey = "ANTHROPIC_API_KEY";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AppPaths paths;
    private readonly IDataProtector protector;
    private readonly Lock gate = new();
    private StoredSettings stored;

    public SettingsStore(AppPaths paths, IDataProtectionProvider protection)
    {
        this.paths = paths;
        protector = protection.CreateProtector("ArtGrader.ApiKey");
        stored = Load();
        ApplyLanguage(stored.Language);
    }

    public event Action? Changed;

    public bool KeyFromEnvironment => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentKey));

    public bool HasStoredKey => !string.IsNullOrEmpty(stored.ProtectedApiKey);

    public AiProvider Provider => stored.Provider;

    public bool IsConfigured =>
        stored.Provider == AiProvider.Ollama ? !string.IsNullOrWhiteSpace(stored.OllamaModel) : KeyFromEnvironment || HasStoredKey;

    public bool CanGrade(ArtGrader.Core.Domain.Exam exam) =>
        exam.LocalOnly ? !string.IsNullOrWhiteSpace(stored.OllamaModel) : IsConfigured;

    public bool RunsLocally(ArtGrader.Core.Domain.Exam exam) => stored.Provider == AiProvider.Ollama || exam.LocalOnly;

    public IReadOnlyList<string> RedactTerms => stored.RedactTerms;

    public AppLanguage Language => stored.Language;

    public void SaveLanguage(AppLanguage language)
    {
        lock (gate)
        {
            stored = stored with { Language = language };
            Persist();
        }
        ApplyLanguage(language);
        Changed?.Invoke();
    }

    public AppTheme Theme => stored.Theme;

    public void SaveTheme(AppTheme theme)
    {
        lock (gate)
        {
            stored = stored with { Theme = theme };
            Persist();
        }
        Changed?.Invoke();
    }

    /// <summary>Switches texts, AI output and date and number formats for the whole app.</summary>
    private static void ApplyLanguage(AppLanguage language)
    {
        ArtGrader.Core.Localization.Language.Current = language;
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = ArtGrader.Core.Localization.Language.Culture;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = ArtGrader.Core.Localization.Language.Culture;
    }

    public bool CheckAutomatically => stored.CheckForUpdates;

    public Version? SkippedVersion => Version.TryParse(stored.SkippedVersion, out var version) ? version : null;

    public void SaveCheckAutomatically(bool check)
    {
        lock (gate)
        {
            stored = stored with { CheckForUpdates = check };
            Persist();
        }
        Changed?.Invoke();
    }

    public void SkipVersion(Version version)
    {
        lock (gate)
        {
            stored = stored with { SkippedVersion = version.ToString(3) };
            Persist();
        }
        Changed?.Invoke();
    }

    public void SaveRedactTerms(IEnumerable<string> terms)
    {
        lock (gate)
        {
            stored = stored with
            {
                RedactTerms = terms.Select(t => t.Trim()).Where(t => t.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            };
            Persist();
        }
        Changed?.Invoke();
    }

    public string Model => stored.Model;

    public string Effort => stored.Effort;

    public string OllamaUrl => stored.OllamaUrl;

    public string? OllamaModel => stored.OllamaModel;

    public AiSettings Current()
    {
        lock (gate)
        {
            var key = Environment.GetEnvironmentVariable(EnvironmentKey);
            if (string.IsNullOrWhiteSpace(key))
            {
                key = StoredApiKey();
            }
            return new AiSettings(stored.Provider, key, stored.Model, stored.Effort, stored.OllamaUrl, stored.OllamaModel);
        }
    }

    public void Save(SettingsUpdate update)
    {
        lock (gate)
        {
            stored = stored with
            {
                Provider = update.Provider,
                Model = string.IsNullOrWhiteSpace(update.Model) ? AiSettings.DefaultModel : update.Model.Trim(),
                Effort = string.IsNullOrWhiteSpace(update.Effort) ? AiSettings.DefaultEffort : update.Effort.Trim(),
                OllamaUrl = string.IsNullOrWhiteSpace(update.OllamaUrl)
                    ? AiSettings.DefaultOllamaUrl
                    : OllamaGateway.IsThisComputer(update.OllamaUrl) ? update.OllamaUrl.Trim().TrimEnd('/') : stored.OllamaUrl,
                OllamaModel = string.IsNullOrWhiteSpace(update.OllamaModel) ? null : update.OllamaModel.Trim(),
                ProtectedApiKey = string.IsNullOrWhiteSpace(update.NewApiKey)
                    ? stored.ProtectedApiKey
                    : protector.Protect(update.NewApiKey.Trim()),
            };
            Persist();
        }
        Changed?.Invoke();
    }

    public string? StoredApiKey()
    {
        lock (gate)
        {
            if (!HasStoredKey)
            {
                return null;
            }
            try
            {
                return protector.Unprotect(stored.ProtectedApiKey!);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return null;
            }
        }
    }

    public void ReprotectApiKey()
    {
        lock (gate)
        {
            if (!HasStoredKey)
            {
                return;
            }
            stored = stored with { ProtectedApiKey = protector.Protect(protector.Unprotect(stored.ProtectedApiKey!)) };
            Persist();
        }
    }

    public void RemoveKey()
    {
        lock (gate)
        {
            stored = stored with { ProtectedApiKey = null };
            Persist();
        }
        Changed?.Invoke();
    }

    private StoredSettings Load()
    {
        if (!File.Exists(paths.SettingsFile))
        {
            return new StoredSettings();
        }
        try
        {
            return JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(paths.SettingsFile), JsonOptions) ?? new StoredSettings();
        }
        catch (JsonException)
        {
            return new StoredSettings();
        }
    }

    private void Persist()
    {
        var temp = paths.SettingsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(stored, JsonOptions));
        File.Move(temp, paths.SettingsFile, overwrite: true);
    }

    private sealed record StoredSettings
    {
        public AiProvider Provider { get; init; } = AiProvider.Anthropic;
        public string? ProtectedApiKey { get; init; }
        public string Model { get; init; } = AiSettings.DefaultModel;
        public string Effort { get; init; } = AiSettings.DefaultEffort;
        public string OllamaUrl { get; init; } = AiSettings.DefaultOllamaUrl;
        public string? OllamaModel { get; init; }
        public List<string> RedactTerms { get; init; } = [];
        public AppLanguage Language { get; init; } = AppLanguage.De;
        public AppTheme Theme { get; init; } = AppTheme.Atelier;
        public bool CheckForUpdates { get; init; } = true;
        public string? SkippedVersion { get; init; }
    }
}

public sealed record SettingsUpdate(
    AiProvider Provider,
    string? NewApiKey,
    string Model,
    string Effort,
    string OllamaUrl,
    string? OllamaModel);
