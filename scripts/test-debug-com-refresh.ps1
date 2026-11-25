[CmdletBinding(DefaultParameterSetName = 'Test')]
param(
    [Parameter(ParameterSetName = 'Test')]
    [ValidateSet('All', 'Pro', 'FinOffice')][string]$ProductLine = 'All',
    [Parameter(ParameterSetName = 'Test')][string]$OutputDirectory,
    [Parameter(ParameterSetName = 'Test')][switch]$FromDesktop,
    [Parameter(Mandatory, ParameterSetName = 'Gate')][switch]$BuildGate
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($BuildGate) {
    $directory = $env:WawiComRefreshProofDirectory
    if (-not $directory -or -not (Test-Path -LiteralPath $directory -PathType Container)) { throw 'Missing COM proof directory.' }
    [IO.File]::WriteAllText((Join-Path $directory 'building'), [DateTimeOffset]::UtcNow.ToString('o'))
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath (Join-Path $directory 'continue'))) {
        if ($timer.Elapsed.TotalSeconds -ge 60) { throw 'The COM client did not release the build gate.' }
        Start-Sleep -Milliseconds 50
    }
    return
}

$repository = Split-Path -Parent $PSScriptRoot
$app = Join-Path $repository 'WawiCallPro.App/WawiCallPro.App.csproj'
$hook = Join-Path $PSScriptRoot 'ComExamples/DebugRefreshTest.targets'
$clientScript = Join-Path $PSScriptRoot 'ComExamples/debug-refresh-client.vbs'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository "artifacts/com-debug-refresh/$([DateTime]::Now.ToString('yyyyMMdd-HHmmss'))" }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) { throw 'Use a fresh result directory.' }
$null = [IO.Directory]::CreateDirectory($output)

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try { $elevated = [Security.Principal.WindowsPrincipal]::new($identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
finally { $identity.Dispose() }
if ($elevated) {
    if ($FromDesktop) { throw 'Native HKCU activation requires the normal desktop token.' }
    $shell = New-Object -ComObject Shell.Application
    $handle = 0
    $desktop = $shell.Windows().FindWindowSW(0, 0, 8, [ref]$handle, 1)
    if (-not $desktop) { throw 'Normal user desktop unavailable.' }
    $arguments = '-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -File "{0}" -OutputDirectory "{1}" -ProductLine {2} -FromDesktop' -f $PSCommandPath, $output, $ProductLine
    $desktop.Document.Application.ShellExecute((Join-Path $PSHOME 'pwsh.exe'), $arguments, $repository, 'open', 0)
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath (Join-Path $output 'results.json'))) {
        if ($timer.Elapsed.TotalMinutes -ge 8) { throw "Desktop test timed out. Results: $output" }
        Start-Sleep -Milliseconds 100
    }
    $result = Get-Content -LiteralPath (Join-Path $output 'results.json') -Raw | ConvertFrom-Json
    if ($result.Error) { throw "COM debug refresh test failed: $($result.Error). Results: $output" }
    Write-Output "COM debug refresh passed. Results: $output"
    return
}

