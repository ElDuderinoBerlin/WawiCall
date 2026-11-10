#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [string]$Platform = 'AnyCPU'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root '.artifacts/build-layout'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$previousConfigDirectory = $env:WAWICALLPRO_CONFIG_DIR
try {
    foreach ($edition in @('Pro','FinOffice','Pro','FinOffice')) {
        $other = if ($edition -eq 'Pro') { 'FinOffice' } else { 'Pro' }
        foreach ($hostName in @('App','Console')) {
            $project = Join-Path $root "WawiCallPro.$hostName/WawiCallPro.$hostName.csproj"
            & dotnet build $project -c $Configuration "-p:Platform=$Platform" "-p:LexwareProductLine=$edition" --nologo --verbosity minimal |
                Set-Content -LiteralPath (Join-Path $artifacts "$hostName-$edition-build.log") -Encoding utf8
            if ($LASTEXITCODE -ne 0) { throw "Build failed: $hostName/$edition" }
            $output = Join-Path $root "WawiCallPro.$hostName/bin/$Configuration/net10.0-windows10.0.19041.0"
            if (!(Test-Path -LiteralPath (Join-Path $output "WawiCallPro.$hostName.$edition.dll"))) { throw 'Selected host missing.' }
            if (Test-Path -LiteralPath (Join-Path $output "WawiCallPro.$hostName.$other.dll")) { throw 'Stale other-edition host remains.' }
            $plugins = @(Get-ChildItem -LiteralPath (Join-Path $output 'plugins') -Directory |
                Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName ($_.Name + '.dll')) })
            if ($plugins.Count -ne 2 -or "WawiCallPro.Plugins.Lexware.$edition" -notin $plugins.Name -or 'WawiCallPro.Plugins.Dummy' -notin $plugins.Name) {
                throw 'Selected plugin and Dummy are not isolated from the previous edition.'
            }
            $env:WAWICALLPRO_CONFIG_DIR = Join-Path $artifacts "diagnostic-config/$hostName-$edition"
            $diagnosticText = & dotnet (Join-Path $output "WawiCallPro.$hostName.$edition.dll") --diagnostics
            if ($LASTEXITCODE -ne 0) { throw 'Host diagnostic failed.' }
            $diagnosticText | Set-Content -LiteralPath (Join-Path $artifacts "$hostName-$edition-diagnostics.json") -Encoding utf8
            $diagnostic = $diagnosticText | ConvertFrom-Json
            if (!$diagnostic.HostStarted -or !$diagnostic.PluginLoaded -or $diagnostic.ProductLine -ne $edition) { throw 'Wrong edition runtime graph.' }
        }
        & (Join-Path $PSScriptRoot 'test-plugin-deployment.ps1') -ProductLine $edition |
            Set-Content -LiteralPath (Join-Path $artifacts "deployment-$edition.log") -Encoding utf8
        Write-Output "Shared output and edition replacement passed: $edition/$Configuration"
    }
} finally { $env:WAWICALLPRO_CONFIG_DIR = $previousConfigDirectory }
