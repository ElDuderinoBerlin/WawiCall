using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Database;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

/// <summary>Invoked by the native acceptance script after provisioning its private disposable server database.</summary>
public static class NativeHostAcceptance
{
    public static async Task RunAsync(DatabaseType type, string connectionString, string artifactDirectory)
    {
        var root = Path.Combine(artifactDirectory, "host-" + Guid.NewGuid().ToString("N"));
        var host = WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(root));
            services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
        }).Build();
        var protector = host.Services.GetRequiredService<ISecretProtector>();
        var provider = type switch
        {
            DatabaseType.SqlServer => CommunicationDatabaseProvider.SqlServer,
            DatabaseType.MySql => CommunicationDatabaseProvider.MySql,
            DatabaseType.PostgreSql => CommunicationDatabaseProvider.PostgreSql,
            _ => throw new ArgumentException("Native server required.")
        };
        await host.Services.GetRequiredService<ConfigurationStore>().SaveAsync(new()
        {
            Database = new() { Provider = provider, ConnectionString = protector.Protect(connectionString, CommunicationDatabaseSettings.ConnectionStringPurpose) }
        });
        await HostExecution.RunAsync(host, async token =>
        {
            var database = host.Services.GetRequiredService<ICommunicationDatabase>();
            Assert.AreEqual(DatabaseSchemaState.Current, (await database.GetStatusAsync(token)).State);
            var progress = new CaptureProgress();
            Assert.AreEqual(DatabaseSchemaState.Current, (await database.MigrateAsync(progress: progress, cancellationToken: token)).State);
            Assert.AreEqual(DatabaseMigrationPhase.Completed, progress.Values.Last().Phase);
            var workstation = await host.Services.GetRequiredService<IWorkstationIdentity>().InitializeAsync(token);
            var now = DateTimeOffset.UtcNow;
            var call = new CallSnapshot { Id = Guid.NewGuid(), Revision = 0, Direction = CallDirection.Outgoing, State = CallState.Created,
                StartedAtUtc = now, StateChangedAtUtc = now, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "internal-42") };
            var saved = await host.Services.GetRequiredService<ICallJournal>().BeginAsync(call, token);
            Assert.AreEqual(workstation, saved.Origin!.WorkstationId); Assert.AreEqual(Environment.MachineName, saved.Origin.MachineName);
            var history = host.Services.GetRequiredService<ICallHistoryService>();
            await history.SaveNoteAsync(call.Id, Guid.NewGuid(), "Host note Ä 电话", token);
            Assert.AreEqual("Host note Ä 电话", (await history.GetCallAsync(call.Id, token))!.Notes.Single().Text);
            Assert.AreEqual(1, (await history.GetCallsAsync(new(new(0, 10)) { WorkstationId = workstation }, token)).Items.Length);
            await PhoneMatchingAcceptance.RunAsync(host.Services, token);
            await PhoneSearchIndexTests.RunAcceptanceAsync(host.Services);
            await CallAssociationAcceptance.RunAsync(host.Services, token);
            await PhoneNumberDiagnosticsAcceptance.RunAsync(host.Services, token);
        });
        Console.WriteLine(type + ": real DI host with DPAPI settings, schema status/progress, workstation, history, StartWidth, cross-company phone index and source-number diagnostics passed.");
    }

    private sealed class CaptureProgress : IProgress<DatabaseMigrationProgress>
    {
        internal List<DatabaseMigrationProgress> Values { get; } = [];
        public void Report(DatabaseMigrationProgress value) => Values.Add(value);
    }
}
