#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")" && pwd)"
artifacts="$root/artifacts"
runtimes=("${@:-osx-arm64 osx-x64 win-x64}")
mkdir -p "$artifacts"

for rid in ${runtimes[@]}; do
    out="$artifacts/ArtGrader-$rid"
    rm -rf "$out" "$out.zip"

    if [[ "$rid" == osx-* ]]; then
        app="$out/ArtGrader.app/Contents"
        dotnet publish "$root/src/ArtGrader.Web/ArtGrader.Web.csproj" -c Release -r "$rid" -o "$app/Resources/app"
        mkdir -p "$app/MacOS"
        cp "$root/build/mac/Info.plist" "$app/"
        cp "$root/build/mac/ArtGrader" "$app/MacOS/"
        cp "$root/build/install.sh" "$out/"
        chmod +x "$app/MacOS/ArtGrader" "$app/Resources/app/ArtGrader" "$out/install.sh"
    else
        dotnet publish "$root/src/ArtGrader.Web/ArtGrader.Web.csproj" -c Release -r "$rid" -o "$out"
    fi

    (cd "$artifacts" && if command -v ditto >/dev/null; then ditto -c -k --keepParent "ArtGrader-$rid" "ArtGrader-$rid.zip"; else zip -qry "ArtGrader-$rid.zip" "ArtGrader-$rid"; fi)
    echo "Created $out.zip"
done
