param(
    [ValidateSet('Pro','FinOffice')][string]$ProductLine='Pro',
    [ValidateSet('Debug','Release')][string]$Configuration='Debug',
    [string]$OutputDirectory,
    [switch]$RegistrationOnly,
    [switch]$FromDesktop
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $repository ".artifacts/com-deployment/$ProductLine/$([DateTime]::Now.ToString('yyyyMMdd-HHmmss'))" }
$output=[IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count) { throw 'Use a fresh evidence directory.' }
[IO.Directory]::CreateDirectory($output) | Out-Null
$principal=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    if ($FromDesktop) { throw 'HKCU activation requires the normal desktop token; no machine fallback.' }
    $arguments='-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -File "{0}" -ProductLine {1} -Configuration {2} -OutputDirectory "{3}" -FromDesktop' -f $PSCommandPath,$ProductLine,$Configuration,$output
    if ($RegistrationOnly) { $arguments+=' -RegistrationOnly' }
    $shell=New-Object -ComObject Shell.Application; $handle=0
    $desktop=$shell.Windows().FindWindowSW(0,0,8,[ref]$handle,1)
    if (-not $desktop) { throw 'Normal user desktop unavailable.' }
    $desktop.Document.Application.ShellExecute((Join-Path $PSHOME 'pwsh.exe'),$arguments,$repository,'open',0)
    $until=[DateTime]::UtcNow.AddSeconds(20)
    while (-not (Test-Path -LiteralPath (Join-Path $output 'bootstrap.json'))) {
        if ([DateTime]::UtcNow -gt $until) { throw 'Harness bootstrap timed out.' }; Start-Sleep -Milliseconds 60
    }
    $bootstrap=Get-Content -LiteralPath (Join-Path $output 'bootstrap.json') -Raw | ConvertFrom-Json
    $child=Get-Process -Id $bootstrap.Pid
    try {
        $until=[DateTime]::UtcNow.AddMinutes(8)
        while (-not $child.WaitForExit(1000)) { if ([DateTime]::UtcNow -gt $until) { throw 'Deployment harness timed out.' } }
        $proof=Get-Content -LiteralPath (Join-Path $output 'results.json') -Raw | ConvertFrom-Json
        if (-not $proof.Completed) { throw "Deployment verification failed: $($proof.Error). Evidence: $output" }
        Write-Output "Passed $($proof.Checks.Count) deployment checks. Evidence: $output"
    } finally { $child.Dispose() }
    return
}
@{Pid=$PID;Elevated=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'bootstrap.json')
$checks=[Collections.Generic.List[string]]::new(); $completed=$false; $failure=$null; $reserved=$false; $cleanup=$true
$views=@([Microsoft.Win32.RegistryView]::Registry64,[Microsoft.Win32.RegistryView]::Registry32)
$id=[Guid]::NewGuid(); $progId="WCP.p$($id.ToString('N'))"
$profile=Join-Path $output 'isolated profile'; $thin=Join-Path $output 'thin install with spaces'
[IO.Directory]::CreateDirectory($profile) | Out-Null; [IO.Directory]::CreateDirectory($thin) | Out-Null
@{ClassId=$id;ProgId=$progId} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $profile 'com-fixture.json')
$source=Join-Path $repository "WawiCallPro.App/bin/$Configuration/net11.0-windows10.0.19041.0"
$baseName="WawiCallPro.App.$ProductLine"; $exe=Join-Path $thin "$baseName.exe"
function Check([bool]$Value,[string]$Name) { if (-not $Value) { throw "Failed: $Name" }; $checks.Add($Name) }
function Read-Key($View,[string]$Path) {
    $root=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,$View)
    try { $key=$root.OpenSubKey("Software\Classes\$Path"); if (-not $key) { return $null }
        try { $values=[ordered]@{}; foreach ($name in @($key.GetValueNames() | Sort-Object)) { $values[$name]=$key.GetValue($name) }; return ,$values }
        finally { $key.Dispose() }
    } finally { $root.Dispose() }
}
function Invoke-App([string[]]$Arguments,[int]$Expected=0) {
    $start=[Diagnostics.ProcessStartInfo]::new($exe); $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    foreach ($arg in $Arguments) { $start.ArgumentList.Add($arg) }
    $watch=[Diagnostics.Stopwatch]::StartNew(); $process=[Diagnostics.Process]::Start($start)
    try { if (-not $process.WaitForExit(10000)) { $process.Kill($true); throw 'Registration process exceeded its fast budget.' }
        if ($process.ExitCode -ne $Expected) { throw "Registration exit $($process.ExitCode), expected $Expected." }
        Check ($watch.Elapsed.TotalSeconds -lt 10) 'Registration command exits within ten seconds without a host or UI'
    } finally { $process.Dispose() }
}
$clientProcesses=[Collections.Generic.List[Diagnostics.Process]]::new()
function Wait-For([scriptblock]$Condition,[string]$Name) {
    $until=[DateTime]::UtcNow.AddSeconds(50)
    while (-not (& $Condition)) { if ([DateTime]::UtcNow -gt $until) { throw "Timed out: $Name" }; Start-Sleep -Milliseconds 80 }
}
function Json-File([string]$Path) { try { return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json } catch { return $null } }
function Protected-Registration {
    $keys=@('CLSID\{B24140A1-859F-4A8F-97F6-962668F0D20D}','CLSID\{F078BC10-F918-49FF-99C7-C5A2B44CFD5E}',
        'WawiCallPro.Pro.Application.1','WawiCallPro.FinOffice.Application.1','TypeLib\{7155BD3A-6580-4A46-A034-1E4F55CFA047}',
        'Interface\{710E28F8-6286-4F6E-8898-C88A3F872FC1}','Interface\{9A897EBA-795D-4FDD-9417-97B156287D22}',
        'Interface\{643E9245-55FF-466E-A7E7-57B18E9D3FC6}','Interface\{68DA705A-6F68-49F7-8C6A-B6CC3F29A4FE}')
    $records=[Collections.Generic.List[string]]::new()
    foreach ($hive in @([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryHive]::LocalMachine)) {
        foreach ($view in $views) {
            $root=[Microsoft.Win32.RegistryKey]::OpenBaseKey($hive,$view)
            try {
                function Record-Tree([string]$Path) {
                    $node=$root.OpenSubKey("Software\Classes\$Path")
                    if (-not $node) { $records.Add("$hive/$view/$Path/absent"); return }
                    try {
                        foreach ($name in @($node.GetValueNames() | Sort-Object)) { $records.Add("$hive/$view/$Path/$name=" + ($node.GetValue($name) | ConvertTo-Json -Compress)) }
                        foreach ($subkey in @($node.GetSubKeyNames() | Sort-Object)) { Record-Tree "$Path\$subkey" }
                    } finally { $node.Dispose() }
                }
                foreach ($key in $keys) { Record-Tree $key }
            } finally { $root.Dispose() }
        }
    }
    return $records.ToArray() -join "`n"
}
$protectedBefore=Protected-Registration
try {
    foreach ($view in $views) { Check ($null -eq (Read-Key $view "CLSID\{$id}") -and $null -eq (Read-Key $view $progId)) 'Random fixture identity did not exist before this run' }
    $reserved=$true
    foreach ($file in @("$baseName.exe","$baseName.dll","$baseName.deps.json","$baseName.runtimeconfig.json",'Velopack.dll')) {
        Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $thin $file)
    }
    '{invalid JSON' | Set-Content -LiteralPath (Join-Path $thin 'appsettings.json')
    Invoke-App @('--com-fixture',$profile,'/regserver')
    $before=@($views | ForEach-Object { Read-Key $_ "CLSID\{$id}\LocalServer32" }) | ConvertTo-Json -Compress
    Invoke-App @('--com-fixture',$profile,'/regserver')
    Check ($before -eq (@($views | ForEach-Object { Read-Key $_ "CLSID\{$id}\LocalServer32" }) | ConvertTo-Json -Compress)) 'Repeated registration is idempotent'
    foreach ($view in $views) {
        $server=Read-Key $view "CLSID\{$id}\LocalServer32"
        Check ($server['ServerExecutable'] -eq $exe -and $server[''] -eq ('"{0}" --com-fixture "{1}"' -f $exe,$profile)) 'Both views contain the exact quoted executable and fixture path'
        Check ((Read-Key $view "$progId\CLSID")[''] -eq $id.ToString('B')) 'ProgID resolves to its own fixture CLSID'
    }
    Check (-not (Test-Path -LiteralPath (Join-Path $thin 'logs')) -and -not (Test-Path -LiteralPath (Join-Path $profile 'com-runtime.json'))) 'Thin registration ignores invalid appsettings and never starts application services'
    Invoke-App @('/regserver','/machine') 2
    Invoke-App @('/regserver','/tlb') 2
    Invoke-App @('--com-fixture',$profile,'/unregserver')
    Invoke-App @('--com-fixture',$profile,'/unregserver')
    foreach ($view in $views) { Check ($null -eq (Read-Key $view "CLSID\{$id}") -and $null -eq (Read-Key $view $progId)) 'Repeated unregister removes only the fixture registration' }
    if (-not $RegistrationOnly) {
        $full=Join-Path $output 'full install with spaces'
        [IO.Directory]::CreateDirectory($full) | Out-Null
        Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $full -Recurse -Force
        $exe=Join-Path $full "$baseName.exe"
        @{formatVersion=6;settings=@{user=@{activePluginId='WawiCallPro.Plugins.Dummy'}}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $profile 'connection.local.json')
        Invoke-App @('--com-fixture',$profile,'/regserver')
        $sample=Join-Path $output 'automation-example-fixture.js'
        $sampleText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ComExamples/automation.js')).Replace('WawiCallPro.Pro.Application.1',$progId).Replace('WawiCallPro.FinOffice.Application.1',$progId)
        if ($sampleText.ToCharArray() | Where-Object { [int]$_ -gt 127 }) { throw 'The WSH example must remain ASCII with Unicode escapes.' }
        [IO.File]::WriteAllText($sample,$sampleText,[Text.ASCIIEncoding]::new())
        foreach ($bits in @('x86','x64')) {
            foreach ($action in @('status','call')) {
                $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $env:WINDIR $(if ($bits -eq 'x86') {'SysWOW64/cscript.exe'} else {'System32/cscript.exe'})))
                $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
                foreach ($arg in @('//nologo','//E:JScript',$sample,$ProductLine,$action)) { $start.ArgumentList.Add($arg) }
                if ($action -eq 'call') { $start.ArgumentList.Add('invalid-guid') }
                $process=[Diagnostics.Process]::Start($start); $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
                try {
                    if (-not $process.WaitForExit(50000)) { $process.Kill($true); throw 'Example client did not finish.' }
                    $text=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
                    [IO.File]::WriteAllText((Join-Path $output "sample-$bits-$action.log"),$text)
                    if ($action -eq 'status') { Check ($process.ExitCode -eq 0 -and $text.Trim().StartsWith($ProductLine+' / ')) "$bits shipped JavaScript status example succeeds with an isolated ProgID" }
                    else { Check ($process.ExitCode -eq 1 -and $text.Contains('80070057')) "$bits shipped JavaScript example reports the expected COM HRESULT for invalid input" }
                } finally { $process.Dispose() }
            }
        }
        foreach ($bits in @('x86','x64')) {
            $prefix=Join-Path $profile "$bits-"
            $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $env:WINDIR $(if ($bits -eq 'x86') {'SysWOW64/cscript.exe'} else {'System32/cscript.exe'})))
            $start.UseShellExecute=$false; $start.CreateNoWindow=$true
            foreach ($arg in @('//nologo','//E:JScript',(Join-Path $PSScriptRoot 'ComPrototype/lifetime.js'),$progId,$prefix)) { $start.ArgumentList.Add($arg) }
            $client=[Diagnostics.Process]::Start($start); $clientProcesses.Add($client)
            Wait-For { if (Test-Path -LiteralPath ($prefix+'failure.json')) { throw (Get-Content -LiteralPath ($prefix+'failure.json') -Raw) }; Test-Path -LiteralPath ($prefix+'ready.json') } 'native activation'
            $ready=Json-File ($prefix+'ready.json')
            Check ($ready.State -eq 'Ready' -and $ready.ProductLine -eq $ProductLine) "$bits ActiveXObject resolves the copied edition through HKCU without a registered TLB"
            [IO.File]::WriteAllText(($prefix+'release'),'')
            if (-not $client.WaitForExit(10000) -or $client.ExitCode -ne 0) { throw 'JScript client failed to release.' }
        }
        Wait-For { $r=Json-File (Join-Path $profile 'com-runtime.json'); $r -and $r.Forms -eq 0 -and $r.ServerLocks -eq 0 } 'windowless host after client release'
        $runtime=Json-File (Join-Path $profile 'com-runtime.json')
        $owner=Get-Process -Id $runtime.Pid
        try { [IO.File]::WriteAllText((Join-Path $profile 'stop.request'),''); if (-not $owner.WaitForExit(30000)) { throw 'Copied host did not shut down cleanly.' } }
        finally { $owner.Dispose() }
        $stopped=Json-File (Join-Path $profile 'com-stopped.json')
        Check ($stopped.Success -and $stopped.Revoked) 'Copied application completes its regular shutdown without opening forms'
        Invoke-App @('--com-fixture',$profile,'/unregserver')
    }
    Check ($protectedBefore -eq (Protected-Registration)) 'Product registrations and all contract TypeLib/Interface metadata in HKCU/HKLM stayed unchanged in both views'
    $completed=$true
} catch { $failure=$_.Exception.Message; throw }
finally {
    foreach ($client in $clientProcesses) { try { if (-not $client.HasExited) { $client.Kill($true); $client.WaitForExit() } } finally { $client.Dispose() } }
    # Use the real ownership-aware removal, never recursive deletion of a foreign registration.
    if ($reserved) {
        try {
            $exists=@($views | Where-Object { $null -ne (Read-Key $_ "CLSID\{$id}") -or $null -ne (Read-Key $_ $progId) }).Count -gt 0
            if ($exists -and (Test-Path -LiteralPath $exe)) { Invoke-App @('--com-fixture',$profile,'/unregserver') }
            foreach ($view in $views) {
                if ($null -ne (Read-Key $view "CLSID\{$id}") -or $null -ne (Read-Key $view $progId)) { $cleanup=$false }
            }
        } catch { $cleanup=$false }
    }
    if (-not $cleanup) { $completed=$false; $failure='Fixture registration cleanup could not be confirmed.' }
    @{Completed=$completed;Error=$failure;Checks=$checks.ToArray();Edition=$ProductLine;Elevated=$false;RegistryCleanupConfirmed=$cleanup} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'results.json')
}
if (-not $completed) { throw $failure }