function Initialize-WindowObserver {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

public sealed class DebugComWindows : IDisposable {
    private delegate void WinEvent(IntPtr hook, uint kind, IntPtr window, int obj, int child, uint thread, uint time);
    private delegate bool EnumCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEvent callback, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, IntPtr window, uint first, uint last, uint remove);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref Message message);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenThread(uint access, bool inherit, uint thread);
    [DllImport("kernel32.dll")] private static extern uint GetProcessIdOfThread(IntPtr thread);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [StructLayout(LayoutKind.Sequential)] private struct Message {
        public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam;
        public uint Time; public int X, Y; public uint Private;
    }

    private readonly WinEvent callback;
    private readonly IntPtr hook;
    private readonly List<uint> shown = new List<uint>();
    public int UnresolvedEvents { get; private set; }

    public DebugComWindows() {
        callback = OnShow;
        const uint EventObjectShow = 0x8002;
        const uint SkipOwnProcess = 2;
        hook = SetWinEventHook(EventObjectShow, EventObjectShow, IntPtr.Zero, callback, 0, 0, SkipOwnProcess);
        if (hook == IntPtr.Zero) throw new Win32Exception();
    }

    private void OnShow(IntPtr hook, uint kind, IntPtr window, int obj, int child, uint thread, uint time) {
        if (obj != 0 || child != 0 || window == IntPtr.Zero) return;
        uint pid;
        if (GetWindowThreadProcessId(window, out pid) == thread && pid != 0) {
            var root = GetAncestor(window, 2);
            if (root == window || root == IntPtr.Zero) shown.Add(pid);
            return;
        }

        // Out-of-context callbacks may arrive after the window was destroyed. Attribute those
        // events to the originating thread when Windows can still identify it.
        const uint ThreadQueryLimitedInformation = 0x0800;
        var source = OpenThread(ThreadQueryLimitedInformation, false, thread);
        if (source == IntPtr.Zero) { UnresolvedEvents++; return; }
        try {
            pid = GetProcessIdOfThread(source);
            if (pid == 0) UnresolvedEvents++;
            else shown.Add(pid);
        } finally { CloseHandle(source); }
    }

    public void Pump() { Message message; while (PeekMessage(out message, IntPtr.Zero, 0, 0, 1)) DispatchMessage(ref message); }
    public int ShowEvents(uint pid) { return shown.FindAll(value => value == pid).Count; }
    public int VisibleWindows(uint pid) {
        int count = 0;
        EnumWindows((window, _) => {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner == pid && IsWindowVisible(window)) count++;
            return true;
        }, IntPtr.Zero);
        return count;
    }
    public void Dispose() { UnhookWinEvent(hook); }
}
'@
}

function Get-Registration([string]$Edition) {
    $paths = foreach ($view in [Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32) {
        $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey('CurrentUser', $view)
        try {
            $prog = $root.OpenSubKey("Software\Classes\WawiCallPro.$Edition.Application.1\CLSID")
            if (-not $prog) { throw "$Edition COM is not registered in HKCU. Run register-debug-com.ps1 first." }
            try { $classId = $prog.GetValue('') } finally { $prog.Dispose() }
            $server = $root.OpenSubKey("Software\Classes\CLSID\$classId\LocalServer32")
            if (-not $server) { throw "$Edition LocalServer32 is missing." }
            try { $server.GetValue('ServerExecutable') } finally { $server.Dispose() }
        } finally { $root.Dispose() }
    }
    if ($paths.Count -ne 2 -or $paths[0] -ne $paths[1]) { throw "$Edition HKCU registration views differ." }
    $expected = [WildcardPattern]::Escape((Join-Path $repository "WawiCallPro.App\bin\ComDebug\$Edition"))
    if ($paths[0] -notlike "$expected\*\WawiCallPro.App.$Edition.exe" -or -not (Test-Path -LiteralPath $paths[0] -PathType Leaf)) {
        throw "$Edition COM must point to a completed Debug output of this repository. Run register-debug-com.ps1 first."
    }
    return $paths[0]
}

function Wait-ForProof([scriptblock]$Condition, [int]$Seconds, [string]$Description) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    do {
        $windows.Pump()
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 15
    } until ($timer.Elapsed.TotalSeconds -ge $Seconds)
    throw "Timed out waiting for $Description."
}

function Start-ComClient([string]$Stage, [string]$Bitness, [string]$ExpectedPath, [int]$ExpectedPid = 0) {
    $systemDirectory = if ($Bitness -eq 'x86') { 'SysWOW64' } else { 'System32' }
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $env:WINDIR "$systemDirectory/cscript.exe"))
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardError = $true
    $prefix = Join-Path $editionOutput "$Stage-"
    foreach ($argument in '//nologo', $clientScript, "WawiCallPro.$edition.Application.1", $prefix) { $start.ArgumentList.Add($argument) }
    $client = @{ Process = [Diagnostics.Process]::Start($start); Prefix = $prefix }
    $clients.Add($client)
    Wait-ForProof {
        if ($client.Process.HasExited) { throw "CreateObject failed at $Stage. $($client.Process.StandardError.ReadToEnd())" }
        Test-Path -LiteralPath ($prefix + 'status')
    } 50 "CreateObject at $Stage"
    $values = @(Get-Content -LiteralPath ($prefix + 'status'))
    $servers = @(Get-Process -Name "WawiCallPro.App.$edition" -ErrorAction SilentlyContinue | Where-Object Path -EQ $ExpectedPath)
    if ($servers.Count -ne 1 -or ($ExpectedPid -and $servers[0].Id -ne $ExpectedPid)) { throw "Unexpected server process at $Stage." }
    $server = $servers[0]
    $null = $server.Handle
    $windows.Pump()
    $visible = $windows.VisibleWindows($server.Id)
    $shows = $windows.ShowEvents($server.Id)
    $activations.Add([ordered]@{
        Stage = $Stage; Bitness = $Bitness; ProcessId = $server.Id; Executable = $server.Path
        State = $values[2]; CreateObjectMs = [int]$values[0]; StatusObservedMs = [int]$values[1]
        ComVisible = $values[3]; VisibleWindows = $visible; ShowEvents = $shows; ObservedAt = [DateTimeOffset]::UtcNow.ToString('o')
    })
    if ($values[2] -ne 'Ready' -or $values[3] -ne 'False' -or $visible -or $shows) {
        $server.Dispose()
        throw "Unexpected COM status or native window activity at $Stage."
    }
    return $server
}

