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
    [string]$CallId,
    [Parameter(DontShow)][string]$DesktopRequest
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-DesktopClient($ClientParameters, [string]$UserSid) {
    $shell = New-Object -ComObject Shell.Application
    $desktopHandle = 0
    $desktop = $shell.Windows().FindWindowSW(0, 0, 8, [ref]$desktopHandle, 1)
    if (-not $desktop) { throw 'The Windows desktop is unavailable. Run the command in a normal PowerShell in the same user session.' }

    $exchange = Join-Path ([IO.Path]::GetTempPath()) ('WawiCallPro.Com.desktop-' + [guid]::NewGuid().ToString('N'))
    $requestPath = Join-Path $exchange 'request.json'
    $resultPath = Join-Path $exchange 'result.json'
    $powerShellName = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh.exe' } else { 'powershell.exe' }
    $launchArguments = '-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -File "{0}" -DesktopRequest "{1}"' -f $PSCommandPath, $requestPath
    $requestJson = @{ UserSid = $UserSid; Parameters = $ClientParameters } | ConvertTo-Json -Compress

    $null = [IO.Directory]::CreateDirectory($exchange)
    try {
        [IO.File]::WriteAllText($requestPath, $requestJson)
        $desktop.Document.Application.ShellExecute((Join-Path $PSHOME $powerShellName), $launchArguments, $PWD.Path, 'open', 0)
        $timer = [Diagnostics.Stopwatch]::StartNew()
        while (-not [IO.File]::Exists($resultPath)) {
            if ($timer.Elapsed.TotalSeconds -ge 120) { throw 'The desktop client did not return a result. Check the app before repeating the command.' }
            Start-Sleep -Milliseconds 100
        }

        $result = [IO.File]::ReadAllText($resultPath) | ConvertFrom-Json
        $result.Lines | Write-Output
        if ($result.Error) { throw $result.Error }
    } finally {
        Remove-Item -LiteralPath $requestPath, $resultPath, "$resultPath.tmp" -ErrorAction SilentlyContinue
        try { [IO.Directory]::Delete($exchange) } catch [IO.IOException] { } catch [UnauthorizedAccessException] { }
    }
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $isElevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    $userSid = $identity.User.Value
} finally { $identity.Dispose() }

if ($DesktopRequest) {
    if ($isElevated) { throw 'The desktop client requires a normal Windows user token.' }
    $request = [IO.File]::ReadAllText($DesktopRequest) | ConvertFrom-Json
    if ($request.UserSid -ne $userSid) { throw 'The desktop client requires the same Windows account as the calling PowerShell.' }

    $clientParameters = @{}
    foreach ($parameter in $request.Parameters.PSObject.Properties) {
        $clientParameters[$parameter.Name] = $parameter.Value
    }
    $lines = [Collections.Generic.List[string]]::new()
    $errorMessage = $null
    try {
        & $PSCommandPath @clientParameters | ForEach-Object { $lines.Add([string]$_) }
    } catch {
        $errorMessage = $_.Exception.Message
    }
    $resultPath = Join-Path (Split-Path -Parent $DesktopRequest) 'result.json'
    $resultJson = @{ Lines = $lines.ToArray(); Error = $errorMessage } | ConvertTo-Json -Compress
    [IO.File]::WriteAllText("$resultPath.tmp", $resultJson)
    [IO.File]::Move("$resultPath.tmp", $resultPath)
    return
}

if ($isElevated) {
    Invoke-DesktopClient $PSBoundParameters $userSid
    return
}

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
