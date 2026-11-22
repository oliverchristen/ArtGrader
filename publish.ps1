param(
    [string[]] $Runtimes = @('win-x64', 'osx-arm64', 'osx-x64')
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force $artifacts | Out-Null
$project = Join-Path $root 'src/ArtGrader.Web/ArtGrader.Web.csproj'

foreach ($rid in $Runtimes) {
    $out = Join-Path $artifacts "ArtGrader-$rid"
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }

    if ($rid -like 'osx-*') {
        $app = Join-Path $out 'ArtGrader.app/Contents'
        dotnet publish $project -c Release -r $rid -o (Join-Path $app 'Resources/app')
        if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid" }
        New-Item -ItemType Directory -Force (Join-Path $app 'MacOS') | Out-Null
        Copy-Item (Join-Path $root 'build/mac/Info.plist') $app
        Copy-Item (Join-Path $root 'build/mac/ArtGrader') (Join-Path $app 'MacOS')
        Copy-Item (Join-Path $root 'build/install.sh') $out
        Write-Host "${rid}: copy the folder to the Mac and run 'bash install.sh' there."
        continue
    }

    dotnet publish $project -c Release -r $rid -o $out
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $rid" }

    $zip = "$out.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
    Write-Host "Created $zip"
}
