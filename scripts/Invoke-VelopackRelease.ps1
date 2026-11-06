#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Pro', 'FinOffice')][string[]]$ProductLine = @('Pro', 'FinOffice'),
    [string]$Version,
    [ValidateSet('Any CPU', 'x64')][string]$Platform = 'Any CPU',
    [switch]$PackOnly,
    [string]$PublishDirectory,
    [string]$Icon,
    [switch]$RequireCleanDependencies
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Release.Common.psm1') -Force
if ($PublishDirectory -and (!$PackOnly -or $ProductLine.Count -ne 1)) { throw 'PublishDirectory benötigt PackOnly und genau eine Produktlinie.' }
$root = Split-Path $PSScriptRoot -Parent
foreach ($line in $ProductLine) {
    $context = Get-ReleaseContext -ProductLine $line -Version $Version
    if (!$PackOnly) {
        & (Join-Path $PSScriptRoot 'build-release.ps1') -ProductLine $line -Version $context.Version -Platform $Platform -RequireCleanDependencies:$RequireCleanDependencies
        & (Join-Path $PSScriptRoot 'publish-release.ps1') -ProductLine $line -Version $context.Version -Platform $Platform -RequireCleanDependencies:$RequireCleanDependencies
    }
    $publish = Get-ReleasePublishDirectory $context $PublishDirectory
    & (Join-Path $PSScriptRoot 'Assert-ReleasePackage.ps1') -Directory $publish -ProductLine $line -Version $context.Version
    $stage = New-ReleaseStage $context
    $packages = Join-Path $stage 'packages'
    Invoke-ReleaseProcess dotnet @('tool', 'restore', '--tool-manifest', (Join-Path $root '.config/dotnet-tools.json')) -LogPath (Join-Path $stage 'tool-restore.log')
    $arguments = @('tool', 'run', 'vpk', '--', 'pack', '--packId', $context.PackageId,
        '--packVersion', $context.Version, '--packDir', $publish, '--mainExe', "WawiCallPro.App.$line.exe",
        '--packTitle', "WawiCallPro $line", '--runtime', $context.Runtime, '--channel', 'win',
        '--outputDir', $packages, '--noPortable', '--yes')
    if ($Icon) { $arguments += @('--icon', (Resolve-Path -LiteralPath $Icon).Path) }
    Invoke-ReleaseProcess dotnet $arguments -LogPath (Join-Path $stage 'pack.log')
    & (Join-Path $PSScriptRoot 'Set-VelopackSetupFileName.ps1') -Directory $packages -PackageId $context.PackageId -PackageVersion $context.Version
    $fullPackages = @(Get-ChildItem -LiteralPath $packages -Filter '*-full.nupkg' -File)
    if ($fullPackages.Count -ne 1) { throw 'Genau ein vollständiges Velopack-Paket erwartet.' }
    & (Join-Path $PSScriptRoot 'Assert-ReleasePackage.ps1') -Directory $publish -ProductLine $line -Version $context.Version -PackagePath $fullPackages[0].FullName
    Write-ReleaseManifest $context $packages (Join-Path $stage 'release-manifest.json')
    Write-Host "Velopack-Release lokal erstellt: $packages"
}
