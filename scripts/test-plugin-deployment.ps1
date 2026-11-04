$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
foreach ($variant in @('Pro','FinOffice')) {
    $plugin = if ($variant -eq 'Pro') { 'WawiCallPro.Plugins.Lexware.Pro' } else { 'WawiCallPro.Plugins.Lexware.FinOffice' }
    $source = Join-Path $root "$plugin/bin/Debug/$variant/net10.0-windows"
    foreach ($hostName in @('App','Console')) {
        $target = Join-Path $root "WawiCallPro.$hostName/bin/Debug/$variant/net10.0-windows/plugins/$plugin"
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
        Write-Output "Plugin deployment passed: $hostName $variant (manifest, shared exclusion, private/native paths and file contents)"
    }
}
