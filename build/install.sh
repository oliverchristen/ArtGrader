#!/bin/bash
set -euo pipefail

source_dir="$(cd "$(dirname "$0")" && pwd)"
target="$HOME/Applications/ArtGrader.app"
url="http://localhost:5080"

if [[ "$(uname)" != "Darwin" ]]; then
    echo "Dieses Skript ist für macOS." >&2
    exit 1
fi

if [[ ! -d "$source_dir/ArtGrader.app" ]]; then
    echo "Im Ordner $source_dir fehlt «ArtGrader.app». Bitte das Skript im entpackten ArtGrader-Ordner starten." >&2
    exit 1
fi

if curl -fs "$url/api/ping" 2>/dev/null | grep -q ArtGrader; then
    echo "ArtGrader läuft noch und wird beendet …"
    # ArtGrader only obeys someone who can read this account's access key. The key goes to curl
    # through stdin so that it never shows up in the process list.
    key="$(cat "${ARTGRADER_DATA:-$HOME/Library/Application Support/ArtGrader}/access-key" 2>/dev/null || true)"
    printf 'header = "X-ArtGrader-Key: %s"
' "$key" | curl -fs -X POST -K - "$url/beenden" > /dev/null 2>&1 || true
    for _ in $(seq 1 20); do
        curl -fs "$url/api/ping" > /dev/null 2>&1 || break
        sleep 0.5
    done
fi

echo "Installiere nach $target …"
mkdir -p "$HOME/Applications"
rm -rf "$target.new"
cp -R "$source_dir/ArtGrader.app" "$target.new"
rm -rf "$target" "$HOME/Applications/ArtGrader"
mv "$target.new" "$target"

xattr -dr com.apple.quarantine "$target" 2>/dev/null || true
chmod +x "$target/Contents/MacOS/ArtGrader" "$target/Contents/Resources/app/ArtGrader"
codesign --force --sign - "$target/Contents/Resources/app/ArtGrader" > /dev/null

echo
echo "ArtGrader ist installiert und startet jetzt."
echo "Später starten: im Finder «Programme» im Benutzerordner öffnen und ArtGrader doppelklicken."
echo "Tipp: ArtGrader ins Dock ziehen. Beenden geht in ArtGrader oben rechts."
echo "Deine Prüfungen und Einstellungen liegen unverändert in ~/Library/Application Support/ArtGrader."
open "$target"
