param(
    [switch]$FullAcceptance,
    [switch]$ProviderCompatibility,
    [string]$MariaDbBin,
    [ValidateSet('SQL Server','MySQL','PostgreSQL','MariaDB')][string[]]$Engine,
    [ValidateSet('Pro','FinOffice')][string]$LexwareProductLine = 'Pro'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot
$audit = Join-Path $repositoryRoot ('.artifacts/backup-audit-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $audit | Out-Null
$runner = Join-Path $audit 'Runner'
New-Item -ItemType Directory -Path $runner | Out-Null
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net11.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../../WawiCallPro.Database.Tests/WawiCallPro.Database.Tests.csproj" />
    <ProjectReference Include="../../../WawiCallPro.Utilities.Tests/WawiCallPro.Utilities.Tests.csproj" />
  </ItemGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $runner 'Runner.csproj') -Encoding utf8
@'
using System.Text.Json;
using WawiCallPro.Database;
using WawiCallPro.Database.Tests;
using WawiCallPro.Utilities.Tests;
var configurations = JsonSerializer.Deserialize<AuditTarget[]>(File.ReadAllText(args[0]))!;
foreach(var item in configurations)
{
    var target = DatabaseTarget.Server(Enum.Parse<DatabaseType>(item.Type), item.ConnectionString, 5, 30);
    var factory = new DatabaseFactory(); var engine = new FluentMigratorEngine(target.Type);
    var receipt = await new DatabaseProvisioner(factory).CreateAsync(target);
    var full = args[4] is "full" or "providers";
    if(args[4] == "providers")
    {
        await DatabaseSetupAcceptance.RunAsync(target.Type, item.ConnectionString, args[1]);
        Console.WriteLine(item.Engine + ": setup wizard service probe, create, migrate, protected save and reload passed.");
        await using(var connection = factory.CreateConnection(target))
        {
            await connection.OpenAsync();
            Console.WriteLine(item.Engine + ": " + connection.ServerVersion);
        }
        await ProviderCompatibilityAcceptance.RunAsync(target);
        Console.WriteLine(item.Engine + ": migrations 1-9, populated upgrade, source metadata, Unicode, field limits, diagnostic codes, queries and repeated migration passed.");
    }
    if(full) await PhaseThreeAcceptance.RunAsync(target, receipt, args[1]);
    else
    {
        var status = await new DatabaseMigrator(factory, engine).MigrateAsync(target, creation: receipt);
        if(status.State != DatabaseSchemaState.Current) throw new Exception("Schema creation failed");
    }
    await RepositoryAcceptance.CompaniesAndCacheAsync(target);
    await RepositoryAcceptance.HistoryAsync(target);
    await RepositoryAcceptance.RulesAsync(target);
    await RepositoryAcceptance.ConcurrencyAsync(target);
    if(full) await NativeHostAcceptance.RunAsync(target.Type, item.ConnectionString, args[1]);
    if(args[4] != "providers") await ServerBackupAcceptance.RunAsync(target, args[1], args[2], args[3]);
    Console.WriteLine(item.Engine + ": repository acceptance passed.");
}
internal sealed record AuditTarget(string Type, string ConnectionString, string Engine);
'@ | Set-Content -LiteralPath (Join-Path $runner 'Program.cs') -Encoding utf8
$runId = [guid]::NewGuid().ToString('N')
$runRoot = Join-Path $audit ('servers-' + $runId)
$mysqlBin = Split-Path (Get-Command mysql).Source
$mysqlBase = Split-Path $mysqlBin
$pgBin = Split-Path (Get-Command psql).Source
$mysqlData = Join-Path $runRoot 'mysql'
$pgData = Join-Path $runRoot 'postgres'
$instance = 'WcpSchema' + $runId
$database = 'wcp_schema_' + $runId
$mysqlProcess = $null
$mariaProcess = $null
$pgProcess = $null
$sqlCreated = $false
if ($ProviderCompatibility) {
    if (!$MariaDbBin) {
        $mariaCommand = Get-Command mariadbd.exe -ErrorAction SilentlyContinue
        if ($mariaCommand) { $MariaDbBin = Split-Path $mariaCommand.Source }
        else {
            $mariaInstallation = Get-ChildItem -LiteralPath $env:ProgramFiles -Directory -Filter 'MariaDB *' |
                Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'bin/mariadbd.exe') } |
                Sort-Object Name -Descending | Select-Object -First 1
            if ($mariaInstallation) { $MariaDbBin = Join-Path $mariaInstallation.FullName 'bin' }
        }
    }
    foreach ($binary in @('mariadbd.exe','mysql_install_db.exe','mysqladmin.exe')) {
        if (!$MariaDbBin -or !(Test-Path -LiteralPath (Join-Path $MariaDbBin $binary))) { throw "MariaDB-Testwerkzeuge fehlen; -MariaDbBin angeben ($binary)." }
    }
}
function FreePort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop(); return $port
}
function WaitPort([int]$Port) {
    $deadline = [datetime]::UtcNow.AddSeconds(35)
    while ([datetime]::UtcNow -lt $deadline) {
        $client = [Net.Sockets.TcpClient]::new()
        try { $client.Connect('127.0.0.1', $Port); return } catch { Start-Sleep -Milliseconds 250 } finally { $client.Dispose() }
    }
    throw 'Server startup timeout'
}
try {
    New-Item -ItemType Directory -Path $runRoot | Out-Null
    $sqlOutput = & SqlLocalDB create $instance '15.0' -s 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('LocalDB startup failed: ' + $sqlOutput) }
    $sqlCreated = $true
    Write-Output 'Disposable SQL Server LocalDB instance started.'
    $mysqlPort = FreePort
    $init = Start-Process -FilePath (Join-Path $mysqlBin 'mysqld.exe') -ArgumentList @('--no-defaults','--initialize-insecure',"--basedir=`"$mysqlBase`"","--datadir=`"$mysqlData`"","--log-error=`"$(Join-Path $runRoot 'mysql-init.log')`"") -WindowStyle Hidden -PassThru
    if (-not $init.WaitForExit(45000)) { $init.Kill($true); throw 'MySQL initialization timeout' }
    if ($init.ExitCode -ne 0) { throw 'MySQL initialization failed' }
    $mysqlProcess = Start-Process -FilePath (Join-Path $mysqlBin 'mysqld.exe') -ArgumentList @('--no-defaults',"--basedir=`"$mysqlBase`"","--datadir=`"$mysqlData`"",'--bind-address=127.0.0.1',"--port=$mysqlPort",'--mysqlx=0','--skip-log-bin','--innodb-buffer-pool-size=32M',"--log-error=`"$(Join-Path $runRoot 'mysql-server.log')`"") -WindowStyle Hidden -PassThru
    WaitPort $mysqlPort
    Write-Output 'Disposable MySQL instance started.'
    if ($ProviderCompatibility) {
        $mariaPort = FreePort
        $mariaData = Join-Path $runRoot 'mariadb'
        $init = Start-Process -FilePath (Join-Path $MariaDbBin 'mysql_install_db.exe') -ArgumentList @("--datadir=`"$mariaData`"", "--port=$mariaPort", '--silent') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot 'mariadb-init.log') -RedirectStandardError (Join-Path $runRoot 'mariadb-init.err')
        if (-not $init.WaitForExit(45000)) { $init.Kill($true); throw 'MariaDB initialization timeout' }
        if ($init.ExitCode -ne 0) { throw 'MariaDB initialization failed' }
        $mariaProcess = Start-Process -FilePath (Join-Path $MariaDbBin 'mariadbd.exe') -ArgumentList @('--no-defaults', "--basedir=`"$(Split-Path $MariaDbBin)`"", "--datadir=`"$mariaData`"", '--bind-address=127.0.0.1', "--port=$mariaPort", '--skip-log-bin', '--innodb-buffer-pool-size=32M', "--log-error=`"$(Join-Path $runRoot 'mariadb-server.log')`"") -WindowStyle Hidden -PassThru
        WaitPort $mariaPort
        Write-Output 'Disposable MariaDB instance started.'
    }
    $pgPort = FreePort
    $init = Start-Process -FilePath (Join-Path $pgBin 'initdb.exe') -ArgumentList @('-D',"`"$pgData`"",'--username=wcp_audit','--auth=trust','--encoding=UTF8','--locale=C') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot 'postgres-init.log') -RedirectStandardError (Join-Path $runRoot 'postgres-init.err')
    if (-not $init.WaitForExit(45000)) { $init.Kill($true); throw 'PostgreSQL initialization timeout' }
    if ($init.ExitCode -ne 0) { throw 'PostgreSQL initialization failed' }
    $starter = Start-Process -FilePath (Join-Path $pgBin 'pg_ctl.exe') -ArgumentList @('-D',"`"$pgData`"",'-l',"`"$(Join-Path $runRoot 'postgres-server.log')`"",'-o',"`"-h 127.0.0.1 -p $pgPort`"",'-w','start') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runRoot 'postgres-start.log') -RedirectStandardError (Join-Path $runRoot 'postgres-start.err')
    if (-not $starter.WaitForExit(45000)) { $starter.Kill($true); throw 'PostgreSQL startup timeout' }
    if ($starter.ExitCode -ne 0) { throw 'PostgreSQL startup failed' }
    $pgProcess = Get-Process -Id ([int](Get-Content -LiteralPath (Join-Path $pgData 'postmaster.pid') -First 1))
    WaitPort $pgPort
    Write-Output 'Disposable PostgreSQL instance started.'
    $config = Join-Path $audit 'server-config.json'
    $targets = @(
        @{Type='SqlServer';Engine='SQL Server';ConnectionString="Server=(localdb)\$instance;Database=$database;Integrated Security=True;Encrypt=False"},
        @{Type='MySql';Engine='MySQL';ConnectionString="Server=127.0.0.1;Port=$mysqlPort;Database=$database;User ID=root;SslMode=None"},
        @{Type='PostgreSql';Engine='PostgreSQL';ConnectionString="Host=127.0.0.1;Port=$pgPort;Database=$database;Username=wcp_audit;SSL Mode=Disable"}
    )
    if ($ProviderCompatibility) {
        $targets += @{Type='MySql';Engine='MariaDB';ConnectionString="Server=127.0.0.1;Port=$mariaPort;Database=$database;User ID=root;SslMode=None"}
    }
    if ($Engine) { $targets = @($targets | Where-Object { $_.Engine -in $Engine }) }
    if ($targets.Count -eq 0) { throw 'Keine passenden nativen Prüffälle ausgewählt.' }
    ConvertTo-Json -InputObject @($targets) | Set-Content -LiteralPath $config -Encoding utf8
    $mode = if ($ProviderCompatibility) { 'providers' } elseif ($FullAcceptance) { 'full' } else { 'backup' }
    & dotnet run --project (Join-Path $runner 'Runner.csproj') -c Debug '-p:Platform=Any CPU' "-p:LexwareProductLine=$LexwareProductLine" -- $config $audit $mysqlBin $pgBin $mode
    if ($LASTEXITCODE -ne 0) { throw 'Server schema audit failed' }
} finally {
    if ($mariaProcess -and -not $mariaProcess.HasExited) {
        & (Join-Path $MariaDbBin 'mysqladmin.exe') '--no-defaults' '--protocol=TCP' '-h127.0.0.1' "-P$mariaPort" '-uroot' 'shutdown' 2>&1 | Out-Null
        if (-not $mariaProcess.WaitForExit(15000)) { $mariaProcess.Kill($true) }
    }
    if ($mysqlProcess -and -not $mysqlProcess.HasExited) {
        & (Join-Path $mysqlBin 'mysqladmin.exe') '--no-defaults' '--protocol=TCP' '-h127.0.0.1' "-P$mysqlPort" '-uroot' 'shutdown' 2>&1 | Out-Null
        if (-not $mysqlProcess.WaitForExit(15000)) { $mysqlProcess.Kill($true) }
    }
    if ($pgProcess -and -not $pgProcess.HasExited) {
        & (Join-Path $pgBin 'pg_ctl.exe') '-D' $pgData '-m' 'fast' '-w' 'stop' 2>&1 | Out-Null
        if (-not $pgProcess.WaitForExit(15000)) { $pgProcess.Kill($true) }
    }
    if ($sqlCreated) {
        & sqlcmd '-S' "(localdb)\$instance" '-E' '-Q' "IF DB_ID('$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END" 2>&1 | Out-Null
        & SqlLocalDB stop $instance -k 2>&1 | Out-Null
        & SqlLocalDB delete $instance 2>&1 | Out-Null
    }
    Write-Output 'Disposable database instances stopped; existing services and databases were not used.'
    $resolvedRunRoot = [IO.Path]::GetFullPath($runRoot)
    $processes = Get-CimInstance Win32_Process
    foreach ($dataPath in @($mysqlData, $pgData)) {
        if (!(Test-Path -LiteralPath $dataPath)) { continue }
        $resolvedDataPath = (Resolve-Path -LiteralPath $dataPath).Path
        if (!$resolvedDataPath.StartsWith($resolvedRunRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup boundary violation' }
        if ((Get-Item -LiteralPath $resolvedDataPath).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse point refused' }
        if ($processes | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($resolvedDataPath, [StringComparison]::OrdinalIgnoreCase) }) { throw 'Database process still active' }
        Remove-Item -LiteralPath $resolvedDataPath -Recurse -Force
    }
}
