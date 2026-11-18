using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MMS.Office.Outlook;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Office;

/// <summary>Classic Outlook only. Every COM object is owned and released on one STA worker; no RCW escapes.</summary>
[SupportedOSPlatform("windows")]
public sealed class OutlookDesktopConnector(MailAccountSettings account) : IMailConnector
{
    public Guid AccountId => account.Id;
    public string Provider => "outlook";
    private MailFolderKey FolderKey(string store, string entry) => new(Provider, account.Id.ToString("N"), Encode(store, entry));
    private MailMessageKey MessageKey(string store, string entry) => new(Provider, account.Id.ToString("N"), Encode(store, entry));
    public Task<ImmutableArray<MailStore>> GetStoresAsync(CancellationToken token = default) => RunAsync(session =>
    {
        object? stores = null; var result = new List<MailStore>();
        try
        {
            stores = session.Stores;
            for (var index = 1; index <= (int)((dynamic)stores).Count; index++)
            {
                token.ThrowIfCancellationRequested(); object? store = null;
                try { store = ((dynamic)stores).Item(index); result.Add(new((string)((dynamic)store).StoreID, (string)((dynamic)store).DisplayName)); }
                finally { Release(store); }
            }
            return result.ToImmutableArray();
        }
        finally { Release(stores); }
    }, token);
    public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default) => RunAsync(session =>
    {
        var result = new List<MailFolder>(); object? stores = null;
        try
        {
            stores = session.Stores;
            for (var index = 1; index <= (int)((dynamic)stores).Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested(); object? store = null; object? root = null;
                try
                {
                    store = ((dynamic)stores).Item(index); var storeId = (string)((dynamic)store).StoreID;
                    if (account.OutlookStoreId is not null && account.OutlookStoreId != storeId) continue;
                    root = ((dynamic)store).GetRootFolder();
                    void Visit(dynamic folder, MailFolderKey? parent, int depth)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (depth > 32 || result.Count >= 2048) throw new MailException(MailFailure.LimitExceeded);
                        var key = FolderKey(storeId, (string)folder.EntryID);
                        if ((int)folder.DefaultItemType == 0) result.Add(new(key, parent, (string)folder.Name, (int)folder.UnReadItemCount));
                        object? children = null;
                        try
                        {
                            children = folder.Folders;
                            for (var childIndex = 1; childIndex <= (int)((dynamic)children).Count; childIndex++)
                            { object? child = null; try { child = ((dynamic)children).Item(childIndex); Visit(child, result.Any(item => item.Key == key) ? key : parent, depth + 1); } finally { Release(child); } }
                        }
                        finally { Release(children); }
                    }
                    Visit(root, null, 0);
                }
                finally { Release(root); Release(store); }
            }
        }
        finally { Release(stores); }
        return new MailFolderCatalog([.. result], result.Count + " Outlook-Ordner geladen.");
    }, cancellationToken);

    public async Task<ImmutableArray<MailListItem>> GetMessagesAsync(string folderId, string search, CancellationToken cancellationToken = default)
    {
        var messages = await ReadFolderAsync(folderId, cancellationToken);
        return [.. messages.Where(message => string.IsNullOrWhiteSpace(search) || new[] { message.Content.Subject, message.Content.From, message.Content.To, message.Content.Text, message.Content.Html ?? "" }
            .Any(text => text.Contains(search, StringComparison.OrdinalIgnoreCase))).Select(message => new MailListItem(message.Content.Key,
                new(Provider, account.Id.ToString("N"), folderId), message.Content.From, message.Content.Subject, message.Content.Date,
                message.Content.Text.Length > 200 ? message.Content.Text[..200] : message.Content.Text, message.Revision.EndsWith(":read", StringComparison.Ordinal), message.Content.Attachments.Length))];
    }
    public Task<MailEnvelope?> GetMessageAsync(string messageId, CancellationToken cancellationToken = default) => RunAsync<MailEnvelope?>(session =>
    {
        var id = Decode(messageId); object? item = null;
        try { item = session.GetItemFromID(id.Entry, id.Store); return (int)((dynamic)item).Class == 43 ? Materialize(item, id.Store) : null; }
        catch (COMException error) when ((uint)error.HResult == 0x8004010f) { return null; }
        finally { Release(item); }
    }, cancellationToken);
    public async Task<MailChangePage> GetChangesAsync(string folderId, string? checkpoint, string? nextPage, CancellationToken cancellationToken = default)
    {
        if (nextPage is not null) throw new MailException(MailFailure.InvalidResponse);
        var messages = await ReadFolderAsync(folderId, cancellationToken);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", messages.OrderBy(item => item.StableId ?? item.Content.Key.Id, StringComparer.Ordinal)
            .Select(item => (item.StableId ?? item.Content.Key.Id) + ":" + item.Revision))))).ToLowerInvariant();
        return new(checkpoint == fingerprint ? [] : messages, [], null, fingerprint) { FullSnapshot = checkpoint != fingerprint };
    }
    private Task<ImmutableArray<MailEnvelope>> ReadFolderAsync(string folderId, CancellationToken token) => RunAsync(session =>
    {
        var id = Decode(folderId); object? folder = null;
        try
        {
            folder = session.GetFolderFromID(id.Entry, id.Store);
            using var source = new Folder(this, folder, id.Store, token); folder = null;
            var reader = new MailImportReader<MailEnvelope>(null, false, account.ImportDays == 0 ? null : DateTime.UtcNow.AddDays(-account.ImportDays), null, false, account.MaximumMessages, token);
            reader.Read(source); return reader.Result.ToImmutableArray();
        }
        finally { Release(folder); }
    }, token);
    public Task<byte[]> GetAttachmentAsync(string messageId, string attachmentId, CancellationToken cancellationToken = default) => RunAsync(session =>
    {
        var id = Decode(messageId); var parts = attachmentId.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var index) || index < 1) throw new MailException(MailFailure.InvalidResponse);
        object? item = null; object? attachments = null; object? attachment = null;
        var directory = Path.Combine(Path.GetTempPath(), "wcp-mail-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "attachment.bin");
        try
        {
            item = session.GetItemFromID(id.Entry, id.Store); attachments = ((dynamic)item).Attachments; attachment = ((dynamic)attachments).Item(index);
            var size = (long)((dynamic)attachment).Size; var name = (string)((dynamic)attachment).FileName;
            if (size is < 0 or > MailLimits.AttachmentBytes || attachmentId != AttachmentId(index, name, size)) throw new MailException(MailFailure.Conflict);
            ((dynamic)attachment).SaveAsFile(file); cancellationToken.ThrowIfCancellationRequested();
            if (new FileInfo(file).Length > MailLimits.AttachmentBytes) throw new MailException(MailFailure.LimitExceeded);
            return File.ReadAllBytes(file);
        }
        finally { Release(attachment); Release(attachments); Release(item); if (File.Exists(file)) File.Delete(file); Directory.Delete(directory); }
    }, cancellationToken);

    private MailEnvelope Materialize(dynamic item, string store)
    {
        object? accessor = null; object? recipients = null; object? attachments = null; object? parent = null;
        try
        {
            accessor = item.PropertyAccessor; recipients = item.Recipients; attachments = item.Attachments; parent = item.Parent;
            string Property(string tag) { try { return Convert.ToString(((dynamic)accessor).GetProperty("http://schemas.microsoft.com/mapi/proptag/" + tag)) ?? ""; } catch (COMException) { return ""; } }
            var sender = Property("0x5D01001F");
            if (sender.Length == 0)
            {
                object? entry = null;
                try { entry = item.Sender; sender = ReadSmtp(entry, (string)item.SenderEmailAddress ?? ""); }
                finally { Release(entry); }
            }
            var addresses = new List<MailRecipient>();
            for (var index = 1; index <= (int)((dynamic)recipients).Count; index++)
            {
                object? recipient = null; object? address = null;
                try
                {
                    recipient = ((dynamic)recipients).Item(index); address = ((dynamic)recipient).PropertyAccessor;
                    string smtp;
                    try { smtp = (string)((dynamic)address).GetProperty("http://schemas.microsoft.com/mapi/proptag/0x39FE001F"); }
                    catch (COMException)
                    {
                        object? entry = null;
                        try { entry = ((dynamic)recipient).AddressEntry; smtp = ReadSmtp(entry, (string)((dynamic)recipient).Address ?? ""); }
                        finally { Release(entry); }
                    }
                    addresses.Add(new(smtp, (string)((dynamic)recipient).Name, (int)((dynamic)recipient).Type == 2 ? "cc" : (int)((dynamic)recipient).Type == 3 ? "bcc" : "to"));
                }
                finally { Release(address); Release(recipient); }
            }
            var files = new List<MailAttachmentInfo>();
            for (var index = 1; index <= (int)((dynamic)attachments).Count; index++)
            {
                if (index > 1000) throw new MailException(MailFailure.LimitExceeded); object? file = null;
                try { file = ((dynamic)attachments).Item(index); string name = ((dynamic)file).FileName; long size = ((dynamic)file).Size; files.Add(new(name, size) { Id = AttachmentId(index, name, size) }); }
                finally { Release(file); }
            }
            string messageEntryId = item.EntryID; string text = item.Body ?? ""; string html = item.HTMLBody ?? "";
            if (text.Length + html.Length > 2 * 1024 * 1024) throw new MailException(MailFailure.LimitExceeded);
            var content = new MailContent(MessageKey(store, messageEntryId), (string)item.Subject ?? "", (string)item.SenderName + " <" + sender + ">",
                string.Join("; ", addresses.Where(value => value.Kind == "to").Select(value => value.Address)), string.Join("; ", addresses.Where(value => value.Kind == "cc").Select(value => value.Address)),
                new DateTimeOffset(((DateTime)item.ReceivedTime).ToUniversalTime()), text, html.Length == 0 ? null : html, [.. files]);
            string stable;
            try { stable = store + ":" + Convert.ToHexString((byte[])((dynamic)accessor).GetProperty("http://schemas.microsoft.com/mapi/proptag/0x0FF90102")); }
            catch (COMException) { stable = Encode(store, messageEntryId); }
            return new(content, Property("0x1035001F"), sender, [.. addresses], [Encode(store, (string)((dynamic)parent).EntryID)],
                ((DateTime)item.LastModificationTime).ToUniversalTime().Ticks + ((bool)item.UnRead ? ":unread" : ":read")) { StableId = stable, IsRead = !(bool)item.UnRead, Preview = text[..Math.Min(256, text.Length)] };
        }
        finally { Release(parent); Release(attachments); Release(recipients); Release(accessor); }
    }
    private async Task<T> RunAsync<T>(Func<dynamic, T> read, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() =>
        {
            object? application = null; object? session = null;
            T? value = default; Exception? failure = null;
            try
            {
                token.ThrowIfCancellationRequested(); var type = Type.GetTypeFromProgID("Outlook.Application") ?? throw new MailException(MailFailure.NotSupported);
                application = Activator.CreateInstance(type) ?? throw new MailException(MailFailure.NotSupported); session = ((dynamic)application).GetNamespace("MAPI");
                value = read(session); token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { failure = new OperationCanceledException(token); }
            catch (MailException error) { failure = error; }
            catch (Exception) { failure = new MailException(MailFailure.ProviderUnavailable); }
            finally
            {
                try { try { Release(session); } finally { Release(application); } }
                catch (Exception) { failure ??= new MailException(MailFailure.ProviderUnavailable); }
            }
            if (failure is OperationCanceledException && token.IsCancellationRequested) result.SetCanceled(token);
            else if (failure is not null) result.SetException(failure);
            else result.SetResult(value!);
        }) { IsBackground = true, Name = "WawiCallPro Outlook reader" };
        worker.SetApartmentState(ApartmentState.STA); worker.Start();
        try { return await result.Task.ConfigureAwait(false); } finally { worker.Join(); }
    }
    private static string ReadSmtp(dynamic? entry, string fallback)
    {
        if (entry is null) return fallback;
        object? user = null; object? properties = null;
        try
        {
            if ((string)entry.Type != "EX") return (string)entry.Address ?? fallback;
            try { user = entry.GetExchangeUser(); if (user is not null && !string.IsNullOrWhiteSpace((string)((dynamic)user).PrimarySmtpAddress)) return (string)((dynamic)user).PrimarySmtpAddress; }
            catch (COMException) { }
            properties = entry.PropertyAccessor;
            try { return (string)((dynamic)properties).GetProperty("http://schemas.microsoft.com/mapi/proptag/0x39FE001F"); }
            catch (COMException) { return fallback; }
        }
        finally { Release(properties); Release(user); }
    }
    private static string AttachmentId(int index, string name, long size) => index + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name + "\n" + size)));
    private static string Encode(string store, string entry) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { store, entry })));
    private (string Store, string Entry) Decode(string value)
    {
        try { var pair = JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(Convert.FromBase64String(value)))!; if (pair.Length != 2 || pair.Any(string.IsNullOrWhiteSpace) || account.OutlookStoreId is not null && account.OutlookStoreId != pair[0]) throw new FormatException(); return (pair[0], pair[1]); }
        catch (Exception) { throw new MailException(MailFailure.InvalidResponse); }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    private sealed class Folder(OutlookDesktopConnector owner, object folder, string store, CancellationToken token) : IMailImportFolder<MailEnvelope>
    {
        private int _characters;
        public string StoreId => store; public string FolderId => (string)((dynamic)folder).EntryID;
        public IEnumerable<IMailImportFolder<MailEnvelope>> GetChildren() => [];
        public IEnumerable<IMailImportMessage<MailEnvelope>> GetMessages(DateTime? from, DateTime? to)
        {
            object? items = null;
            try
            {
                items = ((dynamic)folder).Items;
                for (var index = 1; index <= (int)((dynamic)items).Count; index++)
                {
                    token.ThrowIfCancellationRequested(); object? item = null;
                    try
                    {
                        item = ((dynamic)items).Item(index); if ((int)((dynamic)item).Class != 43) continue; var owned = item; item = null;
                        yield return new Message(owner, owned, store) { Reserve = count => { _characters = checked(_characters + count); if (_characters > 32 * 1024 * 1024) throw new MailException(MailFailure.LimitExceeded); } };
                    }
                    finally { Release(item); }
                }
            }
            finally { Release(items); }
        }
        public void Dispose() => Release(folder);
    }
    private sealed class Message(OutlookDesktopConnector owner, object item, string store) : IMailImportMessage<MailEnvelope>
    {
        internal Action<int>? Reserve { get; init; }
        public string EntryId => (string)((dynamic)item).EntryID;
        public DateTime ReceivedTime => (DateTime)((dynamic)item).ReceivedTime;
        public string? GetSenderAddress() => throw new NotSupportedException();
        public IEnumerable<string> GetRecipientAddresses() => throw new NotSupportedException();
        public MailEnvelope Materialize(bool includeAttachments, object? tag)
        { var value = owner.Materialize(item, store); Reserve?.Invoke(value.Content.Text.Length + (value.Content.Html?.Length ?? 0)); return value; }
        public void Dispose() => Release(item);
    }
}
