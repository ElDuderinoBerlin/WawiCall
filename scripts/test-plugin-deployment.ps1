param(
    [ValidateSet('Pro','FinOffice')][string]$ProductLine,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    # Optional staging root with App/ and Console/ subdirectories, as used by the publish verification.
    [string]$HostOutputRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
if ($HostOutputRoot) { $HostOutputRoot = [IO.Path]::GetFullPath($HostOutputRoot) }
if (!$ProductLine) {
    $hostRoot = if ($HostOutputRoot) { Join-Path $HostOutputRoot 'Console' } else { Join-Path $root "WawiCallPro.Console/bin/$Configuration/net11.0-windows10.0.19041.0" }
    $editions = @('Pro','FinOffice' | Where-Object { Test-Path -LiteralPath (Join-Path $hostRoot "WawiCallPro.Console.$_.dll") })
    if ($editions.Count -ne 1) { throw 'Zuerst genau eine Edition bauen oder -ProductLine angeben.' }
    $ProductLine = $editions[0]
}
# Evaluate conditions through MSBuild; raw XML would include the opposite edition too.
$metadata = & dotnet msbuild (Join-Path $root 'WawiCallPro.Utilities/WawiCallPro.Utilities.csproj') "-p:Configuration=$Configuration" "-p:LexwareProductLine=$ProductLine" '-getItem:SharedProjectAssembly,SharedPackage' -v:quiet
if ($LASTEXITCODE -ne 0) { throw 'Could not evaluate edition-specific shared assemblies.' }
$items = ($metadata -join [Environment]::NewLine | ConvertFrom-Json).Items
$shared = @($items.SharedProjectAssembly | ForEach-Object { $_.Identity })
$shared += @($items.SharedPackage | ForEach-Object {
    $_.Identity
    if ($_.PSObject.Properties['RuntimeAssemblyNames']) { $_.RuntimeAssemblyNames.Split(';', [StringSplitOptions]::RemoveEmptyEntries) }
})
$preloaded = @($items.SharedProjectAssembly | Where-Object { $_.PSObject.Properties['Preload'] -and $_.Preload -eq 'true' } | ForEach-Object { $_.Identity })
foreach ($variant in @($ProductLine)) {
    $productPlugin = if ($variant -eq 'Pro') { 'WawiCallPro.Plugins.Lexware.Pro' } else { 'WawiCallPro.Plugins.Lexware.FinOffice' }
    foreach ($plugin in @($productPlugin, 'WawiCallPro.Plugins.JtlWawi')) {
        $framework = if ($plugin -eq 'WawiCallPro.Plugins.JtlWawi') { 'net11.0' } else { 'net11.0-windows' }
        $source = Join-Path $root "$plugin/bin/$Configuration/$framework"
        foreach ($hostName in @('App','Console')) {
            $hostRoot = if ($HostOutputRoot) { Join-Path $HostOutputRoot $hostName } else { Join-Path $root "WawiCallPro.$hostName/bin/$Configuration/net11.0-windows10.0.19041.0" }
            $target = Join-Path $hostRoot "plugins/$plugin"
            foreach ($name in $preloaded) {
                $hostAssembly = Join-Path $hostRoot "$name.dll"
                if (!(Test-Path -LiteralPath $hostAssembly -PathType Leaf)) { throw "Host-owned shared assembly missing: $name" }
                if ([Reflection.AssemblyName]::GetAssemblyName($hostAssembly).Name -ne $name) { throw "Invalid host-owned shared assembly: $name" }
            }
            foreach ($file in @("$plugin.dll", "$plugin.deps.json")) { if (!(Test-Path -LiteralPath (Join-Path $target $file))) { throw 'Plugin or dependency manifest missing' } }
            foreach ($item in Get-ChildItem -LiteralPath $source -File -Recurse) {
                if ($item.BaseName -in $shared) {
                    if ($item.Extension -eq '.dll') {
                        $hostAssembly = Join-Path $hostRoot $item.Name
                        if (!(Test-Path -LiteralPath $hostAssembly) -or (Get-FileHash -LiteralPath $item.FullName).Hash -ne (Get-FileHash -LiteralPath $hostAssembly).Hash) { throw "Incompatible shared assembly: $($item.Name)" }
                    }
                    continue
                }
                $relative = [IO.Path]::GetRelativePath($source, $item.FullName)
                $copy = Join-Path $target $relative
                if (!(Test-Path -LiteralPath $copy)) { throw "Missing private dependency: $relative" }
                if ((Get-FileHash -LiteralPath $item.FullName).Hash -ne (Get-FileHash -LiteralPath $copy).Hash) { throw "Changed private dependency: $relative" }
            }
            if (Get-ChildItem -LiteralPath $target -File -Recurse | Where-Object { $_.BaseName -in $shared -and $_.Extension -in @('.dll','.pdb') }) { throw 'Shared assembly copied into plugin deployment' }
            foreach ($assembly in Get-ChildItem -LiteralPath $target -Filter '*.dll') {
                if (Test-Path -LiteralPath (Join-Path $hostRoot $assembly.Name)) { throw "Managed assembly duplicated between host and plugin: $($assembly.Name)" }
            }
            Write-Output "Plugin deployment passed: $hostName $variant $plugin (manifest, shared exclusion, private/native paths and file contents)"
        }
    }

}
