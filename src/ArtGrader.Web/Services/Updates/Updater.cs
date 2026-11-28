using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using ArtGrader.Core.Storage;

namespace ArtGrader.Web.Services.Updates;

public interface IUpdatePreferences
{
    bool CheckAtStart { get; }

    Version? SkippedVersion { get; }

    void SkipVersion(Version version);
}

public enum UpdateCheckOutcome
{
    UpToDate,
    Available,
    Failed,
}

public enum UpdateState
{
    Idle,
    Downloading,
    Installing,
    Failed,
}

public enum UpdateFailure
{
    None,
    Download,
    Damaged,
    NoWriteAccess,
}

/// <summary>The unpacked new version, ready to replace the files at <see cref="InstallPath"/>.</summary>
public sealed record PreparedUpdate(string NewContent, string InstallPath);

/// <summary>
/// Looks for a newer ArtGrader on GitHub, offers it, and installs it on request. The request to GitHub carries no data of
/// the user, only the ArtGrader version in the User-Agent header.
/// </summary>
public sealed class Updater(
    HttpClient http,
    IUpdatePreferences preferences,
    AppPaths paths,
    UpdateTarget target,
    ILogger<Updater> logger,
    IUpdateLauncher? launcher = null)
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(30);

    public event Action? Changed;

    public UpdateTarget Target => target;

    /// <summary>The release the banner offers. Null when there is none, or after Later or Skip.</summary>
    public AppRelease? Offer { get; private set; }

    public UpdateState State { get; private set; }

    public UpdateFailure Failure { get; private set; }

    /// <summary>Download progress from 0 to 100.</summary>
    public int Percent { get; private set; }

    public bool IsBusy => State is UpdateState.Downloading or UpdateState.Installing;

    public async Task CheckOnStartAsync(CancellationToken cancellationToken)
    {
        if (preferences.CheckAtStart)
        {
            await CheckAsync(manual: false, cancellationToken);
        }
    }

    /// <summary>A manual check also offers a version that the user skipped before.</summary>
    public async Task<UpdateCheckOutcome> CheckAsync(bool manual, CancellationToken cancellationToken)
    {
        if (target.RuntimeId is null || IsBusy)
        {
            return UpdateCheckOutcome.UpToDate;
        }
        AppRelease? newest;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CheckTimeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleaseFeed.ReleasesUrl);
            request.Headers.UserAgent.Add(UserAgent());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await http.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            newest = ReleaseFeed.Newest(await response.Content.ReadAsStringAsync(timeout.Token), target.Current, target.RuntimeId);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogInformation("Could not check for a new ArtGrader version: {Reason}", ex.Message);
            return UpdateCheckOutcome.Failed;
        }

        var skipped = !manual && newest is not null && preferences.SkippedVersion is { } version && newest.Version <= version;
        Offer = skipped ? null : newest;
        if (Offer is not null)
        {
            State = UpdateState.Idle;
            Failure = UpdateFailure.None;
            logger.LogInformation("ArtGrader {Version} is available.", Offer.Version.ToString(3));
        }
        Changed?.Invoke();
        return Offer is null ? UpdateCheckOutcome.UpToDate : UpdateCheckOutcome.Available;
    }

    public void Later()
    {
        if (IsBusy)
        {
            return;
        }
        Offer = null;
        Changed?.Invoke();
    }

    public void Skip()
    {
        if (Offer is null || IsBusy)
        {
            return;
        }
        preferences.SkipVersion(Offer.Version);
        Offer = null;
        Changed?.Invoke();
    }

    /// <summary>Downloads, checks and unpacks the offered release, then lets the launcher replace ArtGrader and restart it.</summary>
    public async Task InstallAsync(CancellationToken cancellationToken)
    {
        if (Offer is not { } release || !target.CanInstall || launcher is null || IsBusy)
        {
            return;
        }
        SetState(UpdateState.Downloading, UpdateFailure.None);
        try
        {
            if (!CanWriteTo(target.InstallPath!))
            {
                SetState(UpdateState.Failed, UpdateFailure.NoWriteAccess);
                return;
            }
            var prepared = await PrepareAsync(release, cancellationToken);
            SetState(UpdateState.Installing, UpdateFailure.None);
            logger.LogInformation("Installing ArtGrader {Version} and restarting.", release.Version.ToString(3));
            launcher.Launch(prepared);
        }
        catch (InvalidDataException ex)
        {
            logger.LogWarning(ex, "The downloaded ArtGrader {Version} is damaged.", release.Version.ToString(3));
            SetState(UpdateState.Failed, UpdateFailure.Damaged);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not install ArtGrader {Version}.", release.Version.ToString(3));
            SetState(UpdateState.Failed, UpdateFailure.Download);
        }
    }

    public async Task<PreparedUpdate> PrepareAsync(AppRelease release, CancellationToken cancellationToken)
    {
        var installPath = target.InstallPath ?? throw new InvalidOperationException("This ArtGrader cannot replace itself.");
        var work = Path.Combine(paths.Updates, release.Version.ToString(3));
        if (Directory.Exists(work))
        {
            Directory.Delete(work, recursive: true);
        }
        Directory.CreateDirectory(work);
        try
        {
            var package = Path.Combine(work, release.Asset.Name);
            await DownloadAsync(release.Asset, package, cancellationToken);
            var content = Path.Combine(work, "content");
            await ZipFile.ExtractToDirectoryAsync(package, content, cancellationToken);
            File.Delete(package);
            var newContent = FindContent(content) ?? throw new InvalidDataException("The package does not contain ArtGrader.");
            return new PreparedUpdate(newContent, installPath);
        }
        catch
        {
            TryDelete(work);
            throw;
        }
    }

    /// <summary>Removes downloads of earlier updates.</summary>
    public void CleanUp() => TryDelete(paths.Updates);

    private ProductInfoHeaderValue UserAgent() => new("ArtGrader", target.Current.ToString(3));

    private async Task DownloadAsync(ReleaseAsset asset, string file, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DownloadTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
        request.Headers.UserAgent.Add(UserAgent());
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using (var source = await response.Content.ReadAsStreamAsync(timeout.Token))
        await using (var output = File.Create(file))
        {
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
            {
                total += read;
                if (total > asset.Size)
                {
                    throw new InvalidDataException("The package is larger than announced.");
                }
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                ReportProgress(total, asset.Size);
            }
            if (total != asset.Size)
            {
                throw new InvalidDataException("The package is smaller than announced.");
            }
        }
        if (Convert.ToHexStringLower(hash.GetHashAndReset()) != asset.Sha256)
        {
            throw new InvalidDataException("The checksum of the package does not match.");
        }
    }

    private string? FindContent(string folder)
    {
        if (target.RuntimeId?.StartsWith("osx", StringComparison.Ordinal) == true)
        {
            return Directory.EnumerateDirectories(folder, "ArtGrader.app", SearchOption.AllDirectories).FirstOrDefault();
        }
        var exe = Directory.EnumerateFiles(folder, "ArtGrader.exe", SearchOption.AllDirectories).FirstOrDefault();
        return exe is null ? null : Path.GetDirectoryName(exe);
    }

    /// <summary>On Windows the files in the folder are replaced; on the Mac the app bundle in its parent folder.</summary>
    private static bool CanWriteTo(string installPath)
    {
        var folder = OperatingSystem.IsMacOS() ? Path.GetDirectoryName(installPath)! : installPath;
        var probe = Path.Combine(folder, $".artgrader-write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void ReportProgress(long done, long total)
    {
        var percent = total <= 0 ? 0 : (int)(done * 100 / total);
        if (percent != Percent)
        {
            Percent = percent;
            Changed?.Invoke();
        }
    }

    private void SetState(UpdateState state, UpdateFailure failure)
    {
        State = state;
        Failure = failure;
        Percent = 0;
        Changed?.Invoke();
    }

    private void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Could not delete {Folder}.", folder);
        }
    }
}
