$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$audit = Join-Path $root ('.artifacts/plugin-native-' + [guid]::NewGuid().ToString('N'))
foreach ($variant in @('Pro','FinOffice')) {
    foreach ($missing in @($false,$true)) {
        $case = Join-Path $audit "$variant-$missing"
        New-Item -ItemType Directory -Path $case -Force | Out-Null
        $hostRoot = Join-Path $root "WawiCallPro.Console/bin/Debug/$variant/net10.0-windows"
        Get-ChildItem -LiteralPath $hostRoot | Where-Object { $_.Name -ne 'plugins' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $case -Recurse }
        $bundle = Join-Path $case 'plugins/SignedRuntimePlugin'
        New-Item -ItemType Directory -Path $bundle -Force | Out-Null
        $fixture = Join-Path $root "WawiCallPro.Utilities.Tests/Fixtures/SignedRuntimePlugin/bin/Debug/$variant/net10.0"
        Get-ChildItem -LiteralPath $fixture -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $bundle }
        $rid = [Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
        $relative = "runtimes/$rid/native/wcp-native-probe.dll"
        if (!$missing) {
            $native = Join-Path $bundle $relative
            New-Item -ItemType Directory -Path (Split-Path $native) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path ([Environment]::SystemDirectory) 'version.dll') -Destination $native
        }
        $manifest = Join-Path $bundle 'SignedRuntimePlugin.deps.json'
        $json = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json -AsHashtable
        $framework = $json.runtimeTarget.name
        $dependency = @($json.targets[$framework].Keys | Where-Object { $_.StartsWith('PrivateDependency/') })[0]
        $json.targets[$framework][$dependency]['runtimeTargets'] = @{ $relative = @{rid=$rid;assetType='native'} }
        $json | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $manifest -Encoding utf8
        $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $case "WawiCallPro.Console.$variant.exe"))
        $start.UseShellExecute = $false; $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
        $start.Environment['WAWICALLPRO_CONFIG_DIR'] = Join-Path $case 'config'
        $start.Environment['WCP_PLUGIN_FIXTURE_FAULT'] = 'native'
        $start.ArgumentList.Add('--diagnostics')
        $process = [Diagnostics.Process]::Start($start)
        try {
            $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
            if (!$process.WaitForExit(30000)) { $process.Kill($true); throw 'Native plugin test timed out' }
            if ($missing) {
                if ($process.ExitCode -eq 0 -or !$stderr.Result.Contains('Warenwirtschaftsplugin')) { throw 'Missing native dependency was not reported' }
            } else {
                if ($process.ExitCode -ne 0 -or !(($stdout.Result | ConvertFrom-Json).PluginLoaded)) { throw ('Private native dependency could not be loaded: ' + $stderr.Result) }
            }
            if ($stderr.Result.Contains('fixture-private-secret')) { throw 'Private exception text exposed' }
        } finally { $process.Dispose() }
        Write-Output "Native plugin dependency passed: $variant missing=$missing"
    }
}
