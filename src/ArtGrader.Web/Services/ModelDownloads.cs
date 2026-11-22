using System.Collections.Concurrent;
using ArtGrader.Core.Ai;

namespace ArtGrader.Web.Services;

public sealed class ModelDownloads(OllamaGateway ollama, SettingsStore settings, ILogger<ModelDownloads> logger)
{
    private readonly ConcurrentDictionary<string, DownloadState> running = new();

    public event Action? Changed;

    public bool IsBusy => running.Values.Any(s => !s.Done && s.Error is null);

    public DownloadState? Get(string model) => running.TryGetValue(model, out var state) ? state : null;

    public void Start(string model)
    {
        var state = new DownloadState();
        if (!running.TryAdd(model, state))
        {
            return;
        }
        Changed?.Invoke();
        _ = RunAsync(model, state);
    }

    private async Task RunAsync(string model, DownloadState state)
    {
        var lastReport = DateTime.MinValue;
        var progress = new Progress<PullProgress>(p =>
        {
            state.Progress = p;
            if (DateTime.UtcNow - lastReport > TimeSpan.FromMilliseconds(500))
            {
                lastReport = DateTime.UtcNow;
                Changed?.Invoke();
            }
        });
        try
        {
            await ollama.PullAsync(settings.OllamaUrl, model, progress, CancellationToken.None);
            state.Done = true;
        }
        catch (ModelException ex)
        {
            logger.LogWarning(ex, "Download of {Model} failed", model);
            state.Error = ex.Message;
        }
        finally
        {
            Changed?.Invoke();
            if (state.Done || state.Error is not null)
            {
                await Task.Delay(TimeSpan.FromSeconds(state.Done ? 1 : 30));
                running.TryRemove(model, out _);
                Changed?.Invoke();
            }
        }
    }
}

public sealed class DownloadState
{
    public PullProgress? Progress { get; set; }
    public bool Done { get; set; }
    public string? Error { get; set; }
}
