using System.Collections.Immutable;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Office;

/// <summary>Provider reads without local persistence, with the same account/folder/customer boundaries as the cache.</summary>
public sealed class DirectMailViewSource(MailAccounts accounts, IMailConnectorFactory connectors) : IMailViewSource
{
    public IMailViewSource ForParty(Party party) => new Scoped(this, party);
    public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default) => Folders(null, cancellationToken);
    private async Task<ImmutableArray<MailAccountSettings>> Allowed(Party? party, CancellationToken token) =>
        [.. (await accounts.GetAsync(token)).Where(item => item.Enabled && !item.FolderIds.IsEmpty && (party is null || item.Company == party.Key.Company))];
    private async Task<MailAccountSettings> Resolve(string provider, string account, Party? party, CancellationToken token) =>
        (await Allowed(party, token)).SingleOrDefault(item => item.Id.ToString("N") == account && connectors.Create(item).Provider == provider) ?? throw new MailException(MailFailure.AccessDenied);
    private async Task<MailFolderCatalog> Folders(Party? party, CancellationToken token)
    {
        var folders = new List<MailFolder>();
        foreach (var account in await Allowed(party, token))
        {
            var provider = connectors.Create(account).Provider; var root = new MailFolderKey(provider, account.Id.ToString("N"), "account:" + account.Id.ToString("N"));
            folders.Add(new(root, null, account.DisplayName));
            folders.AddRange(account.FolderIds.Select(id => new MailFolder(new(provider, root.Account, id), root, account.FolderNames.GetValueOrDefault(id, "Ausgewählter Ordner"))));
        }
        return new([.. folders], folders.Count == 0 ? "Kein Postfach freigegeben. Unter Einstellungen → Office / Mail einrichten und Ordner auswählen." : "Direkter Anbieterzugriff. Mailcache ist ausgeschaltet.");
    }
    public Task<ImmutableArray<MailListItem>> GetMessagesAsync(MailFolderKey folder, string search, CancellationToken cancellationToken = default) => List(folder, search, null, cancellationToken);
    private async Task<ImmutableArray<MailListItem>> List(MailFolderKey folder, string search, Party? party, CancellationToken token)
    {
        var account = await Resolve(folder.Provider, folder.Account, party, token);
        if (folder.Id.StartsWith("account:", StringComparison.Ordinal)) return [];
        if (!account.FolderIds.Contains(folder.Id)) throw new MailException(MailFailure.AccessDenied);
        if (party is not null && Addresses(party).Count == 0) return [];
        var connector = connectors.Create(account); var messages = await connector.GetMessagesAsync(folder.Id, search, token);
        await MailAccountScope.VerifyAsync(accounts, account, token);
        if (party is null) return messages;
        var result = new List<MailListItem>();
        foreach (var message in messages)
        {
            var full = await connector.GetMessageAsync(message.Key.Id, token);
            await MailAccountScope.VerifyAsync(accounts, account, token);
            if (full is not null && Matches(full, party) && full.FolderIds.Contains(folder.Id)) result.Add(message);
        }
        return [.. result];
    }
    public Task<MailContent?> GetMessageAsync(MailMessageKey message, CancellationToken cancellationToken = default) => Read(message, null, cancellationToken);
    private async Task<MailContent?> Read(MailMessageKey key, Party? party, CancellationToken token)
    {
        var account = await Resolve(key.Provider, key.Account, party, token);
        if (party is not null && Addresses(party).Count == 0) return null;
        var message = await connectors.Create(account).GetMessageAsync(key.Id, token);
        await MailAccountScope.VerifyAsync(accounts, account, token);
        return message is null || message.Content.Key != key || !message.FolderIds.Intersect(account.FolderIds).Any() ||
            account.ImportDays != 0 && message.Content.Date < DateTimeOffset.UtcNow.AddDays(-account.ImportDays) || party is not null && !Matches(message, party) ? null : message.Content;
    }
    private static HashSet<string> Addresses(Party party) => party.Addresses.Select(item => item.Email).Append(party.Email)
        .Select(MailImportService.CanonicalAddress).Where(item => item is not null).Select(item => item!).ToHashSet(StringComparer.Ordinal);
    private static bool Matches(MailEnvelope message, Party party) => message.Recipients.Select(item => item.Address).Append(message.SenderAddress)
        .Select(MailImportService.CanonicalAddress).Any(item => item is not null && Addresses(party).Contains(item));
    private sealed class Scoped(DirectMailViewSource source, Party party) : IMailViewSource
    {
        public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default) => source.Folders(party, cancellationToken);
        public Task<ImmutableArray<MailListItem>> GetMessagesAsync(MailFolderKey folder, string search, CancellationToken cancellationToken = default) => source.List(folder, search, party, cancellationToken);
        public Task<MailContent?> GetMessageAsync(MailMessageKey message, CancellationToken cancellationToken = default) => source.Read(message, party, cancellationToken);
    }
}
