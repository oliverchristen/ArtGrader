#!/bin/bash
set -euo pipefail

source_dir="$(cd "$(dirname "$0")" && pwd)"
target="$HOME/Applications/ArtGrader.app"
url="http://localhost:5080"

if [[ "$(uname)" != "Darwin" ]]; then
    echo "This script is for macOS." >&2
    exit 1
fi

if [[ ! -d "$source_dir/ArtGrader.app" ]]; then
    echo "'ArtGrader.app' is missing in $source_dir. Please run the script in the unzipped ArtGrader folder." >&2
    exit 1
fi

if curl -fs "$url/api/ping" 2>/dev/null | grep -q ArtGrader; then
    echo "ArtGrader is still running and will be stopped …"
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

echo "Installing to $target …"
mkdir -p "$HOME/Applications"
rm -rf "$target.new"
cp -R "$source_dir/ArtGrader.app" "$target.new"
rm -rf "$target" "$HOME/Applications/ArtGrader"
mv "$target.new" "$target"

xattr -dr com.apple.quarantine "$target" 2>/dev/null || true
chmod +x "$target/Contents/MacOS/ArtGrader" "$target/Contents/Resources/app/ArtGrader"
codesign --force --sign - "$target/Contents/Resources/app/ArtGrader" > /dev/null

echo
echo "ArtGrader is installed and starting now."
echo "To start it later: open 'Applications' in your home folder in the Finder and double-click ArtGrader."
echo "Tip: drag ArtGrader to the Dock. To quit, use the button at the top right in ArtGrader."
echo "Your assignments and settings remain unchanged in ~/Library/Application Support/ArtGrader."
open "$target"
