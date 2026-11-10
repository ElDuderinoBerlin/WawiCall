using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using Npgsql;
using WawiCallPro.Core.Threading;
using WawiCallPro.Database;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

/// <summary>Exercises the wizard's real DI service on disposable native server instances owned by the acceptance script.</summary>
public static class DatabaseSetupAcceptance
{
    public static async Task RunAsync(DatabaseType type, string connectionString, string artifactDirectory)
    {
        var candidate = type switch
        {
            DatabaseType.SqlServer => SqlCandidate(connectionString),
            DatabaseType.MySql => MySqlCandidate(connectionString),
            DatabaseType.PostgreSql => PostgresCandidate(connectionString), _ => throw new ArgumentException("Server required.")
        };
        var host = WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(Path.Combine(artifactDirectory, "setup-" + Guid.NewGuid().ToString("N"))));
            services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
        }).Build();
        await HostExecution.RunAsync(host, async token =>
        {
            var setup = host.Services.GetRequiredService<IDatabaseSetupService>();
            var draft = new DatabaseSetupDraft { Type = type, ConnectionString = candidate };
            var snapshot = await setup.LoadAsync(token);
            Assert.IsTrue((await setup.TestAsync(draft, token)).Success);
            Assert.IsTrue((await setup.InspectAsync(draft, token)).State is DatabaseSchemaState.MissingDatabase or DatabaseSchemaState.MissingOrInaccessible);
            Assert.AreEqual(DatabaseSchemaState.Current, (await setup.InitializeAsync(draft, false, cancellationToken: token)).State);
            Assert.AreEqual(DatabaseSchemaState.Current, (await setup.InitializeAsync(draft, false, cancellationToken: token)).State);
            await setup.SaveAsync(draft, snapshot, token);
            var saved = setup.ReadDraft(await setup.LoadAsync(token));
            Assert.AreEqual(type, saved.Type);
            Assert.AreEqual(candidate, saved.ConnectionString);
            Assert.AreEqual(DatabaseSchemaState.Current, (await setup.InspectAsync(saved, token)).State);
        });
    }
    private static string SqlCandidate(string connection) { var builder = new SqlConnectionStringBuilder(connection); builder.InitialCatalog += "_wizard"; return builder.ConnectionString; }
    private static string MySqlCandidate(string connection) { var builder = new MySqlConnectionStringBuilder(connection); builder.Database += "_wizard"; return builder.ConnectionString; }
    private static string PostgresCandidate(string connection) { var builder = new NpgsqlConnectionStringBuilder(connection); builder.Database += "_wizard"; return builder.ConnectionString; }
}
