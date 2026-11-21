param(
    [ValidateSet('Pro','FinOffice')][string]$ProductLine = 'Pro',
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [string]$OutputDirectory,
    [switch]$AllowVisibleWindows,
    [switch]$Views,
    [switch]$Lifetime,
    [switch]$FromDesktop
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $Lifetime -and -not $AllowVisibleWindows) { throw 'The native application test opens windows. Use -AllowVisibleWindows only after agreement.' }
if ($Lifetime -and $Views) { throw 'Choose either the windowless lifetime tests or the visible view tests.' }
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository "artifacts/com-app/$ProductLine/$([DateTime]::Now.ToString('yyyyMMdd-HHmmss'))" }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count -gt 0) { throw 'Use a fresh result directory.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    if ($FromDesktop) { throw 'HKCU COM testing requires a normal desktop token.' }
    $arguments = '-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -File "{0}" -ProductLine {1} -Configuration {2} -OutputDirectory "{3}" -AllowVisibleWindows -FromDesktop' -f $PSCommandPath,$ProductLine,$Configuration,$output
    if ($Views) { $arguments += ' -Views' }
    if ($Lifetime) { $arguments += ' -Lifetime' }
    $shell = New-Object -ComObject Shell.Application
    $desktopHandle = 0
    $desktop = $shell.Windows().FindWindowSW(0,0,8,[ref]$desktopHandle,1)
    if (-not $desktop) { throw 'Normal desktop unavailable; no HKLM fallback.' }
    $desktop.Document.Application.ShellExecute((Join-Path $PSHOME 'pwsh.exe'), $arguments, $repository, 'open', 0)
    $limit = [DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath (Join-Path $output 'bootstrap.json'))) {
        if ([DateTime]::UtcNow -gt $limit) { throw 'Normal user harness did not start.' }
        Start-Sleep -Milliseconds 50
    }
    $bootstrap = Get-Content -LiteralPath (Join-Path $output 'bootstrap.json') -Raw | ConvertFrom-Json
    $child = Get-Process -Id $bootstrap.Pid
    try {
        $limit = [DateTime]::UtcNow.AddSeconds($(if ($Views -or $Lifetime) { 480 } else { 240 }))
        while (-not $child.WaitForExit(1000)) { if ([DateTime]::UtcNow -gt $limit) { $child.Kill($true); throw 'Native harness timed out.' } }
        $proof = Get-Content -LiteralPath (Join-Path $output 'results.json') -Raw | ConvertFrom-Json
        if (-not $proof.Completed) { throw "Native application test failed: $($proof.Error). Results: $output" }
        Write-Output "Passed $($proof.Checks.Count) native application checks. Results: $output"
    } finally { $child.Dispose() }
    return
}
@{ Pid=$PID; Elevated=$false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'bootstrap.json')
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class WawiComWindows {
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    public static int VisibleCount(int process) {
        int count=0; EnumWindows((window, parameter) => { uint pid; GetWindowThreadProcessId(window, out pid); if(pid==process && IsWindowVisible(window)) count++; return true; }, IntPtr.Zero); return count;
    }
}
'@
$executable = Join-Path $repository "WawiCallPro.App/bin/$Configuration/net11.0-windows10.0.19041.0/WawiCallPro.App.$ProductLine.exe"
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build the selected application edition first.' }
$fixtures = [Collections.Generic.List[object]]::new()
$clients = [Collections.Generic.List[object]]::new()
$checks = [Collections.Generic.List[string]]::new()
$completed = $false; $failure = $null; $forcedServerCleanup = 0
function Check([bool]$Value, [string]$Name) { if (-not $Value) { throw "Failed: $Name" }; $checks.Add($Name) }
function Wait-Until([scriptblock]$Condition, [string]$Name) {
    $limit = [DateTime]::UtcNow.AddSeconds($(if ($Views) { 180 } else { 50 }))
    while (-not (& $Condition)) { if ([DateTime]::UtcNow -gt $limit) { throw "Timed out: $Name" }; Start-Sleep -Milliseconds 80 }
}
function Read-Json([string]$File) { try { Get-Content -LiteralPath $File -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop } catch { return $null } }
function New-Fixture([string]$Name, [bool]$Invalid = $false) {
    $id = [Guid]::NewGuid(); $profile = Join-Path $output $Name
    [IO.Directory]::CreateDirectory($profile) | Out-Null
    $fixture = [pscustomobject]@{ Profile=$profile; ClassId=$id; ProgId="WCP.p$($id.ToString('N'))"; Views=[Collections.Generic.List[object]]::new() }
    $fixtures.Add($fixture)
    @{ClassId=$id; ProgId=$fixture.ProgId} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $profile 'com-fixture.json')
    if ($Invalid) { '{invalid' | Set-Content -LiteralPath (Join-Path $profile 'connection.local.json') }
    else { @{formatVersion=6;settings=@{user=@{activePluginId='WawiCallPro.Plugins.Dummy'}}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $profile 'connection.local.json') }
    foreach ($view in @([Microsoft.Win32.RegistryView]::Registry64,[Microsoft.Win32.RegistryView]::Registry32)) {
        $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,$view)
        try {
            foreach ($key in @("Software\Classes\CLSID\{$id}","Software\Classes\$($fixture.ProgId)")) {
                $existing = $root.OpenSubKey($key)
                if ($existing) {
                    try { if ($existing.GetValue('WawiComFixtureOwner') -ne $profile) { throw 'Registry collision.' } }
                    finally { $existing.Dispose() }
                }
            }
            $fixture.Views.Add($view)
            foreach ($key in @("Software\Classes\CLSID\{$id}","Software\Classes\$($fixture.ProgId)")) {
                $entry = $root.CreateSubKey($key); try { $entry.SetValue('WawiComFixtureOwner',$profile) } finally { $entry.Dispose() }
            }
            $entry = $root.CreateSubKey("Software\Classes\CLSID\{$id}\LocalServer32")
            try { $entry.SetValue('', ('"{0}" --com-fixture "{1}"' -f $executable,$profile)); $entry.SetValue('ServerExecutable',$executable) } finally { $entry.Dispose() }
            $entry = $root.CreateSubKey("Software\Classes\$($fixture.ProgId)\CLSID")
            try { $entry.SetValue('',$id.ToString('B')) } finally { $entry.Dispose() }
        } finally { $root.Dispose() }
    }
    return $fixture
}
function Start-Client($Fixture, [string]$Name, [string]$Bits) {
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $env:WINDIR $(if ($Bits -eq 'x86') {'SysWOW64/cscript.exe'} else {'System32/cscript.exe'})))
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    $prefix = Join-Path $Fixture.Profile "$Name-"
    $script = if ($Lifetime) { 'ComPrototype/lifetime.js' } elseif ($Views) { 'ComPrototype/views.js' } else { 'ComPrototype/app.js' }
    foreach ($argument in @('//nologo','//E:JScript',(Join-Path $PSScriptRoot $script),$Fixture.ProgId,$prefix)) { $start.ArgumentList.Add($argument) }
    $client = [pscustomobject]@{ Process=[Diagnostics.Process]::Start($start); Prefix=$prefix }
    $clients.Add($client); return $client
}
function Ready($Client) {
    Wait-Until { if (Test-Path -LiteralPath ($Client.Prefix+'failure.json')) { throw (Get-Content -LiteralPath ($Client.Prefix+'failure.json') -Raw) }; Test-Path -LiteralPath ($Client.Prefix+'ready.json') } 'JScript activation'
    return Read-Json ($Client.Prefix+'ready.json')
}
function Runtime($Fixture) {
    # The app refreshes this diagnostic file each second. An overlapping read may see a partial write.
    for ($attempt=0; $attempt -lt 5; $attempt++) {
        $snapshot=Read-Json (Join-Path $Fixture.Profile 'com-runtime.json')
        if ($snapshot) { return $snapshot }
        Start-Sleep -Milliseconds 20
    }
    return $null
}
function Owner($Fixture) {
    $runtime = Runtime $Fixture
    if (-not $runtime) { throw 'Missing application runtime proof.' }
    return Get-Process -Id $runtime.Pid
}
function Release-Client($Client) {
    [IO.File]::WriteAllText(($Client.Prefix+'release'),'')
    if (-not $Client.Process.WaitForExit(10000) -or $Client.Process.ExitCode -ne 0) { throw 'JScript release failed.' }
}
function Stop-Fixture($Fixture) {
    Wait-Until { $null -ne (Runtime $Fixture) } 'runtime before stopping'
    $owner = Owner $Fixture
    try {
        [IO.File]::WriteAllText((Join-Path $Fixture.Profile 'stop.request'),'')
        if (-not $owner.WaitForExit(30000)) { throw 'Application did not shut down through its context.' }
        $stopped = Read-Json (Join-Path $Fixture.Profile 'com-stopped.json')
        Check ($stopped -and $stopped.Success -and $stopped.Revoked) 'Host stopped services, disposed DI and revoked its class without an exception'
        Check $true 'App host completed its shutdown path'
    } finally { $owner.Dispose() }
}
function Explorer-Start($Fixture) {
    $shell = New-Object -ComObject Shell.Application
    $shell.ShellExecute($executable, ('--com-fixture "{0}"' -f $Fixture.Profile), (Split-Path $executable -Parent), 'open', 1)
}
try {
    if ($Lifetime) {
        $fixture = New-Fixture 'native-lifetime'
        $a = Start-Client $fixture 'x64' 'x64'; $first = Ready $a
        $b = Start-Client $fixture 'x86' 'x86'; $second = Ready $b
        Check ($first.State -eq 'Ready' -and $second.State -eq 'Ready') 'Both client bitnesses activate the actual application without registered type libraries'
        Wait-Until { $r=Runtime $fixture; $r -and $r.ServerLocks -eq 4 } 'two facades and two snapshots'
        $owner = Owner $fixture
        try {
            Check ((Runtime $fixture).Forms -eq 0 -and -not (Runtime $fixture).FormCreated) 'Parallel native clients create no forms'
            foreach ($client in @($a,$b)) { [IO.File]::WriteAllText(($client.Prefix+'drop-root'),'') }
            Wait-Until { (Test-Path -LiteralPath ($a.Prefix+'dropped.json')) -and (Test-Path -LiteralPath ($b.Prefix+'dropped.json')) } 'facade release'
            Wait-Until { $r=Runtime $fixture; $r -and $r.ServerLocks -eq 2 } 'only snapshot references remain'
            Check $true 'Native facade Release releases its host lease without forcing server garbage collection'
            $limit=[DateTime]::UtcNow.AddSeconds(33)
            while ([DateTime]::UtcNow -lt $limit) {
                if ($owner.HasExited) { throw 'The host exited while snapshots were still referenced.' }
                Start-Sleep -Milliseconds 250
            }
            Check (-not $owner.HasExited) 'Status subobjects alone keep the host alive beyond the 30-second idle period'
            foreach ($client in @($a,$b)) { [IO.File]::WriteAllText(($client.Prefix+'read-child'),'') }
            Wait-Until { (Test-Path -LiteralPath ($a.Prefix+'child.json')) -and (Test-Path -LiteralPath ($b.Prefix+'child.json')) } 'snapshot after facade release'
            Check ((Read-Json ($a.Prefix+'child.json')).State -eq 'Ready' -and (Read-Json ($b.Prefix+'child.json')).State -eq 'Ready') 'Both retained snapshots remain callable after their parent facades are released'
            Release-Client $a
            Wait-Until { $r=Runtime $fixture; $r -and $r.ServerLocks -eq 1 } 'first child release'
            Check (-not $owner.HasExited) 'One client can release independently of the other client'
            Release-Client $b
            if (-not $owner.WaitForExit(45000)) { throw 'The host failed to exit after its last native Release and idle period.' }
            $stopped=Read-Json (Join-Path $fixture.Profile 'com-stopped.json')
            Check ($stopped -and $stopped.Success -and $stopped.Revoked) 'Last native Release permits a clean idle shutdown, class revocation and DI disposal'
        } finally { $owner.Dispose() }
        $previous=(Runtime $fixture).Pid
        $restart=Start-Client $fixture 'restart' 'x64'; $restarted=Ready $restart
        Wait-Until { $r=Runtime $fixture; $r -and $r.Pid -ne $previous } 'new COM owner after idle shutdown'
        Check ($restarted.State -eq 'Ready' -and (Runtime $fixture).Forms -eq 0) 'A new activation after idle shutdown starts a new windowless host'
        $previous=(Runtime $fixture).Pid
        $oldOwner=Owner $fixture
        try {
            [IO.File]::WriteAllText((Join-Path $fixture.Profile 'shutdown-delay.request'),'5000')
            [IO.File]::WriteAllText((Join-Path $fixture.Profile 'stop.request'),'')
            Wait-Until { $s=Read-Json (Join-Path $fixture.Profile 'com-stopping.json'); $s -and $s.Pid -eq $previous } 'revoked old owner during controlled shutdown'
            Check (-not $oldOwner.HasExited) 'Class is revoked while the old host still owns its instance mutex'
            $handoff=Start-Client $fixture 'handoff' 'x86'; $next=Ready $handoff
            Wait-Until { $r=Runtime $fixture; $r -and $r.Pid -ne $previous } 'successor publishes after old shutdown'
            Check ($next.State -eq 'Ready' -and $oldOwner.WaitForExit(10000)) 'Activation during shutdown waits for the old mutex and publishes a working successor'
            Check ((Runtime $fixture).Forms -eq 0 -and (Runtime $fixture).SipState -eq 'Stopped') 'Shutdown handoff creates neither a form nor a SIP registration'
            Release-Client $restart
        } finally { $oldOwner.Dispose() }
        $peer=Start-Client $fixture 'survivor' 'x64'; $peerStatus=Ready $peer
        Wait-Until { $r=Runtime $fixture; $r -and $r.ServerLocks -eq 4 } 'two clients before crash'
        $handoff.Process.Kill($true); $handoff.Process.WaitForExit()
        Wait-Until { $r=Runtime $fixture; $r -and $r.ServerLocks -eq 2 } 'RPC rundown after abrupt JScript termination'
        Check ($peerStatus.State -eq 'Ready') 'Abrupt client death releases its COM references without stopping the other client or forcing server GC'
        [IO.File]::WriteAllText(($peer.Prefix+'drop-root'),'')
        [IO.File]::WriteAllText(($peer.Prefix+'read-child'),'')
        Wait-Until { Test-Path -LiteralPath ($peer.Prefix+'child.json') } 'surviving child after peer crash'
        Check ((Read-Json ($peer.Prefix+'child.json')).State -eq 'Ready') 'Surviving client retains a callable snapshot after peer crash'
        $survivor=Owner $fixture
        try {
            Release-Client $peer
            if (-not $survivor.WaitForExit(45000)) { throw 'Post-crash host did not complete idle shutdown.' }
            $stopped=Read-Json (Join-Path $fixture.Profile 'com-stopped.json')
            Check ($stopped -and $stopped.Success -and $stopped.Revoked) 'Post-crash host shuts down cleanly after its final surviving client releases'
        } finally { $survivor.Dispose() }
    } elseif ($Views) {
        foreach ($bits in @('x64','x86')) {
            $fixture=New-Fixture "views-$bits"
            $client=Start-Client $fixture 'views' $bits
            $ready=Ready $client
            foreach ($name in $ready.Checks) { Check $true "$bits $name" }
            Release-Client $client; Stop-Fixture $fixture
        }
    }
    else {
    $fixture = New-Fixture 'cold'
    $a = Start-Client $fixture 'x64' 'x64'; $b = Start-Client $fixture 'x86' 'x86'
    $first = Ready $a; $second = Ready $b
    Check ($first.Status.State -eq 'Ready' -and $second.Status.State -eq 'Ready') 'x64/x86 clients activate the actual app host'
    Wait-Until { $null -ne (Runtime $fixture) } 'runtime identity'
    $owner = Owner $fixture
    try {
        $runtime = Runtime $fixture
        Check (-not $runtime.FormCreated -and $runtime.Forms -eq 0 -and [WawiComWindows]::VisibleCount($owner.Id) -eq 0) 'Cold COM startup constructs no form and shows no windows'
        Check ($first.Status.SipState -eq 'Stopped' -and -not $first.Status.Visible) 'COM startup does not register SIP or show UI'
        $processes = @(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $executable -and $_.CommandLine.Contains($fixture.Profile) })
        Check ($processes.Count -eq 1) 'Parallel cold COM clients share exactly one app process'
        Explorer-Start $fixture
        Wait-Until { $r = Runtime $fixture; $r -and $r.Visible } 'Explorer shows the existing COM host'
        $owner.Refresh()
        $main = [IntPtr](Runtime $fixture).WindowHandle
        Check ($main -ne [IntPtr]::Zero) 'Explorer second start creates the main window in the COM owner'
        [IO.File]::WriteAllText(($a.Prefix+'show'),'')
        Wait-Until { Test-Path -LiteralPath ($a.Prefix+'shown.json') } 'COM Show in existing window'
        $shown = Read-Json ($a.Prefix+'shown.json')
        Check ($shown.Operation -eq 'Succeeded' -and $shown.Status.Visible) 'Show reuses the existing main window'
        Check ((Runtime $fixture).Forms -eq 1) 'COM and Explorer share one main form'
        $focus = [Diagnostics.ProcessStartInfo]::new((Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'))
        $focus.UseShellExecute=$false; $focus.CreateNoWindow=$true; $focus.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
        foreach ($argument in @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'Invoke-ComAppFocus.ps1'),'-WindowHandle',$main.ToInt64(),'-OwnerProcessId',$owner.Id)) { $focus.ArgumentList.Add([string]$argument) }
        $helper = [Diagnostics.Process]::Start($focus)
        try { if (-not $helper.WaitForExit(10000) -or $helper.ExitCode -ne 0) { throw 'Initial application focus helper failed.' } } finally { $helper.Dispose() }
        Wait-Until { [WawiComWindows]::GetForegroundWindow() -eq $main } 'initial focused owner'
        foreach ($action in @('minimized','maximized','hidden')) {
            [IO.File]::WriteAllText((Join-Path $fixture.Profile 'launch.request'),$action)
            Wait-Until { -not (Test-Path -LiteralPath (Join-Path $fixture.Profile 'launch.request')) } 'fixture second launch'
            Wait-Until { [WawiComWindows]::GetForegroundWindow() -eq $main -and [WawiComWindows]::IsWindowVisible($main) -and -not [WawiComWindows]::IsIconic($main) } 'second start foreground handoff'
            Check $true "Second Explorer start restores and activates $action app"
            if ($action -eq 'maximized') { Check ([WawiComWindows]::IsZoomed($main)) 'Previously maximized state survives COM/Explorer activation' }
        }
    } finally { $owner.Dispose() }
    Release-Client $a; Release-Client $b; Stop-Fixture $fixture

    $fixture = New-Fixture 'show'
    $client = Start-Client $fixture 'client' 'x86'; $ready = Ready $client
    [IO.File]::WriteAllText(($client.Prefix+'show'),'')
    Wait-Until { Test-Path -LiteralPath ($client.Prefix+'shown.json') } 'explicit Show'
    $shown = Read-Json ($client.Prefix+'shown.json')
    Check ($shown.Operation -eq 'Succeeded' -and $shown.Status.Visible) 'Explicit COM Show creates the real main window on demand'
    Release-Client $client; Stop-Fixture $fixture

    $fixture = New-Fixture 'normal'
    Explorer-Start $fixture
    $client = Start-Client $fixture 'joining' 'x64'; $ready = Ready $client
    Wait-Until { $r=Runtime $fixture; $r -and $r.Visible } 'normal startup'
    Check ($ready.Status.State -eq 'Ready' -and (Runtime $fixture).Forms -eq 1) 'COM joins the normal app startup without a second main form'
    Release-Client $client; Stop-Fixture $fixture

    $fixture = New-Fixture 'invalid-configuration' $true
    $client = Start-Client $fixture 'client' 'x64'; $ready = Ready $client
    Check ($ready.Status.State -eq 'ConfigurationRequired' -and -not $ready.Status.Visible) 'Configuration failure remains queryable without a message box'
    Wait-Until { $null -ne (Runtime $fixture) } 'failed startup runtime'
    Check ((Runtime $fixture).Forms -eq 0) 'Configuration failure constructs no forms'
    Release-Client $client; Stop-Fixture $fixture
    }
    $completed = $true
} catch { $failure = $_.Exception.Message; throw }
finally {
    foreach ($client in $clients) { try { if (-not $client.Process.HasExited) { $client.Process.Kill($true); $client.Process.WaitForExit() } } finally { $client.Process.Dispose() } }
    foreach ($fixture in $fixtures) {
        foreach ($process in @(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $executable -and $_.CommandLine.Contains($fixture.Profile) })) {
            $forcedServerCleanup++
            if ($completed) { $completed=$false; $failure='A fixture host remained alive after the successful-path checks.' }
            Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
        }
        foreach ($view in $fixture.Views) {
            $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,$view)
            try {
                foreach ($key in @("Software\Classes\CLSID\{$($fixture.ClassId)}","Software\Classes\$($fixture.ProgId)")) {
                    $root.DeleteSubKeyTree($key,$false)
                    $left=$root.OpenSubKey($key); if ($left) { $left.Dispose(); throw 'Fixture registry cleanup failed.' }
                }
            } finally { $root.Dispose() }
        }
    }
    @{Completed=$completed;Error=$failure;Checks=$checks.ToArray();RegistryCleanupConfirmed=$true;ForcedServerCleanup=$forcedServerCleanup;Elevated=$false;Edition=$ProductLine;Executable=$executable;DllSha256=(Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($executable,'.dll'))).Hash} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'results.json')
}
if (-not $completed) { throw $failure }
