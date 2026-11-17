using System.Collections.Immutable;
using System.Net;
using Dapper;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Office.Tests;

[TestClass]
public sealed class MailImportServiceTests
{
    [TestMethod]
    public async Task ExplicitMicrosoftConnectionChecksReadPermissionAndFreezesTheRemoteIdentity()
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-mail-connect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var secrets = new WindowsUserSecretProtector(); var validator = new ApplicationOptionsValidator(secrets); var store = new ConfigurationStore(new(root), validator);
            var account = new MailAccountSettings { Id = Guid.NewGuid(), Provider = MailProviderKind.Microsoft365, DisplayName = "Synthetic", Enabled = false, ClientId = Guid.NewGuid().ToString() };
            await store.SaveAsync(new() { MailAccounts = [account] }, await store.LoadAsync());
            var auth = new ConnectAuthentication(); using var client = new HttpClient(new FolderResponse()); var http = new MailHttp(client, auth);
            var accounts = new MailAccounts(store, validator, secrets, auth, new MailConnectorFactory(http), new(secrets), http);
            var connected = await accounts.ConnectAsync(account.Id); Assert.IsTrue(connected.Enabled); Assert.AreEqual("synthetic-identity", connected.RemoteIdentity);
            Assert.AreEqual(1, auth.Interactive); auth.Identity = "another-identity";
            await Assert.ThrowsExactlyAsync<MailException>(() => accounts.ConnectAsync(account.Id));
            Assert.AreEqual("synthetic-identity", (await accounts.GetAsync(account.Id)).RemoteIdentity);
            await accounts.DisconnectAsync(account.Id); Assert.IsFalse((await accounts.GetAsync(account.Id)).Enabled);
        }
        finally { Directory.Delete(root, true); }
    }
    [TestMethod]
    public async Task InterruptedImportResumesTheCommittedCursorAndDoesNotExposePlaintextOrOtherCustomers()
    {
        await using var fixture = await Fixture.Create(); var calls = 0;
        fixture.Connector.Changes = (_, _, page, _) =>
        {
            calls++;
            if (page is null) return Task.FromResult(new MailChangePage([fixture.Message("one", "customer@example.test")], [], "private-next", null));
            if (calls == 2) throw new MailException(MailFailure.ProviderUnavailable);
            Assert.AreEqual("private-next", page);
            return Task.FromResult(new MailChangePage([fixture.Message("two", "other@example.test")], [], null, "private-checkpoint"));
        };
        await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Sync.SynchronizeAsync(fixture.Account.Id));
        Assert.AreEqual(1, (await fixture.Source.GetMessagesAsync(fixture.Folder, "")).Length);
        await fixture.Sync.SynchronizeAsync(fixture.Account.Id);
        Assert.AreEqual(3, calls); Assert.AreEqual(2, (await fixture.Source.GetMessagesAsync(fixture.Folder, "")).Length);
        var customer = fixture.Source.ForParty(fixture.Party("customer@example.test"));
        var list = await customer.GetMessagesAsync(fixture.Folder, "private body"); Assert.AreEqual(1, list.Length);
        Assert.AreEqual("one", list[0].Key.Id); Assert.IsTrue(list[0].IsRead);
        Assert.IsNull(await customer.GetMessageAsync(fixture.Message("two", "other@example.test").Content.Key));
        Assert.AreEqual(0, (await fixture.Source.ForParty(fixture.Party(null)).GetMessagesAsync(fixture.Folder, "")).Length);
        var anotherCompany = fixture.Party("customer@example.test") with { Key = new(new(LexwareProductLine.Pro, "test", "other"), PartyKind.Customer, "1") };
        Assert.IsTrue((await fixture.Source.ForParty(anotherCompany).GetFoldersAsync()).Folders.IsEmpty);
        await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Source.ForParty(anotherCompany).GetMessagesAsync(fixture.Folder, ""));
        await using var db = new DatabaseFactory().CreateConnection(fixture.Target); await db.OpenAsync();
        var payload = await db.QuerySingleAsync<string>("SELECT payload FROM mailmessages WHERE sourcehash=@hash", new { hash = MailIdentity.Hash("one") });
        Assert.DoesNotContain("private body", payload); Assert.DoesNotContain("customer@example.test", payload);
        var cursor = await db.QuerySingleAsync<string>("SELECT checkpoint FROM mailsyncstate"); Assert.DoesNotContain("private-checkpoint", cursor);
    }

    [TestMethod]
    public async Task GlobalHistoryCannotImportUnselectedFoldersAndExpiredCursorStartsASafeSnapshot()
    {
        await using var fixture = await Fixture.Create();
        var settings = await fixture.Store.LoadAsync();
        await fixture.Store.SaveAsync(settings.Options with { MailAccounts = [fixture.Account with { MaximumMessages = 1 }] }, settings);
        fixture.Connector.Changes = (_, _, _, _) => Task.FromResult(new MailChangePage([fixture.Message("one", "customer@example.test"), fixture.Message("outside", "customer@example.test") with { FolderIds = ["private-folder"] }], [], null, "checkpoint1"));
        await fixture.Sync.SynchronizeAsync(fixture.Account.Id);
        Assert.AreEqual(1, (await fixture.Source.GetMessagesAsync(fixture.Folder, "")).Length);
        Assert.IsNull(await fixture.Repository.GetStableAsync(fixture.Account.Id, MailIdentity.Hash("outside")));
        var calls = 0;
        fixture.Connector.Changes = (_, checkpoint, _, _) =>
        {
            if (++calls == 1) { Assert.AreEqual("checkpoint1", checkpoint); return Task.FromResult(new MailChangePage([], [], null, null, true)); }
            Assert.IsNull(checkpoint); return Task.FromResult(new MailChangePage([fixture.Message("new", "customer@example.test")], [], null, "checkpoint2"));
        };
        await fixture.Sync.SynchronizeAsync(fixture.Account.Id);
        var result = await fixture.Source.GetMessagesAsync(fixture.Folder, ""); Assert.AreEqual(1, result.Length); Assert.AreEqual("new", result[0].Key.Id);
        Assert.IsTrue((await fixture.Repository.GetStableAsync(fixture.Account.Id, MailIdentity.Hash("one")))!.Hidden);
    }

    [TestMethod]
    public async Task DisableAndMailboxRebindingBlockStoredContentAndMalformedPagesDoNotAdvanceState()
    {
        await using var fixture = await Fixture.Create();
        fixture.Connector.Changes = (_, _, _, _) => Task.FromResult(new MailChangePage([fixture.Message("one", "customer@example.test")], [], null, "checkpoint"));
        await fixture.Sync.SynchronizeAsync(fixture.Account.Id);
        fixture.Connector.Changes = (_, _, _, _) => Task.FromResult(new MailChangePage([], [], null, null));
        await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Sync.SynchronizeAsync(fixture.Account.Id));
        Assert.AreEqual(1, (await fixture.Source.GetMessagesAsync(fixture.Folder, "")).Length);
        var snapshot = await fixture.Store.LoadAsync(); await fixture.Store.SaveAsync(snapshot.Options with { MailAccounts = [fixture.Account with { Enabled = false }] }, snapshot);
        await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Source.GetMessageAsync(fixture.Message("one", "customer@example.test").Content.Key));
        snapshot = await fixture.Store.LoadAsync(); await fixture.Store.SaveAsync(snapshot.Options with { MailAccounts = [fixture.Account with { OutlookStoreId = "another-store" }] }, snapshot);
        await Assert.ThrowsAsync<RepositoryConflictException>(() => fixture.Source.GetMessagesAsync(fixture.Folder, ""));
        snapshot = await fixture.Store.LoadAsync();
        await fixture.Store.SaveAsync(snapshot.Options with { MailAccounts = [fixture.Account with { Provider = MailProviderKind.Microsoft365, ClientId = Guid.NewGuid().ToString() }] }, snapshot);
        var missingConnection = await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Sync.SynchronizeAsync(fixture.Account.Id));
        Assert.AreEqual(MailFailure.SignInRequired, missingConnection.Failure);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wcp-mail-sync-" + Guid.NewGuid().ToString("N"));
        internal MailAccountSettings Account { get; } = new() { Id = Guid.NewGuid(), Provider = MailProviderKind.OutlookDesktop, DisplayName = "Synthetic", FolderIds = ["inbox"], FolderNames = ImmutableDictionary<string, string>.Empty.Add("inbox", "Posteingang"), ImportDays = 0, Company = new(LexwareProductLine.Pro, "test", "company") };
        internal ConfigurationStore Store { get; private set; } = null!;
        internal DatabaseTarget Target { get; private set; } = null!;
        internal DapperMailImportRepository Repository { get; private set; } = null!;
        internal Connector Connector { get; private set; } = null!;
        internal MailImportService Sync { get; private set; } = null!;
        internal ImportedMailViewSource Source { get; private set; } = null!;
        internal MailFolderKey Folder => new("outlook", Account.Id.ToString("N"), "inbox");
        internal static async Task<Fixture> Create()
        {
            var result = new Fixture(); Directory.CreateDirectory(result.Root);
            var secrets = new WindowsUserSecretProtector(); var validator = new ApplicationOptionsValidator(secrets);
            result.Store = new(new(result.Root), validator); await result.Store.SaveAsync(new() { MailAccounts = [result.Account] }, await result.Store.LoadAsync());
            result.Target = DatabaseTarget.Sqlite(Path.Combine(result.Root, "mail.db"));
            await new DatabaseProvisioner(new()).CreateAsync(result.Target); await new DatabaseMigrator(new(), new FluentMigratorEngine(DatabaseType.Sqlite)).MigrateAsync(result.Target);
            result.Repository = new(new(result.Target, new())); result.Connector = new(result.Account.Id);
            var http = new MailHttp(new HttpClient(new NoNetwork()), new Authentication());
            var accounts = new MailAccounts(result.Store, validator, secrets, new Authentication(), new Factory(result.Connector), new(secrets), http);
            result.Sync = new(accounts, new Factory(result.Connector), result.Repository, new(secrets)); result.Source = new(accounts, result.Repository, new(secrets)); return result;
        }
        internal MailEnvelope Message(string id, string address) => new(new(new("outlook", Account.Id.ToString("N"), id), "Synthetic subject", address, "owner@example.test", "", DateTimeOffset.UtcNow, "private body", null, []), null, address, [], ["inbox"], id) { IsRead = true };
        internal Party Party(string? email) => new() { Key = new(Account.Company!, PartyKind.Customer, "1"), DisplayName = "Synthetic customer", Email = email };
        public ValueTask DisposeAsync() { Directory.Delete(Root, true); return ValueTask.CompletedTask; }
    }
    private sealed class Factory(Connector connector) : IMailConnectorFactory { public IMailConnector Create(MailAccountSettings account) => connector; }
    private sealed class Connector(Guid id) : IMailConnector
    {
        public Guid AccountId => id; public string Provider => "outlook";
        internal Func<string, string?, string?, CancellationToken, Task<MailChangePage>> Changes { get; set; } = null!;
        public Task<MailChangePage> GetChangesAsync(string folderId, string? checkpoint, string? nextPage, CancellationToken cancellationToken = default) => Changes(folderId, checkpoint, nextPage, cancellationToken);
        public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default) => Task.FromResult(new MailFolderCatalog([], ""));
        public Task<ImmutableArray<MailListItem>> GetMessagesAsync(string folderId, string search, CancellationToken cancellationToken = default) => throw new AssertFailedException("The browser must use persisted data.");
        public Task<MailEnvelope?> GetMessageAsync(string messageId, CancellationToken cancellationToken = default) => throw new AssertFailedException("The browser must use persisted data.");
        public Task<byte[]> GetAttachmentAsync(string messageId, string attachmentId, CancellationToken cancellationToken = default) => throw new AssertFailedException();
    }
    private sealed class NoNetwork : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new AssertFailedException("No real network access permitted."); }
    private sealed class FolderResponse : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.AreEqual(HttpMethod.Get, request.Method); Assert.Contains("/mailFolders", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"value\":[]}") });
        }
    }
    private sealed class ConnectAuthentication : IMailAuthentication
    {
        internal int Interactive; internal string Identity = "synthetic-identity";
        public Task<MailAccessToken> GetAsync(MailAccountSettings account, bool forceRefresh, CancellationToken cancellationToken) => Task.FromResult(new MailAccessToken("synthetic-token", Identity));
        public Task<MailAccessToken> ConnectAsync(MailAccountSettings account, CancellationToken cancellationToken) { Interactive++; return GetAsync(account, false, cancellationToken); }
        public Task DisconnectAsync(MailAccountSettings account, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class Authentication : IMailAuthentication
    {
        public Task<MailAccessToken> GetAsync(MailAccountSettings account, bool forceRefresh, CancellationToken token) => throw new AssertFailedException();
        public Task<MailAccessToken> ConnectAsync(MailAccountSettings account, CancellationToken token) => throw new AssertFailedException();
        public Task DisconnectAsync(MailAccountSettings account, CancellationToken token) => Task.CompletedTask;
    }
}
