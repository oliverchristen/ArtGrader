namespace ArtGrader.Web.Services;

/// <summary>
/// Runs the last scheduled action once no new one was scheduled for <see cref="Delay"/>.
/// Used for auto-saving: every edit schedules a save, and only the last edit is saved.
/// Disposing runs a pending action at once, so nothing is lost when the page is left.
/// </summary>
public sealed class Debouncer(TimeSpan? delay = null) : IAsyncDisposable
{
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(1);

    private readonly Lock gate = new();
    private CancellationTokenSource? timer;
    private Func<Task>? pending;

    public TimeSpan Delay { get; } = delay ?? DefaultDelay;

    public bool IsPending
    {
        get
        {
            lock (gate)
            {
                return pending is not null;
            }
        }
    }

    public void Schedule(Func<Task> action)
    {
        CancellationTokenSource next;
        lock (gate)
        {
            timer?.Cancel();
            timer?.Dispose();
            timer = next = new CancellationTokenSource();
            pending = action;
        }
        _ = RunLaterAsync(next.Token);
    }

    /// <summary>Runs the pending action now, if there is one.</summary>
    public Task FlushAsync()
    {
        Func<Task>? action;
        lock (gate)
        {
            timer?.Cancel();
            action = pending;
            pending = null;
        }
        return action?.Invoke() ?? Task.CompletedTask;
    }

    private async Task RunLaterAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(Delay, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        await FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await FlushAsync();
        lock (gate)
        {
            timer?.Dispose();
            timer = null;
        }
    }
}
