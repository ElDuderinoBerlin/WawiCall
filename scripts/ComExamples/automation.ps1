param(
    [ValidateSet('Pro','FinOffice')][string]$ProductLine = 'Pro',
    [ValidateSet('smoke','status','views','show','view','companies','customer','supplier','connect','dial','hangup','call')][string]$Action = 'smoke',
    [ValidateSet('Auto','32','64')][string]$ClientBitness = 'Auto',
    [string]$ViewId,
    [string]$ContextJson,
    [string]$CompanyKey,
    [string]$PartyNumber,
    [string]$Destination,
    [string]$RequestId,
    [string]$CallId
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'COM activation uses a per-user registration (HKCU). Open PowerShell without "Run as administrator" and repeat this command.'
    }
} finally { $identity.Dispose() }
# WSH provides native late binding for the COM return objects on both Windows PowerShell 5.1 and PowerShell 7.
# No registration, type library, credentials, server shutdown or automatic telephone action.
$bits = if ($ClientBitness -eq 'Auto') { if ([Environment]::Is64BitOperatingSystem) { '64' } else { '32' } } else { $ClientBitness }
if ($bits -eq '64' -and -not [Environment]::Is64BitOperatingSystem) { throw 'A 64-bit WSH host requires 64-bit Windows.' }
$folder = if ($bits -eq '32' -and [Environment]::Is64BitOperatingSystem) { 'SysWOW64' }
    elseif ($bits -eq '64' -and -not [Environment]::Is64BitProcess) { 'Sysnative' } else { 'System32' }
$hostPath = Join-Path $env:WINDIR "$folder\cscript.exe"
if (-not (Test-Path -LiteralPath $hostPath -PathType Leaf)) { throw 'The selected Windows Script Host is unavailable.' }
$arguments = @($ProductLine, $Action)
$contextFile = $null
$contextPayload = $null
switch ($Action) {
    'view' {
        if ([string]::IsNullOrWhiteSpace($ViewId)) { throw 'ViewId is required.' }
        if ($ContextJson -and $CompanyKey) { throw 'Choose ContextJson or CompanyKey.' }
        $arguments += $ViewId
        if ($CompanyKey) { $contextPayload = @{companyKey=$CompanyKey} | ConvertTo-Json -Compress }
        elseif ($ContextJson) { $null = $ContextJson | ConvertFrom-Json; $contextPayload = $ContextJson }
        if ($contextPayload) {
            if ($contextPayload.Length -gt 16384) { throw 'ContextJson is too large.' }
            $contextFile = Join-Path ([IO.Path]::GetTempPath()) ('WawiCallPro.Com.context-'+[guid]::NewGuid().ToString('N')+'.tmp')
            $arguments += '@'+$contextFile
        }
    }
    { $_ -in 'customer','supplier' } {
        if ([string]::IsNullOrWhiteSpace($CompanyKey) -or [string]::IsNullOrWhiteSpace($PartyNumber)) { throw 'CompanyKey and PartyNumber are required.' }
        $arguments += $CompanyKey, $PartyNumber
    }
    'dial' {
        if ([string]::IsNullOrWhiteSpace($CompanyKey) -or [string]::IsNullOrWhiteSpace($Destination)) { throw 'CompanyKey and Destination are required.' }
        if (-not $RequestId) { $RequestId = [guid]::NewGuid().ToString('D') }
        $requestGuid = [guid]::Empty
        if (-not [guid]::TryParse($RequestId, [ref]$requestGuid) -or $requestGuid -eq [guid]::Empty) { throw 'RequestId must be a non-empty GUID.' }
        Write-Output "RequestId=$RequestId"
        $arguments += $CompanyKey, $Destination, $RequestId
    }
    { $_ -in 'hangup','call' } {
        $callGuid = [guid]::Empty
        if (-not [guid]::TryParse($CallId, [ref]$callGuid) -or $callGuid -eq [guid]::Empty) { throw 'CallId must be a non-empty GUID.' }
        $arguments += $CallId
    }
}
foreach ($argument in $arguments) {
    if ($argument.Length -gt 2048 -or $argument -match '[\x00-\x1f"]') { throw 'Arguments must be bounded single-line values without quotes or control characters.' }
}
try {
    if ($contextFile) {
        # A private Unicode file preserves JSON quotes/Unicode with Windows PowerShell's older argument transport.
        $stream = [IO.File]::Open($contextFile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $writer = [IO.StreamWriter]::new($stream, [Text.Encoding]::Unicode); try { $writer.Write($contextPayload) } finally { $writer.Dispose() } }
        finally { $stream.Dispose() }
    }
    & $hostPath //nologo (Join-Path $PSScriptRoot 'automation.vbs') @arguments
    if ($LASTEXITCODE -ne 0) { throw "COM test failed (exit code $LASTEXITCODE). No action is retried automatically." }
}
finally { if ($contextFile -and (Test-Path -LiteralPath $contextFile)) { Remove-Item -LiteralPath $contextFile -Force } }
