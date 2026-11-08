param(
    [string[]] $Runtimes = @('win-x64', 'osx-arm64', 'osx-x64')
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $artifacts | Out-Null

foreach ($rid in $Runtimes) {
    $out = Join-Path $artifacts "ArtGrader-$rid"
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    dotnet publish (Join-Path $root 'src/ArtGrader.Web/ArtGrader.Web.csproj') -c Release -r $rid -o $out
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid" }

    if ($rid -like 'osx-*') {
        Copy-Item (Join-Path $root 'build/ArtGrader.command') $out
        Write-Warning "${rid}: zip on macOS with publish.sh (or use the CI artifact) so the executable bit survives."
        continue
    }

    $zip = "$out.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
    Write-Host "Created $zip"
}
