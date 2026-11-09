#requires -Version 7.0
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Release.Common.psm1') -Force
$root = Split-Path $PSScriptRoot -Parent
$fixtureRoot = Join-Path $root ('.artifacts/script-tests/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
$script:passed = 0
function Assert-True([bool]$Value, [string]$Message) {
    if (!$Value) { throw "Prüfung fehlgeschlagen: $Message" }
    $script:passed++
}
function Assert-Fails([scriptblock]$Action, [string]$Pattern) {
    try { & $Action } catch {
        if ($_.Exception.Message -notmatch $Pattern) { throw }
        $script:passed++
        return
    }
    throw "Erwarteter Fehler fehlt: $Pattern"
}
foreach ($file in @(Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Extension -in @('.ps1', '.psm1') }) + @(Get-ChildItem -LiteralPath $root -Filter '*.ps1' -File)) {
    $tokens = $null
    $parseErrors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
    Assert-True ($parseErrors.Count -eq 0) "Syntax $($file.Name)"
}
$defaults = Get-SolutionBuildDefaults
$defaultContext = Get-ReleaseContext
Assert-True ($defaultContext.ProductLine -eq $defaults.ProductLine) 'Zentrale Standardedition'
Assert-True ($defaultContext.Version -eq $defaults.Version) 'Zentrale Versionsvorgabe unabhängig von PropertyGroup-Position'
foreach ($version in @('../outside', '1.0', '01.2.3', '1.2.3+metadata', '1.2.3-01', '1.2.3;exit')) {
    Assert-Fails { Get-ReleaseContext -Version $version } 'SemVer'
}
foreach ($line in @('Pro', 'FinOffice')) {
    $context = Get-ReleaseContext -ProductLine $line -Version '1.2.3-rc.1'
    $projects = Get-ProductProjects $context
    $other = if ($line -eq 'Pro') { 'FinOffice' } else { 'Pro' }
    Assert-True (@($projects | Where-Object { $_ -match "WawiCallPro.Plugins.Lexware.$line[\\/]" }).Count -eq 1) "Plugin $line enthalten"
    Assert-True (@($projects | Where-Object { $_ -match "WawiCallPro.Plugins.Lexware.$other" }).Count -eq 0) "Fremdplugin $other ausgeschlossen"
}
$source = Join-Path $fixtureRoot 'source with spaces'
$destination = Join-Path $fixtureRoot 'merged'
New-Item -ItemType Directory -Path $source -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $source 'same.dll'), 'identical')
Merge-ReleaseFiles $source $destination
Merge-ReleaseFiles $source $destination
Assert-True ((Get-Content -LiteralPath (Join-Path $destination 'same.dll') -Raw) -eq 'identical') 'Identische Dateien zusammenführen'
[IO.File]::WriteAllText((Join-Path $source 'same.dll'), 'different')
Assert-Fails { Merge-ReleaseFiles $source $destination } 'Abweichende'
Assert-True ((Get-Content -LiteralPath (Join-Path $destination 'same.dll') -Raw) -eq 'identical') 'Konflikt verändert Ziel nicht'
$echoScript = Join-Path $fixtureRoot 'native args.ps1'
[IO.File]::WriteAllText($echoScript, 'param([string]$Value) [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false); Write-Output $Value')
$log = Join-Path $fixtureRoot 'args.log'
$literal = 'spaces " quotes $() & ; ä'
Invoke-ReleaseProcess pwsh @('-NoProfile', '-File', $echoScript, '-Value', $literal) -LogPath $log
Assert-True ((Get-Content -LiteralPath $log -Raw).Trim() -ceq $literal) 'ArgumentList erhält Sonderzeichen'
Assert-Fails { Invoke-ReleaseProcess pwsh @('-NoProfile', '-Command', 'exit 7') } 'Exitcode 7'
Assert-Fails { Invoke-ReleaseProcess pwsh @('-NoProfile', '-Command', 'Start-Sleep -Seconds 10') -TimeoutSeconds 1 } 'Prozessfrist'
$packageId = 'WawiCallPro.Pro'
$originalName = "$packageId-win-Setup.exe"
$setup = Join-Path $fixtureRoot $originalName
[IO.File]::WriteAllBytes($setup, [byte[]]@(1, 2, 3, 4))
$before = (Get-FileHash -LiteralPath $setup).Hash
@([ordered]@{ Type = 'Installer'; RelativeFileName = $originalName }) | ConvertTo-Json -AsArray | Set-Content -LiteralPath (Join-Path $fixtureRoot 'assets.win.json')
& (Join-Path $PSScriptRoot 'Set-VelopackSetupFileName.ps1') -Directory $fixtureRoot -PackageId $packageId -PackageVersion '1.2.3'
$versioned = Join-Path $fixtureRoot "$packageId-1.2.3-win-Setup.exe"
Assert-True ((Get-FileHash -LiteralPath $versioned).Hash -eq $before) 'Setupbytes erhalten'
$manifest = Get-Content -LiteralPath (Join-Path $fixtureRoot 'assets.win.json') -Raw | ConvertFrom-Json
Assert-True ($manifest.RelativeFileName -eq "$packageId-1.2.3-win-Setup.exe") 'Assetsmanifest angepasst'
$bad = Join-Path $fixtureRoot 'bad publish'
New-Item -ItemType Directory -Path $bad | Out-Null
foreach ($name in @('connection.local.json', 'secret.snk', 'MMS.Core.dll', 'Quantarix.Pcfk.Core.dll', 'WawiCallPro.App.FinOffice.dll')) {
    $file = Join-Path $bad $name
    [IO.File]::WriteAllText($file, 'fixture')
    Assert-Fails { & (Join-Path $PSScriptRoot 'Assert-ReleasePackage.ps1') -Directory $bad -ProductLine Pro -Version '1.2.3' } 'Unzulässiger'
    Remove-Item -LiteralPath $file
}
$feedDestination = Join-Path $fixtureRoot 'feed destination'
foreach ($version in @('1.2.3', '1.2.4')) {
    $packages = Join-Path $fixtureRoot "packages-$version"
    New-Item -ItemType Directory -Path $packages | Out-Null
    $packageName = "WawiCallPro.Pro-$version-full.nupkg"
    $package = Join-Path $packages $packageName
    [IO.File]::WriteAllBytes($package, [byte[]]@(1, 2, 3))
    [IO.File]::WriteAllBytes((Join-Path $packages "WawiCallPro.Pro-$version-win-Setup.exe"), [byte[]]@(4, 5))
    @{ Assets = @(@{ PackageId = 'WawiCallPro.Pro'; Version = $version; Type = 'Full'; FileName = $packageName; Size = 3; SHA1 = (Get-FileHash -LiteralPath $package -Algorithm SHA1).Hash }) } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $packages 'releases.win.json')
    & (Join-Path $PSScriptRoot 'Invoke-VelopackReleaseUpload.ps1') -Mode Prepare -ProductLine Pro -Version $version -ReleasesDirectory $packages
    & (Join-Path $PSScriptRoot 'Invoke-VelopackReleaseUpload.ps1') -Mode Upload -ProductLine Pro -Version $version -ReleasesDirectory $packages -DestinationDirectory $feedDestination
}
$destinationFeed = Join-Path $feedDestination 'releases.win.json'
$feed = Get-Content -LiteralPath $destinationFeed -Raw | ConvertFrom-Json
Assert-True ($feed.Assets.Count -eq 2) 'Vorherige Feedversion erhalten'
$before = (Get-FileHash -LiteralPath $destinationFeed).Hash
[IO.File]::WriteAllBytes((Join-Path $feedDestination 'WawiCallPro.Pro-1.2.4-full.nupkg'), [byte[]]@(9))
Assert-Fails { & (Join-Path $PSScriptRoot 'Invoke-VelopackReleaseUpload.ps1') -Mode Upload -ProductLine Pro -Version '1.2.4' -ReleasesDirectory $packages -DestinationDirectory $feedDestination } 'unveränderliche'
Assert-True ((Get-FileHash -LiteralPath $destinationFeed).Hash -eq $before) 'Fehler verändert funktionierenden Feed nicht'
Assert-Fails { & (Join-Path $PSScriptRoot 'Invoke-VelopackReleaseUpload.ps1') -Mode Prepare -ProductLine FinOffice -Version '1.2.4' -ReleasesDirectory $packages } 'falsches Produkt'
Write-Host "$script:passed Skriptprüfungen bestanden. Fixtures: $fixtureRoot"
