param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [ValidateSet('Pro', 'FinOffice')][string]$LexwareProductLine = 'Pro',
    [string]$OutputDirectory,
    [switch]$FromDesktop
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repository ".artifacts/com-prototype/$LexwareProductLine/$([DateTime]::Now.ToString('yyyyMMdd-HHmmss'))" }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count -gt 0) { throw 'Use a new output directory for each native run.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    # Elevated COM clients ignore per-user registration. Do not compensate with HKLM changes.
    if ($FromDesktop) { throw 'The desktop launcher did not provide a normal user context.' }
    $powerShell = Join-Path $PSHOME 'pwsh.exe'
    $arguments = '-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -File "{0}" -Configuration {1} -LexwareProductLine {2} -OutputDirectory "{3}" -FromDesktop' -f $PSCommandPath, $Configuration, $LexwareProductLine, $output
    $shell = New-Object -ComObject Shell.Application
    $windows = $shell.Windows()
    $desktopHandle = 0
    $desktop = $windows.FindWindowSW(0, 0, 8, [ref]$desktopHandle, 1)
    if (-not $desktop) { throw 'A normal user desktop is required for HKCU activation tests.' }
    $desktop.Document.Application.ShellExecute($powerShell, $arguments, $repository, 'open', 0)
    $bootstrapFile = Join-Path $output 'bootstrap.json'
    $limit = [DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath $bootstrapFile)) {
        if ([DateTime]::UtcNow -gt $limit) { throw 'Normal user fixture did not start.' }
        Start-Sleep -Milliseconds 50
    }
    $bootstrap = Get-Content -LiteralPath $bootstrapFile -Raw | ConvertFrom-Json
    $child = Get-Process -Id $bootstrap.ProcessId
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(180)
        while (-not $child.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -gt $deadline) { $child.Kill($true); throw 'Unelevated fixture run timed out.' }
        }
        $result = Get-Content -LiteralPath (Join-Path $output 'results.json') -Raw | ConvertFrom-Json
        if ($null -ne $child.ExitCode -and $child.ExitCode -ne 0) { throw "Unelevated fixture run failed: $($result.Error). Inspect $output" }
        if (-not $result.Completed) { throw 'Unelevated fixture run incomplete.' }
        Write-Output "Passed $($result.Checks.Count) checks in the normal user context. Results: $output"
    } finally { $child.Dispose() }
    return
}
[pscustomobject]@{ ProcessId = $PID; Elevated = $false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'bootstrap.json')
$executable = Join-Path $repository "Tools/ComPrototype.TestWorker/bin/$Configuration/$LexwareProductLine/net11.0-windows10.0.19041.0/ComPrototype.TestWorker.exe"
if (-not (Test-Path -LiteralPath $executable)) { throw "Build the fixture first: $executable" }
$scriptFile = Join-Path $PSScriptRoot 'ComPrototype/client.js'
$passed = [Collections.Generic.List[object]]::new()
$fixtures = [Collections.Generic.List[object]]::new()
$children = [Collections.Generic.List[Diagnostics.Process]]::new()
$completed = $false
$failureText = $null

