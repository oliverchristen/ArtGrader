using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ArtGrader.Web.Services.Updates;

public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size, string Sha256);

public sealed record AppRelease(Version Version, string Tag, string PageUrl, ReleaseAsset Asset);

/// <summary>Reads the releases of ArtGrader on GitHub and selects the package for this computer.</summary>
public static partial class ReleaseFeed
{
    public const string Repository = "oliverchristen/ArtGrader";
    public const string ReleasesUrl = $"https://api.github.com/repos/{Repository}/releases?per_page=30";
    private const string DownloadPrefix = $"https://github.com/{Repository}/releases/download/";

    public static string AssetName(string runtimeId) => $"ArtGrader-{runtimeId}.zip";

    /// <summary>The version of a tag such as "v0.2" or "0.3.1", with missing parts set to 0. Null for other tags.</summary>
    public static Version? ParseTag(string tag)
    {
        var match = TagPattern().Match(tag.Trim());
        if (!match.Success)
        {
            return null;
        }
        var parts = match.Groups[1].Value.Split('.').Select(int.Parse).Concat([0, 0]).Take(3).ToArray();
        return new Version(parts[0], parts[1], parts[2]);
    }

    public static Version Normalize(Version version) =>
        new(version.Major, Math.Max(version.Minor, 0), Math.Max(version.Build, 0));

    /// <summary>
    /// The newest release that is newer than <paramref name="current"/> and has a package for this platform. Pre-releases
    /// count, because all releases of ArtGrader so far are pre-releases. A package must come from the release downloads of
    /// the repository and have a SHA-256 checksum, so that the download can be checked.
    /// </summary>
    public static AppRelease? Newest(string json, Version current, string runtimeId)
    {
        var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json) ?? [];
        var name = AssetName(runtimeId);
        var currentVersion = Normalize(current);
        return releases
            .Where(r => !r.Draft && r.TagName is not null)
            .Select(r => ToRelease(r, name))
            .OfType<AppRelease>()
            .Where(r => r.Version > currentVersion)
            .MaxBy(r => r.Version);
    }

    private static AppRelease? ToRelease(GitHubRelease release, string assetName)
    {
        var version = ParseTag(release.TagName!);
        var asset = release.Assets?.FirstOrDefault(a => a.Name == assetName);
        if (version is null || asset?.DownloadUrl is null || asset.Digest is null)
        {
            return null;
        }
        if (!asset.DownloadUrl.StartsWith(DownloadPrefix + release.TagName + "/", StringComparison.Ordinal)
            || !Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out var url))
        {
            return null;
        }
        var digest = Sha256Pattern().Match(asset.Digest);
        if (!digest.Success)
        {
            return null;
        }
        return new AppRelease(
            version,
            release.TagName!,
            release.HtmlUrl ?? $"https://github.com/{Repository}/releases",
            new ReleaseAsset(assetName, url, asset.Size, digest.Groups[1].Value.ToLowerInvariant()));
    }

    [GeneratedRegex(@"^v?(\d+(?:\.\d+){0,2})$")]
    private static partial Regex TagPattern();

    [GeneratedRegex("^sha256:([0-9a-fA-F]{64})$")]
    private static partial Regex Sha256Pattern();

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("assets")] List<GitHubAsset>? Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("digest")] string? Digest,
        [property: JsonPropertyName("browser_download_url")] string? DownloadUrl);
}
