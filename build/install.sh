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

# ArtGrader stays in the home folder, where the automatic update can always replace it. Users who may write to
# /Applications (admins) also get a link there, so that it shows in the general Applications folder. A real
# ArtGrader.app that is already there stays untouched.
shared="/Applications/ArtGrader.app"
linked=false
if [[ -L "$shared" || ! -e "$shared" ]] && [[ -w /Applications ]]; then
    if ln -sfn "$target" "$shared" 2>/dev/null; then
        linked=true
    fi
fi

# The Dock entry points to the app in the home folder, so it stays valid after updates.
docked=false
dock_apps="$(defaults read com.apple.dock persistent-apps 2>/dev/null || true)"
if [[ "$dock_apps" == *"Applications/ArtGrader.app"* ]]; then
    docked=true
else
    url="file://$(printf '%s' "$target/" | sed 's/ /%20/g')"
    tile="<dict><key>tile-data</key><dict><key>file-data</key><dict><key>_CFURLString</key><string>$url</string><key>_CFURLStringType</key><integer>15</integer></dict></dict></dict>"
    if defaults write com.apple.dock persistent-apps -array-add "$tile" 2>/dev/null; then
        killall Dock 2>/dev/null || true
        docked=true
    fi
fi
# The automatic update adds the Dock entry only when this marker is missing, so a removed entry stays removed.
if [[ "$docked" == true ]]; then
    data="${ARTGRADER_DATA:-$HOME/Library/Application Support/ArtGrader}"
    mkdir -p "$data" && touch "$data/dock-entry" || true
fi

echo
echo "ArtGrader is installed and starting now."
if [[ "$linked" == true ]]; then
    echo "To start it later: click ArtGrader in the Dock, or double-click it in 'Applications' in the Finder."
else
    echo "To start it later: click ArtGrader in the Dock, or double-click it in 'Applications' in your home folder."
fi
if [[ "$docked" != true ]]; then
    echo "Tip: drag ArtGrader to the Dock."
fi
echo "To quit, use the button at the top right in ArtGrader."
echo "Your assignments and settings remain unchanged in ~/Library/Application Support/ArtGrader."
open "$target"
