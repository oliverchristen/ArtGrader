using ArtGrader.Web.Services;
using Microsoft.Extensions.Logging;

namespace ArtGrader.Web.Tests;

public class LifetimeTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);

    [Theory]
    [InlineData(0, false, 31, true)]
    [InlineData(0, false, 29, false)]
    [InlineData(1, false, 120, false)]
    [InlineData(0, true, 120, false)]
    public void ArtGrader_stops_only_when_no_tab_is_open_and_nothing_runs(int tabs, bool busy, int minutes, bool expected)
    {
        Assert.Equal(expected, IdleShutdown.ShouldStop(tabs, busy, Start, Start.AddMinutes(minutes), Idle));
    }

    [Fact]
    public void Idle_stop_can_be_switched_off()
    {
        Assert.False(IdleShutdown.ShouldStop(0, false, Start, Start.AddDays(1), TimeSpan.Zero));
    }

    [Fact]
    public void Closing_more_tabs_than_were_opened_never_goes_below_zero()
    {
        var tabs = new ActiveTabs(TimeProvider.System);
        tabs.Opened();
        tabs.Closed();
        tabs.Closed();

        Assert.Equal(0, tabs.Count);
    }

    [Fact]
    public void Log_messages_and_errors_go_to_a_daily_file()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"artgrader-logs-{Guid.NewGuid():N}");
        try
        {
            using (var provider = new FileLoggerProvider(folder))
            {
                var logger = provider.CreateLogger("ArtGrader.Test");
                logger.LogWarning(new InvalidOperationException("kaputt"), "Bewertung von {Id} fehlgeschlagen", 7);
            }

            var text = File.ReadAllText(Assert.Single(Directory.GetFiles(folder, "artgrader-*.log")));
            Assert.Contains("[Warning] ArtGrader.Test: Bewertung von 7 fehlgeschlagen", text);
            Assert.Contains("InvalidOperationException: kaputt", text);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