function Start-DebugBuild([ValidateSet('Refresh', 'Fail', 'Restore')][string]$Mode) {
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
    $start.WorkingDirectory = $repository
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = [Text.Encoding]::UTF8
    $start.StandardErrorEncoding = [Text.Encoding]::UTF8
    foreach ($argument in 'build', $app, '-c', 'Debug', '-p:Platform=AnyCPU', "-p:LexwareProductLine=$edition", '--nologo') { $start.ArgumentList.Add($argument) }
    if ($Mode -ne 'Restore') {
        $start.ArgumentList.Add("-p:CustomAfterMicrosoftCommonTargets=$hook")
        $start.Environment['WawiComRefreshTestMode'] = $Mode
        $start.Environment['WawiComRefreshTestId'] = [Guid]::NewGuid().ToString('N')
        $start.Environment['WawiComRefreshProofDirectory'] = $editionOutput
    }
    $process = [Diagnostics.Process]::Start($start)
    return @{ Process = $process; Output = $process.StandardOutput.ReadToEndAsync(); Error = $process.StandardError.ReadToEndAsync() }
}

function Finish-DebugBuild($Build, [string]$LogName, [switch]$ExpectFailure) {
    try {
        if (-not $Build.Process.WaitForExit(120000)) {
            $Build.Process.Kill($true)
            $Build.Process.WaitForExit()
            throw 'Debug build timed out.'
        }
        $log = $Build.Output.GetAwaiter().GetResult() + $Build.Error.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $editionOutput $LogName), $log)
        $exitCode = $Build.Process.ExitCode
        if ($ExpectFailure) {
            if ($exitCode -eq 0 -or $log -notmatch '(?m)\berror WAWICOM001:') {
                throw "Debug build did not fail at the intended COM publication boundary. See $LogName."
            }
            return $exitCode
        }
        if ($exitCode -ne 0) { throw "Debug build failed with exit code $exitCode. See $LogName." }
    } finally { $Build.Process.Dispose() }
}

