using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Office.Tests;

[TestClass]
public sealed class MailConnectorTests
{
    [TestMethod]
    [DataRow("")][DataRow("Kunde Ä 电话")][DataRow("large")]
    public void ProtectedChunksPreserveUnicodeAndRejectOtherAccountsOrTampering(string text)
    {
        if (text == "large") text = string.Concat(Enumerable.Repeat("Ä电话Content", 10000));
        var codec = new ProtectedMailData(new WindowsUserSecretProtector()); var encrypted = codec.Protect(text, "account-1");
        Assert.AreEqual(text, codec.Unprotect(encrypted, "account-1")); if (text.Length > 0) Assert.DoesNotContain(text, encrypted);
        Assert.ThrowsExactly<MailException>(() => codec.Unprotect(encrypted, "account-2"));
        Assert.ThrowsExactly<MailException>(() => codec.Unprotect(encrypted[..^5], "account-1"));
    }
    [TestMethod]
    public async Task TokenStateIsProtectedClientBoundAndCancelledWritesKeepThePreviousValue()
    {
        using var files = new Files(); var store = files.Tokens; var account = GraphAccount(); var key = MailTokenStore.Key(account);
        await store.WriteAsync(key, "private-token", CancellationToken.None);
        Assert.DoesNotContain("private-token", File.ReadAllText(Directory.GetFiles(Path.Combine(files.Root, "mail-tokens-v1"), "*.json").Single()));
        Assert.AreEqual("private-token", await store.ReadAsync(key, CancellationToken.None));
        Assert.IsNull(await store.ReadAsync(MailTokenStore.Key(account with { ClientId = Guid.NewGuid().ToString() }), CancellationToken.None));
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.WriteAsync(key, "replacement", new CancellationToken(true)));
        Assert.AreEqual("private-token", await store.ReadAsync(key, CancellationToken.None));
        await using (var lease = await store.AcquireAsync(key, CancellationToken.None))
            await Assert.ThrowsAsync<OperationCanceledException>(() => store.AcquireAsync(key, new CancellationToken(true)));
        store.Delete(key); Assert.IsNull(await store.ReadAsync(key, CancellationToken.None));
    }

    [TestMethod]
    public async Task GraphTraversesFoldersAndPagesWithImmutableIdsAndOnlyGetRequests()
    {
        var calls = new List<string>();
        using var client = Client(request =>
        {
            calls.Add(request.RequestUri!.PathAndQuery); Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual("Bearer", request.Headers.Authorization!.Scheme); Assert.Contains("ImmutableId", string.Join("", request.Headers.GetValues("Prefer")));
            return request.RequestUri!.AbsolutePath.EndsWith("childFolders", StringComparison.Ordinal) ? Json("{\"value\":[{\"id\":\"child\",\"displayName\":\"Archiv\"}]}") :
                request.RequestUri!.Query.Contains("second", StringComparison.Ordinal) ? Json("{\"value\":[{\"id\":\"other\",\"displayName\":\"Andere\"}]}") :
                Json("{\"value\":[{\"id\":\"inbox\",\"displayName\":\"Posteingang\",\"childFolderCount\":1}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/me/mailFolders?second=1\"}");
        });
        var folders = await new GraphMailConnector(GraphAccount(), new(client, new Tokens())).GetFoldersAsync();
        Assert.AreEqual(3, folders.Folders.Length); Assert.AreEqual("inbox", folders.Folders.Single(item => item.Key.Id == "child").Parent!.Id); Assert.AreEqual(3, calls.Count);
    }
    [TestMethod]
    [DataRow("https://attacker.invalid/leak")][DataRow("http://graph.microsoft.com/v1.0/me/mailFolders")]
    [DataRow("https://graph.microsoft.com/v1.0/users/other/mailFolders")]
    public async Task GraphRejectsForeignContinuationBeforeSendingAnotherToken(string continuation)
    {
        var calls = 0; using var client = Client(_ => { calls++; return Json(JsonSerializer.Serialize(new { value = Array.Empty<object>(), @odataNext = continuation }).Replace("odataNext", "@odata.nextLink")); });
        await Assert.ThrowsExactlyAsync<MailException>(() => new GraphMailConnector(GraphAccount(), new(client, new Tokens())).GetFoldersAsync()); Assert.AreEqual(1, calls);
    }
    [TestMethod]
    public async Task GraphContentAndMovedDeltaKeepOriginalIdentityAndAttachmentMetadata()
    {
        using var client = Client(request => request.RequestUri!.AbsolutePath.EndsWith("/delta", StringComparison.Ordinal) ?
            Json("{\"value\":[{\"id\":\"message\",\"@removed\":{\"reason\":\"deleted\"}}],\"@odata.deltaLink\":\"https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages/delta?token=next\"}") :
            request.RequestUri!.AbsolutePath.EndsWith("/attachments", StringComparison.Ordinal) ? Json("{\"value\":[{\"id\":\"file\",\"name\":\"Angebot.pdf\",\"size\":123}]}") : Json(GraphMessage));
        var connector = new GraphMailConnector(GraphAccount(), new(client, new Tokens()));
        var changes = await connector.GetChangesAsync("inbox", null, null); Assert.AreEqual(1, changes.Messages.Length); Assert.IsTrue(changes.RemovedIds.IsEmpty);
        var message = changes.Messages.Single(); Assert.AreEqual("moved", message.FolderIds.Single()); Assert.AreEqual("message", message.Content.Key.Id);
        Assert.AreEqual("file", message.Content.Attachments.Single().Id); Assert.AreEqual("<b>Hallo</b>", message.Content.Html); Assert.AreEqual("kunde@example.test", message.SenderAddress);
    }
    [TestMethod]
    public async Task ExpiredGraphCheckpointRequestsResetWithoutInventingChanges()
    {
        using var client = Client(_ => new(HttpStatusCode.Gone));
        var page = await new GraphMailConnector(GraphAccount(), new(client, new Tokens())).GetChangesAsync("inbox", "https://graph.microsoft.com/v1.0/me/mailFolders/inbox/messages/delta?expired=1", null);
        Assert.IsTrue(page.Reset); Assert.IsTrue(page.Messages.IsEmpty); Assert.IsNull(page.Checkpoint);
    }
    [TestMethod]
    public async Task UnauthorizedRetriesSilentlyOnceAndNeverRequestsInteractiveAuth()
    {
        var calls = 0; var auth = new Tokens(); using var client = Client(_ => ++calls == 1 ? new(HttpStatusCode.Unauthorized) : Json("{\"value\":[]}"));
        await new GraphMailConnector(GraphAccount(), new(client, auth)).GetFoldersAsync(); Assert.AreEqual(2, calls); CollectionAssert.AreEqual(new[] { false, true }, auth.Refreshes);
    }
    [TestMethod]
    public async Task GmailReadsLabelsMimeBodiesAndCompleteSearchWithoutMutatingReadState()
    {
        var calls = new List<Uri>(); using var client = Client(request =>
        {
            calls.Add(request.RequestUri!); Assert.AreEqual(HttpMethod.Get, request.Method);
            if (request.RequestUri!.AbsolutePath.EndsWith("/profile", StringComparison.Ordinal)) return Json("{\"emailAddress\":\"owner@example.test\",\"historyId\":\"100\"}");
            if (request.RequestUri.AbsolutePath.EndsWith("/labels", StringComparison.Ordinal)) return Json("{\"labels\":[{\"id\":\"INBOX\",\"name\":\"Posteingang\"}]}");
            if (request.RequestUri.AbsolutePath.EndsWith("/messages", StringComparison.Ordinal)) return Json("{\"messages\":[{\"id\":\"g1\"}]}");
            return Json(GmailMessage);
        });
        var connector = new GmailConnector(GmailAccount(), new(client, new Tokens())); var catalog = await connector.GetFoldersAsync(); Assert.AreEqual(1, catalog.Folders.Length);
        var messages = await connector.GetMessagesAsync("INBOX", "find me"); Assert.AreEqual(1, messages.Length); Assert.IsFalse(messages[0].IsRead);
        Assert.IsTrue(calls.Any(uri => Uri.UnescapeDataString(uri.Query).Contains("find me", StringComparison.Ordinal)));
        var mail = (await connector.GetMessageAsync("g1"))!; Assert.Contains("Hallo", mail.Content.Text); Assert.AreEqual("kunde@example.test", mail.SenderAddress); Assert.AreEqual("part", mail.Content.Attachments.Single().Id);
    }
    [TestMethod]
    public async Task GmailRejectsAnotherAuthenticatedMailbox()
    {
        var calls = 0; using var client = Client(_ => { calls++; return Json("{\"emailAddress\":\"foreign@example.test\"}"); });
        await Assert.ThrowsExactlyAsync<MailException>(() => new GmailConnector(GmailAccount(), new(client, new Tokens())).GetFoldersAsync()); Assert.AreEqual(1, calls);
    }
    [TestMethod]
    public async Task GmailInitialRoundKeepsTheHistoryAnchorAcrossPages()
    {
        using var client = Client(request => request.RequestUri!.AbsolutePath.EndsWith("/profile", StringComparison.Ordinal) ? Json("{\"emailAddress\":\"owner@example.test\",\"historyId\":\"100\"}") :
            request.RequestUri!.Query.Contains("pageToken", StringComparison.Ordinal) ? Json("{\"messages\":[]}") : Json("{\"messages\":[],\"nextPageToken\":\"next\"}"));
        var connector = new GmailConnector(GmailAccount(), new(client, new Tokens())); var first = await connector.GetChangesAsync("INBOX", null, null);
        Assert.IsNull(first.Checkpoint); Assert.IsNotNull(first.NextPage);
        var last = await connector.GetChangesAsync("INBOX", null, first.NextPage); Assert.AreEqual("100", last.Checkpoint); Assert.IsNull(last.NextPage);
    }
    internal static MailAccountSettings GraphAccount() => new() { Id = Guid.NewGuid(), Provider = MailProviderKind.Microsoft365, ClientId = Guid.NewGuid().ToString(), RemoteIdentity = "account" };
    private static MailAccountSettings GmailAccount() => new() { Id = Guid.NewGuid(), Provider = MailProviderKind.Gmail, RemoteIdentity = "owner@example.test", ImportDays = 0 };
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) => new(new Handler(response));
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(response(request)); } }
    private sealed class Tokens : IMailAuthentication
    {
        internal List<bool> Refreshes { get; } = [];
        public Task<MailAccessToken> GetAsync(MailAccountSettings account, bool forceRefresh, CancellationToken cancellationToken) { Refreshes.Add(forceRefresh); return Task.FromResult(new MailAccessToken("synthetic-token", "account")); }
        public Task<MailAccessToken> ConnectAsync(MailAccountSettings account, CancellationToken cancellationToken) => throw new AssertFailedException("Tests never open a login.");
        public Task DisconnectAsync(MailAccountSettings account, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wcp-mail-tests-" + Guid.NewGuid().ToString("N"));
        internal ISecretProtector Secrets { get; } = new WindowsUserSecretProtector();
        internal MailTokenStore Tokens => new(new(new ConfigurationLocation(Root)), new(Secrets));
        internal Files() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }
    private const string GraphMessage = "{\"id\":\"message\",\"parentFolderId\":\"moved\",\"subject\":\"Angebot\",\"from\":{\"emailAddress\":{\"address\":\"kunde@example.test\",\"name\":\"Kunde\"}},\"toRecipients\":[{\"emailAddress\":{\"address\":\"owner@example.test\"}}],\"receivedDateTime\":\"2026-11-10T10:00:00Z\",\"body\":{\"contentType\":\"html\",\"content\":\"<b>Hallo</b>\"},\"changeKey\":\"rev1\"}";
    private const string GmailMessage = "{\"id\":\"g1\",\"historyId\":\"101\",\"internalDate\":\"1794304800000\",\"labelIds\":[\"INBOX\",\"UNREAD\"],\"payload\":{\"headers\":[{\"name\":\"From\",\"value\":\"Kunde <kunde@example.test>\"},{\"name\":\"To\",\"value\":\"owner@example.test\"},{\"name\":\"Subject\",\"value\":\"Frage\"}],\"parts\":[{\"mimeType\":\"text/plain\",\"body\":{\"data\":\"SGFsbG8\"}},{\"filename\":\"Angebot.pdf\",\"body\":{\"attachmentId\":\"part\",\"size\":123}}]}}";
}
