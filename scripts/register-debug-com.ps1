[CmdletBinding(DefaultParameterSetName = 'Build')]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Pro', 'FinOffice')][string]$ProductLine,
    [Parameter(ParameterSetName = 'Build')][switch]$PrepareOnly,
    [Parameter(Mandatory, ParameterSetName = 'Refresh')][string]$CompletedBuildDirectory,
    [Parameter(Mandatory, ParameterSetName = 'Refresh')][string]$BuildFileList
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$app = Join-Path $repository 'WawiCallPro.App'
$generation = [Guid]::NewGuid().ToString('N')
$output = Join-Path $app "bin/ComDebug/$ProductLine/$generation"
$name = "WawiCallPro.App.$ProductLine"
$required = @("$name.exe", "$name.dll", "$name.deps.json", "$name.runtimeconfig.json", 'appsettings.json')
$refresh = $PSCmdlet.ParameterSetName -eq 'Refresh'

function Get-RegisteredDebugExecutable([string]$BuildDirectory) {
    $progId = "WawiCallPro.$ProductLine.Application.1"
    $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey('CurrentUser', 'Registry64')
    try {
        $prog = $root.OpenSubKey("Software\Classes\$progId")
        if (-not $prog) { return }
        try {
            if ($prog.GetValue('WawiCallPro.InstallationRoot') -ne $BuildDirectory -or
                $prog.GetValue('WawiCallPro.ProductLine') -ne $ProductLine -or
                $prog.GetValue('WawiCallPro.RegistrationSchema') -ne '1') { return }
            return $prog.GetValue('WawiCallPro.OwnerExecutable')
        } finally { $prog.Dispose() }
    } finally { $root.Dispose() }
}

function Copy-CompletedBuild([string]$Source, [string]$Destination, [string]$RegisteredExecutable) {
    $relativePaths = @(Get-Content -LiteralPath $BuildFileList -Encoding utf8 | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($Source, $_)
        if ([IO.Path]::IsPathRooted($relative) -or $relative -eq '..' -or $relative.StartsWith('..\')) {
            throw 'Die Dateiliste enthält einen Pfad außerhalb der Debug-Ausgabe.'
        }
        $relative
    } | Sort-Object -Unique)
    foreach ($file in $required) {
        if ($file -notin $relativePaths) { throw "Die Debug-Ausgabe ist unvollständig. Es fehlt $file." }
    }

    # Hold read leases until the complete generation has been copied. A parallel writer fails the copy
    # before anything is registered. File identities also avoid duplicates for unchanged builds.
    $streams = [Collections.Generic.Dictionary[string,IO.FileStream]]::new([StringComparer]::OrdinalIgnoreCase)
    try {
        foreach ($relative in $required + @($relativePaths | Where-Object { $_ -notin $required })) {
            $streams.Add($relative, [IO.File]::Open((Join-Path $Source $relative), 'Open', 'Read', 'Read'))
        }
        $manifest = [string]::Join("`n", @($relativePaths | ForEach-Object {
            [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($streams[$_])) + ' ' + $_
        }))
        $manifestName = '.wawi-debug-com-files'
        $previous = Split-Path -Parent $RegisteredExecutable
        $previousManifest = Join-Path $previous $manifestName
        if ((Test-Path -LiteralPath $previousManifest -PathType Leaf) -and
            $manifest -ceq [IO.File]::ReadAllText($previousManifest) -and
            @( $relativePaths | Where-Object { -not (Test-Path -LiteralPath (Join-Path $previous $_) -PathType Leaf) } ).Count -eq 0) { return $false }

        foreach ($relative in $relativePaths) {
            $path = Join-Path $Destination $relative
            $null = [IO.Directory]::CreateDirectory((Split-Path -Parent $path))
            $target = [IO.File]::Open($path, 'CreateNew', 'Write', 'None')
            try {
                $streams[$relative].Position = 0
                $streams[$relative].CopyTo($target)
            } finally { $target.Dispose() }
        }
        [IO.File]::WriteAllText((Join-Path $Destination $manifestName), $manifest)
        return $true
    } finally {
        foreach ($stream in $streams.Values) { $stream.Dispose() }
    }
}

if ($refresh) {
    $source = [IO.Path]::GetFullPath($CompletedBuildDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ((Split-Path -Parent $source) -ne (Join-Path $app 'bin\Debug')) { return }
    $registered = Get-RegisteredDebugExecutable $source
    if (-not $registered) { return }
    if (-not (Copy-CompletedBuild $source (Join-Path $output (Split-Path -Leaf $source)) $registered)) { return }
} else {
    Push-Location $repository
    try {
        & dotnet build (Join-Path $app 'WawiCallPro.App.csproj') -c Debug --nologo `
            "-p:LexwareProductLine=$ProductLine" "-p:WawiDebugComGeneration=$generation" | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Debug-Build für COM fehlgeschlagen (Exitcode $LASTEXITCODE)." }
    } finally { Pop-Location }
}

$executables = @(Get-ChildItem -LiteralPath $output -Recurse -File -Filter "$name.exe")
if ($executables.Count -ne 1) { throw "Die COM-Ausgabe enthält keine eindeutige $name.exe." }
$executable = $executables[0]
foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $executable.DirectoryName $file) -PathType Leaf)) {
        throw "Die COM-Ausgabe ist unvollständig. Es fehlt $file."
    }
}

if (-not $PrepareOnly) {
    $start = [Diagnostics.ProcessStartInfo]::new($executable.FullName)
    $start.UseShellExecute = $false
    if ($refresh) {
        $start.ArgumentList.Add('--refresh-debug-com')
    } else {
        $start.ArgumentList.Add('/regserver')
        $start.ArgumentList.Add('/user')
    }
    $start.Environment['DOTNET_DISABLE_GUI_ERRORS'] = '1'
    $registration = [Diagnostics.Process]::Start($start)
    try {
        $registration.WaitForExit()
        if ($registration.ExitCode -ne 0) { throw "COM-Benutzerregistrierung fehlgeschlagen (Exitcode $($registration.ExitCode))." }
    } finally { $registration.Dispose() }
}

$executable.FullName
