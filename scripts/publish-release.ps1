#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Pro', 'FinOffice')][string[]]$ProductLine,
    [string]$Version,
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [ValidateSet('Any CPU', 'x64')][string]$Platform = 'Any CPU',
    [string]$PluginKeyFile = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Quantarix.Plugin.System/PluginKey.snk'),
    [switch]$RequireCleanDependencies
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Release.Common.psm1') -Force
if (!$ProductLine) { $ProductLine = @((Get-SolutionBuildDefaults).ProductLine) }
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $root 'bootstrap.ps1') -CheckOnly -RequireClean:$RequireCleanDependencies
foreach ($line in $ProductLine) {
    $context = Get-ReleaseContext -ProductLine $line -Version $Version -Configuration $Configuration
    $stage = New-ReleaseStage $context
    $publish = Join-Path $stage 'publish'
    foreach ($hostName in @('App', 'Console')) {
        $hostOutput = Join-Path $stage "hosts/$hostName"
        $arguments = @('publish', (Join-Path $root "WawiCallPro.$hostName/WawiCallPro.$hostName.csproj"),
            '-c', $Configuration, "-p:Platform=$Platform", '-r', $context.Runtime, '--self-contained', 'true',
            "-p:LexwareProductLine=$line", "-p:Version=$($context.Version)", "-p:WawiCallPluginKeyFile=$PluginKeyFile",
            '-p:WawiUseVelopackAppSettings=true', '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', '--nologo', '--verbosity', 'minimal', '-o', $hostOutput)
        Invoke-ReleaseProcess dotnet $arguments -LogPath (Join-Path $stage "publish-$hostName.log")
        Merge-ReleaseFiles $hostOutput $publish
    }
    if ($Configuration -eq 'Release') {
        $tool = Join-Path $root "Tools/StripDxLicenseInformation/bin/Release/net11.0/WawiCallPro.StripDxLicenseInformation.dll"
        Invoke-ReleaseProcess dotnet @($tool, '--input', $publish, '--recursive', '--pattern', 'WawiCallPro*.dll', '--pattern', 'WawiCallPro*.exe', '--key-file', $PluginKeyFile) -LogPath (Join-Path $stage 'strip-final.log')
    }
    & (Join-Path $PSScriptRoot 'Assert-ReleasePackage.ps1') -Directory $publish -ProductLine $line -Version $context.Version
    foreach ($hostName in @('App', 'Console')) {
        Invoke-ReleaseProcess dotnet @((Join-Path $publish "WawiCallPro.$hostName.$line.dll"), '--diagnostics') -LogPath (Join-Path $stage "diagnostics-$hostName.log") -TimeoutSeconds 60
    }
    Write-ReleaseManifest $context $publish (Join-Path $stage 'publish-manifest.json')
    [ordered]@{ ProductLine = $line; Version = $context.Version; Configuration = $Configuration; PublishDirectory = $publish } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $context.ReleaseRoot "latest-$Configuration-publish.json") -Encoding utf8
    Write-Host "Publish erfolgreich: $publish"
}
