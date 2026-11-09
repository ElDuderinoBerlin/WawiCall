param([ValidateSet('Pro','FinOffice')][string]$ProductLine)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
if (!$ProductLine) {
    $hostRoot = Join-Path $root 'WawiCallPro.Console/bin/Debug/net10.0-windows'
    $editions = @('Pro','FinOffice' | Where-Object { Test-Path -LiteralPath (Join-Path $hostRoot "WawiCallPro.Console.$_.dll") })
    if ($editions.Count -ne 1) { throw 'Zuerst genau eine Edition bauen oder -ProductLine angeben.' }
    $ProductLine = $editions[0]
}
foreach ($variant in @($ProductLine)) {
    $productPlugin = if ($variant -eq 'Pro') { 'WawiCallPro.Plugins.Lexware.Pro' } else { 'WawiCallPro.Plugins.Lexware.FinOffice' }
    foreach ($plugin in @($productPlugin, 'WawiCallPro.Plugins.Dummy')) {
        $framework = if ($plugin -eq 'WawiCallPro.Plugins.Dummy') { 'net10.0' } else { 'net10.0-windows' }
        $source = Join-Path $root "$plugin/bin/Debug/$framework"
        foreach ($hostName in @('App','Console')) {
            $target = Join-Path $root "WawiCallPro.$hostName/bin/Debug/net10.0-windows/plugins/$plugin"
            foreach ($file in @("$plugin.dll", "$plugin.deps.json")) { if (!(Test-Path -LiteralPath (Join-Path $target $file))) { throw 'Plugin or dependency manifest missing' } }
            [xml]$props = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
            $shared = @($props.Project.ItemGroup.SharedProjectAssembly.Include; $props.Project.ItemGroup.SharedPackage.Include) -join ';'
            $shared = $shared.Split(';', [StringSplitOptions]::RemoveEmptyEntries)
            foreach ($item in Get-ChildItem -LiteralPath $source -File -Recurse) {
                if ($item.BaseName -in $shared) {
                    if ($item.Extension -eq '.dll') {
                        $hostAssembly = Join-Path (Split-Path (Split-Path $target)) $item.Name
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
                if (Test-Path -LiteralPath (Join-Path (Split-Path (Split-Path $target)) $assembly.Name)) { throw 'Managed assembly duplicated between host and plugin' }
            }
            Write-Output "Plugin deployment passed: $hostName $variant $plugin (manifest, shared exclusion, private/native paths and file contents)"
        }
    }

}
