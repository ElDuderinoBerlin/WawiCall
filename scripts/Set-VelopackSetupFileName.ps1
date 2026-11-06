[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Directory,
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]*$')][string]$PackageId,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][A-Za-z0-9.-]+)*$')][string]$PackageVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$originalName = "$PackageId-win-Setup.exe"
$versionedName = "$PackageId-$PackageVersion-win-Setup.exe"
$originalPath = Join-Path $Directory $originalName
$versionedPath = Join-Path $Directory $versionedName
$manifestPath = Join-Path $Directory 'assets.win.json'
$assets = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$assets = @($assets)
$installers = @($assets | Where-Object { $_.Type -eq 'Installer' })
if ($installers.Count -ne 1 -or $installers[0].RelativeFileName -cne $originalName) {
    throw "Genau ein Setup-Artefakt mit dem Namen $originalName erwartet."
}
if (!(Test-Path -LiteralPath $originalPath -PathType Leaf)) {
    throw "Setup fehlt: $originalPath"
}
if (Test-Path -LiteralPath $versionedPath) {
    throw "Versioniertes Setup existiert bereits: $versionedPath"
}

$installers[0].RelativeFileName = $versionedName
$manifestJson = ConvertTo-Json -InputObject $assets -Depth 10
Move-Item -LiteralPath $originalPath -Destination $versionedPath
[IO.File]::WriteAllText($manifestPath, $manifestJson + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Host "Versioniertes Setup: $versionedPath"
