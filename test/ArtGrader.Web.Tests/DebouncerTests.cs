using ArtGrader.Web.Services;

namespace ArtGrader.Web.Tests;

public class DebouncerTests
{
    [Fact]
    public async Task Only_the_last_of_several_quick_edits_is_saved()
    {
        var saved = new List<int>();
        var debouncer = new Debouncer(TimeSpan.FromMilliseconds(100));

        for (var i = 1; i <= 3; i++)
        {
            var value = i;
            debouncer.Schedule(() => { saved.Add(value); return Task.CompletedTask; });
            await Task.Delay(20);
        }
        Assert.Empty(saved);

        await Task.Delay(400);
        Assert.Equal([3], saved);
        Assert.False(debouncer.IsPending);
    }

    [Fact]
    public async Task Disposing_saves_a_pending_edit_at_once()
    {
        var saved = 0;
        var debouncer = new Debouncer(TimeSpan.FromMinutes(1));
        debouncer.Schedule(() => { saved++; return Task.CompletedTask; });

        await debouncer.DisposeAsync();
        await debouncer.FlushAsync();

        Assert.Equal(1, saved);
    }
}
