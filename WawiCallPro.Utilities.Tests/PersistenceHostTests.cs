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
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
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
            CollectionAssert.AreEqual(new long[] { 1, 2, 3, 4, 5, 6, 7, 8 }, progress.Values.Where(value => value.Phase == DatabaseMigrationPhase.Applying).Select(value => value.Version!.Value).ToArray());
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
    public async Task IdentityReadWaitsForTransientWindowsSharingLockWithoutReplacingTheIdentifier()
    {
        var identity = new WorkstationIdentity(Paths); var expected = await identity.InitializeAsync();
        Task<Guid?> read;
        using (var blocked = new FileStream(Path.Combine(_root, "workstation.local.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            read = identity.GetAsync();
            await Task.Delay(30);
            Assert.IsFalse(read.IsCompleted);
        }
        Assert.AreEqual(expected, await read);
        Assert.AreEqual(expected, await identity.InitializeAsync());
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

    [TestMethod]
    public async Task SetupWizardProbesWithoutCreatingThenInitializesAndSavesOnlyItsDatabaseScope()
    {
        var host = CreateHost();
        await HostExecution.RunAsync(host, async token =>
        {
            var setup = host.Services.GetRequiredService<IDatabaseSetupService>();
            var active = host.Services.GetRequiredService<ICommunicationDatabase>();
            var original = await setup.LoadAsync(token);
            var secrets = host.Services.GetRequiredService<ISecretProtector>();
            var options = original.Options with
            {
                User = original.Options.User with { ShowCallNotifications = false },
                SipAccount = new() { Server = "fixture.invalid", Username = "fixture",
                    Password = secrets.Protect("synthetic-setup-password", SipAccountOptions.PasswordPurpose) }
            };
            await host.Services.GetRequiredService<ConfigurationStore>().SaveAsync(options, original, token);
            var snapshot = await setup.LoadAsync(token);
            var draft = new DatabaseSetupDraft { Type = DatabaseType.Sqlite, SqlitePath = Path.Combine(_root, "selected", "communications.db") };
            Assert.IsTrue((await setup.TestAsync(draft, token)).Success);
            Assert.AreEqual(DatabaseSchemaState.MissingDatabase, (await setup.InspectAsync(draft, token)).State);
            Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(draft.SqlitePath)));
            Assert.AreEqual(DatabaseSchemaState.MissingDatabase, (await active.GetStatusAsync(token)).State);
            Assert.AreEqual(DatabaseSchemaState.Current, (await setup.InitializeAsync(draft, false, cancellationToken: token)).State);
            Assert.IsTrue(File.Exists(draft.SqlitePath));
            Assert.IsTrue((await setup.TestAsync(draft, token)).Success);
            await setup.SaveAsync(draft, snapshot, token);
            var saved = await setup.LoadAsync(token);
            Assert.AreEqual(draft.SqlitePath, saved.Options.Database.SqliteFileName);
            Assert.AreEqual(options.User, saved.Options.User);
            Assert.AreEqual(options.SipAccount.Server, saved.Options.SipAccount!.Server);
            Assert.AreEqual(options.SipAccount.Connection, saved.Options.SipAccount.Connection);
            using var password = secrets.Unprotect(saved.Options.SipAccount.Password!, SipAccountOptions.PasswordPurpose);
            Assert.AreEqual("synthetic-setup-password", password.Reveal());
            Assert.AreEqual(DatabaseSchemaState.MissingDatabase, (await active.GetStatusAsync(token)).State, "Live repositories keep the startup target until restart.");
            await Assert.ThrowsExactlyAsync<ConfigurationException>(() => setup.SaveAsync(draft, snapshot, token));
            Assert.DoesNotContain("synthetic-setup-password", await File.ReadAllTextAsync(Path.Combine(_root, "connection.local.json"), token));
        });
    }

    [TestMethod]
    public async Task SetupWizardRequiresEmptyDatabaseApprovalAndRejectsForeignTables()
    {
        var host = CreateHost();
        await HostExecution.RunAsync(host, async token =>
        {
            var setup = host.Services.GetRequiredService<IDatabaseSetupService>();
            Directory.CreateDirectory(_root);
            var draft = new DatabaseSetupDraft { Type = DatabaseType.Sqlite, SqlitePath = Path.Combine(_root, "empty.db") };
            var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = draft.SqlitePath, Pooling = false };
            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(builder.ConnectionString)) { await connection.OpenAsync(token); }
            Assert.AreEqual(DatabaseSchemaState.EmptyDatabase, (await setup.InspectAsync(draft, token)).State);
            await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => setup.InitializeAsync(draft, false, cancellationToken: token));
            Assert.AreEqual(DatabaseSchemaState.Current, (await setup.InitializeAsync(draft, true, cancellationToken: token)).State);
            var foreign = new DatabaseSetupDraft { Type = DatabaseType.Sqlite, SqlitePath = Path.Combine(_root, "foreign.db") };
            builder.DataSource = foreign.SqlitePath;
            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(builder.ConnectionString))
            {
                await connection.OpenAsync(token);
                await using var command = connection.CreateCommand(); command.CommandText = "CREATE TABLE foreign_data (id INTEGER);";
                await command.ExecuteNonQueryAsync(token);
            }
            Assert.AreEqual(DatabaseSchemaState.ForeignDatabase, (await setup.InspectAsync(foreign, token)).State);
            await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => setup.InitializeAsync(foreign, true, cancellationToken: token));
        });
    }

    [TestMethod]
    public void SetupPathsRemainExplicitAndProbeConnectionsAreClosedForAllProviders()
    {
        var validator = new ApplicationOptionsValidator(new WindowsUserSecretProtector());
        validator.Validate(new() { Database = new() { SqliteFileName = Path.Combine(_root, "selected.db") } });
        foreach (var path in new[] { "../outside.db", "folder/database.db", Path.Combine(_root, "CON.db") })
            Assert.ThrowsExactly<ConfigurationException>(() => validator.Validate(new() { Database = new() { SqliteFileName = path } }));
        foreach (var type in new[] { DatabaseType.SqlServer, DatabaseType.MySql, DatabaseType.PostgreSql })
        {
            var connectionString = type == DatabaseType.PostgreSql ? "Host=synthetic.invalid;Database=communications;Username=fixture;Password=sentinel" : "Server=synthetic.invalid;Database=communications;User ID=fixture;Password=sentinel";
            using var probe = new DatabaseFactory().CreateProbeConnection(DatabaseTarget.Server(type, connectionString));
            Assert.AreEqual(ConnectionState.Closed, probe.State);
            Assert.AreEqual(type == DatabaseType.SqlServer ? "master" : type == DatabaseType.PostgreSql ? "postgres" : "", probe.Database);
        }
        Assert.IsFalse(Directory.Exists(_root));
    }

    [TestMethod]
    public async Task PhoneMatchingUsesRealRepositoriesAndVersionedRulesWithoutChangingSourcePhones()
    {
        var host = CreateHost();
        await HostExecution.RunAsync(host, async token =>
        {
            await host.Services.GetRequiredService<ICommunicationDatabase>().CreateAndMigrateAsync(cancellationToken: token);
            await PhoneMatchingAcceptance.RunAsync(host.Services, token);
        });
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PhoneIndexRejectsConcurrentCacheOrRuleChangesDuringPaging(bool changeRules)
    {
        var host = CreateHost();
        await HostExecution.RunAsync(host, async token =>
        {
            var services = host.Services;
            await services.GetRequiredService<ICommunicationDatabase>().CreateAndMigrateAsync(cancellationToken: token);
            var descriptor = services.GetRequiredService<ApplicationDescriptor>();
            var company = new Company(new(Enum.Parse<LexwareProductLine>(descriptor.ProductLine), "race", "01"), "Synthetic race company");
            var companies = services.GetRequiredService<ICompanyRepository>();
            await companies.WriteAsync(new(company, null, DateTimeOffset.UtcNow.AddMinutes(-1)), token);
            var cache = services.GetRequiredService<IPartyCacheRepository>();
            await cache.ReplaceAsync(new(company.Key, 0, [], DateTimeOffset.UtcNow.AddMinutes(-1)), token);
            var rules = services.GetRequiredService<IPhoneMatchingRepository>();
            var wrapped = new InterleavingCache(cache, async () =>
            {
                if (changeRules) await rules.WriteSettingsAsync(new(new(company.Key, true, 4), null, DateTimeOffset.UtcNow), token);
                else await cache.ReplaceAsync(new(company.Key, 1, [], DateTimeOffset.UtcNow), token);
            });
            var matching = new CompanyPhoneMatchingService(companies, wrapped, rules,
                services.GetRequiredService<ICompanyPhoneMatchIndexFactory>(), descriptor, TimeProvider.System);
            var error = await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => matching.BuildIndexAsync(company.Key, token));
            Assert.AreEqual(RepositoryConflictKind.SnapshotMismatch, error.Kind);
        });
    }

    private sealed class InterleavingCache(IPartyCacheRepository inner, Func<Task> change) : IPartyCacheRepository
    {
        private int _changed;
        public Task<PartyCacheState> GetStateAsync(CompanyKey company, CancellationToken cancellationToken = default) => inner.GetStateAsync(company, cancellationToken);
        public Task<Party?> GetAsync(PartyKey key, CancellationToken cancellationToken = default) => inner.GetAsync(key, cancellationToken);
        public async Task<PartyPage> GetPageAsync(PartyQuery query, CancellationToken cancellationToken = default)
        {
            var page = await inner.GetPageAsync(query, cancellationToken);
            if (Interlocked.Exchange(ref _changed, 1) == 0) await change();
            return page;
        }
        public Task<PartyPage> FindByE164Async(CompanyKey company, string e164, WawiCallPro.Interfaces.Common.PageRequest page, CancellationToken cancellationToken = default) => inner.FindByE164Async(company, e164, page, cancellationToken);
        public Task<PartyCacheState> ReplaceAsync(PartyCacheReplacement replacement, CancellationToken cancellationToken = default) => inner.ReplaceAsync(replacement, cancellationToken);
    }

    private sealed class ProgressCapture : IProgress<DatabaseMigrationProgress>
    {
        internal List<DatabaseMigrationProgress> Values { get; } = [];
        public void Report(DatabaseMigrationProgress value) => Values.Add(value);
    }
}
