[CmdletBinding()]
param(
    [ValidateSet('Pro','FinOffice')][string]$LexwareProductLine = 'FinOffice',
    [string]$MariaDbBin
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot
& dotnet test --project (Join-Path $repositoryRoot 'WawiCallPro.Database.Tests/WawiCallPro.Database.Tests.csproj') -c Debug '-p:Platform=Any CPU' "-p:LexwareProductLine=$LexwareProductLine" --no-progress
if ($LASTEXITCODE -ne 0) { throw 'SQLite-/Schema-/Repositorytests fehlgeschlagen.' }
& (Join-Path $PSScriptRoot 'test-database-backup.ps1') -ProviderCompatibility -LexwareProductLine $LexwareProductLine -MariaDbBin $MariaDbBin
