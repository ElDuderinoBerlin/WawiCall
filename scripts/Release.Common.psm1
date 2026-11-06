#requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ReleaseContext {
    param(
        [ValidateSet('Pro', 'FinOffice')][string]$ProductLine = 'Pro',
        [string]$Version,
        [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
    )
    $root = Split-Path $PSScriptRoot -Parent
    $ProductLine = if ($ProductLine -eq 'Pro') { 'Pro' } else { 'FinOffice' }
    if (!$Version) {
        [xml]$props = Get-Content -LiteralPath (Join-Path $root 'Solution.props') -Raw
        $Version = [string]$props.Project.PropertyGroup[0].Version
    }
    if ($Version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$') {
        throw 'Version muss eine dreiteilige SemVer ohne Buildmetadaten sein.'
    }
    foreach ($identifier in (($Version -split '-', 2 | Select-Object -Skip 1) -split '\.')) {
        if ($identifier -match '^0[0-9]+$') { throw 'Numerische SemVer-Prereleasewerte dürfen keine führenden Nullen haben.' }
    }
    [pscustomobject]@{
        Root = $root
        ProductLine = $ProductLine
        Version = $Version
        Configuration = $Configuration
        Runtime = 'win-x64'
        PackageId = "WawiCallPro.$ProductLine"
        ReleaseRoot = Join-Path $root ".artifacts/release/$ProductLine/$Version"
    }
}

function Invoke-ReleaseProcess {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [string]$WorkingDirectory = (Split-Path $PSScriptRoot -Parent),
        [string]$LogPath,
        [ValidateRange(1, 7200)][int]$TimeoutSeconds = 1800
    )
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $FilePath
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = [Text.Encoding]::UTF8
    $start.StandardErrorEncoding = [Text.Encoding]::UTF8
    foreach ($argument in $ArgumentList) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (!$process.Start()) { throw "Prozessstart fehlgeschlagen: $FilePath" }
        $output = $process.StandardOutput.ReadToEndAsync()
        $errorOutput = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Prozessfrist überschritten: $FilePath ($TimeoutSeconds Sekunden)."
        }
        $text = $output.GetAwaiter().GetResult() + $errorOutput.GetAwaiter().GetResult()
        if ($LogPath) {
            New-Item -ItemType Directory -Path (Split-Path $LogPath -Parent) -Force | Out-Null
            [IO.File]::WriteAllText($LogPath, $text)
        }
        if ($text) { Write-Host $text.TrimEnd() }
        if ($process.ExitCode -ne 0) { throw "$FilePath fehlgeschlagen (Exitcode $($process.ExitCode)); Protokoll: $LogPath" }
    } finally { $process.Dispose() }
}

function Get-ProductProjects {
    param([Parameter(Mandatory)]$Context)
    $excluded = if ($Context.ProductLine -eq 'Pro') {
        'Quantarix\.Pcfk\.|sqlite-netFx-source|WawiCallPro\.Plugins\.Lexware\.FinOffice'
    } else { 'Quantarix\.Lexware\.|WawiCallPro\.Plugins\.Lexware\.Pro' }
    @(Get-Content -LiteralPath (Join-Path $Context.Root 'WawiCallProSolution.sln') | ForEach-Object {
        if ($_ -match '^Project\(.*?, "([^"\r\n]+\.csproj)",' -and $Matches[1] -notmatch $excluded) { $Matches[1] }
    })
}

function New-ReleaseStage {
    param([Parameter(Mandatory)]$Context)
    $stage = Join-Path $Context.ReleaseRoot ("runs/" + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $stage
}

function Merge-ReleaseFiles {
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Destination)
    foreach ($file in Get-ChildItem -LiteralPath $Source -File -Recurse) {
        $target = Join-Path $Destination ([IO.Path]::GetRelativePath($Source, $file.FullName))
        if (Test-Path -LiteralPath $target) {
            if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
                throw "Abweichende gemeinsame Publishdatei: $target"
            }
        } else {
            New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target
        }
    }
}

function Get-ReleasePublishDirectory {
    param([Parameter(Mandatory)]$Context, [string]$PublishDirectory)
    if ($PublishDirectory) { return (Resolve-Path -LiteralPath $PublishDirectory).Path }
    $receipt = Get-Content -LiteralPath (Join-Path $Context.ReleaseRoot "latest-$($Context.Configuration)-publish.json") -Raw | ConvertFrom-Json
    if ($receipt.ProductLine -ne $Context.ProductLine -or $receipt.Version -ne $Context.Version -or $receipt.Configuration -ne $Context.Configuration) {
        throw 'Publishnachweis passt nicht zu Produktlinie, Version oder Konfiguration.'
    }
    (Resolve-Path -LiteralPath $receipt.PublishDirectory).Path
}

function Write-ReleaseManifest {
    param([Parameter(Mandatory)]$Context, [Parameter(Mandatory)][string]$Directory, [Parameter(Mandatory)][string]$Path)
    $files = @(Get-ChildItem -LiteralPath $Directory -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{ Path = [IO.Path]::GetRelativePath($Directory, $_.FullName); Length = $_.Length; Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    $commit = & git -C $Context.Root rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Git-Revision konnte nicht bestimmt werden.' }
    $changes = @(& git -C $Context.Root status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'Git-Status konnte nicht bestimmt werden.' }
    $sdk = & dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'SDK-Version konnte nicht bestimmt werden.' }
    $dependencies = Get-Content -LiteralPath (Join-Path $Context.Root 'deps.json') -Raw | ConvertFrom-Json
    $dependencyState = @($dependencies.dependencies | ForEach-Object {
        $dependency = $_
        $repository = Join-Path (Split-Path $Context.Root -Parent) $dependency.name
        $revision = & git -C $repository rev-parse HEAD
        if ($LASTEXITCODE -ne 0) { throw "Abhängigkeit nicht lesbar: $($dependency.name)" }
        $dirty = @(& git -C $repository status --porcelain)
        if ($LASTEXITCODE -ne 0) { throw "Abhängigkeitsstatus nicht lesbar: $($dependency.name)" }
        [ordered]@{ Name = $dependency.name; ExpectedRevision = $dependency.revision; Revision = $revision; Dirty = $dirty.Count -gt 0 }
    })
    [ordered]@{
        ProductLine = $Context.ProductLine; PackageId = $Context.PackageId; Version = $Context.Version
        Configuration = $Context.Configuration; Runtime = $Context.Runtime; Channel = 'win'
        Commit = $commit; Dirty = $changes.Count -gt 0; Sdk = $sdk; Velopack = '1.2.161'
        CreatedUtc = [DateTime]::UtcNow.ToString('O'); Dependencies = $dependencyState; Files = $files
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path -Encoding utf8
}

Export-ModuleMember -Function Get-ReleaseContext, Invoke-ReleaseProcess, Get-ProductProjects, New-ReleaseStage, Merge-ReleaseFiles, Get-ReleasePublishDirectory, Write-ReleaseManifest
