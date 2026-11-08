#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")" && pwd)"
artifacts="$root/artifacts"
runtimes=("${@:-osx-arm64 osx-x64 win-x64}")
mkdir -p "$artifacts"

for rid in ${runtimes[@]}; do
    out="$artifacts/ArtGrader-$rid"
    rm -rf "$out" "$out.zip"
    dotnet publish "$root/src/ArtGrader.Web/ArtGrader.Web.csproj" -c Release -r "$rid" -o "$out"

    if [[ "$rid" == osx-* ]]; then
        cp "$root/build/ArtGrader.command" "$out/"
        chmod +x "$out/ArtGrader" "$out/ArtGrader.command"
    fi

    (cd "$artifacts" && if command -v ditto >/dev/null; then ditto -c -k --keepParent "ArtGrader-$rid" "ArtGrader-$rid.zip"; else zip -qry "ArtGrader-$rid.zip" "ArtGrader-$rid"; fi)
    echo "Created $out.zip"
done
