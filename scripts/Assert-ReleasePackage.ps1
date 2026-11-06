#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][ValidateSet('Pro', 'FinOffice')][string]$ProductLine,
    [Parameter(Mandatory)][string]$Version,
    [string]$PackagePath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'Release.Common.psm1') -Force
$context = Get-ReleaseContext -ProductLine $ProductLine -Version $Version
$Directory = (Resolve-Path -LiteralPath $Directory).Path
$other = if ($ProductLine -eq 'Pro') { 'FinOffice' } else { 'Pro' }
$forbidden = if ($ProductLine -eq 'Pro') { '^Quantarix\.Pcfk\.' } else { '^Quantarix\.Lexware\.' }
$sensitive = '(?i)(^|/)(connection\.local\.json|startup\.local\.json|workstation\.local\.json|appsettings\.Local\.json|license|crash-reports|recordings|logs)(/|$)|\.(snk|pfx|pem|key|db|sqlite|wav|mp3|log|tmp|lock|zip|nupkg)$'
function Assert-FileName([string]$Name) {
    $leaf = ($Name -split '/')[-1]
    if ($Name -match $sensitive -or $leaf -match '^MMS\.' -or $leaf -match $forbidden -or $leaf -match "^WawiCallPro\..*\.$other(\.|$)") {
        throw "Unzulässiger Paketinhalt: $Name"
    }
}
$inventory = @{}
foreach ($file in Get-ChildItem -LiteralPath $Directory -File -Recurse) {
    $relative = [IO.Path]::GetRelativePath($Directory, $file.FullName).Replace('\', '/')
    Assert-FileName $relative
    $inventory[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if ($file.Extension -eq '.json' -and $file.Name -like '*.deps.json') {
        $graph = Get-Content -LiteralPath $file.FullName -Raw
        $wrongGraph = if ($ProductLine -eq 'Pro') { 'Quantarix.Pcfk.' } else { 'Quantarix.Lexware.' }
        if ($graph -match '"MMS\.' -or $graph.Contains($wrongGraph) -or $graph.Contains("WawiCallPro.Plugins.Lexware.$other/")) { throw "Unzulässiger Runtimegraph: $relative" }
    }
}
foreach ($hostName in @('App', 'Console')) {
    foreach ($extension in @('exe', 'dll', 'deps.json', 'runtimeconfig.json')) {
        $file = "WawiCallPro.$hostName.$ProductLine.$extension"
        if (!$inventory.ContainsKey($file)) { throw "Hostdatei fehlt: $file" }
    }
    $actualVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $Directory "WawiCallPro.$hostName.$ProductLine.dll")).ProductVersion.Split('+')[0]
    if ($actualVersion -cne $Version) { throw "Falsche Hostversion: $hostName $actualVersion statt $Version" }
}
$plugin = "plugins/WawiCallPro.Plugins.Lexware.$ProductLine"
foreach ($file in @("$plugin/WawiCallPro.Plugins.Lexware.$ProductLine.dll", "$plugin/WawiCallPro.Plugins.Lexware.$ProductLine.deps.json", 'Velopack.dll', 'coreclr.dll')) {
    if (!$inventory.ContainsKey($file)) { throw "Laufzeitdatei fehlt: $file" }
}
[xml]$props = Get-Content -LiteralPath (Join-Path $context.Root 'Directory.Build.props') -Raw
$shared = (@($props.Project.ItemGroup.SharedProjectAssembly.Include; $props.Project.ItemGroup.SharedPackage.Include) -join ';').Split(';', [StringSplitOptions]::RemoveEmptyEntries)
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $Directory $plugin) -Filter '*.dll' -Recurse) {
    if ($file.BaseName -in $shared -or (Test-Path -LiteralPath (Join-Path $Directory $file.Name))) { throw "Doppelte Shared-/Hostassembly im Plugin: $($file.Name)" }
}
if ($PackagePath) {
    $archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
    try {
        $seen = @{}
        $generated = @{}
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            Assert-FileName $name
            if ($name -notmatch '^lib/app/(.+[^/])$') { continue }
            $relative = $Matches[1]
            if ($relative -in @('sq.version', 'Squirrel.exe', "WawiCallPro $ProductLine`_ExecutionStub.exe")) {
                if ($generated.ContainsKey($relative) -or $entry.Length -eq 0) { throw "Ungültige Velopack-Laufzeitdatei: $relative" }
                $generated[$relative] = $true
            }
            if ($relative -in @('Squirrel.exe', "WawiCallPro $ProductLine`_ExecutionStub.exe")) { continue }
            if ($relative -eq 'sq.version') {
                $reader = [IO.StreamReader]::new($entry.Open())
                try { [xml]$metadata = $reader.ReadToEnd() } finally { $reader.Dispose() }
                if ($metadata.package.metadata.id -cne $context.PackageId -or $metadata.package.metadata.version -cne $Version -or $metadata.package.metadata.channel -cne 'win') { throw 'Falsche Velopack-Paketmetadaten.' }
                continue
            }
            if ($seen.ContainsKey($relative) -or !$inventory.ContainsKey($relative)) { throw "Unerwartete/mehrfache Paketdatei: $relative" }
            $stream = $entry.Open()
            try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
            if ($hash -ne $inventory[$relative]) { throw "Paketdatei weicht vom Publish ab: $relative" }
            $seen[$relative] = $true
        }
        # vpk excludes symbols and the optional runtime dump utility from Windows packages.
        foreach ($name in $inventory.Keys | Where-Object { $_ -notlike '*.pdb' -and $_ -ne 'createdump.exe' }) {
            if (!$seen.ContainsKey($name)) { throw "Publishdatei fehlt im Paket: $name" }
        }
        if ($generated.Count -ne 3) { throw 'Velopack-Metadaten, Updater oder Startstub fehlen im Paket.' }
    } finally { $archive.Dispose() }
}
Write-Host "Paketprüfung erfolgreich: $ProductLine $Version ($($inventory.Count) Publishdateien)."
