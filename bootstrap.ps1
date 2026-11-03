[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [switch]$RequireClean,
    [string]$ManifestPath = (Join-Path $PSScriptRoot 'deps.json'),
    [string]$DependencyRoot
)

$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if (-not $DependencyRoot) {
    $DependencyRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path $ManifestPath -Parent) $manifest.baseDir))
}

foreach ($dependency in $manifest.dependencies) {
    if ($dependency.name -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$' -or $dependency.revision -notmatch '^[a-f0-9]{40}$') {
        throw "Ungültiger Manifest-Eintrag: $($dependency.name)"
    }

    $repositoryPath = Join-Path $DependencyRoot $dependency.name
    if (-not (Test-Path -LiteralPath $repositoryPath)) {
        if ($CheckOnly) { throw "Fehlendes Schwester-Repository: $repositoryPath" }
        & git clone --no-checkout --filter=blob:none --branch $dependency.branch -- $dependency.url $repositoryPath
        if ($LASTEXITCODE -ne 0) { throw "Clone fehlgeschlagen: $($dependency.name)" }
        & git -C $repositoryPath checkout --detach $dependency.revision
        if ($LASTEXITCODE -ne 0) { throw "Checkout fehlgeschlagen: $($dependency.name)" }
    }

    $actualRoot = & git -C $repositoryPath rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($actualRoot) -ne [IO.Path]::GetFullPath($repositoryPath)) {
        throw "Kein eigenständiges Git-Repository: $repositoryPath"
    }
    $actualRevision = & git -C $repositoryPath rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $actualRevision -ne $dependency.revision) {
        throw "Abweichende Revision in $($dependency.name): erwartet $($dependency.revision), vorhanden $actualRevision. Bestehende Arbeitskopie bleibt unverändert."
    }

    $changes = @(& git -C $repositoryPath status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw "Git-Status fehlgeschlagen: $($dependency.name)" }
    if ($changes.Count -gt 0) {
        if ($RequireClean) { throw "Lokale Änderungen in $($dependency.name); unveränderter CI-Stand erforderlich." }
        Write-Warning "$($dependency.name): lokale Änderungen vorhanden; sie werden unverändert verwendet."
    }
    foreach ($projectPath in $dependency.projects) {
        if (-not (Test-Path -LiteralPath (Join-Path $repositoryPath $projectPath))) {
            throw "Projekt fehlt in $($dependency.name): $projectPath"
        }
    }
    Write-Output "$($dependency.name): $actualRevision geprüft."
}
