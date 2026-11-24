[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [switch]$RequireClean,
    [switch]$Update,
    [string]$ManifestPath = (Join-Path $PSScriptRoot 'deps.json'),
    [string]$DependencyRoot,
    [string[]]$Name = @()
)

$ErrorActionPreference = 'Stop'
if ($CheckOnly -and $Update) { throw '-CheckOnly und -Update können nicht kombiniert werden.' }
$ManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$manifestDirectory = Split-Path $ManifestPath -Parent
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if (-not $DependencyRoot) {
    $DependencyRoot = [IO.Path]::GetFullPath((Join-Path $manifestDirectory $manifest.baseDir))
}

$targets = @($Name -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($target in $targets) {
    if ($target -notin $manifest.dependencies.name) { throw "Unbekannte Abhängigkeit: $target" }
}

foreach ($dependency in $manifest.dependencies) {
    if ($dependency.name -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$' -or $dependency.revision -notmatch '^[a-f0-9]{40}$') {
        throw "Ungültiger Manifest-Eintrag: $($dependency.name)"
    }
    if ($dependency.workspaceSubdir -and $dependency.workspaceSubdir -notmatch '^[A-Za-z0-9][A-Za-z0-9_-]*(/[A-Za-z0-9][A-Za-z0-9_-]*)*$') {
        throw "Ungültiger Arbeitsbereichspfad in $($dependency.name): $($dependency.workspaceSubdir)"
    }
    foreach ($project in $dependency.projects) {
        if ([IO.Path]::IsPathRooted($project) -or $project -match '(^|[/\\])\.\.($|[/\\])') {
            throw "Ungültiger Projektpfad in $($dependency.name): $project"
        }
    }
}

function Sync-Dependency($dependency) {
    $repositoryRoot = if ($dependency.workspaceSubdir) { Join-Path $manifestDirectory $dependency.workspaceSubdir } else { $DependencyRoot }
    $repositoryPath = Join-Path $repositoryRoot $dependency.name

    if (-not (Test-Path -LiteralPath $repositoryPath)) {
        if ($CheckOnly) { throw "Fehlendes Schwester-Repository: $repositoryPath" }
        [IO.Directory]::CreateDirectory($repositoryRoot) | Out-Null
        & git clone --no-checkout --filter=blob:none --branch $dependency.branch -- $dependency.url $repositoryPath
        if ($LASTEXITCODE -ne 0) { throw "Clone fehlgeschlagen: $($dependency.name)" }
        & git -C $repositoryPath checkout --no-overwrite-ignore --detach $dependency.revision
        if ($LASTEXITCODE -ne 0) { throw "Checkout fehlgeschlagen: $($dependency.name)" }
    }

    $actualRoot = & git -C $repositoryPath rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($actualRoot) -ne [IO.Path]::GetFullPath($repositoryPath)) {
        throw "Kein eigenständiges Git-Repository: $repositoryPath"
    }
    $actualRevision = & git -C $repositoryPath rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw "Git-Revision konnte nicht gelesen werden: $($dependency.name)" }

    $changes = @(& git -C $repositoryPath status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw "Git-Status fehlgeschlagen: $($dependency.name)" }
    if ($actualRevision -ne $dependency.revision) {
        if (-not $Update) {
            throw "Abweichende Revision in $($dependency.name): erwartet $($dependency.revision), vorhanden $actualRevision. Bestehende Arbeitskopie bleibt unverändert."
        }
        if ($changes.Count -gt 0) { throw "Lokale Änderungen in $($dependency.name); Aktualisierung übersprungen." }
        & git -C $repositoryPath cat-file -e "$($dependency.revision)^{commit}" 2>$null
        if ($LASTEXITCODE -ne 0) {
            & git -C $repositoryPath fetch --no-tags -- $dependency.url "refs/heads/$($dependency.branch)"
            if ($LASTEXITCODE -ne 0) { throw "Fetch fehlgeschlagen: $($dependency.name)" }
        }
        & git -C $repositoryPath merge-base --is-ancestor $actualRevision $dependency.revision
        if ($LASTEXITCODE -ne 0) {
            throw "Kein Fast-forward auf $($dependency.revision) möglich: $($dependency.name). Arbeitskopie bleibt unverändert."
        }
        foreach ($project in $dependency.projects) {
            & git -C $repositoryPath cat-file -e "$($dependency.revision):$project"
            if ($LASTEXITCODE -ne 0) { throw "Projekt fehlt in Zielrevision von $($dependency.name): $project" }
        }
        & git -C $repositoryPath merge --ff-only --no-overwrite-ignore $dependency.revision
        if ($LASTEXITCODE -ne 0) { throw "Fast-forward fehlgeschlagen: $($dependency.name)" }
        $actualRevision = & git -C $repositoryPath rev-parse HEAD
        if ($LASTEXITCODE -ne 0 -or $actualRevision -ne $dependency.revision) {
            throw "Zielrevision wurde nicht erreicht: $($dependency.name)"
        }
    }

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

$failed = @()
foreach ($dependency in $manifest.dependencies) {
    if ($targets.Count -gt 0 -and $dependency.name -notin $targets) { continue }
    try { Sync-Dependency $dependency }
    catch {
        if (-not $Update) { throw }
        $failed += $dependency.name
        Write-Warning $_.Exception.Message
    }
}
if ($failed.Count -gt 0) { throw "Abhängigkeiten konnten nicht aktualisiert werden: $($failed -join ', ')" }
