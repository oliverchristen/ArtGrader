#!/bin/bash
set -euo pipefail

source_dir="$(cd "$(dirname "$0")" && pwd)"
target="$HOME/Applications/ArtGrader"

if [[ "$(uname)" != "Darwin" ]]; then
    echo "Dieses Skript ist für macOS." >&2
    exit 1
fi

if [[ ! -f "$source_dir/ArtGrader" ]]; then
    echo "Im Ordner $source_dir fehlt die Datei «ArtGrader». Bitte das Skript im entpackten ArtGrader-Ordner starten." >&2
    exit 1
fi

if pgrep -x ArtGrader >/dev/null; then
    echo "ArtGrader läuft noch. Bitte das ArtGrader-Terminalfenster schliessen und das Skript erneut starten." >&2
    exit 1
fi

if [[ "$source_dir" != "$target" ]]; then
    echo "Installiere nach $target …"
    mkdir -p "$HOME/Applications"
    rm -rf "$target.new"
    cp -R "$source_dir" "$target.new"
    rm -rf "$target"
    mv "$target.new" "$target"
fi

cd "$target"
xattr -dr com.apple.quarantine . 2>/dev/null || true
chmod +x ArtGrader ArtGrader.command install.sh
codesign --force --sign - ArtGrader >/dev/null

echo
echo "ArtGrader ist installiert."
echo "Starten: im Finder den Ordner «Programme» in deinem Benutzerordner öffnen,"
echo "dann ArtGrader › ArtGrader.command doppelklicken."
echo "Deine Prüfungen und Einstellungen liegen unverändert in ~/Library/Application Support/ArtGrader."
open "$target"
