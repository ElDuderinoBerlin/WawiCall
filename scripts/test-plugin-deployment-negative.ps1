#requires -Version 7.0
param(
    [Parameter(Mandatory)][ValidateSet('Pro','FinOffice')][string]$ProductLine,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [Parameter(Mandatory)][string]$HostOutputRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repository '.artifacts')) + [IO.Path]::DirectorySeparatorChar
$isolatedRoot = [IO.Path]::GetFullPath($HostOutputRoot)
if (!$isolatedRoot.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Negative deployment checks require an isolated publish copy below this repository''s .artifacts directory.'
}
$isolatedPrefix = $isolatedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$check = Join-Path $PSScriptRoot 'test-plugin-deployment.ps1'
$arguments = @{ ProductLine = $ProductLine; Configuration = $Configuration; HostOutputRoot = $isolatedRoot }
$script:passed = 0

function Assert-IsolatedFile([string]$Path) {
    $absolute = [IO.Path]::GetFullPath($Path)
    if (!$absolute.StartsWith($isolatedPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Test mutation outside the isolated publish copy.' }
    return $absolute
}
function Assert-Rejected([string]$Expected) {
    try { & $check @arguments | Out-Null }
    catch {
        if (!$_.Exception.Message.Contains($Expected, [StringComparison]::Ordinal)) { throw }
        $script:passed++
        return
    }
    throw "Deployment check accepted invalid output: $Expected"
}
function Test-MissingFile([string]$Path, [string]$Expected) {
    $file = Assert-IsolatedFile $Path
    $backup = Assert-IsolatedFile ($file + '.deployment-check-backup')
    if (Test-Path -LiteralPath $backup) { throw 'Test backup already exists.' }
    Move-Item -LiteralPath $file -Destination $backup
    try { Assert-Rejected $Expected }
    finally { Move-Item -LiteralPath $backup -Destination $file }
}

# The baseline must pass before any file is moved; regular host outputs are never mutated.
& $check @arguments | Out-Null
$app = Join-Path $isolatedRoot 'App'
$plugin = if ($ProductLine -eq 'Pro') { 'WawiCallPro.Plugins.Lexware.Pro' } else { 'WawiCallPro.Plugins.Lexware.FinOffice' }
$pluginDirectory = Join-Path $app "plugins/$plugin"
foreach ($name in @('Quantarix.Lexware.Core','Quantarix.Pcfk.Core')) {
    Test-MissingFile (Join-Path $app "$name.dll") "Host-owned shared assembly missing: $name"
}
$core = if ($ProductLine -eq 'Pro') { 'Quantarix.Lexware.Core.dll' } else { 'Quantarix.Pcfk.Core.dll' }
$extra = Assert-IsolatedFile (Join-Path $pluginDirectory $core)
if (Test-Path -LiteralPath $extra) { throw 'Unexpected baseline plugin copy.' }
Copy-Item -LiteralPath (Join-Path $app $core) -Destination $extra
try { Assert-Rejected 'Shared assembly copied into plugin deployment' }
finally { Remove-Item -LiteralPath $extra }

$privateName = if ($ProductLine -eq 'Pro') { 'Quantarix.Lexware.APIService.dll' } else { 'Quantarix.Pcfk.DataAccess.dll' }
$privateFile = Assert-IsolatedFile (Join-Path $pluginDirectory $privateName)
Test-MissingFile $privateFile 'Missing private dependency:'
$backup = Assert-IsolatedFile ($privateFile + '.deployment-check-backup')
if (Test-Path -LiteralPath $backup) { throw 'Test backup already exists.' }
Move-Item -LiteralPath $privateFile -Destination $backup
try {
    [IO.File]::WriteAllBytes($privateFile, [byte[]](1,2,3))
    Assert-Rejected 'Changed private dependency:'
}
finally {
    if (Test-Path -LiteralPath $privateFile) { Remove-Item -LiteralPath $privateFile }
    Move-Item -LiteralPath $backup -Destination $privateFile
}
Test-MissingFile (Join-Path $pluginDirectory "$plugin.deps.json") 'Plugin or dependency manifest missing'
& $check @arguments | Out-Null
Write-Output "$script:passed negative deployment checks passed: $ProductLine (missing host dependencies, stale shared copy, missing/changed private dependency, missing manifest; baseline restored)."
