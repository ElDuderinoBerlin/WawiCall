using System.Data;
using System.Reflection;
using System.Security.Principal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Core.Threading;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class PersistenceHostTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-persistence-host-" + Guid.NewGuid().ToString("N"));
    private ApplicationPaths Paths => new(new ConfigurationLocation(_root));
    private IHost CreateHost() => WawiCallHost.CreateBuilder(Assembly.GetExecutingAssembly()).ConfigureServices(services =>
    {
        services.AddSingleton(new ConfigurationLocation(_root));
        services.AddSingleton<IUiDispatcher, InlineUiDispatcher>();
    }).Build();
    [TestCleanup] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [TestMethod]
    public async Task DependencyGraphResolvesBeforeStartupWithoutTouchingConfigurationDatabaseOrIdentity()
    {
        var host = CreateHost();
        foreach (var service in new[] { typeof(ICommunicationDatabase), typeof(ICompanyRepository), typeof(IPartyCacheRepository), typeof(ICallRepository), typeof(ICallNoteRepository), typeof(IRecordingRepository), typeof(INumberParseLogRepository), typeof(IPhoneMatchingRepository), typeof(ICallHistoryService), typeof(ICallJournal), typeof(ICallOriginProvider), typeof(ISqliteDatabaseBackup) })
            Assert.IsNotNull(host.Services.GetRequiredService(service));
        Assert.IsFalse(Directory.Exists(_root));
        await HostExecution.RunAsync(host, async token =>
        {
            var database = host.Services.GetRequiredService<ICommunicationDatabase>();
            Assert.AreEqual(DatabaseSchemaState.MissingDatabase, (await database.GetStatusAsync(token)).State);
            Assert.IsNull(await host.Services.GetRequiredService<IWorkstationIdentity>().GetAsync(token));
            Assert.IsFalse(File.Exists(Path.Combine(_root, "connection.local.json")));
            Assert.IsFalse(File.Exists(Path.Combine(_root, "communications.db")));
            Assert.IsFalse(File.Exists(Path.Combine(_root, "communications.db.migration.lock")));
        });
    }

    [TestMethod]
    public async Task ExplicitSetupReportsProgressAndJournalCapturesActualOriginForPersistentHistory()
    {
        var host = CreateHost(); Guid id = default; Guid callId = default;
        await HostExecution.RunAsync(host, async token =>
        {
            var database = host.Services.GetRequiredService<ICommunicationDatabase>(); var progress = new ProgressCapture();
            Assert.AreEqual(DatabaseSchemaState.Current, (await database.CreateAndMigrateAsync(progress, token)).State);
            Assert.AreEqual(DatabaseMigrationPhase.Completed, progress.Values.Last().Phase);
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 5, 6, 7 }, progress.Values.Where(value => value.Phase == DatabaseMigrationPhase.Applying).Select(value => value.Version!.Value).ToArray());
            id = (await host.Services.GetRequiredService<IWorkstationIdentity>().GetAsync(token))!.Value;
            var started = DateTimeOffset.UtcNow;
            var call = new CallSnapshot { Id = Guid.NewGuid(), Revision = 0, Direction = CallDirection.Outgoing, State = CallState.Created,
                LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "42"), StartedAtUtc = started, StateChangedAtUtc = started };
            callId = call.Id;
            var saved = await host.Services.GetRequiredService<ICallJournal>().BeginAsync(call, token);
            Assert.AreEqual(id, saved.Origin!.WorkstationId); Assert.AreEqual(Environment.MachineName, saved.Origin.MachineName);
            if (OperatingSystem.IsWindows()) { using var identity = WindowsIdentity.GetCurrent(); Assert.AreEqual(identity.Name, saved.Origin.WindowsUserName); }
            var history = host.Services.GetRequiredService<ICallHistoryService>();
            var noteId = Guid.NewGuid(); await history.SaveNoteAsync(call.Id, noteId, "Notiz Ä 电话", token);
            await history.SaveNoteAsync(call.Id, noteId, "Bearbeitet", token);
            Assert.AreEqual("Bearbeitet", (await history.GetCallAsync(call.Id, token))!.Notes.Single().Text);
            Assert.AreEqual(1, (await history.GetCallsAsync(new(new(0, 10)) { WorkstationId = id }, token)).Items.Length);
            Assert.AreEqual(DatabaseErrorCode.AlreadyExists, (await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => database.CreateAndMigrateAsync(cancellationToken: token))).Code);
            Assert.AreEqual(DatabaseSchemaState.Current, (await database.MigrateAsync(cancellationToken: token)).State);
        });
        var restarted = CreateHost();
        await HostExecution.RunAsync(restarted, async token =>
        {
            Assert.AreEqual(id, await restarted.Services.GetRequiredService<IWorkstationIdentity>().GetAsync(token));
            Assert.AreEqual(id, (await restarted.Services.GetRequiredService<ICallHistoryService>().GetCallAsync(callId, token))!.Call.Origin!.WorkstationId);
        });
    }

    [TestMethod]
    public async Task ConcurrentIdentityInitializationIsAtomicAndCorruptOrNewerFilesAreNeverReplaced()
    {
        var identities = Enumerable.Range(0, 12).Select(_ => new WorkstationIdentity(Paths)).ToArray();
        Assert.IsNull(await identities[0].GetAsync()); Assert.IsFalse(Directory.Exists(_root));
        var values = await Task.WhenAll(identities.Select(identity => identity.InitializeAsync()));
        Assert.AreEqual(1, values.Distinct().Count()); Assert.AreNotEqual(Guid.Empty, values[0]);
        var file = Path.Combine(_root, "workstation.local.json");
        foreach (var text in new[] { "", "{}", "{\"formatVersion\":2,\"workstationId\":\"" + values[0].ToString("N") + "\"}", "{\"formatVersion\":1,\"workstationId\":\"00000000000000000000000000000000\"}" })
        {
            await File.WriteAllTextAsync(file, text);
            await Assert.ThrowsExactlyAsync<ConfigurationException>(() => identities[0].InitializeAsync());
            Assert.AreEqual(text, await File.ReadAllTextAsync(file));
        }
        Assert.AreEqual(0, Directory.GetFiles(_root, "*.tmp").Length);
    }

    [TestMethod]
    public async Task CaptureRequiresExplicitSetupAndCancellationDoesNotInventAnIdentity()
    {
        var identity = new WorkstationIdentity(Paths);
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => new WindowsCallOriginProvider(identity).CaptureAsync());
        Assert.IsFalse(Directory.Exists(_root));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => identity.InitializeAsync(canceled.Token));
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public async Task ProtectedServerTargetsResolveLazilyForEveryProviderAndStayClosed()
    {
        var examples = new[]
        {
            (CommunicationDatabaseProvider.SqlServer, "Server=synthetic.invalid;Database=communications;User ID=user;Password=sentinel-secret", DatabaseType.SqlServer),
            (CommunicationDatabaseProvider.MySql, "Server=synthetic.invalid;Database=communications;User ID=user;Password=sentinel-secret", DatabaseType.MySql),
            (CommunicationDatabaseProvider.PostgreSql, "Host=synthetic.invalid;Database=communications;Username=user;Password=sentinel-secret", DatabaseType.PostgreSql)
        };
        foreach (var (provider, connection, expected) in examples)
        {
            var host = CreateHost(); var store = host.Services.GetRequiredService<ConfigurationStore>();
            var loaded = await store.LoadAsync();
            var protector = host.Services.GetRequiredService<ISecretProtector>();
            await store.SaveAsync(new() { Database = new() { Provider = provider, ConnectionString = protector.Protect(connection, CommunicationDatabaseSettings.ConnectionStringPurpose) } }, loaded);
            await HostExecution.RunAsync(host, token =>
            {
                var source = host.Services.GetRequiredService<IDatabaseTargetSource>(); var target = source.GetTarget();
                Assert.AreSame(target, source.GetTarget()); Assert.AreEqual(expected, target.Type);
                using var db = host.Services.GetRequiredService<DatabaseFactory>().CreateConnection(target); Assert.AreEqual(ConnectionState.Closed, db.State);
                Assert.DoesNotContain("sentinel-secret", System.Text.Json.JsonSerializer.Serialize(target));
                return Task.CompletedTask;
            });
        }
        Assert.AreEqual(0, Directory.GetFiles(_root, "*.db").Length);
    }

    [TestMethod]
    public void InvalidServerTargetsAreRejectedBeforeAnyIoWithFixedMessages()
    {
        var protector = new WindowsUserSecretProtector(); var validator = new ApplicationOptionsValidator(protector);
        foreach (var connection in new[] { "Server=x;Database=master;Password=sentinel-secret", "sentinel-secret", "Server=x;Password=sentinel-secret" })
        {
            var failure = Assert.ThrowsExactly<ConfigurationException>(() => validator.Validate(new() { Database = new() { Provider = CommunicationDatabaseProvider.SqlServer, ConnectionString = protector.Protect(connection, CommunicationDatabaseSettings.ConnectionStringPurpose) } }));
            Assert.DoesNotContain("sentinel-secret", failure.ToString());
        }
        Assert.IsFalse(Directory.Exists(_root));
    }

    private sealed class ProgressCapture : IProgress<DatabaseMigrationProgress>
    {
        internal List<DatabaseMigrationProgress> Values { get; } = [];
        public void Report(DatabaseMigrationProgress value) => Values.Add(value);
    }
}
