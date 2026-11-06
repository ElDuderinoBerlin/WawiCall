#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Pro', 'FinOffice')][string]$ProductLine = 'Pro',
    [switch]$ListOnly,
    [string[]]$Package = @()
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'scripts/Release.Common.psm1') -Force
$context = Get-ReleaseContext -ProductLine $ProductLine
$projects = Get-ProductProjects $context | Where-Object { $_ -notmatch '^\.\.' }
if (!$ListOnly -and $Package.Count -eq 0) { throw 'Paketupdates benötigen -Package PaketId@Version; für die Übersicht -ListOnly verwenden.' }
foreach ($entry in $Package) {
    if ($entry -notmatch '^[A-Za-z0-9_.-]+@[0-9][A-Za-z0-9.+-]*$') { throw "Paket mit expliziter Version erwartet: $entry" }
    if ($entry -match '^(Velopack|DevExpress\.)') { throw 'Velopack gemeinsam mit dem Toolmanifest bzw. DevExpress zentral in Solution.props aktualisieren.' }
}
$previousProductLine = $env:LexwareProductLine
try {
  $env:LexwareProductLine = $ProductLine
  foreach ($project in $projects) {
    if ($ListOnly) {
        Invoke-ReleaseProcess dotnet @('package', 'list', '--project', (Join-Path $PSScriptRoot $project), '--outdated')
        continue
    }
    [xml]$xml = Get-Content -LiteralPath (Join-Path $PSScriptRoot $project) -Raw
    $references = @($xml.SelectNodes('//PackageReference/@Include') | ForEach-Object { $_.Value })
    $selected = @($Package | Where-Object { ($_ -split '@')[0] -in $references })
    if ($selected.Count -gt 0) {
        Invoke-ReleaseProcess dotnet (@('package', 'update', '--project', (Join-Path $PSScriptRoot $project)) + $selected)
    }
}
} finally { $env:LexwareProductLine = $previousProductLine }