function Assert-Case([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "Failed: $Name" }
    $passed.Add([pscustomobject]@{ Name = $Name; Passed = $true })
    Write-Host "PASS $Name"
}
function Start-FixtureProcess([string]$FileName, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($FileName)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $children.Add($process)
    return $process
}
function Wait-File([string]$Path) {
    $limit = [DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath $Path)) {
        if ([DateTime]::UtcNow -gt $limit) { throw "Fixture file timed out: $Path" }
        Start-Sleep -Milliseconds 50
    }
}
function New-Fixture([string]$Name, [int]$Delay = 0, [bool]$Legacy = $false) {
    $classId = [Guid]::NewGuid()
    $profile = Join-Path $output $Name
    [IO.Directory]::CreateDirectory($profile) | Out-Null
    $fixture = [pscustomobject]@{ Profile = $profile; ClassId = $classId; ProgId = "WCP.p$($classId.ToString('N'))"; StartupDelayMs = $Delay; Legacy = $Legacy; RegisteredViews = [Collections.Generic.List[object]]::new() }
    $fixture | Select-Object Profile,ClassId,ProgId,StartupDelayMs,Legacy | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $profile 'fixture.json')
    $fixtures.Add($fixture)
    foreach ($view in @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32)) {
        $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, $view)
        try {
            $classKey = "Software\Classes\CLSID\{$classId}"
            $progKey = "Software\Classes\$($fixture.ProgId)"
            $existingClass = $root.OpenSubKey($classKey)
            $existingProg = $root.OpenSubKey($progKey)
            try {
                if (($existingClass -or $existingProg) -and -not ($fixture.RegisteredViews.Count -gt 0 -and
                    (-not $existingClass -or $existingClass.GetValue('WawiComPrototypeOwner') -eq $profile) -and
                    (-not $existingProg -or $existingProg.GetValue('WawiComPrototypeOwner') -eq $profile))) {
                    throw 'Random fixture registration collided with existing keys.'
                }
            }
            finally { if ($existingClass) { $existingClass.Dispose() }; if ($existingProg) { $existingProg.Dispose() } }
            $fixture.RegisteredViews.Add($view)
            $ownedClass = $root.CreateSubKey($classKey)
            try { $ownedClass.SetValue('WawiComPrototypeOwner', $profile) } finally { $ownedClass.Dispose() }
            $server = $root.CreateSubKey("$classKey\LocalServer32")
            try {
                $server.SetValue('', ('"{0}" --profile "{1}"' -f $executable, $profile))
                $server.SetValue('ServerExecutable', $executable)
            } finally { $server.Dispose() }
            $ownedProg = $root.CreateSubKey($progKey)
            try { $ownedProg.SetValue('WawiComPrototypeOwner', $profile) } finally { $ownedProg.Dispose() }
            $prog = $root.CreateSubKey("$progKey\CLSID")
            try { $prog.SetValue('', $classId.ToString('B')) } finally { $prog.Dispose() }
        } finally { $root.Dispose() }
    }
    return $fixture
}
function Start-Client($Fixture, [string]$Name, [string]$Bitness = 'x64', [string]$Mode = 'inspect', [int]$Hold = 0, [string]$Barrier = '') {
    $client = if ($Bitness -eq 'x86') { Join-Path $env:WINDIR 'SysWOW64/cscript.exe' } else { Join-Path $env:WINDIR 'System32/cscript.exe' }
    $result = Join-Path $Fixture.Profile "$Name.json"
    $arguments = @('//nologo', '//E:JScript', $scriptFile, $Fixture.ProgId, $Mode, $result, [string]$Hold)
    if ($Barrier) { $arguments += $Barrier }
    return [pscustomobject]@{ Process = (Start-FixtureProcess $client $arguments); Result = $result }
}
function Read-Client($Client) {
    Wait-File $Client.Result
    $result = Get-Content -LiteralPath $Client.Result -Raw | ConvertFrom-Json
    if ($result.PSObject.Properties.Name -contains 'Failure') { throw ($result | ConvertTo-Json -Compress) }
    Assert-Case ($result.VisibleWindows -eq 0) 'COM activation has no visible windows'
    Assert-Case ($result.ErrorNumber -eq -2147024809 -and $result.ErrorDescription -eq 'Synthetic prototype failure.') 'HRESULT and IErrorInfo reach JScript'
    return $result
}
function Wait-Client($Client) {
    if (-not $Client.Process.WaitForExit(20000)) { throw 'JScript client timed out.' }
    if ($Client.Process.ExitCode -ne 0) { throw "JScript failed: $($Client.Result)" }
}
function Wait-HostExit($Fixture, [int]$Timeout = 12000) {
    Wait-File (Join-Path $Fixture.Profile 'factory.json')
    $identity = Get-Content -LiteralPath (Join-Path $Fixture.Profile 'factory.json') -Raw | ConvertFrom-Json
    $process = Get-Process -Id $identity.Pid -ErrorAction SilentlyContinue
    if ($process) {
        try {
            if (-not $process.WaitForExit($Timeout)) { throw 'Prototype host did not stop.' }
        } finally { $process.Dispose() }
    }
    Wait-File (Join-Path $Fixture.Profile 'stopped.json')
    Wait-File (Join-Path $Fixture.Profile 'exit.json')
    $exitResult = Get-Content -LiteralPath (Join-Path $Fixture.Profile 'exit.json') -Raw | ConvertFrom-Json
    if ($exitResult.ExitCode -ne 0 -or (Test-Path -LiteralPath (Join-Path $Fixture.Profile 'failure.json'))) { throw 'Prototype host failed.' }
    Assert-Case $true 'Host exited through its shutdown path'
}
function Assert-HostAlive($Fixture, [int]$ProcessId, [string]$Reason) {
    Start-Sleep -Milliseconds 2600
    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    try { Assert-Case ($null -ne $process -and -not (Test-Path -LiteralPath (Join-Path $Fixture.Profile 'stopped.json'))) $Reason }
    finally { if ($process) { $process.Dispose() } }
}

