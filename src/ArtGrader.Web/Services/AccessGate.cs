using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ArtGrader.Core.Localization;
using static ArtGrader.Core.Localization.Texts;

namespace ArtGrader.Web.Services;

/// <summary>
/// Keeps other user accounts and other websites away from the local port. ArtGrader has no login; instead
/// only a browser that was opened by ArtGrader itself gets in:
/// the access key lives in the data folder, which only this user account can read. Starting ArtGrader opens the
/// browser with a ticket that works once and for a minute; the ticket is exchanged for a cookie holding the key.
/// Programs of the same user (a second start, install.sh) send the key in a header.
/// </summary>
public sealed class AccessGate(string key, TimeProvider clock)
{
    public const string CookieName = "artgrader_key";
    public const string HeaderName = "X-ArtGrader-Key";
    public const string TicketParameter = "ticket";
    public const string TicketPath = "/api/ticket";

    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(180);

    private readonly byte[] key = Encoding.UTF8.GetBytes(key);
    private readonly ConcurrentDictionary<string, DateTimeOffset> tickets = new();

    /// <summary>Reads the access key of this user account, or creates it on the first start.</summary>
    public static string LoadOrCreateKey(string file)
    {
        if (File.Exists(file))
        {
            var existing = File.ReadAllText(file).Trim();
            if (existing.Length >= 32)
            {
                return existing;
            }
        }
        var created = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }
        using (var writer = new StreamWriter(file, Encoding.ASCII, options))
        {
            writer.Write(created);
        }
        return created;
    }

    public static string Link(string url, string ticket) => $"{url.TrimEnd('/')}/?{TicketParameter}={ticket}";

    public bool HasKey(string? candidate) =>
        !string.IsNullOrEmpty(candidate) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), key);

    public string NewTicket(TimeSpan? lifetime = null)
    {
        var now = clock.GetUtcNow();
        foreach (var expired in tickets.Where(t => t.Value <= now))
        {
            tickets.TryRemove(expired.Key, out _);
        }
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        tickets[ticket] = now + (lifetime ?? TicketLifetime);
        return ticket;
    }

    public bool Redeem(string? ticket) =>
        !string.IsNullOrEmpty(ticket) && tickets.TryRemove(ticket, out var expires) && expires > clock.GetUtcNow();

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;
        if (request.Path.Equals(SingleInstance.PingPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var allowed = HasKey(request.Cookies[CookieName]) || HasKey(request.Headers[HeaderName]);
        if (HttpMethods.IsGet(request.Method) && request.Query.TryGetValue(TicketParameter, out var ticket) && (allowed || Redeem(ticket)))
        {
            if (!allowed)
            {
                context.Response.Cookies.Append(CookieName, Encoding.UTF8.GetString(key), new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    MaxAge = CookieLifetime,
                    IsEssential = true,
                });
            }
            // Leave the ticket out of the address bar and the history.
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Redirect("/");
            return;
        }
        if (allowed)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.Headers.CacheControl = "no-store";
        if (HttpMethods.IsGet(request.Method) && request.GetTypedHeaders().Accept.Any(a => a.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(LockedPage());
        }
    }

    /// <summary>The page a browser sees that ArtGrader did not open itself. It loads nothing from the app.</summary>
    public static string LockedPage() => $$"""
        <!doctype html>
        <html lang="{{Language.HtmlLang}}">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>ArtGrader</title>
          <style>
            body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #fff; color: #141414;
                   font-family: "DM Sans", "Segoe UI", -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif; }
            main { max-width: 34rem; padding: 32px 16px; text-align: center; }
            h1 { font-size: 2.2rem; font-weight: 800; letter-spacing: -0.03em; margin: 16px 0 8px; }
            p { color: #55534f; line-height: 1.5; margin: 0 0 8px; }
          </style>
        </head>
        <body>
          <main>
            <h1>{{T("Please open ArtGrader again")}}</h1>
            <p>{{T("So that nobody else can get to your exams, ArtGrader only opens through the app itself.")}}</p>
            <p>{{T("Double-click the ArtGrader icon. The browser then opens by itself.")}}</p>
          </main>
        </body>
        </html>
        """;
}
