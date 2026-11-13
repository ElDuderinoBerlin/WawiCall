param(
    [ValidateSet('Pro', 'FinOffice')][string]$ProductLine = 'FinOffice',
    [switch]$AllowVisibleWindows
)
$ErrorActionPreference = 'Stop'
if (!$AllowVisibleWindows) { throw 'Die native Prüfung öffnet Fenster und wechselt den Vordergrund. -AllowVisibleWindows nur nach Absprache verwenden.' }
$repository = Split-Path $PSScriptRoot -Parent
$executable = Join-Path $repository "WawiCallPro.App/bin/Debug/net11.0-windows10.0.19041.0/WawiCallPro.App.$ProductLine.exe"
if (!(Test-Path -LiteralPath $executable)) { throw "Die aktive Debug-Ausgabe für $ProductLine fehlt." }
$run = Join-Path $repository ('.artifacts/tray-native/' + $ProductLine + '/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
$profile = Join-Path $run 'profile'
New-Item -ItemType Directory -Path $profile -Force | Out-Null
@{ formatVersion = 6; settings = @{ user = @{ activePluginId = 'WawiCallPro.Plugins.Dummy' } } } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $profile 'connection.local.json') -Encoding utf8NoBOM
$start = [Diagnostics.ProcessStartInfo]::new($executable)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Normal
$start.WorkingDirectory = Split-Path $executable -Parent
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.Environment['WAWICALLPRO_CONFIG_DIR'] = $profile
$start.Environment['LX_UI_AUDIT_SURFACES'] = 'TrayNative'
$start.ArgumentList.Add('--ui-audit')
$start.ArgumentList.Add($run)
$process = [Diagnostics.Process]::Start($start)
try {
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (!(Test-Path -LiteralPath (Join-Path $run 'activation-requested')) -and !$process.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 50 }
    if (Test-Path -LiteralPath (Join-Path $run 'activation-requested')) {
        $window = [long](Get-Content -LiteralPath (Join-Path $run 'activation-requested') -Raw)
        $activation = [Diagnostics.ProcessStartInfo]::new((Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'))
        $activation.UseShellExecute = $false; $activation.CreateNoWindow = $true; $activation.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
        $activation.RedirectStandardError = $true
        foreach ($argument in @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Invoke-NativeTrayActivation.ps1'), '-WindowHandle', $window, '-OwnerProcessId', $process.Id)) { $activation.ArgumentList.Add([string]$argument) }
        $helper = [Diagnostics.Process]::Start($activation)
        try {
            $activationErrors = $helper.StandardError.ReadToEndAsync()
            if (!$helper.WaitForExit(10000)) { $helper.Kill(); throw 'Die Aktivierung des Prüffensters hat die Frist überschritten.' }
            $activationErrors.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $run 'activation.stderr.log')
            if ($helper.ExitCode -ne 0) { throw 'Das eigene Prüffenster konnte nicht aktiviert werden.' }
        }
        finally { $helper.Dispose() }
        Set-Content -LiteralPath (Join-Path $run 'activated') -Value 'UIAutomation'
    }
    if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Die native Prüfung hat die Frist überschritten.' }
    $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $run 'stdout.log')
    $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $run 'stderr.log')
    if ($process.ExitCode -ne 0) { throw "Native Prüfung fehlgeschlagen (Exit $($process.ExitCode)). Nachweise: $run" }
    Write-Output "Native Prüfung bestanden: $run"
    Get-Content -LiteralPath (Join-Path $run 'native-tray.json')
}
finally { if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }; $process.Dispose() }
