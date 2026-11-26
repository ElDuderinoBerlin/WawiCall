[CmdletBinding(DefaultParameterSetName = 'Build')]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Pro', 'FinOffice')][string]$ProductLine,
    [Parameter(ParameterSetName = 'Build')][switch]$PrepareOnly,
    [Parameter(Mandatory, ParameterSetName = 'Refresh')][string]$CompletedBuildDirectory,
    [Parameter(Mandatory, ParameterSetName = 'Refresh')][string]$BuildFileList,
    [Parameter(ParameterSetName = 'Refresh')][ValidatePattern('^[a-f0-9]{32}$')][string]$DebugGeneration,
    [Parameter(ParameterSetName = 'Refresh')][string]$PreparedHost
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$app = Join-Path $repository 'WawiCallPro.App'
$generation = if ($DebugGeneration) { $DebugGeneration } else { [Guid]::NewGuid().ToString('N') }
$editionRoot = Join-Path $app "bin/ComDebug/$ProductLine"
$output = Join-Path $editionRoot $generation
$name = "WawiCallPro.App.$ProductLine"
$stableHost = Join-Path $editionRoot "$name.exe"
$activeGenerationFile = Join-Path $editionRoot '.wawi-debug-com-active'
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
        if ($RegisteredExecutable) {
            $previous = Split-Path -Parent $RegisteredExecutable
            $previousManifest = Join-Path $previous $manifestName
            if ((Test-Path -LiteralPath $previousManifest -PathType Leaf) -and
                $manifest -ceq [IO.File]::ReadAllText($previousManifest) -and
                @( $relativePaths | Where-Object { -not (Test-Path -LiteralPath (Join-Path $previous $_) -PathType Leaf) } ).Count -eq 0) { return $false }
        }

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

function Register-DebugHost([string]$Executable) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
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

function Publish-ProDebugHost([string]$HostImage) {
    # The apphost embeds the immutable generation's DLL path. Replace only the launch image,
    # leaving a running process and all of its managed and native dependencies in place.
    $pendingHost = Join-Path $editionRoot "$generation.pending.exe"
    $backupHost = Join-Path $output 'WawiDebugComPrevious.exe'
    [IO.File]::Copy($HostImage, $pendingHost)
    $replacing = [IO.File]::Exists($stableHost)
    $published = $false
    try {
        if ($replacing) {
            [IO.File]::Copy($stableHost, $backupHost)
            [IO.File]::Replace($pendingHost, $stableHost, [NullString]::Value)
        }
        else { [IO.File]::Move($pendingHost, $stableHost) }

        try { Register-DebugHost $stableHost }
        catch {
            if ($replacing) { [IO.File]::Replace($backupHost, $stableHost, [NullString]::Value) }
            else { [IO.File]::Delete($stableHost) }
            throw
        }
        $published = $true
        try { [IO.File]::WriteAllText($activeGenerationFile, $generation) }
        catch { Write-Warning 'Die COM-Ausgabe wurde aktualisiert. Der Build-Marker konnte nicht gespeichert werden.' }
        return $stableHost
    } finally {
        Remove-Item -LiteralPath $pendingHost -ErrorAction SilentlyContinue
        if ($published) { Remove-Item -LiteralPath $backupHost -ErrorAction SilentlyContinue }
    }
}

function Deploy-DebugBuild {
    if ($refresh) {
        $source = [IO.Path]::GetFullPath($CompletedBuildDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
        if ((Split-Path -Parent $source) -ne (Join-Path $app 'bin\Debug')) { return }
        $registered = Get-RegisteredDebugExecutable $source
        if (-not $registered) { return }
        $framework = Split-Path -Leaf $source
        $previous = $registered
        if ($ProductLine -eq 'Pro') {
            if (-not $DebugGeneration -or -not $PreparedHost) { throw 'Der vorbereitete Pro-COM-Apphost fehlt.' }
            # Force the first publication from a legacy registration even for an identical build.
            $previous = $null
            if ($registered -eq $stableHost -and [IO.File]::Exists($stableHost) -and [IO.File]::Exists($activeGenerationFile)) {
                $active = [IO.File]::ReadAllText($activeGenerationFile)
                $managedPath = "$active\$framework\$name.dll" + [char]0
                # After an interrupted publication the cache may still describe the previous image.
                if ($active -cmatch '^[a-f0-9]{32}$' -and
                    [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($stableHost)).Contains($managedPath)) {
                    $previous = Join-Path $editionRoot "$active/$framework/$name.exe"
                }
            }
        }
        if (-not (Copy-CompletedBuild $source (Join-Path $output $framework) $previous)) { return }
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

    if ($PrepareOnly) { return $executable.FullName }
    if ($ProductLine -eq 'Pro') {
        $hostImage = if ($refresh) { $PreparedHost } else { Join-Path $output 'WawiDebugComHost.exe' }
        return (Publish-ProDebugHost $hostImage)
    }

    Register-DebugHost $executable.FullName
    return $executable.FullName
}

if ($ProductLine -ne 'Pro') { Deploy-DebugBuild; return }
$rootHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($editionRoot.ToUpperInvariant())))
$publishLock = [Threading.Mutex]::new($false, "Local\WawiCallPro.DebugCom.$rootHash")
$ownsLock = $false
try {
    try { $ownsLock = $publishLock.WaitOne([TimeSpan]::FromMinutes(2)) }
    catch [Threading.AbandonedMutexException] { $ownsLock = $true }
    if (-not $ownsLock) { throw 'Die Veröffentlichung der COM-Debug-Ausgabe ist belegt.' }
    Deploy-DebugBuild
} finally {
    if ($ownsLock) { $publishLock.ReleaseMutex() }
    $publishLock.Dispose()
}
