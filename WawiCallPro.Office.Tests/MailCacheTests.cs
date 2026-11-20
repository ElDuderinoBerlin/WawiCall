using System.Collections.Immutable;
using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.Sqlite;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Office.Tests;

[TestClass]
public sealed partial class MailCacheTests
{
    [TestMethod]
    public async Task SqliteCacheIncludesProtectedAttachmentBlobsAndWorksWithoutTheProvider()
    {
        await using var fixture = await Fixture.Create(); await fixture.Cache.SynchronizeAsync(fixture.Account.Id);
        fixture.Provider.Offline = true;
        var list = await fixture.Cache.GetMessagesAsync(fixture.Folder, "private body"); Assert.AreEqual(1, list.Length);
        Assert.AreEqual("private body", (await fixture.Cache.GetMessageAsync(list[0].Key))!.Text);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await fixture.Cache.GetAttachmentAsync(list[0].Key, "file", default));
        await using var db = new SqliteConnection("Data Source=" + fixture.CachePath + ";Pooling=False"); await db.OpenAsync();
        Assert.AreEqual("blob", await db.QuerySingleAsync<string>("SELECT typeof(payload) FROM mailattachmentblobs"));
        var binary = await db.QuerySingleAsync<byte[]>("SELECT payload FROM mailattachmentblobs"); Assert.IsFalse(binary.SequenceEqual(new byte[] { 1, 2, 3, 4 }));
        Assert.DoesNotContain("private body", await db.QuerySingleAsync<string>("SELECT payload FROM mailmessages"));
        Assert.AreEqual(1, await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM mailcacheversioninfo"));
        Assert.AreEqual(14, await db.QuerySingleAsync<int>("SELECT MAX(version) FROM versioninfo"));
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Docs", "Datenbank-Tabellen-und-Spalten.md"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        var documentation = await File.ReadAllLinesAsync(Path.Combine(directory.FullName, "Docs", "Datenbank-Tabellen-und-Spalten.md"));
        foreach (var table in new[] { "mailcacheinfo", "mailattachmentblobs", "mailcacheversioninfo" })
        {
            var columns = (await db.QueryAsync<string>("SELECT name FROM pragma_table_info(@table)", new { table })).ToArray();
            var described = documentation.SkipWhile(line => !line.StartsWith("## " + table + " ", StringComparison.Ordinal)).Skip(1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
                .Select(line => System.Text.RegularExpressions.Regex.Match(line, @"^\|\s*`([^`]+)`\s*\|")).Where(match => match.Success).Select(match => match.Groups[1].Value).ToArray();
            CollectionAssert.AreEquivalent(columns, described);
        }
    }
    [TestMethod]
    public async Task ExistingVersionThirteenCacheUpgradesContactSchemaWithoutReplacingMailOrBlobs()
    {
        await using var fixture = await Fixture.Create(); await fixture.Cache.SynchronizeAsync(fixture.Account.Id);
        await using (var db = new SqliteConnection("Data Source=" + fixture.CachePath + ";Pooling=False"))
        {
            await db.OpenAsync();
            await db.ExecuteAsync("DROP TABLE partyemails; ALTER TABLE parties DROP COLUMN contactfieldsversion; DELETE FROM versioninfo WHERE version=14;");
        }
        fixture.Provider.Offline = true;
        Assert.AreEqual("private body", (await fixture.Cache.GetMessageAsync(fixture.Message.Content.Key))!.Text);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await fixture.Cache.GetAttachmentAsync(fixture.Message.Content.Key, "file", default));
        await using var upgraded = new SqliteConnection("Data Source=" + fixture.CachePath + ";Pooling=False"); await upgraded.OpenAsync();
        Assert.AreEqual(14, await upgraded.QuerySingleAsync<int>("SELECT MAX(version) FROM versioninfo"));
    }
    [TestMethod]
    public async Task DisabledCacheUsesLiveReadsAndCreatesNoSqliteFile()
    {
        await using var fixture = await Fixture.Create(false);
        Assert.AreEqual(1, (await fixture.Cache.GetMessagesAsync(fixture.Folder, "")).Length);
        var content = await fixture.Cache.GetMessageAsync(fixture.Message.Content.Key); Assert.IsNotNull(content);
        await fixture.Cache.SynchronizeAsync(fixture.Account.Id);
        Assert.IsFalse(File.Exists(fixture.CachePath)); Assert.AreEqual(0, fixture.Provider.ChangeCalls);
        await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Cache.RebuildAsync());
        Assert.IsFalse(File.Exists(fixture.CachePath));
    }
    [TestMethod]
    public async Task InterruptedAttachmentFillIsRecoveredEvenWhenTheNextDeltaIsEmpty()
    {
        await using var fixture = await Fixture.Create(); fixture.Provider.AttachmentUnavailable = true;
        await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Cache.SynchronizeAsync(fixture.Account.Id));
        fixture.Provider.AttachmentUnavailable = false; fixture.Provider.EmptyChanges = true;
        await fixture.Cache.SynchronizeAsync(fixture.Account.Id); fixture.Provider.Offline = true;
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await fixture.Cache.GetAttachmentAsync(fixture.Message.Content.Key, "file", default));
    }
    [TestMethod]
    public async Task RetentionRemovesBodiesIndexesAndBlobsAndExpansionRestartsDelta()
    {
        await using var fixture = await Fixture.Create();
        fixture.Provider.Message = fixture.Message with { Content = fixture.Message.Content with { Date = DateTimeOffset.UtcNow.AddDays(-10) } };
        await fixture.Cache.SynchronizeAsync(fixture.Account.Id);
        await fixture.Policy(true, 2); Assert.IsTrue((await fixture.Cache.GetMessagesAsync(fixture.Folder, "")).IsEmpty);
        await using (var db = new SqliteConnection("Data Source=" + fixture.CachePath + ";Pooling=False"))
        { await db.OpenAsync(); Assert.AreEqual(0, await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM mailattachmentblobs")); Assert.AreEqual(0, await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM mailmessages")); }
        await fixture.Policy(true, 30); await fixture.Cache.SynchronizeAsync(fixture.Account.Id);
        Assert.IsNull(fixture.Provider.Checkpoints.Last()); Assert.AreEqual(1, (await fixture.Cache.GetMessagesAsync(fixture.Folder, "")).Length);
    }
    [TestMethod]
    public async Task FailedAndCancelledRebuildsKeepThePreviousCacheAndSuccessfulRebuildReplacesIt()
    {
        await using var fixture = await Fixture.Create(); await fixture.Cache.SynchronizeAsync(fixture.Account.Id);
        var prior = SHA256.HashData(await File.ReadAllBytesAsync(fixture.CachePath));
        fixture.Provider.Offline = true; await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Cache.RebuildAsync());
        CollectionAssert.AreEqual(prior, SHA256.HashData(await File.ReadAllBytesAsync(fixture.CachePath)));
        fixture.Provider.Offline = false;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Provider.BeforeChange = async token => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); };
        using var activeCancellation = new CancellationTokenSource();
        var rebuilding = fixture.Cache.RebuildAsync(cancellationToken: activeCancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15)); activeCancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => rebuilding);
        CollectionAssert.AreEqual(prior, SHA256.HashData(await File.ReadAllBytesAsync(fixture.CachePath))); fixture.Provider.BeforeChange = null;
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Cache.RebuildAsync(cancellationToken: cancellation.Token));
        CollectionAssert.AreEqual(prior, SHA256.HashData(await File.ReadAllBytesAsync(fixture.CachePath)));
        fixture.Provider.Offline = false; fixture.Provider.Message = fixture.Message with { Content = fixture.Message.Content with { Subject = "Updated cache" } };
        await fixture.Cache.RebuildAsync();
        Assert.AreEqual("Updated cache", (await fixture.Cache.GetMessageAsync(fixture.Message.Content.Key))!.Subject);
        Assert.AreEqual(0, Directory.GetFiles(fixture.Root, ".mail-cache-build-*.db").Length);
    }
    [TestMethod]
    public async Task CustomerScopeIsPreservedInBothModesAndEmptyAddressesNeverExposeAllMail()
    {
        await using var fixture = await Fixture.Create(); await fixture.Cache.SynchronizeAsync(fixture.Account.Id);
        foreach (var enabled in new[] { true, false })
        {
            await fixture.Policy(enabled, 30);
            var party = new Party { Key = new(fixture.Account.Company!, PartyKind.Customer, "1"), DisplayName = "Synthetic", Email = "customer@example.test" };
            Assert.AreEqual(1, (await fixture.Cache.ForParty(party).GetMessagesAsync(fixture.Folder, "")).Length);
            Assert.IsTrue((await fixture.Cache.ForParty(party with { Email = null }).GetMessagesAsync(fixture.Folder, "")).IsEmpty);
            var foreign = party with { Key = new(new(LexwareProductLine.Pro, "test", "other-company"), PartyKind.Customer, "1") };
            await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Cache.ForParty(foreign).GetMessageAsync(fixture.Message.Content.Key));
        }
    }
    [TestMethod]
    [DataRow("disabled")]
    [DataRow("removed")]
    [DataRow("folders")]
    [DataRow("company")]
    [DataRow("store")]
    [DataRow("range")]
    [DataRow("limit")]
    [DataRow("added")]
    [DataRow("cache-off")]
    [DataRow("cache-days")]
    public async Task RebuildDoesNotPublishAfterConfigurationChangesDuringTheLastAttachment(string change)
    {
        await using var fixture = await Fixture.Create(); await fixture.Cache.SynchronizeAsync(fixture.Account.Id);
        var previous = SHA256.HashData(await File.ReadAllBytesAsync(fixture.CachePath));
        fixture.Provider.BeforeAttachment = _ => fixture.Change(change);
        var failure = await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Cache.RebuildAsync());
        Assert.AreEqual(MailFailure.Conflict, failure.Failure);
        CollectionAssert.AreEqual(previous, SHA256.HashData(await File.ReadAllBytesAsync(fixture.CachePath)));
        Assert.AreEqual(0, Directory.GetFiles(fixture.Root, ".mail-cache-build-*").Length);
    }
    [TestMethod]
    public async Task SyncRejectsARevokedAccountBeforePersistingTheProviderResponse()
    {
        await using var fixture = await Fixture.Create(); fixture.Provider.BeforeChange = _ => fixture.Change("disabled");
        var failure = await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Cache.SynchronizeAsync(fixture.Account.Id));
        Assert.AreEqual(MailFailure.Conflict, failure.Failure); Assert.AreEqual(0, fixture.Provider.AttachmentCalls);
        await using var db = new SqliteConnection("Data Source=" + fixture.CachePath + ";Pooling=False"); await db.OpenAsync();
        Assert.AreEqual(0, await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM mailmessages"));
    }
    [TestMethod]
    public async Task SyncDiscardsRevokedAttachmentBytesAndCanResumeAfterTheAccountIsRestored()
    {
        await using var fixture = await Fixture.Create(); fixture.Provider.BeforeAttachment = _ => fixture.Change("disabled");
        var failure = await Assert.ThrowsExactlyAsync<MailException>(() => fixture.Cache.SynchronizeAsync(fixture.Account.Id));
        Assert.AreEqual(MailFailure.Conflict, failure.Failure);
        await using (var db = new SqliteConnection("Data Source=" + fixture.CachePath + ";Pooling=False"))
        { await db.OpenAsync(); Assert.AreEqual(0, await db.QuerySingleAsync<int>("SELECT COUNT(*) FROM mailattachmentblobs")); }
        fixture.Provider.BeforeAttachment = null; fixture.Provider.EmptyChanges = true;
        var current = await fixture.Settings.LoadAsync();
        await fixture.Settings.SaveAsync(current.Options with { MailAccounts = [fixture.Account] }, current);
        await fixture.Cache.SynchronizeAsync(fixture.Account.Id); fixture.Provider.Offline = true;
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, await fixture.Cache.GetAttachmentAsync(fixture.Message.Content.Key, "file", default));
    }
    [TestMethod]
    public async Task RebuildAllowsRenamingTheAccountWithoutChangingItsReadScope()
    {
        await using var fixture = await Fixture.Create(); fixture.Provider.BeforeAttachment = _ => fixture.Change("labels");
        await fixture.Cache.RebuildAsync(); Assert.IsNotNull(await fixture.Cache.GetMessageAsync(fixture.Message.Content.Key));
    }
    [TestMethod]
    [DataRow("list")]
    [DataRow("preview")]
    [DataRow("attachment")]
    public async Task DirectReadsDoNotReturnLateResultsAfterAccessIsRevoked(string lane)
    {
        await using var fixture = await Fixture.Create(false);
        if (lane == "list") fixture.Provider.BeforeList = _ => fixture.Change("disabled");
        if (lane == "preview") fixture.Provider.BeforeRead = _ => fixture.Change("disabled");
        if (lane == "attachment") fixture.Provider.BeforeAttachment = _ => fixture.Change("disabled");
        var failure = await Assert.ThrowsExactlyAsync<MailException>(async () =>
        {
            if (lane == "list") _ = await fixture.Cache.GetMessagesAsync(fixture.Folder, "");
            if (lane == "preview") _ = await fixture.Cache.GetMessageAsync(fixture.Message.Content.Key);
            if (lane == "attachment") _ = await fixture.Cache.GetAttachmentAsync(fixture.Message.Content.Key, "file", default);
        });
        Assert.AreEqual(MailFailure.Conflict, failure.Failure); Assert.IsFalse(File.Exists(fixture.CachePath));
    }
    [TestMethod]
    public void AttachmentProtectionRejectsAnotherAccountOrMessageRevision()
    {
        var encryption = new ProtectedMailBlob(); var saved = encryption.Protect([1, 2, 3], "account1:message1:revision1:file");
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, encryption.Unprotect(saved, "account1:message1:revision1:file"));
        Assert.ThrowsExactly<MailException>(() => encryption.Unprotect(saved, "account2:message1:revision1:file"));
        Assert.ThrowsExactly<MailException>(() => encryption.Unprotect(saved, "account1:message1:revision2:file"));
    }
    [TestMethod]
    public void BinaryProtectionPreservesAttachmentsLargerThanTheOldFiveMiBLimit()
    {
        var bytes = RandomNumberGenerator.GetBytes(6 * 1024 * 1024); var protection = new ProtectedMailBlob();
        var encrypted = protection.Protect(bytes, "account:large-attachment");
        CollectionAssert.AreEqual(bytes, protection.Unprotect(encrypted, "account:large-attachment"));
    }
    [TestMethod]
    public async Task CachePreferencesRoundtripAndPortableExportExcludeAccountCredentials()
    {
        await using var fixture = await Fixture.Create(); await fixture.Policy(false, 75);
        var current = await fixture.Settings.LoadAsync(); Assert.IsFalse(current.Options.MailCache.Enabled); Assert.AreEqual(75, current.Options.MailCache.Days);
        var path = Path.Combine(fixture.Root, "portable.json"); await fixture.Settings.ExportPortableAsync(path);
        var exported = await File.ReadAllTextAsync(path); Assert.Contains("mailCache", exported); Assert.DoesNotContain("customer@example.test", exported);
        await Assert.ThrowsAsync<ConfigurationException>(() => fixture.Settings.SaveAsync(current.Options with { MailCache = new() { Days = 0 } }, current));
    }
    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wcp-cache-" + Guid.NewGuid().ToString("N"));
        internal string CachePath => Path.Combine(Root, "mail-cache-v1.db");
        internal ConfigurationStore Settings { get; private set; } = null!;
        internal MailCacheService Cache { get; private set; } = null!;
        internal FakeConnector Provider { get; private set; } = null!;
        internal IMailConnectorFactory Factory { get; private set; } = null!;
        internal MailAccounts AccountService { get; private set; } = null!;
        internal MailAccountSettings Account { get; } = new() { Id = Guid.NewGuid(), Provider = MailProviderKind.OutlookDesktop, DisplayName = "Synthetic", ImportDays = 0, FolderIds = ["inbox"], FolderNames = ImmutableDictionary<string, string>.Empty.Add("inbox", "Posteingang"), Company = new(LexwareProductLine.Pro, "test", "company") };
        internal MailFolderKey Folder => new("outlook", Account.Id.ToString("N"), "inbox");
        internal MailEnvelope Message { get; private set; } = null!;
        internal static async Task<Fixture> Create(bool enabled = true)
        {
            var fixture = new Fixture(); Directory.CreateDirectory(fixture.Root);
            fixture.Message = new(new(new("outlook", fixture.Account.Id.ToString("N"), "message"), "Synthetic", "customer@example.test", "owner@example.test", "", DateTimeOffset.UtcNow, "private body", null, [new("file.pdf", 4) { Id = "file" }]), null, "customer@example.test", [], ["inbox"], "revision1");
            fixture.Provider = new(fixture.Account.Id, fixture.Message); var factory = new FakeFactory(fixture.Provider); fixture.Factory = factory;
            var secrets = new WindowsUserSecretProtector(); var validator = new ApplicationOptionsValidator(secrets); var location = new ConfigurationLocation(fixture.Root);
            fixture.Settings = new(location, validator); await fixture.Settings.SaveAsync(new() { MailAccounts = [fixture.Account], MailCache = new() { Enabled = enabled } }, await fixture.Settings.LoadAsync());
            var auth = new NoAuthentication(); var http = new MailHttp(new HttpClient(new NoNetwork()), auth);
            var accounts = new MailAccounts(fixture.Settings, validator, secrets, auth, factory, http);
            fixture.AccountService = accounts;
            fixture.Cache = new(fixture.Settings, new(location), accounts, factory, new(secrets), new ProtectedMailBlob(), new(accounts, factory)); return fixture;
        }
        internal async Task Policy(bool enabled, int days)
        { var previous = await Settings.LoadAsync(); await Settings.SaveAsync(previous.Options with { MailCache = new() { Enabled = enabled, Days = days } }, previous); }
        internal async Task Change(string change)
        {
            var snapshot = await Settings.LoadAsync(); var options = snapshot.Options;
            var account = options.MailAccounts.Single(item => item.Id == Account.Id);
            var edited = change switch
            {
                "disabled" => account with { Enabled = false },
                "folders" => account with { FolderIds = [], FolderNames = ImmutableDictionary<string, string>.Empty },
                "company" => account with { Company = new(LexwareProductLine.Pro, "test", "other-company") },
                "store" => account with { OutlookStoreId = "another-store" },
                "range" => account with { ImportDays = 1 },
                "limit" => account with { MaximumMessages = 3 },
                "labels" => account with { DisplayName = "Renamed mailbox", FolderNames = account.FolderNames.SetItem("inbox", "Renamed folder") },
                _ => account
            };
            options = options with { MailAccounts = change == "removed" ? [] : change == "added" ? [account, account with { Id = Guid.NewGuid() }] : [edited] };
            if (change == "cache-off") options = options with { MailCache = options.MailCache with { Enabled = false } };
            if (change == "cache-days") options = options with { MailCache = options.MailCache with { Days = 3 } };
            await Settings.SaveAsync(options, snapshot);
        }
        public ValueTask DisposeAsync() { Directory.Delete(Root, true); return ValueTask.CompletedTask; }
    }
    private sealed class FakeFactory(FakeConnector connector) : IMailConnectorFactory { public IMailConnector Create(MailAccountSettings account) => connector; }
    private sealed class FakeConnector(Guid id, MailEnvelope message) : IMailConnector
    {
        internal MailEnvelope Message = message; internal bool Offline; internal int ChangeCalls; internal List<string?> Checkpoints = [];
        internal Func<CancellationToken, Task>? BeforeChange, BeforeAttachment, BeforeList, BeforeRead;
        internal int AttachmentCalls;
        internal byte[]? LastAttachment;
        internal bool AttachmentUnavailable, EmptyChanges;
        public Guid AccountId => id; public string Provider => "outlook";
        private void Online() { if (Offline) throw new MailException(MailFailure.ProviderUnavailable); }
        public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default) { Online(); return Task.FromResult(new MailFolderCatalog([], "")); }
        public async Task<ImmutableArray<MailListItem>> GetMessagesAsync(string folderId, string search, CancellationToken cancellationToken = default)
        { Online(); if (BeforeList is not null) await BeforeList(cancellationToken); return [new MailListItem(Message.Content.Key, new(Provider, id.ToString("N"), folderId), Message.Content.From, Message.Content.Subject, Message.Content.Date, "", false, 1)]; }
        public async Task<MailEnvelope?> GetMessageAsync(string messageId, CancellationToken cancellationToken = default) { Online(); if (BeforeRead is not null) await BeforeRead(cancellationToken); return Message; }
        public async Task<MailChangePage> GetChangesAsync(string folderId, string? checkpoint, string? nextPage, CancellationToken cancellationToken = default)
        { Online(); if (BeforeChange is not null) await BeforeChange(cancellationToken); ChangeCalls++; Checkpoints.Add(checkpoint); return new(EmptyChanges ? [] : [Message], [], null, "checkpoint") { FullSnapshot = checkpoint is null }; }
        public async Task<byte[]> GetAttachmentAsync(string messageId, string attachmentId, CancellationToken cancellationToken = default)
        { Online(); AttachmentCalls++; if (BeforeAttachment is not null) await BeforeAttachment(cancellationToken); if (AttachmentUnavailable) throw new MailException(MailFailure.ProviderUnavailable); return LastAttachment = [1, 2, 3, 4]; }
    }
    private sealed class NoAuthentication : IMailAuthentication
    {
        public Task<MailAccessToken> GetAsync(MailAccountSettings account, bool forceRefresh, CancellationToken token) => throw new AssertFailedException();
        public Task<MailAccessToken> ConnectAsync(MailAccountSettings account, CancellationToken token) => throw new AssertFailedException();
        public Task DisconnectAsync(MailAccountSettings account, CancellationToken token) => throw new AssertFailedException();
    }
    private sealed class NoNetwork : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => throw new AssertFailedException("Real network access is forbidden."); }
}
