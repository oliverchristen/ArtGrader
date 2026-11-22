using ArtGrader.Core.Storage;
using ArtGrader.Web.Services;
using Microsoft.AspNetCore.Http;

namespace ArtGrader.Web.Tests;

public class AccessGateTests
{
    private const string Key = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static async Task<(HttpContext Context, bool Reached)> SendAsync(AccessGate gate, string method, string path, Action<HttpRequest>? setup = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Method = method;
        var query = path.IndexOf('?');
        context.Request.Path = query < 0 ? path : path[..query];
        context.Request.QueryString = query < 0 ? QueryString.Empty : new QueryString(path[query..]);
        setup?.Invoke(context.Request);
        var reached = false;
        await gate.InvokeAsync(context, _ =>
        {
            reached = true;
            return Task.CompletedTask;
        });
        return (context, reached);
    }

    private static void WithCookie(HttpRequest request, string value) => request.Headers.Cookie = $"{AccessGate.CookieName}={value}";

    [Theory]
    [InlineData("GET", "/")]
    [InlineData("GET", "/files/abc/arbeiten/1.jpg")]
    [InlineData("GET", "/export/5b0c7f0e-0000-0000-0000-000000000000/noten.xlsx")]
    [InlineData("GET", "/_blazor")]
    [InlineData("POST", "/beenden")]
    [InlineData("POST", "/api/ticket")]
    public async Task Without_the_key_nothing_is_served(string method, string path)
    {
        var (context, reached) = await SendAsync(new AccessGate(Key, new Clock()), method, path);

        Assert.False(reached);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task A_wrong_key_is_refused()
    {
        var gate = new AccessGate(Key, new Clock());

        var (_, withCookie) = await SendAsync(gate, "GET", "/", r => WithCookie(r, Key[..^1] + "0"));
        var (_, withHeader) = await SendAsync(gate, "POST", "/beenden", r => r.Headers[AccessGate.HeaderName] = "nope");

        Assert.False(withCookie);
        Assert.False(withHeader);
    }

    [Fact]
    public async Task The_ping_for_the_second_start_needs_no_key()
    {
        var (_, reached) = await SendAsync(new AccessGate(Key, new Clock()), "GET", SingleInstance.PingPath);

        Assert.True(reached);
    }

    [Fact]
    public async Task The_cookie_or_the_header_lets_a_request_through()
    {
        var gate = new AccessGate(Key, new Clock());

        var (_, withCookie) = await SendAsync(gate, "GET", "/files/abc/arbeiten/1.jpg", r => WithCookie(r, Key));
        var (_, withHeader) = await SendAsync(gate, "POST", "/beenden", r => r.Headers[AccessGate.HeaderName] = Key);

        Assert.True(withCookie);
        Assert.True(withHeader);
    }

    [Fact]
    public async Task A_ticket_is_exchanged_for_the_cookie_and_leaves_the_address()
    {
        var gate = new AccessGate(Key, new Clock());

        var (context, reached) = await SendAsync(gate, "GET", $"/?{AccessGate.TicketParameter}={gate.NewTicket()}");

        Assert.False(reached);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/", context.Response.Headers.Location.ToString());
        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{AccessGate.CookieName}={Key}", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_ticket_works_only_once()
    {
        var gate = new AccessGate(Key, new Clock());
        var ticket = gate.NewTicket();

        await SendAsync(gate, "GET", $"/?{AccessGate.TicketParameter}={ticket}");
        var (context, _) = await SendAsync(gate, "GET", $"/?{AccessGate.TicketParameter}={ticket}");

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Empty(context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task A_ticket_expires_after_a_minute()
    {
        var clock = new Clock();
        var gate = new AccessGate(Key, clock);
        var ticket = gate.NewTicket();
        clock.Now += TimeSpan.FromSeconds(61);

        var (context, _) = await SendAsync(gate, "GET", $"/?{AccessGate.TicketParameter}={ticket}");

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task A_made_up_ticket_or_a_ticket_in_a_post_gets_nothing()
    {
        var gate = new AccessGate(Key, new Clock());

        var (madeUp, _) = await SendAsync(gate, "GET", $"/?{AccessGate.TicketParameter}=ABCDEF");
        var (posted, reached) = await SendAsync(gate, "POST", $"/beenden?{AccessGate.TicketParameter}={gate.NewTicket()}");

        Assert.Equal(StatusCodes.Status403Forbidden, madeUp.Response.StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, posted.Response.StatusCode);
        Assert.False(reached);
    }

    [Fact]
    public async Task A_browser_without_the_key_is_told_to_open_the_app()
    {
        var (context, _) = await SendAsync(new AccessGate(Key, new Clock()), "GET", "/", r => r.Headers.Accept = "text/html,application/xhtml+xml");

        context.Response.Body.Position = 0;
        var html = await new StreamReader(context.Response.Body).ReadToEndAsync(TestContext.Current.CancellationToken);
        Assert.Contains("ArtGrader", html);
        Assert.StartsWith("text/html", context.Response.ContentType);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void The_key_is_created_once_and_then_read_again()
    {
        var folder = Path.Combine(Path.GetTempPath(), "artgrader-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(folder);

            var created = AccessGate.LoadOrCreateKey(paths.AccessKeyFile);

            Assert.Equal(64, created.Length);
            Assert.Equal(created, AccessGate.LoadOrCreateKey(paths.AccessKeyFile));
            Assert.NotEqual(created, AccessGate.LoadOrCreateKey(Path.Combine(folder, "other-key")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