function Test-Edition([string]$Edition) {
    $editionOutput = Join-Path $output $Edition
    $null = [IO.Directory]::CreateDirectory($editionOutput)
    $activations = [Collections.Generic.List[object]]::new()
    $clients = [Collections.Generic.List[object]]::new()
    $before = $null; $after = $null; $restored = $null; $build = $null; $old = $null; $cold = $null; $failedBuild = $null
    $windows = [DebugComWindows]::new()
    try {
        $running = @(Get-Process -Name 'WawiCallPro.App.Pro', 'WawiCallPro.App.FinOffice' -ErrorAction SilentlyContinue)
        if ($running.Count) { throw "Close running WawiCallPro instances before the test. PIDs: $($running.Id -join ', ')." }
        $before = Get-Registration $Edition
        $old = Start-ComClient 'before' 'x64' $before
        $held = $clients[0]
        $build = Start-DebugBuild Refresh
        Wait-ForProof { $build.Process.HasExited -or (Test-Path -LiteralPath (Join-Path $editionOutput 'building')) } 100 'the build gate'
        if ($build.Process.HasExited) { throw 'Debug build exited before it reached the gate. See build.log.' }
        (Start-ComClient 'during-build' 'x86' $before $old.Id).Dispose()
        [IO.File]::WriteAllText((Join-Path $editionOutput 'continue'), [DateTimeOffset]::UtcNow.ToString('o'))
        try { Finish-DebugBuild $build 'build.log' } finally { $build = $null }
        $after = Get-Registration $Edition
        if ($after -eq $before -or -not (Test-Path -LiteralPath $before -PathType Leaf)) { throw 'The registration did not switch or the previous generation disappeared.' }
        (Start-ComClient 'after-refresh' 'x86' $before $old.Id).Dispose()
        [IO.File]::WriteAllText(($held.Prefix + 'ping'), '')
        Wait-ForProof { Test-Path -LiteralPath ($held.Prefix + 'pong') } 10 'the original client status after refresh'
        if ((Get-Content -LiteralPath ($held.Prefix + 'pong') -Raw).Trim() -ne 'Ready') { throw 'The original COM reference did not remain usable.' }

        foreach ($client in $clients) { [IO.File]::WriteAllText(($client.Prefix + 'release'), '') }
        Wait-ForProof { $old.HasExited } 80 'natural exit of the original server'
        if ($old.ExitCode -ne 0) { throw 'The original server did not exit cleanly.' }

        $registeredAssembly = [IO.Path]::ChangeExtension($after, '.dll')
        $registeredHash = (Get-FileHash -LiteralPath $registeredAssembly).Hash
        $failedExitCode = Finish-DebugBuild (Start-DebugBuild Fail) 'failed-build.log' -ExpectFailure
        $afterFailure = Get-Registration $Edition
        $assemblyUnchanged = $afterFailure -eq $after -and (Get-FileHash -LiteralPath $registeredAssembly).Hash -eq $registeredHash
        $failedBuild = [ordered]@{
            ExitCode = $failedExitCode; ErrorCode = 'WAWICOM001'; RegisteredBefore = $after
            RegisteredAfter = $afterFailure; AssemblyUnchanged = $assemblyUnchanged
        }
        if (-not $assemblyUnchanged) { throw 'The failed build changed the registered COM generation.' }
        $cold = Start-ComClient 'cold-start-after-failed-build' 'x64' $afterFailure
        [IO.File]::WriteAllText(($clients[-1].Prefix + 'release'), '')
        Wait-ForProof { $cold.HasExited } 80 'natural exit of the cold-start server'
        if ($cold.ExitCode -ne 0) { throw 'The cold-start server did not exit cleanly.' }
        if ($windows.ShowEvents($old.Id) -or $windows.ShowEvents($cold.Id)) {
            throw 'The native window monitor detected a window during the test.'
        }
    } finally {
        try {
            foreach ($client in $clients) {
                try {
                    [IO.File]::WriteAllText(($client.Prefix + 'release'), '')
                    if (-not $client.Process.WaitForExit(5000)) { $client.Process.Kill(); $client.Process.WaitForExit() }
                } finally { $client.Process.Dispose() }
            }
            if ($build) {
                [IO.File]::WriteAllText((Join-Path $editionOutput 'continue'), '')
                Finish-DebugBuild $build 'build.log'
            }
        } finally {
            if ($old) { $old.Dispose() }
            if ($cold) { $cold.Dispose() }
            $windows.Dispose()
            try {
                if ($before -and (Get-Registration $Edition) -ne $before) {
                    Finish-DebugBuild (Start-DebugBuild Restore) 'restore.log'
                    $restored = Get-Registration $Edition
                }
            } finally {
                [ordered]@{
                    Edition = $Edition; Before = $before; Updated = $after; FailedBuild = $failedBuild; Restored = $restored
                    UnattributedWindowEvents = $windows.UnresolvedEvents; Activations = $activations.ToArray()
                } |
                    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $editionOutput 'results.json')
            }
        }
    }
}

$failure = $null
$editions = if ($ProductLine -eq 'All') { 'FinOffice', 'Pro' } else { ,$ProductLine }
try {
    Initialize-WindowObserver
    foreach ($edition in $editions) { Test-Edition $edition }
} catch {
    $failure = $_.Exception.Message
    throw
} finally {
    $pending = Join-Path $output 'results.pending'
    [ordered]@{ Editions = $editions; Error = $failure } | ConvertTo-Json | Set-Content -LiteralPath $pending
    [IO.File]::Move($pending, (Join-Path $output 'results.json'))
}
Write-Output "COM debug refresh passed. Results: $output"
