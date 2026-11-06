#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Pro', 'FinOffice')][string[]]$ProductLine = @('Pro', 'FinOffice'),
    [string]$Version,
    [ValidateSet('Any CPU', 'x64')][string]$Platform = 'Any CPU',
    [switch]$RequireCleanDependencies
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Release.Common.psm1') -Force
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $root 'bootstrap.ps1') -CheckOnly -RequireClean:$RequireCleanDependencies
foreach ($line in $ProductLine) {
    $context = Get-ReleaseContext -ProductLine $line -Version $Version
    $stage = New-ReleaseStage $context
    $projects = Get-ProductProjects $context
    $filter = Join-Path $stage "WawiCallPro.$line.slnf"
    @{ solution = @{ path = Join-Path $root 'WawiCallProSolution.sln'; projects = $projects } } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $filter -Encoding utf8
    $properties = @('-c', 'Release', "-p:Platform=$Platform", "-p:LexwareProductLine=$line", "-p:Version=$($context.Version)")
    Invoke-ReleaseProcess dotnet (@('build', $filter, '--nologo', '--verbosity', 'minimal') + $properties) -LogPath (Join-Path $stage 'build.log')
    foreach ($project in $projects | Where-Object { $_ -match '\.Tests\.csproj$' -and $_ -notmatch '^\.\.' }) {
        Invoke-ReleaseProcess dotnet (@('test', '--project', (Join-Path $root $project), '--no-build', '--no-progress') + $properties) -LogPath (Join-Path $stage (([IO.Path]::GetFileNameWithoutExtension($project)) + '.log'))
    }
    Write-Host "Release-Build und Tests erfolgreich: $line $($context.Version). Protokolle: $stage"
}
