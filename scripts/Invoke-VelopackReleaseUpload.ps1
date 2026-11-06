#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Prepare', 'Upload')][string]$Mode,
    [Parameter(Mandatory)][ValidateSet('Pro', 'FinOffice')][string]$ProductLine,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$ReleasesDirectory,
    [string]$DestinationDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Release.Common.psm1') -Force
$context = Get-ReleaseContext -ProductLine $ProductLine -Version $Version
$ReleasesDirectory = (Resolve-Path -LiteralPath $ReleasesDirectory).Path
$feedPath = Join-Path $ReleasesDirectory 'releases.win.json'
$feed = Get-Content -LiteralPath $feedPath -Raw | ConvertFrom-Json
$assets = @($feed.Assets)
if ($assets.Count -eq 0) { throw 'Leerer Paketfeed.' }
foreach ($asset in $assets) {
    if ($asset.PackageId -cne $context.PackageId -or $asset.Version -cne $Version -or [IO.Path]::GetFileName($asset.FileName) -cne $asset.FileName) { throw 'Feed enthält falsches Produkt, Version oder ungültigen Dateinamen.' }
    $file = Join-Path $ReleasesDirectory $asset.FileName
    if (!(Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Item -LiteralPath $file).Length -ne $asset.Size -or (Get-FileHash -LiteralPath $file -Algorithm SHA1).Hash -ne $asset.SHA1) { throw "Feeddatei fehlt oder ist verändert: $($asset.FileName)" }
}
$setup = Join-Path $ReleasesDirectory "$($context.PackageId)-$Version-win-Setup.exe"
if (!(Test-Path -LiteralPath $setup -PathType Leaf)) { throw 'Versioniertes Setup fehlt.' }
if ($Mode -eq 'Prepare') { Write-Host "Lokaler Feed geprüft: $ReleasesDirectory"; return }
if (!$DestinationDirectory -or ![IO.Path]::IsPathFullyQualified($DestinationDirectory)) { throw 'Upload benötigt ein ausdrücklich angegebenes absolutes Zielverzeichnis (lokal oder UNC).' }
$destination = [IO.Path]::GetFullPath($DestinationDirectory)
if ($destination.TrimEnd('\', '/') -eq $ReleasesDirectory.TrimEnd('\', '/')) { throw 'Quell- und Zielverzeichnis müssen verschieden sein.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$destinationFeed = Join-Path $destination 'releases.win.json'
if (Test-Path -LiteralPath $destinationFeed) {
    $existing = Get-Content -LiteralPath $destinationFeed -Raw | ConvertFrom-Json
    foreach ($asset in @($existing.Assets)) {
        if ($asset.PackageId -cne $context.PackageId) { throw 'Ziel enthält den Feed eines anderen Produkts.' }
    }
    $assets = @($existing.Assets | Where-Object { $_.Version -cne $Version }) + $assets
}
# Immutable payloads first; the working feed is replaced only after all copies are verified.
foreach ($file in @((Get-Item -LiteralPath $setup)) + @(Get-ChildItem -LiteralPath $ReleasesDirectory -Filter '*.nupkg' -File)) {
    $target = Join-Path $destination $file.Name
    $hash = (Get-FileHash -LiteralPath $file.FullName).Hash
    if (!(Test-Path -LiteralPath $target)) {
        $temporary = Join-Path $destination ('.upload-' + [guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath $file.FullName -Destination $temporary
        if ((Get-FileHash -LiteralPath $temporary).Hash -ne $hash) { throw 'Kopie nicht vollständig; bisheriger Feed bleibt erhalten.' }
        [IO.File]::Move($temporary, $target, $false)
    }
    if ((Get-FileHash -LiteralPath $target).Hash -ne $hash) { throw "Vorhandene unveränderliche Datei weicht ab: $target" }
}
$feed.Assets = $assets
$temporaryFeed = Join-Path $destination ('.feed-' + [guid]::NewGuid().ToString('N'))
$feed | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $temporaryFeed -Encoding utf8
[IO.File]::Move($temporaryFeed, $destinationFeed, $true)
Write-Host "Feed veröffentlicht: $destinationFeed"