try {
    $checks = Join-Path $output 'lifetime-checks.json'
    $unit = Start-FixtureProcess $executable @('--self-test', $checks)
    if (-not $unit.WaitForExit(10000) -or $unit.ExitCode -ne 0) { throw 'Lifetime checks failed.' }
    $unitResult = Get-Content -LiteralPath $checks -Raw | ConvertFrom-Json
    foreach ($name in $unitResult.Passed) { Assert-Case $true $name }

    foreach ($bits in @('x64', 'x86')) {
        $fixture = New-Fixture "cold-$bits"
        $client = Start-Client $fixture 'client' $bits
        $result = Read-Client $client
        Assert-Case ($result.DependencyId -ne 'legacy' -and $result.State -eq 'Ready') "DI activation from $bits JScript"
        $factory = Get-Content -LiteralPath (Join-Path $fixture.Profile 'factory.json') -Raw | ConvertFrom-Json
        Assert-Case ($factory.UiThread -eq $result.ThreadId -and $factory.Apartment -eq 'STA') 'COM uses the WinForms STA'
        Wait-Client $client
        Wait-HostExit $fixture
        if ($bits -eq 'x64') {
            foreach ($name in @('factory.json', 'ready.json', 'stopped.json', 'exit.json')) { Remove-Item -LiteralPath (Join-Path $fixture.Profile $name) }
            $restarted = Start-Client $fixture 'restarted' $bits
            $restartedResult = Read-Client $restarted
            Assert-Case ($restartedResult.ProcessId -ne $result.ProcessId) 'Same CLSID activates a fresh host after clean idle shutdown'
            Wait-Client $restarted; Wait-HostExit $fixture
        }
    }

    $fixture = New-Fixture 'simultaneous-cold'
    $barrier = Join-Path $fixture.Profile 'start.barrier'
    $first = Start-Client $fixture 'first' 'x64' 'inspect' 3000 $barrier
    $second = Start-Client $fixture 'second' 'x86' 'inspect' 3000 $barrier
    [IO.File]::WriteAllText($barrier, '')
    $a = Read-Client $first; $b = Read-Client $second
    Assert-Case ($a.ProcessId -eq $b.ProcessId -and $a.DependencyId -eq $b.DependencyId) 'Concurrent cold activation shares one host and injected singleton'
    Wait-Client $first; Wait-Client $second; Wait-HostExit $fixture

    $fixture = New-Fixture 'normal-start' 4500
    $manual = Start-FixtureProcess $executable @('--profile', $fixture.Profile, '--normal')
    Wait-File (Join-Path $fixture.Profile 'factory.json')
    $client = Start-Client $fixture 'client' 'x64' 'ready'
    $result = Read-Client $client
    Assert-Case ($result.ProcessId -eq $manual.Id -and $result.InitialState -eq 'Starting' -and $result.State -eq 'Ready') 'COM joins a normal start while slow initialization is still running'
    Wait-Client $client
    Assert-HostAlive $fixture $manual.Id 'User ownership survives the last client'
    [IO.File]::WriteAllText((Join-Path $fixture.Profile 'user-off.request'), '')
    Wait-HostExit $fixture

    foreach ($owner in @('user', 'call')) {
        $fixture = New-Fixture "$owner-ownership"
        $client = Start-Client $fixture 'client' 'x64' $owner
        $result = Read-Client $client; Wait-Client $client
        Assert-HostAlive $fixture $result.ProcessId "$owner ownership survives session close"
        [IO.File]::WriteAllText((Join-Path $fixture.Profile "$owner-off.request"), '')
        Wait-HostExit $fixture
    }

    $fixture = New-Fixture 'released-object'
    $client = Start-Client $fixture 'client' 'x64' 'abandon'
    $null = Read-Client $client; Wait-Client $client
    [IO.File]::WriteAllText((Join-Path $fixture.Profile 'collect.request'), '')
    Wait-File (Join-Path $fixture.Profile 'collected.json')
    $collected = Get-Content -LiteralPath (Join-Path $fixture.Profile 'collected.json') -Raw | ConvertFrom-Json
    Assert-Case ($collected.Clients -eq 0) 'Released CCW fallback frees its lease after explicit test GC'
    Wait-HostExit $fixture

    $fixture = New-Fixture 'no-client'
    $null = Start-FixtureProcess $executable @('--profile', $fixture.Profile)
    Wait-HostExit $fixture

    foreach ($bits in @('x64', 'x86')) {
        $fixture = New-Fixture "quantarix-legacy-$bits" 0 $true
        $client = Start-Client $fixture 'client' $bits
        $result = Read-Client $client; Wait-Client $client
        Assert-Case ($result.DependencyId -eq 'legacy') "Unmodified Quantarix LocalServer/ClassFactory/ComBase works from $bits JScript"
        Wait-HostExit $fixture
    }
    $completed = $true
} catch {
    $failureText = $_.Exception.Message
    throw
} finally {
    # Only fixture-owned processes are stopped. Never touch the real App, SIP, Outlook or Lexware processes.
    foreach ($process in $children) {
        try { if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit(5000) | Out-Null } }
        finally { $process.Dispose() }
    }
    foreach ($fixture in $fixtures) {
        foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name='ComPrototype.TestWorker.exe'" | Where-Object { $_.ExecutablePath -eq $executable -and $_.CommandLine -and $_.CommandLine.Contains($fixture.Profile) })) {
            Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
        }
        foreach ($view in $fixture.RegisteredViews) {
            $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser, $view)
            try {
                $root.DeleteSubKeyTree("Software\Classes\CLSID\{$($fixture.ClassId)}", $false)
                $root.DeleteSubKeyTree("Software\Classes\$($fixture.ProgId)", $false)
                foreach ($key in @("Software\Classes\CLSID\{$($fixture.ClassId)}", "Software\Classes\$($fixture.ProgId)")) {
                    $remaining = $root.OpenSubKey($key)
                    if ($remaining) { $remaining.Dispose(); throw 'Fixture registration cleanup failed.' }
                }
            } finally { $root.Dispose() }
        }
    }
    [pscustomobject]@{ Completed = $completed; Error = $failureText; RegistryCleanupConfirmed = $true; Elevated = $false; Configuration = $Configuration; Edition = $LexwareProductLine; Executable = $executable; DllSha256 = (Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($executable, '.dll')) -Algorithm SHA256).Hash; Checks = $passed.ToArray(); Fixtures = $fixtures.Count } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'results.json')
}
Write-Output "Passed $($passed.Count) checks; isolated fixture registrations removed. Results: $output"
