param(
    [ValidateSet('Pro','FinOffice')][string]$ProductLine='Pro',
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [Parameter(Mandatory)][string]$BuildV1,
    [Parameter(Mandatory)][string]$BuildV2,
    [string]$RuntimeDirectory,
    [string]$OutputDirectory,
    [switch]$FromDesktop
)
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$BuildV1=[IO.Path]::GetFullPath($BuildV1); $BuildV2=[IO.Path]::GetFullPath($BuildV2)
if ($RuntimeDirectory) { $RuntimeDirectory=[IO.Path]::GetFullPath($RuntimeDirectory) }
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $repository ".artifacts/com-installation/$ProductLine/$([DateTime]::Now.ToString('yyyyMMdd-HHmmss'))" }
$output=[IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) { throw 'Use a fresh evidence directory.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$principal=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    if ($FromDesktop) { throw 'Installer tests require a normal user token.' }
    $arguments='-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -File "{0}" -ProductLine {1} -Configuration {2} -BuildV1 "{3}" -BuildV2 "{4}" -OutputDirectory "{5}" -FromDesktop' -f $PSCommandPath,$ProductLine,$Configuration,$BuildV1,$BuildV2,$output
    if ($RuntimeDirectory) { $arguments+=' -RuntimeDirectory "{0}"' -f $RuntimeDirectory }
    $shell=New-Object -ComObject Shell.Application; $handle=0; $desktop=$shell.Windows().FindWindowSW(0,0,8,[ref]$handle,1)
    if (-not $desktop) { throw 'Normal desktop unavailable.' }
    $desktop.Document.Application.ShellExecute((Join-Path $PSHOME 'pwsh.exe'),$arguments,$repository,'open',0)
    $until=[DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath (Join-Path $output 'bootstrap.json'))) { if ([DateTime]::UtcNow -gt $until) { throw 'Bootstrap timed out.' }; Start-Sleep -Milliseconds 60 }
    $bootstrap=Get-Content -LiteralPath (Join-Path $output 'bootstrap.json') -Raw | ConvertFrom-Json; $child=Get-Process -Id $bootstrap.Pid
    try {
        $until=[DateTime]::UtcNow.AddMinutes(10)
        while (-not $child.WaitForExit(1000)) { if ([DateTime]::UtcNow -gt $until) { throw 'Installer harness exceeded its test budget.' } }
        $proof=Get-Content -LiteralPath (Join-Path $output 'results.json') -Raw | ConvertFrom-Json
        if (-not $proof.Completed) { throw "Installation verification failed: $($proof.Error). Evidence: $output" }
        Write-Output "Passed $($proof.Checks.Count) real installation checks. Evidence: $output"
    } finally { $child.Dispose() }
    return
}
@{Pid=$PID;Elevated=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'bootstrap.json')
$id=[Guid]::NewGuid(); $progId="WCP.p$($id.ToString('N'))"; $packageId="WawiCallPro.ComFixture.p$($id.ToString('N'))"
$profile=Join-Path $output 'isolated profile'; $install=Join-Path $output 'installed application with spaces'
$old=Join-Path $output 'old installation snapshot'; $baseName="WawiCallPro.App.$ProductLine"
$checks=[Collections.Generic.List[string]]::new(); $completed=$false; $failure=$null; $cleanup=$false
$views=@([Microsoft.Win32.RegistryView]::Registry64,[Microsoft.Win32.RegistryView]::Registry32)
[IO.Directory]::CreateDirectory($profile) | Out-Null
@{ClassId=$id;ProgId=$progId} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $profile 'com-fixture.json')
function Check([bool]$Value,[string]$Name) { if (-not $Value) { throw "Failed: $Name" }; $checks.Add($Name) }
function Invoke-Tool([string]$File,[string[]]$Arguments,[string]$Log,[int]$Expected=0,[switch]$FixtureFirstRun) {
    $start=[Diagnostics.ProcessStartInfo]::new($File); $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.WorkingDirectory=$repository
    $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    foreach ($arg in $Arguments) { $start.ArgumentList.Add($arg) }
    if ($FixtureFirstRun) { $start.Environment['VELOPACK_FIRSTRUN']='1' }
    $process=[Diagnostics.Process]::Start($start); $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
    try {
        if (-not $process.WaitForExit(60000)) { $process.Kill($true); $process.WaitForExit(); throw 'Isolated tool exceeded its bounded wait.' }
        [IO.File]::WriteAllText((Join-Path $output $Log),$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult())
        if ($process.ExitCode -ne $Expected) { throw "Unexpected exit $($process.ExitCode) from $([IO.Path]::GetFileName($File)); expected $Expected. Log $Log" }
    } finally { $process.Dispose() }
}
function Registration([string]$ExpectedVersion) {
    foreach ($view in $views) {
        $root=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,$view)
        try {
            $key=$root.OpenSubKey("Software\Classes\CLSID\{$id}")
            if (-not $key) { return $false }
            try { if ($key.GetValue('WawiCallPro.Version') -ne $ExpectedVersion -or $key.GetValue('WawiCallPro.InstallationRoot') -ne $install) { return $false } }
            finally { $key.Dispose() }
            $key=$root.OpenSubKey("Software\Classes\CLSID\{$id}\LocalServer32")
            try { if (-not $key -or $key.GetValue('ServerExecutable') -ne (Join-Path $install "current/$baseName.exe")) { return $false } }
            finally { if ($key) { $key.Dispose() } }
        } finally { $root.Dispose() }
    }
    return $true
}
function Absent {
    foreach ($view in $views) {
        $root=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,$view)
        try { foreach ($path in @("Software\Classes\CLSID\{$id}","Software\Classes\$progId")) { $key=$root.OpenSubKey($path); if ($key) { $key.Dispose(); return $false } } }
        finally { $root.Dispose() }
    }
    return $true
}
function Uninstall-Entry {
    foreach ($view in $views) {
        $root=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,$view)
        try {
            $key=$root.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Uninstall\$packageId")
            if ($key) { try { return $key.GetValue('InstallLocation') } finally { $key.Dispose() } }
        } finally { $root.Dispose() }
    }
    return $null
}
function Activate-Installed([string]$Version) {
    if (-not $RuntimeDirectory) { return }
    foreach ($name in @('com-runtime.json','com-stopped.json')) { Remove-Item -LiteralPath (Join-Path $profile $name) -ErrorAction SilentlyContinue }
    @{formatVersion=6;settings=@{user=@{activePluginId='WawiCallPro.Plugins.Dummy'}}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $profile 'connection.local.json')
    foreach ($bits in @('x86','x64')) {
        $prefix=Join-Path $profile "$Version-$bits-$([Guid]::NewGuid().ToString('N'))-"
        $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $env:WINDIR $(if ($bits -eq 'x86') {'SysWOW64/cscript.exe'} else {'System32/cscript.exe'})))
        $start.UseShellExecute=$false; $start.CreateNoWindow=$true
        foreach ($arg in @('//nologo','//E:JScript',(Join-Path $PSScriptRoot 'ComPrototype/lifetime.js'),$progId,$prefix)) { $start.ArgumentList.Add($arg) }
        $client=[Diagnostics.Process]::Start($start)
        try {
            $until=[DateTime]::UtcNow.AddSeconds(40)
            while (-not (Test-Path -LiteralPath ($prefix+'ready.json'))) {
                if ((Test-Path -LiteralPath ($prefix+'failure.json')) -or [DateTime]::UtcNow -gt $until) { throw 'Installed COM activation failed.' }
                Start-Sleep -Milliseconds 60
            }
            $ready=Get-Content -LiteralPath ($prefix+'ready.json') -Raw | ConvertFrom-Json
            Check ($ready.State -eq 'Ready' -and $ready.ProductLine -eq $ProductLine) "$Version installed $bits COM activation resolves the expected application edition"
            [IO.File]::WriteAllText(($prefix+'release'),'')
            if (-not $client.WaitForExit(10000) -or $client.ExitCode -ne 0) { throw 'Installed client did not release cleanly.' }
        } finally { if (-not $client.HasExited) { $client.Kill($true); $client.WaitForExit() }; $client.Dispose() }
    }
    $until=[DateTime]::UtcNow.AddSeconds(5); $runtime=$null
    while (-not $runtime -and [DateTime]::UtcNow -lt $until) { try { $runtime=Get-Content -LiteralPath (Join-Path $profile 'com-runtime.json') -Raw | ConvertFrom-Json } catch { Start-Sleep -Milliseconds 40 } }
    if (-not $runtime -or $runtime.Forms -ne 0) { throw 'Installed host did not remain windowless.' }
    $owner=Get-Process -Id $runtime.Pid
    try { [IO.File]::WriteAllText((Join-Path $profile 'stop.request'),''); if (-not $owner.WaitForExit(30000)) { throw 'Installed host did not stop before update.' } }
    finally { $owner.Dispose() }
    $stopped=Get-Content -LiteralPath (Join-Path $profile 'com-stopped.json') -Raw | ConvertFrom-Json
    Check ($stopped.Success -and $stopped.Revoked) "$Version installed host completes orderly shutdown before any package replacement"
}
try {
    Check (Absent) 'Random installation COM identity starts unregistered'
    $packages=[Collections.Generic.List[object]]::new()
    foreach ($version in @('1.0.0','2.0.0')) {
        $build=if ($version -eq '1.0.0') {$BuildV1} else {$BuildV2}
        $payload=Join-Path $output "payload-$version"; $destination=Join-Path $output "packages-$version"
        [IO.Directory]::CreateDirectory($payload) | Out-Null
        if ($RuntimeDirectory) { Get-ChildItem -LiteralPath $RuntimeDirectory -Force | Copy-Item -Destination $payload -Recurse -Force }
        foreach ($file in @("$baseName.exe","$baseName.dll","$baseName.deps.json","$baseName.runtimeconfig.json",'Velopack.dll')) { Copy-Item -LiteralPath (Join-Path $build $file) -Destination (Join-Path $payload $file) }
        @{Profile=$profile;PackageId=$packageId} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'wawicallpro.com-fixture.json')
        $(if ($RuntimeDirectory) { '{}' } else { '{invalid JSON' }) | Set-Content -LiteralPath (Join-Path $payload 'appsettings.json')
        Invoke-Tool 'dotnet' @('tool','run','vpk','--','pack','--packId',$packageId,'--packVersion',$version,'--packDir',$payload,'--mainExe',"$baseName.exe",'--runtime','win-x64','--outputDir',$destination,'--noPortable','--shortcuts','None','--yes','--skip-updates') "pack-$version.log"
        $package=@(Get-ChildItem -LiteralPath $destination -Filter '*-full.nupkg' -File)
        $setup=@(Get-ChildItem -LiteralPath $destination -Filter '*Setup.exe' -File)
        if ($package.Count -ne 1 -or $setup.Count -ne 1) { throw 'Exactly one full package and Setup.exe expected.' }
        $packages.Add([pscustomobject]@{Version=$version;Package=$package[0].FullName;Setup=$setup[0].FullName})
    }
    # The validated random fixture package exits in OnFirstRun; no extra Setup arguments or visible startup.
    Invoke-Tool $packages[0].Setup @('--silent','--installto',$install,'--log',(Join-Path $output 'setup-native.log')) 'setup.log'
    Check (Registration '1.0.0') 'Real Setup.exe install invokes the HKCU registration hook for version 1'
    Check ((Uninstall-Entry) -eq $install) 'Real installer creates only its own per-user uninstall record at the expected installation root'
    Invoke-Tool -File (Join-Path $install "current/$baseName.exe") -Arguments @() -Log 'first-run.log' -FixtureFirstRun
    Check (Registration '1.0.0') 'Validated isolated first-run callback exits before any normal application services'
    Check (-not (Test-Path -LiteralPath (Join-Path $profile 'com-runtime.json'))) 'Install and first-run fast exit never open application services'
    Activate-Installed '1.0.0'
    [IO.Directory]::CreateDirectory($old) | Out-Null
    Get-ChildItem -LiteralPath $install -Force | Copy-Item -Destination $old -Recurse -Force
    $update=Join-Path $install 'Update.exe'
    Invoke-Tool $update @('--rootDir',$install,'--silent','--log',(Join-Path $output 'update-native.log'),'apply','--norestart','--package',$packages[1].Package) 'update.log'
    Check (Registration '2.0.0') 'Real Update.exe apply moves registration ownership to version 2'
    Activate-Installed '2.0.0'
    $oldExe=Join-Path $old "current/$baseName.exe"
    Invoke-Tool $oldExe @('--veloapp-updated','1.0.0') 'stale-register.log' 1
    Check (Registration '2.0.0') 'An old foreign installation update hook cannot steal the newer registration'
    Invoke-Tool $oldExe @('--veloapp-uninstall','1.0.0') 'stale-uninstall.log'
    Check (Registration '2.0.0') 'An old installation uninstall hook preserves the current registration'
    Invoke-Tool $update @('--rootDir',$install,'--silent','--log',(Join-Path $output 'rollback-native.log'),'apply','--norestart','--package',$packages[0].Package) 'rollback.log'
    Check (Registration '1.0.0') 'Real rollback to the prior package restores the same installation registration'
    Activate-Installed '1.0.0-rollback'
    Invoke-Tool $update @('--rootDir',$install,'--silent','--log',(Join-Path $output 'uninstall-native.log'),'uninstall') 'uninstall.log'
    Check (Absent) 'Real uninstaller removes its owned CLSID and ProgID in both views'
    Check ($null -eq (Uninstall-Entry)) 'Real uninstaller removes its own package uninstall record'
    Check (Test-Path -LiteralPath (Join-Path $profile 'com-fixture.json')) 'Uninstall preserves the separate user profile'
    $cleanup=$true; $completed=$true
} catch { $failure=$_.Exception.Message; throw }
finally {
    if ($RuntimeDirectory) {
        $installedExe=Join-Path $install "current/$baseName.exe"
        foreach ($running in @(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $installedExe })) {
            [IO.File]::WriteAllText((Join-Path $profile 'stop.request'),'')
            $hostProcess=Get-Process -Id $running.ProcessId -ErrorAction SilentlyContinue
            if ($hostProcess) { try { $null=$hostProcess.WaitForExit(30000) } finally { $hostProcess.Dispose() } }
        }
    }
    if (-not $cleanup -and (Test-Path -LiteralPath (Join-Path $install 'Update.exe'))) {
        try { Invoke-Tool (Join-Path $install 'Update.exe') @('--rootDir',$install,'--silent','uninstall') 'cleanup-uninstall.log'; $cleanup=Absent } catch { $cleanup=$false }
    }
    if (-not (Test-Path -LiteralPath $install)) { $cleanup=Absent }
    foreach ($temporary in @('payload-1.0.0','payload-2.0.0','old installation snapshot')) {
        $target=[IO.Path]::GetFullPath((Join-Path $output $temporary))
        if (-not $target.StartsWith($output.TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup escaped its evidence directory.' }
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    }
    @{Completed=$completed;Error=$failure;Checks=$checks.ToArray();Edition=$ProductLine;Elevated=$false;RegistryCleanupConfirmed=$cleanup;PackageId=$packageId} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'results.json')
}
