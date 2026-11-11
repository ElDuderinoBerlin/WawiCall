param([ValidateSet('Pro','FinOffice')][string]$ProductLine)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot
$audit = Join-Path $repositoryRoot ('.artifacts/database-console-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $audit | Out-Null

function RunCommand([string]$Variant, [string]$ConfigurationDirectory, [string[]]$Arguments, [int]$ExpectedExitCode = 0) {
    $executable = Join-Path $repositoryRoot "WawiCallPro.Console/bin/Debug/net11.0-windows10.0.19041.0/WawiCallPro.Console.$Variant.exe"
    $start = [Diagnostics.ProcessStartInfo]::new($executable)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['WAWICALLPRO_CONFIG_DIR'] = $ConfigurationDirectory
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(30000)) { $process.Kill($true); throw 'Console database command timed out' }
        $result = [pscustomobject]@{ExitCode=$process.ExitCode;Output=$stdout.Result;Error=$stderr.Result}
        if ($result.ExitCode -ne $ExpectedExitCode) { throw "Unexpected exit code for $Variant $($Arguments[0]): $($result.ExitCode)" }
        return $result
    } finally { $process.Dispose() }
}

if (!$ProductLine) {
    $hostRoot = Join-Path $repositoryRoot 'WawiCallPro.Console/bin/Debug/net11.0-windows10.0.19041.0'
    $editions = @('Pro','FinOffice' | Where-Object { Test-Path -LiteralPath (Join-Path $hostRoot "WawiCallPro.Console.$_.dll") })
    if ($editions.Count -ne 1) { throw 'Zuerst genau eine Edition bauen oder -ProductLine angeben.' }
    $ProductLine = $editions[0]
}
foreach ($variant in @($ProductLine)) {
    $configuration = Join-Path $audit "$variant/source"
    $status = RunCommand $variant $configuration @('--db-status')
    if (!$status.Output.Contains('MissingDatabase')) { throw 'Missing database status was not reported' }
    if (Get-ChildItem -LiteralPath $configuration -File -Recurse | Where-Object { $_.Extension -ne '.jsonl' }) { throw 'Status created configuration/database/identity' }
    $null = RunCommand $variant $configuration @('--db-migrate') 1
    $setup = RunCommand $variant $configuration @('--db-init')
    $completed = [regex]::Match($setup.Error, 'Datenbank: Completed; Version (\d+); Schritte (\d+)/(\d+)')
    $applied = [regex]::Matches($setup.Error, 'Datenbank: Applying;').Count
    if (!$setup.Output.Contains('Current') -or !$completed.Success -or $applied -lt 1 -or
        $applied -ne [int]$completed.Groups[2].Value -or $completed.Groups[2].Value -ne $completed.Groups[3].Value) { throw 'Setup status/progress missing' }
    $database = Join-Path $configuration 'communications.db'
    $identityFile = Join-Path $configuration 'workstation.local.json'
    $identity = Get-Content -LiteralPath $identityFile -Raw | ConvertFrom-Json
    if ($identity.formatVersion -ne 1 -or [guid]::ParseExact($identity.workstationId,'N') -eq [guid]::Empty) { throw 'Invalid workstation identity' }
    $null = RunCommand $variant $configuration @('--workstation-init')
    $same = Get-Content -LiteralPath $identityFile -Raw | ConvertFrom-Json
    if ($same.workstationId -ne $identity.workstationId) { throw 'Workstation identity changed across processes' }
    $before = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($database)))
    $null = RunCommand $variant $configuration @('--db-init') 1
    $migration = RunCommand $variant $configuration @('--db-migrate')
    if (!$migration.Output.Contains('Current') -or $migration.Error.Contains('Datenbank: Applying;')) { throw 'Repeated migration changed the schema' }
    $after = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($database)))
    if ($before -ne $after) { throw 'Repeated setup/migration changed the existing database' }
    $backup = Join-Path $audit "$variant/backup with spaces.db"
    $null = RunCommand $variant $configuration @('--db-backup',$backup)
    $backupHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($backup)))
    $null = RunCommand $variant $configuration @('--db-backup',$backup) 1
    if ($backupHash -ne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($backup)))) { throw 'Existing backup was overwritten' }
    $null = RunCommand $variant $configuration @('--db-restore',$backup) 1
    $restoredDirectory = Join-Path $audit "$variant/restored"
    $restored = RunCommand $variant $restoredDirectory @('--db-restore',$backup)
    if (!$restored.Output.Contains('Current')) { throw 'Restore did not verify the schema' }
    if (Test-Path -LiteralPath (Join-Path $restoredDirectory 'workstation.local.json')) { throw 'Restore invented a workstation identity' }
    $null = RunCommand $variant $restoredDirectory @('--workstation-init')
    $newIdentity = Get-Content -LiteralPath (Join-Path $restoredDirectory 'workstation.local.json') -Raw | ConvertFrom-Json
    if ($newIdentity.workstationId -eq $identity.workstationId) { throw 'Separate local installations unexpectedly share an identity' }
    $help = RunCommand $variant $configuration @('--help')
    foreach ($command in @('--db-status','--db-init','--db-migrate','--db-approve-empty','--db-backup','--db-restore','--workstation-init')) {
        if (!$help.Output.Contains($command)) { throw 'Redirected help is incomplete' }
    }
    $broken = '{"formatVersion":2,"workstationId":"' + $identity.workstationId + '"}'
    Set-Content -LiteralPath $identityFile -Value $broken -NoNewline
    $null = RunCommand $variant $configuration @('--workstation-init') 1
    if ((Get-Content -LiteralPath $identityFile -Raw) -ne $broken) { throw 'Newer workstation format was overwritten' }
    Write-Output "${variant}: real Console processes passed status, explicit setup, progress, repeated migration, persistent identity, backup, restore and redirected help."
}
