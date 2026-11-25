[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Pro', 'FinOffice')][string]$ProductLine,
    [switch]$PrepareOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$app = Join-Path $repository 'WawiCallPro.App'
$generation = [Guid]::NewGuid().ToString('N')
$output = Join-Path $app "bin/ComDebug/$ProductLine/$generation"

Push-Location $repository
try {
    & dotnet build (Join-Path $app 'WawiCallPro.App.csproj') -c Debug --nologo `
        "-p:LexwareProductLine=$ProductLine" "-p:WawiDebugComGeneration=$generation" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Debug-Build für COM fehlgeschlagen (Exitcode $LASTEXITCODE)." }
} finally { Pop-Location }

$name = "WawiCallPro.App.$ProductLine"
$executables = @(Get-ChildItem -LiteralPath $output -Recurse -File -Filter "$name.exe")
if ($executables.Count -ne 1) { throw "Die COM-Ausgabe enthält keine eindeutige $name.exe." }
$executable = $executables[0]
foreach ($file in @("$name.dll", "$name.deps.json", "$name.runtimeconfig.json", 'appsettings.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $executable.DirectoryName $file) -PathType Leaf)) {
        throw "Die COM-Ausgabe ist unvollständig. Es fehlt $file."
    }
}

if (-not $PrepareOnly) {
    $start = [Diagnostics.ProcessStartInfo]::new($executable.FullName)
    $start.UseShellExecute = $false
    $start.ArgumentList.Add('/regserver')
    $start.ArgumentList.Add('/user')
    $start.Environment['DOTNET_DISABLE_GUI_ERRORS'] = '1'
    $registration = [Diagnostics.Process]::Start($start)
    try {
        $registration.WaitForExit()
        if ($registration.ExitCode -ne 0) { throw "COM-Benutzerregistrierung fehlgeschlagen (Exitcode $($registration.ExitCode))." }
    } finally { $registration.Dispose() }
}

$executable.FullName
