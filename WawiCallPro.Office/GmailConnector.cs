using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Office;

public sealed class GmailConnector(MailAccountSettings account, MailHttp http) : IMailConnector
{
    private static readonly Uri Root = new("https://gmail.googleapis.com/gmail/v1/users/me/");
    public Guid AccountId => account.Id;
    public string Provider => "gmail";
    private MailFolderKey FolderKey(string id) => new(Provider, account.Id.ToString("N"), id);
    private MailMessageKey MessageKey(string id) => new(Provider, account.Id.ToString("N"), id);
    private Task<JsonElement?> Get(string path, CancellationToken token, bool missing = false, bool cursor = false) => http.GetAsync(account, Root, path, token, missing, cursor);
    public async Task<string> GetMailboxAsync(CancellationToken token) => (await Get("profile", token))!.Value.Required("emailAddress");
    private async Task VerifyAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(account.RemoteIdentity) || !string.Equals(await GetMailboxAsync(token), account.RemoteIdentity, StringComparison.OrdinalIgnoreCase))
            throw new MailException(MailFailure.SignInRequired);
    }
    public async Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        await VerifyAsync(cancellationToken); var data = (await Get("labels", cancellationToken))!.Value;
        var folders = data.Items("labels").Select(label => new MailFolder(FolderKey(label.Required("id")), null, label.Text("name"))).ToImmutableArray();
        if (folders.Length > 2048) throw new MailException(MailFailure.LimitExceeded);
        return new(folders, folders.Length + " Gmail-Labels geladen.");
    }
    public async Task<ImmutableArray<MailListItem>> GetMessagesAsync(string folderId, string search, CancellationToken cancellationToken = default)
    {
        await VerifyAsync(cancellationToken);
        var query = search;
        if (account.ImportDays > 0) query = "newer_than:" + account.ImportDays + "d " + query;
        var route = "messages?maxResults=100&includeSpamTrash=true&labelIds=" + MailJson.Encode(folderId) + "&q=" + MailJson.Encode(query);
        var result = new Dictionary<string, MailListItem>(); var seen = new HashSet<string>(); string? page = "";
        do
        {
            if (!seen.Add(page)) throw new MailException(MailFailure.InvalidResponse);
            var data = (await Get(route + (page.Length == 0 ? "" : "&pageToken=" + MailJson.Encode(page)), cancellationToken))!.Value;
            foreach (var item in data.Items("messages"))
            {
                var id = item.Required("id"); var message = await ReadAsync(id, cancellationToken); if (message is null) continue;
                var content = message.Content;
                result[id] = new(content.Key, FolderKey(folderId), content.From, content.Subject, content.Date, content.Text.Length > 200 ? content.Text[..200] : content.Text,
                    message.Revision.EndsWith(":read", StringComparison.Ordinal), content.Attachments.Length);
                if (result.Count > account.MaximumMessages) throw new MailException(MailFailure.LimitExceeded);
            }
            page = data.Text("nextPageToken");
        } while (page.Length > 0);
        return [.. result.Values.OrderByDescending(item => item.Date)];
    }
    public async Task<MailEnvelope?> GetMessageAsync(string messageId, CancellationToken cancellationToken = default)
    { await VerifyAsync(cancellationToken); return await ReadAsync(messageId, cancellationToken); }
    private async Task<MailEnvelope?> ReadAsync(string id, CancellationToken token)
    {
        var message = await Get("messages/" + MailJson.Encode(id) + "?format=full", token, missing: true); if (message is null) return null;
        var data = message.Value; var payload = data.Child("payload");
        var headers = payload.Items("headers").GroupBy(item => item.Text("name"), StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => string.Join(", ", group.Select(item => item.Text("value"))), StringComparer.OrdinalIgnoreCase);
        string Header(string name) => MailMime.Header(headers.GetValueOrDefault(name, ""));
        var text = new StringBuilder(); var html = new StringBuilder(); var attachments = new List<MailAttachmentInfo>();
        void ReadPart(JsonElement part, int depth)
        {
            if (depth > 32 || attachments.Count > 1000 || text.Length + html.Length > 2 * 1024 * 1024) throw new MailException(MailFailure.LimitExceeded);
            var body = part.Child("body"); var attachmentId = body.Text("attachmentId");
            if (part.Text("filename") is { Length: > 0 } name) attachments.Add(new(name, body.Number("size")) { Id = attachmentId.Length > 0 ? attachmentId : "inline:" + part.Text("partId") });
            else if (body.Text("data") is { Length: > 0 } encoded)
            {
                var decoded = Decode(encoded);
                var contentType = part.Items("headers").FirstOrDefault(item => item.Text("name").Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Text("value");
                var content = MailMime.Text(decoded, contentType);
                if (part.Text("mimeType").Equals("text/html", StringComparison.OrdinalIgnoreCase)) html.AppendLine(content);
                else if (part.Text("mimeType").Equals("text/plain", StringComparison.OrdinalIgnoreCase)) text.AppendLine(content);
                if (text.Length + html.Length > 2 * 1024 * 1024) throw new MailException(MailFailure.LimitExceeded);
            }
            foreach (var child in part.Items("parts")) ReadPart(child, depth + 1);
        }
        ReadPart(payload, 0);
        var labels = data.Items("labelIds").Select(item => item.GetString()!).ToImmutableArray();
        if (!long.TryParse(data.Text("internalDate"), out var timestamp)) throw new MailException(MailFailure.InvalidResponse);
        var recipients = new[] { (Header("To"), "to"), (Header("Cc"), "cc"), (Header("Bcc"), "bcc") }.SelectMany(item => ParseAddresses(item.Item1, item.Item2)).ToImmutableArray();
        var sender = ParseAddresses(Header("From"), "from").FirstOrDefault()?.Address ?? "";
        var content = new MailContent(MessageKey(data.Required("id")), Header("Subject"), Header("From"), Header("To"), Header("Cc"), DateTimeOffset.FromUnixTimeMilliseconds(timestamp), text.Length > 0 ? text.ToString() : data.Text("snippet"), html.Length > 0 ? html.ToString() : null, [.. attachments]);
        return new(content, Header("Message-ID"), sender, recipients, labels, data.Text("historyId") + (labels.Contains("UNREAD") ? ":unread" : ":read"));
    }
    public async Task<MailChangePage> GetChangesAsync(string folderId, string? checkpoint, string? nextPage, CancellationToken cancellationToken = default)
    {
        await VerifyAsync(cancellationToken);
        var initial = checkpoint is null; var history = checkpoint; var page = "";
        if (nextPage is not null)
        {
            try { var cursor = JsonSerializer.Deserialize<Cursor>(Encoding.UTF8.GetString(Convert.FromBase64String(nextPage)))!; initial = cursor.Initial; history = cursor.History; page = cursor.Page; }
            catch (Exception) { throw new MailException(MailFailure.InvalidResponse); }
        }
        if (initial && history is null) history = (await Get("profile", cancellationToken))!.Value.Required("historyId");
        var route = initial ? "messages?maxResults=100&includeSpamTrash=true&labelIds=" + MailJson.Encode(folderId) : "history?maxResults=100&startHistoryId=" + MailJson.Encode(history!);
        if (initial && account.ImportDays > 0) route += "&q=" + MailJson.Encode("newer_than:" + account.ImportDays + "d");
        if (page.Length > 0) route += "&pageToken=" + MailJson.Encode(page);
        JsonElement data;
        try { data = (await Get(route, cancellationToken, cursor: !initial))!.Value; }
        catch (MailCursorExpiredException) { return new([], [], null, null, true); }
        var ids = initial ? data.Items("messages").Select(item => item.Required("id")) : data.Items("history").SelectMany(item =>
            item.Items("messages").Concat(item.Items("messagesAdded").Select(value => value.Child("message"))).Concat(item.Items("messagesDeleted").Select(value => value.Child("message")))
            .Concat(item.Items("labelsAdded").Select(value => value.Child("message"))).Concat(item.Items("labelsRemoved").Select(value => value.Child("message")))).Select(item => item.Required("id"));
        var changed = new List<MailEnvelope>(); var removed = new List<string>();
        foreach (var id in ids.Distinct()) { var message = await ReadAsync(id, cancellationToken); if (message is null) removed.Add(id); else changed.Add(message); }
        var following = data.Text("nextPageToken");
        return new([.. changed], [.. removed], following.Length == 0 ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Cursor(initial, history!, following)))),
            following.Length == 0 ? initial ? history : data.Text("historyId", history!) : null);
    }
    public async Task<byte[]> GetAttachmentAsync(string messageId, string attachmentId, CancellationToken cancellationToken = default)
    {
        await VerifyAsync(cancellationToken);
        if (attachmentId.StartsWith("inline:", StringComparison.Ordinal))
        {
            var data = (await Get("messages/" + MailJson.Encode(messageId) + "?format=full", cancellationToken))!.Value;
            JsonElement Find(JsonElement part) { if (part.Text("partId") == attachmentId[7..]) return part; foreach (var child in part.Items("parts")) { var match = Find(child); if (match.ValueKind != JsonValueKind.Undefined) return match; } return default; }
            return Decode(Find(data.Child("payload")).Child("body").Text("data"));
        }
        var attachment = (await Get("messages/" + MailJson.Encode(messageId) + "/attachments/" + MailJson.Encode(attachmentId), cancellationToken))!.Value;
        return Decode(attachment.Text("data"));
    }
    private static byte[] Decode(string encoded)
    { if (encoded.Length > 7 * 1024 * 1024) throw new MailException(MailFailure.LimitExceeded); try { return Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/').PadRight((encoded.Length + 3) / 4 * 4, '=')); } catch (FormatException) { throw new MailException(MailFailure.InvalidResponse); } }
    private static IEnumerable<MailRecipient> ParseAddresses(string text, string kind)
    {
        var addresses = new System.Net.Mail.MailAddressCollection();
        try { if (text.Length > 0) addresses.Add(text); } catch (FormatException) { return []; }
        return addresses.Select(item => new MailRecipient(item.Address, item.DisplayName, kind)).ToArray();
    }
    private sealed record Cursor(bool Initial, string History, string Page);
}
