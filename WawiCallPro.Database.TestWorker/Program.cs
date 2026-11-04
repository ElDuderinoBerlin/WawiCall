using System.Text.Json;
using System.Text.Json.Serialization;
using WawiCallPro.Database;

// Test-only process boundary. Arguments contain a configuration file path, never provider credentials.
try
{
    var options = new JsonSerializerOptions(); options.Converters.Add(new JsonStringEnumConverter());
    var input = JsonSerializer.Deserialize<WorkerOptions>(await File.ReadAllTextAsync(args[1]), options)!;
    var target = input.Type == DatabaseType.Sqlite ? DatabaseTarget.Sqlite(input.SqlitePath!) :
        DatabaseTarget.Server(input.Type, input.ConnectionString!, 5, 10);
    var gate = new DatabaseMigrationLock();
    var timeout = TimeSpan.FromMilliseconds(input.LockTimeoutMilliseconds);
    if (args[0] == "lease")
    {
        await using var lease = await gate.AcquireAsync(target, timeout);
        await File.WriteAllTextAsync(args[2], "ready");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        while (!File.Exists(args[3])) await Task.Delay(20, deadline.Token);
        await lease.EnsureHeldAsync();
        Console.WriteLine("released");
    }
    else if (args[0] == "migrate")
    {
        var factory = new DatabaseFactory();
        var migrator = new DatabaseMigrator(factory, new FluentMigratorEngine(target.Type), gate,
            new DatabaseMigrationPermissions(factory), timeout);
        var status = await migrator.MigrateAsync(target);
        Console.WriteLine(JsonSerializer.Serialize(new { State = status.State.ToString(), Versions = status.AppliedVersions }));
    }
    else throw new ArgumentException();
    return 0;
}
catch (DatabaseOperationException exception)
{
    Console.WriteLine(exception.Code.ToString());
    return 2;
}
catch
{
    Console.WriteLine("WorkerFailed");
    return 3;
}

internal sealed record WorkerOptions(DatabaseType Type, string? SqlitePath, string? ConnectionString, int LockTimeoutMilliseconds = 5000);
