using Microsoft.AspNetCore.Components.Server.Circuits;

namespace ArtGrader.Web.Services;

public sealed class ActiveTabs(TimeProvider clock)
{
    private int count;

    public int Count => Volatile.Read(ref count);

    public DateTimeOffset LastChange { get; private set; } = clock.GetUtcNow();

    public void Opened()
    {
        Interlocked.Increment(ref count);
        LastChange = clock.GetUtcNow();
    }

    public void Closed()
    {
        if (Interlocked.Decrement(ref count) < 0)
        {
            Interlocked.Exchange(ref count, 0);
        }
        LastChange = clock.GetUtcNow();
    }
}

public sealed class TabCircuitHandler(ActiveTabs tabs) : CircuitHandler
{
    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        tabs.Opened();
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        tabs.Closed();
        return Task.CompletedTask;
    }
}

public sealed class IdleShutdown(
    ActiveTabs tabs,
    GradingQueue queue,
    ModelDownloads downloads,
    IHostApplicationLifetime lifetime,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<IdleShutdown> logger) : BackgroundService
{
    public static bool ShouldStop(int openTabs, bool busy, DateTimeOffset lastChange, DateTimeOffset now, TimeSpan idle) =>
        idle > TimeSpan.Zero && openTabs == 0 && !busy && now - lastChange >= idle;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var idle = TimeSpan.FromMinutes(configuration.GetValue("ArtGrader:IdleMinutes", 30));
        if (idle <= TimeSpan.Zero)
        {
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (ShouldStop(tabs.Count, queue.IsBusy || downloads.IsBusy, tabs.LastChange, clock.GetUtcNow(), idle))
            {
                logger.LogInformation("No ArtGrader window has been open for {Minutes} minutes. ArtGrader is shutting down.", idle.TotalMinutes);
                lifetime.StopApplication();
                return;
            }
        }
    }
}

public static class SingleInstance
{
    public const string PingPath = "/api/ping";
    public const string PingAnswer = "ArtGrader";

    public static async Task<bool> IsRunningAsync(string url)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            return await http.GetStringAsync(url.TrimEnd('/') + PingPath) == PingAnswer;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    /// <summary>Asks the running ArtGrader for a ticket to open the browser with. Null if it does not accept the key.</summary>
    public static async Task<string?> TicketAsync(string url, string key)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        using var request = new HttpRequestMessage(HttpMethod.Post, url.TrimEnd('/') + AccessGate.TicketPath);
        request.Headers.Add(AccessGate.HeaderName, key);
        try
        {
            using var response = await http.SendAsync(request);
            return response.IsSuccessStatusCode ? (await response.Content.ReadAsStringAsync()).Trim() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
