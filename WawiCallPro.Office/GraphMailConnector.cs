using System.Collections.Immutable;
using System.Text.Json;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Office;

public sealed class GraphMailConnector(MailAccountSettings account, MailHttp http) : IMailConnector
{
    private Uri Root => new("https://graph.microsoft.com/v1.0/" + (account.ApplicationAuthentication ? "users/" + MailJson.Encode(account.Mailbox ?? throw new MailException(MailFailure.Configuration)) : "me") + "/");
    public Guid AccountId => account.Id;
    public string Provider => "graph";
    private MailFolderKey FolderKey(string id) => new(Provider, account.Id.ToString("N"), id);
    private MailMessageKey MessageKey(string id) => new(Provider, account.Id.ToString("N"), id);
    private Task<JsonElement?> Get(string path, CancellationToken token, bool missing = false, bool cursor = false) => http.GetAsync(account, Root, path, token, missing, cursor);
    public async Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<MailFolder>(); var pending = new Queue<(string Path, string? Parent)>();
        pending.Enqueue(("mailFolders?$top=100&$select=id,displayName,unreadItemCount,childFolderCount", null));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var item))
        {
            string? page = item.Path; var pages = new HashSet<string>(StringComparer.Ordinal);
            while (page is not null)
            {
                if (!pages.Add(page) || result.Count > 2048) throw new MailException(MailFailure.LimitExceeded);
                var document = (await Get(page, cancellationToken))!.Value;
                foreach (var folder in document.Items("value"))
                {
                    var id = folder.Required("id"); if (!seen.Add(id)) throw new MailException(MailFailure.InvalidResponse);
                    result.Add(new(FolderKey(id), item.Parent is null ? null : FolderKey(item.Parent), folder.Text("displayName"), checked((int)(folder.Number("unreadItemCount") ?? 0))));
                    if (folder.Number("childFolderCount") > 0) pending.Enqueue(("mailFolders/" + MailJson.Encode(id) + "/childFolders?$top=100&$select=id,displayName,unreadItemCount,childFolderCount", id));
                }
                page = document.Text("@odata.nextLink") is { Length: > 0 } next ? next : null;
            }
        }
        return new([.. result], result.Count + " Microsoft-365-Ordner geladen.");
    }
    public async Task<ImmutableArray<MailListItem>> GetMessagesAsync(string folderId, string search, CancellationToken cancellationToken = default)
    {
        var route = "mailFolders/" + MailJson.Encode(folderId) + "/messages?$top=100&$select=id,parentFolderId,subject,from,receivedDateTime,bodyPreview,isRead,hasAttachments";
        if (!string.IsNullOrWhiteSpace(search)) route += "&$search=" + MailJson.Encode("\"" + search.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"");
        else route += "&$orderby=receivedDateTime%20desc";
        if (account.ImportDays > 0 && string.IsNullOrWhiteSpace(search)) route += "&$filter=" + MailJson.Encode("receivedDateTime ge " + DateTimeOffset.UtcNow.AddDays(-account.ImportDays).ToString("yyyy-MM-ddTHH:mm:ssZ"));
        var result = new Dictionary<string, MailListItem>(StringComparer.Ordinal); var seen = new HashSet<string>();
        string? page = route;
        while (page is not null)
        {
            if (!seen.Add(page)) throw new MailException(MailFailure.InvalidResponse);
            var document = (await Get(page, cancellationToken))!.Value;
            foreach (var item in document.Items("value"))
            {
                var id = item.Required("id"); var from = item.Child("from").Child("emailAddress");
                result[id] = new(MessageKey(id), FolderKey(folderId), Display(from), item.Text("subject"), item.Date("receivedDateTime"), item.Text("bodyPreview"), item.Flag("isRead"), item.Flag("hasAttachments") ? 1 : 0);
                if (result.Count > account.MaximumMessages) throw new MailException(MailFailure.LimitExceeded);
            }
            page = document.Text("@odata.nextLink") is { Length: > 0 } next ? next : null;
        }
        if (!string.IsNullOrWhiteSpace(search) && result.Count >= 1000) throw new MailException(MailFailure.LimitExceeded);
        return [.. result.Values.OrderByDescending(item => item.Date)];
    }
    public async Task<MailEnvelope?> GetMessageAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var item = await Get("messages/" + MailJson.Encode(messageId) + "?$select=id,parentFolderId,internetMessageId,subject,from,toRecipients,ccRecipients,bccRecipients,receivedDateTime,body,isRead,changeKey", cancellationToken, missing: true);
        if (item is null) return null;
        var attachments = new List<MailAttachmentInfo>(); string? page = "messages/" + MailJson.Encode(messageId) + "/attachments?$select=id,name,size&$top=100"; var seen = new HashSet<string>();
        while (page is not null)
        {
            if (!seen.Add(page) || attachments.Count > 1000) throw new MailException(MailFailure.LimitExceeded);
            var data = (await Get(page, cancellationToken))!.Value;
            attachments.AddRange(data.Items("value").Select(file => new MailAttachmentInfo(file.Text("name"), file.Number("size")) { Id = file.Required("id") }));
            page = data.Text("@odata.nextLink") is { Length: > 0 } next ? next : null;
        }
        var value = item.Value; var from = value.Child("from").Child("emailAddress");
        var recipients = new[] { ("toRecipients", "to"), ("ccRecipients", "cc"), ("bccRecipients", "bcc") }.SelectMany(kind => value.Items(kind.Item1).Select(recipient =>
        { var address = recipient.Child("emailAddress"); return new MailRecipient(address.Text("address"), address.Text("name"), kind.Item2); })).ToImmutableArray();
        var body = value.Child("body"); var html = body.Text("contentType").Equals("html", StringComparison.OrdinalIgnoreCase);
        var content = new MailContent(MessageKey(value.Required("id")), value.Text("subject"), Display(from), Join(recipients, "to"), Join(recipients, "cc"), value.Date("receivedDateTime"), html ? "" : body.Text("content"), html ? body.Text("content") : null, [.. attachments]);
        return new(content, value.Text("internetMessageId"), from.Text("address"), recipients, [value.Required("parentFolderId")], value.Text("changeKey"));
    }
    public async Task<MailChangePage> GetChangesAsync(string folderId, string? checkpoint, string? nextPage, CancellationToken cancellationToken = default)
    {
        var initial = "mailFolders/" + MailJson.Encode(folderId) + "/messages/delta?$top=100&$select=id";
        var route = nextPage ?? checkpoint ?? initial; JsonElement data;
        try { data = (await Get(route, cancellationToken, cursor: checkpoint is not null || nextPage is not null))!.Value; }
        catch (MailCursorExpiredException) { return new([], [], null, null, true); }
        var messages = new List<MailEnvelope>(); var removed = new List<string>();
        foreach (var change in data.Items("value"))
        {
            var id = change.Required("id");
            var message = await GetMessageAsync(id, cancellationToken); if (message is not null) messages.Add(message); else removed.Add(id);
        }
        return new([.. messages], [.. removed], Empty(data.Text("@odata.nextLink")), Empty(data.Text("@odata.deltaLink")));
    }
    public async Task<byte[]> GetAttachmentAsync(string messageId, string attachmentId, CancellationToken cancellationToken = default)
    {
        var file = (await Get("messages/" + MailJson.Encode(messageId) + "/attachments/" + MailJson.Encode(attachmentId), cancellationToken))!.Value;
        var bytes = Convert.FromBase64String(file.Text("contentBytes"));
        if (bytes.Length > 5 * 1024 * 1024) throw new MailException(MailFailure.LimitExceeded);
        return bytes;
    }
    private static string Display(JsonElement address) => address.Text("name") is { Length: > 0 } name ? name + " <" + address.Text("address") + ">" : address.Text("address");
    private static string Join(IEnumerable<MailRecipient> recipients, string kind) => string.Join("; ", recipients.Where(item => item.Kind == kind).Select(item => item.Address));
    private static string? Empty(string value) => value.Length == 0 ? null : value;
}
