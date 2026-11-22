namespace ArtGrader.Web.Services;

public static class StoppedPage
{
    public const string Html = """
        <!doctype html>
        <html lang="de-CH">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>ArtGrader beendet</title>
          <link rel="icon" href="/favicon.svg" type="image/svg+xml" />
          <style>
            body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #fff; color: #141414;
                   font-family: "DM Sans", "Segoe UI", -apple-system, BlinkMacSystemFont, "Helvetica Neue", Arial, sans-serif; }
            main { max-width: 34rem; padding: 32px 16px; text-align: center; }
            h1 { font-size: 2.2rem; font-weight: 800; letter-spacing: -0.03em; margin: 16px 0 8px; }
            p { color: #55534f; line-height: 1.5; margin: 0 0 8px; }
            svg { width: 240px; max-width: 100%; height: auto; }
          </style>
        </head>
        <body>
          <main>
            <svg viewBox="0 0 240 110" aria-hidden="true">
              <path d="M4 70 C 40 70, 50 40, 86 46" fill="none" stroke="#141414" stroke-width="3" stroke-linecap="round" />
              <g transform="rotate(14 84 46) translate(-6 14)">
                <rect x="84" y="30" width="38" height="32" rx="6" fill="#ec3b2c" stroke="#141414" stroke-width="2.5" />
                <path d="M122 38 h14 M122 54 h14" stroke="#141414" stroke-width="4" stroke-linecap="round" />
              </g>
              <rect x="160" y="30" width="38" height="32" rx="6" fill="#1f4fe0" stroke="#141414" stroke-width="2.5" />
              <circle cx="172" cy="46" r="3.5" fill="#141414" />
              <circle cx="186" cy="46" r="3.5" fill="#141414" />
              <path d="M198 46 C 214 46, 220 76, 238 76" fill="none" stroke="#141414" stroke-width="3" stroke-linecap="round" />
            </svg>
            <h1>ArtGrader ist beendet</h1>
            <p>Alles ist gespeichert. Du kannst dieses Fenster schliessen.</p>
            <p>Zum Weiterarbeiten ArtGrader wieder starten.</p>
          </main>
        </body>
        </html>
        """;
}
