using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArtGrader.Core.Storage;
using ArtGrader.Web.Services.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace ArtGrader.Web.Tests;

public sealed class UpdateTests : IDisposable
{
    private const string Download = "https://github.com/oliverchristen/ArtGrader/releases/download/";
    private static readonly Version Current = new(0, 2, 0);

    private readonly string folder = Path.Combine(Path.GetTempPath(), "artgrader-update-" + Guid.NewGuid().ToString("N"));
    private readonly FakePreferences preferences = new();

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("v0.2", "0.2.0")]
    [InlineData("v0.10.1", "0.10.1")]
    [InlineData("0.3", "0.3.0")]
    [InlineData("v1", "1.0.0")]
    [InlineData("latest", null)]
    [InlineData("v0.3-beta", null)]
    public void The_version_is_read_from_the_release_tag(string tag, string? expected)
    {
        Assert.Equal(expected is null ? null : Version.Parse(expected), ReleaseFeed.ParseTag(tag));
    }

    [Fact]
    public void The_newest_newer_release_with_a_package_for_this_platform_is_selected()
    {
        var json = Releases(
            Release("v0.5", draft: true),
            Release("v0.4", assets: ["ArtGrader-osx-arm64.zip"]),
            Release("v0.3.1", prerelease: true),
            Release("v0.3"),
            Release("v0.2"),
            Release("v0.1"));

        var newest = ReleaseFeed.Newest(json, Current, "win-x64");

        Assert.NotNull(newest);
        Assert.Equal(new Version(0, 3, 1), newest.Version);
        Assert.Equal("ArtGrader-win-x64.zip", newest.Asset.Name);
        Assert.Equal(new Uri(Download + "v0.3.1/ArtGrader-win-x64.zip"), newest.Asset.DownloadUrl);
    }

    [Theory]
    [InlineData("win-x64")]
    [InlineData("osx-arm64")]
    [InlineData("osx-x64")]
    public void Each_platform_gets_its_own_package(string runtimeId)
    {
        var newest = ReleaseFeed.Newest(Releases(Release("v0.3")), Current, runtimeId);

        Assert.Equal($"ArtGrader-{runtimeId}.zip", newest?.Asset.Name);
    }

    [Fact]
    public void There_is_no_update_when_this_is_the_newest_version()
    {
        Assert.Null(ReleaseFeed.Newest(Releases(Release("v0.2"), Release("v0.1")), Current, "win-x64"));
    }

    [Fact]
    public void A_package_without_a_checksum_or_from_another_place_is_ignored()
    {
        var json = Releases(
            Release("v0.4", digest: false),
            Release("v0.3", downloadBase: "https://example.com/"));

        Assert.Null(ReleaseFeed.Newest(json, Current, "win-x64"));
    }

    [Theory]
    [InlineData(true, false, Architecture.X64, "win-x64")]
    [InlineData(false, true, Architecture.Arm64, "osx-arm64")]
    [InlineData(false, true, Architecture.X64, "osx-x64")]
    [InlineData(true, false, Architecture.Arm64, null)]
    [InlineData(false, false, Architecture.X64, null)]
    public void The_package_matches_the_operating_system_and_processor(bool windows, bool mac, Architecture architecture, string? expected)
    {
        Assert.Equal(expected, UpdateTarget.RuntimeIdFor(windows, mac, architecture));
    }

    [Fact]
    public void On_the_mac_the_whole_app_bundle_is_replaced()
    {
        var bundle = Path.Combine(folder, "Applications", "ArtGrader.app");
        var app = Path.Combine(bundle, "Contents", "Resources", "app") + Path.DirectorySeparatorChar;

        Assert.Equal(bundle, UpdateTarget.AppBundleOf(app));
        Assert.Null(UpdateTarget.AppBundleOf(Path.Combine(folder, "bin", "Debug") + Path.DirectorySeparatorChar));
    }

    [Fact]
    public async Task A_newer_version_is_offered()
    {
        var updater = Updater(_ => Json(Releases(Release("v0.3"))));

        var outcome = await updater.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCheckOutcome.Available, outcome);
        Assert.Equal(new Version(0, 3, 0), updater.Offer?.Version);
    }

    [Fact]
    public async Task A_skipped_version_is_offered_again_only_on_request()
    {
        preferences.SkippedVersion = new Version(0, 3, 0);
        var updater = Updater(_ => Json(Releases(Release("v0.3"))));

        Assert.Equal(UpdateCheckOutcome.UpToDate, await updater.CheckAsync(manual: false, TestContext.Current.CancellationToken));
        Assert.Null(updater.Offer);

        Assert.Equal(UpdateCheckOutcome.Available, await updater.CheckAsync(manual: true, TestContext.Current.CancellationToken));
        Assert.NotNull(updater.Offer);
    }

    [Fact]
    public async Task A_version_newer_than_the_skipped_one_is_offered()
    {
        preferences.SkippedVersion = new Version(0, 3, 0);
        var updater = Updater(_ => Json(Releases(Release("v0.4"), Release("v0.3"))));

        Assert.Equal(UpdateCheckOutcome.Available, await updater.CheckAsync(manual: false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Skipping_remembers_the_version_and_hides_the_offer()
    {
        var updater = Updater(_ => Json(Releases(Release("v0.3"))));
        await updater.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        updater.Skip();

        Assert.Null(updater.Offer);
        Assert.Equal(new Version(0, 3, 0), preferences.SkippedVersion);
    }

    [Fact]
    public async Task Later_hides_the_offer_without_skipping_the_version()
    {
        var updater = Updater(_ => Json(Releases(Release("v0.3"))));
        await updater.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        updater.Later();

        Assert.Null(updater.Offer);
        Assert.Null(preferences.SkippedVersion);
    }

    [Fact]
    public async Task After_later_the_same_version_is_offered_again_only_on_request()
    {
        var updater = Updater(_ => Json(Releases(Release("v0.3"))));
        await updater.CheckAsync(manual: false, TestContext.Current.CancellationToken);
        updater.Later();

        Assert.Equal(UpdateCheckOutcome.UpToDate, await updater.CheckAsync(manual: false, TestContext.Current.CancellationToken));
        Assert.Null(updater.Offer);

        Assert.Equal(UpdateCheckOutcome.Available, await updater.CheckAsync(manual: true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task After_later_a_newer_version_is_offered()
    {
        var releases = Releases(Release("v0.3"));
        var updater = Updater(_ => Json(releases));
        await updater.CheckAsync(manual: false, TestContext.Current.CancellationToken);
        updater.Later();

        releases = Releases(Release("v0.4"), Release("v0.3"));

        Assert.Equal(UpdateCheckOutcome.Available, await updater.CheckAsync(manual: false, TestContext.Current.CancellationToken));
        Assert.Equal(new Version(0, 4, 0), updater.Offer?.Version);
    }

    [Fact]
    public async Task While_ArtGrader_runs_it_checks_at_start_and_then_every_hour()
    {
        var requests = 0;
        var clock = new FakeTimeProvider();
        using var check = new PeriodicUpdateCheck(Updater(_ =>
        {
            Interlocked.Increment(ref requests);
            return Json(Releases(Release("v0.2")));
        }), clock);

        await check.StartAsync(TestContext.Current.CancellationToken);
        await Eventually(() => Volatile.Read(ref requests) == 1);

        clock.Advance(TimeSpan.FromMinutes(59));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref requests));

        clock.Advance(TimeSpan.FromMinutes(1));
        await Eventually(() => Volatile.Read(ref requests) == 2);

        clock.Advance(TimeSpan.FromHours(1));
        await Eventually(() => Volatile.Read(ref requests) == 3);
        await check.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task The_hourly_check_follows_the_setting()
    {
        preferences.CheckAutomatically = false;
        var requests = 0;
        var clock = new FakeTimeProvider();
        using var check = new PeriodicUpdateCheck(Updater(_ =>
        {
            Interlocked.Increment(ref requests);
            return Json(Releases(Release("v0.2")));
        }), clock);
        await check.StartAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromHours(1));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(0, Volatile.Read(ref requests));

        preferences.CheckAutomatically = true;
        clock.Advance(TimeSpan.FromHours(1));
        await Eventually(() => Volatile.Read(ref requests) == 1);
        await check.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        Assert.True(condition());
    }

    [Fact]
    public async Task The_check_at_start_can_be_switched_off()
    {
        preferences.CheckAutomatically = false;
        var requests = 0;
        var updater = Updater(_ =>
        {
            requests++;
            return Json(Releases(Release("v0.3")));
        });

        await updater.CheckAutomaticallyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, requests);
        Assert.Null(updater.Offer);
    }

    [Fact]
    public async Task Without_internet_the_check_fails_quietly()
    {
        var updater = Updater(_ => throw new HttpRequestException("offline"));

        Assert.Equal(UpdateCheckOutcome.Failed, await updater.CheckAsync(manual: true, TestContext.Current.CancellationToken));
        Assert.Null(updater.Offer);
    }

    [Fact]
    public async Task The_checked_package_is_unpacked_for_installation()
    {
        var package = Package();
        var updater = Updater(request => request.RequestUri!.AbsolutePath.EndsWith(".zip", StringComparison.Ordinal)
            ? Bytes(package)
            : Json(Releases(Release("v0.3", sha256: Sha256(package), size: package.Length))));
        await updater.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        var prepared = await updater.PrepareAsync(updater.Offer!, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(prepared.NewContent, "ArtGrader.exe")));
        Assert.Equal(Path.Combine(folder, "install"), prepared.InstallPath);
    }

    [Fact]
    public async Task A_package_with_a_wrong_checksum_is_not_installed()
    {
        var package = Package();
        var updater = Updater(request => request.RequestUri!.AbsolutePath.EndsWith(".zip", StringComparison.Ordinal)
            ? Bytes(package)
            : Json(Releases(Release("v0.3", sha256: new string('0', 64), size: package.Length))));
        await updater.CheckAsync(manual: false, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidDataException>(() => updater.PrepareAsync(updater.Offer!, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.EnumerateFiles(new AppPaths(Path.Combine(folder, "data")).Updates, "ArtGrader.exe", SearchOption.AllDirectories));
    }

    private Updater Updater(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(
            new HttpClient(new FakeHandler(respond)),
            preferences,
            new AppPaths(Path.Combine(folder, "data")),
            new UpdateTarget(Current, "win-x64", Path.Combine(folder, "install")),
            NullLogger<Updater>.Instance);

    private static byte[] Package()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("ArtGrader.exe").Open()))
            {
                writer.Write("new version");
            }
            using var asset = new StreamWriter(zip.CreateEntry("wwwroot/app.css").Open());
            asset.Write("body {}");
        }
        return stream.ToArray();
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string Releases(params object[] releases) => JsonSerializer.Serialize(releases);

    private static object Release(
        string tag,
        bool draft = false,
        bool prerelease = false,
        string[]? assets = null,
        bool digest = true,
        string downloadBase = Download,
        string? sha256 = null,
        long size = 1000) =>
        new
        {
            tag_name = tag,
            draft,
            prerelease,
            html_url = $"https://github.com/oliverchristen/ArtGrader/releases/tag/{tag}",
            assets = (assets ?? ["ArtGrader-win-x64.zip", "ArtGrader-osx-arm64.zip", "ArtGrader-osx-x64.zip"]).Select(name => new
            {
                name,
                size,
                digest = digest ? "sha256:" + (sha256 ?? new string('a', 64)) : null,
                browser_download_url = $"{downloadBase}{tag}/{name}",
            }),
        };

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Bytes(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class FakePreferences : IUpdatePreferences
    {
        public bool CheckAutomatically { get; set; } = true;

        public Version? SkippedVersion { get; set; }

        public void SkipVersion(Version version) => SkippedVersion = version;
    }
}
