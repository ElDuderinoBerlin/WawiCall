using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net.Mail;
using System.Text.Json;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Office;

public sealed record MailSyncProgress(int FoldersCompleted, int FoldersTotal, int MessagesConfirmed);
public interface IMailSynchronization
{
    Task SynchronizeAsync(Guid accountId, IProgress<MailSyncProgress>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>No interactive authentication. The account gate protects concurrent manual/startup imports.</summary>
public sealed class MailImportService(MailAccounts accounts, IMailConnectorFactory connectors, IMailImportRepository repository,
    ProtectedMailData encryption) : IMailSynchronization
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 32, AllowDuplicateProperties = false };
    public async Task SynchronizeAsync(Guid accountId, IProgress<MailSyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var gate = _gates.GetOrAdd(accountId, _ => new(1)); await gate.WaitAsync(cancellationToken);
        try
        {
            var account = await accounts.GetAsync(accountId, cancellationToken);
            if (!account.Enabled || account.FolderIds.IsEmpty) throw new MailException(MailFailure.Configuration);
            if (account.Provider != MailProviderKind.OutlookDesktop && string.IsNullOrWhiteSpace(account.RemoteIdentity)) throw new MailException(MailFailure.SignInRequired);
            var connector = connectors.Create(account); var binding = Binding(account); var completed = 0; var confirmed = 0;
            foreach (var folder in account.FolderIds)
            {
                var folderHash = MailIdentity.Hash(folder); var state = await repository.GetStateAsync(binding, folderHash, cancellationToken);
                var purpose = CursorPurpose(account.Id, folderHash);
                string? checkpoint = state?.Checkpoint is { } encrypted ? encryption.Unprotect(encrypted, purpose + ":checkpoint") : null;
                var continuation = state?.NextPage is { } next ? JsonSerializer.Deserialize<Continuation>(encryption.Unprotect(next, purpose + ":page"), Json) : null;
                var page = continuation?.Cursor; var count = continuation?.Count ?? 0;
                if (count < 0 || count > account.MaximumMessages || page is "") throw new MailException(MailFailure.Configuration);
                var snapshot = page is not null ? state!.Snapshot : checkpoint is null;
                var generation = page is not null ? state!.Generation : Guid.NewGuid();
                var resets = 0; var seen = new HashSet<string>(StringComparer.Ordinal);
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var current = await accounts.GetAsync(account.Id, cancellationToken);
                    if (!current.Enabled || Binding(current) != binding || !current.FolderIds.SequenceEqual(account.FolderIds) || current.ImportDays != account.ImportDays || current.MaximumMessages != account.MaximumMessages)
                        throw new MailException(MailFailure.Conflict);
                    var changes = await connector.GetChangesAsync(folder, checkpoint, page, cancellationToken);
                    if (changes.Reset)
                    {
                        if (++resets > 1) throw new MailException(MailFailure.InvalidResponse);
                        checkpoint = page = null; snapshot = true; generation = Guid.NewGuid(); count = 0; seen.Clear(); continue;
                    }
                    if (changes.Messages.IsDefault || changes.RemovedIds.IsDefault || changes.NextPage is null && string.IsNullOrEmpty(changes.Checkpoint))
                        throw new MailException(MailFailure.InvalidResponse);
                    if (changes.NextPage is { } nextPage && !seen.Add(nextPage)) throw new MailException(MailFailure.InvalidResponse);
                    if (seen.Count > 10000) throw new MailException(MailFailure.LimitExceeded);
                    if (changes.FullSnapshot) { snapshot = true; generation = Guid.NewGuid(); }
                    var records = new Dictionary<string, MailImportRecord>(StringComparer.Ordinal);
                    foreach (var envelope in changes.Messages)
                    {
                        ValidateEnvelope(account, connector.Provider, envelope);
                        var stable = MailIdentity.Hash(envelope.StableId ?? envelope.Content.Key.Id);
                        var scope = envelope.FolderIds.Intersect(account.FolderIds, StringComparer.Ordinal).Select(MailIdentity.Hash).ToImmutableArray();
                        var hidden = scope.IsEmpty || account.ImportDays != 0 && envelope.Content.Date < DateTimeOffset.UtcNow.AddDays(-account.ImportDays);
                        var old = await repository.GetStableAsync(account.Id, stable, cancellationToken);
                        // Gmail history covers the whole account. Never import an unrelated message merely because it changed.
                        if (hidden && old is null) continue;
                        if (old is not null)
                        {
                            var original = Decode(old, account, encryption);
                            if ((original.StableId ?? original.Content.Key.Id) != (envelope.StableId ?? envelope.Content.Key.Id)) throw new MailException(MailFailure.Conflict);
                        }
                        var serialized = JsonSerializer.Serialize(envelope, Json);
                        var addresses = envelope.Recipients.Select(item => item.Address).Append(envelope.SenderAddress).Select(CanonicalAddress)
                            .Where(item => item is not null).Select(item => MailIdentity.Hash(item!)).Distinct().ToImmutableArray();
                        records[stable] = new(stable, MailIdentity.Hash(envelope.Content.Key.Id), MailIdentity.Hash(serialized), envelope.Content.Date.ToUniversalTime(),
                            encryption.Protect(serialized, MessagePurpose(account.Id, stable)), hidden, addresses, hidden ? [] : scope);
                    }
                    count = checked(count + records.Count);
                    if (count > account.MaximumMessages) throw new MailException(MailFailure.LimitExceeded);
                    var checkpointNext = changes.Checkpoint ?? checkpoint;
                    var pageHash = MailIdentity.Hash(JsonSerializer.Serialize(new { generation, snapshot, changes.NextPage, checkpointNext,
                        Messages = records.Values.Select(item => new { item.StableHash, item.SourceHash, item.RevisionHash, item.Hidden, item.FolderHashes }), changes.RemovedIds }, Json));
                    var write = new MailImportWrite(binding, folderHash, state?.Version, generation, snapshot,
                        checkpointNext is null ? null : encryption.Protect(checkpointNext, purpose + ":checkpoint"),
                        changes.NextPage is null ? null : encryption.Protect(JsonSerializer.Serialize(new Continuation(changes.NextPage, count), Json), purpose + ":page"),
                        pageHash, snapshot && changes.NextPage is null, [.. records.Values], [.. changes.RemovedIds.Select(MailIdentity.Hash).Distinct()], DateTimeOffset.UtcNow);
                    state = await repository.ApplyAsync(write, cancellationToken); page = changes.NextPage; checkpoint = checkpointNext;
                    confirmed += records.Count; progress?.Report(new(completed, account.FolderIds.Length, confirmed));
                } while (page is not null || resets > 0 && checkpoint is null);
                completed++; progress?.Report(new(completed, account.FolderIds.Length, confirmed));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (MailException) { throw; }
        catch (WawiCallPro.Database.DatabaseOperationException) { throw; }
        catch (RepositoryConflictException) { throw new MailException(MailFailure.Conflict); }
        catch (Exception) { throw new MailException(MailFailure.InvalidResponse); }
        finally { gate.Release(); }
    }
    internal static MailImportAccount Binding(MailAccountSettings account) => new(account.Id, account.Provider, MailIdentity.Hash(JsonSerializer.Serialize(new
    { account.Provider, account.ClientId, account.Tenant, account.ApplicationAuthentication, account.Mailbox, account.RemoteIdentity, account.OutlookStoreId }, Json)));
    internal static string? CanonicalAddress(string? value) => MailAddress.TryCreate(value, out var address) ? address.Address.Trim().ToLowerInvariant() : null;
    private static string CursorPurpose(Guid account, string folder) => "mail-cursor-v1:" + account.ToString("N") + ":" + folder;
    private static string MessagePurpose(Guid account, string stable) => "mail-message-v1:" + account.ToString("N") + ":" + stable;
    internal static MailEnvelope Decode(StoredMailRecord record, MailAccountSettings account, ProtectedMailData encryption)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MailEnvelope>(encryption.Unprotect(record.EncryptedPayload, MessagePurpose(account.Id, record.StableHash)), Json) ?? throw new JsonException();
            if (envelope.Content.Key.Account != account.Id.ToString("N") || MailIdentity.Hash(envelope.StableId ?? envelope.Content.Key.Id) != record.StableHash || MailIdentity.Hash(envelope.Content.Key.Id) != record.SourceHash)
                throw new JsonException();
            return envelope;
        }
        catch (Exception) { throw new MailException(MailFailure.Configuration); }
    }
    private static void ValidateEnvelope(MailAccountSettings account, string provider, MailEnvelope envelope)
    {
        if (envelope.Content.Key.Provider != provider || envelope.Content.Key.Account != account.Id.ToString("N") || string.IsNullOrWhiteSpace(envelope.Content.Key.Id) || envelope.Recipients.IsDefault || envelope.FolderIds.IsDefault)
            throw new MailException(MailFailure.InvalidResponse);
    }
    private sealed record Continuation(string Cursor, int Count);
}

/// <summary>Offline mail browser; customer snapshots are additionally restricted by company and exact email addresses.</summary>
public sealed class ImportedMailViewSource(MailAccounts accounts, IMailImportRepository repository, ProtectedMailData encryption) : IMailViewSource
{
    public IMailViewSource ForParty(Party party) => new PartySource(this, party);
    public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default) => Folders(null, cancellationToken);
    private async Task<MailFolderCatalog> Folders(Party? party, CancellationToken token)
    {
        var folders = new List<MailFolder>();
        foreach (var account in await Allowed(party, token))
        {
            var provider = Provider(account); var root = new MailFolderKey(provider, account.Id.ToString("N"), "account:" + account.Id.ToString("N"));
            folders.Add(new(root, null, account.DisplayName));
            folders.AddRange(account.FolderIds.Select(id => new MailFolder(new(provider, account.Id.ToString("N"), id), root, account.FolderNames.GetValueOrDefault(id, "Importierter Ordner"))));
        }
        return new([.. folders], folders.Count == 0 ? "Kein importiertes Postfach freigegeben. Unter Einstellungen → Office / Mail einrichten und Ordner auswählen." :
            party is null ? "Lokal importierte Nachrichten. Neue Nachrichten unter Einstellungen → Office / Mail synchronisieren." : "Importierte Nachrichten zu den genauen E-Mail-Adressen dieses Geschäftspartners.");
    }
    public Task<ImmutableArray<MailListItem>> GetMessagesAsync(MailFolderKey folder, string search, CancellationToken cancellationToken = default) => Messages(folder, search, null, cancellationToken);
    private async Task<ImmutableArray<MailListItem>> Messages(MailFolderKey folder, string search, Party? party, CancellationToken token)
    {
        var account = await Resolve(folder.Provider, folder.Account, party, token);
        if (folder.Id.StartsWith("account:", StringComparison.Ordinal)) return [];
        if (!account.FolderIds.Contains(folder.Id)) throw new MailException(MailFailure.AccessDenied);
        await repository.GetStateAsync(MailImportService.Binding(account), MailIdentity.Hash(folder.Id), token);
        var addresses = Addresses(party); var result = new List<MailListItem>(); string? after = null; var loaded = 0;
        do
        {
            var page = await repository.GetPageAsync(new(account.Id, [.. account.FolderIds.Select(MailIdentity.Hash)], MailIdentity.Hash(folder.Id), addresses, after), token);
            foreach (var record in page.Items)
            {
                if (++loaded > account.MaximumMessages) throw new MailException(MailFailure.LimitExceeded);
                var envelope = MailImportService.Decode(record, account, encryption); var content = envelope.Content;
                if (account.ImportDays != 0 && content.Date < DateTimeOffset.UtcNow.AddDays(-account.ImportDays)) continue;
                if (search.Length > 0 && !string.Join("\n", content.Subject, content.From, content.To, content.Cc, content.Text, content.Html).Contains(search, StringComparison.CurrentCultureIgnoreCase)) continue;
                result.Add(new(content.Key, folder, content.From, content.Subject, content.Date, envelope.Preview, envelope.IsRead, content.Attachments.Length));
            }
            if (!page.HasMore) break; after = page.Items[^1].Id;
        } while (true);
        return [.. result.OrderByDescending(item => item.Date)];
    }
    public Task<MailContent?> GetMessageAsync(MailMessageKey message, CancellationToken cancellationToken = default) => Content(message, null, cancellationToken);
    private async Task<MailContent?> Content(MailMessageKey message, Party? party, CancellationToken token)
    {
        var account = await Resolve(message.Provider, message.Account, party, token);
        // Source ID alone is never authority: re-check current folder and optional customer scopes.
        var record = await repository.GetAsync(account.Id, MailIdentity.Hash(message.Id), token); if (record is null) return null;
        var envelope = MailImportService.Decode(record, account, encryption);
        if (!envelope.FolderIds.Intersect(account.FolderIds).Any() || account.ImportDays != 0 && envelope.Content.Date < DateTimeOffset.UtcNow.AddDays(-account.ImportDays)) return null;
        if (party is not null)
        {
            var allowed = Addresses(party)!.Value;
            if (!envelope.Recipients.Select(item => item.Address).Append(envelope.SenderAddress).Select(MailImportService.CanonicalAddress).Where(item => item is not null).Select(item => MailIdentity.Hash(item!)).Intersect(allowed).Any()) return null;
        }
        await repository.GetStateAsync(MailImportService.Binding(account), MailIdentity.Hash(envelope.FolderIds.Intersect(account.FolderIds).First()), token);
        return envelope.Content;
    }
    private async Task<ImmutableArray<MailAccountSettings>> Allowed(Party? party, CancellationToken token) =>
        [.. (await accounts.GetAsync(token)).Where(account => account.Enabled && !account.FolderIds.IsEmpty && (party is null || account.Company == party.Key.Company))];
    private async Task<MailAccountSettings> Resolve(string provider, string identity, Party? party, CancellationToken token) =>
        (await Allowed(party, token)).SingleOrDefault(account => account.Id.ToString("N") == identity && Provider(account) == provider) ?? throw new MailException(MailFailure.AccessDenied);
    private static string Provider(MailAccountSettings account) => account.Provider switch { MailProviderKind.Microsoft365 => "graph", MailProviderKind.Gmail => "gmail", _ => "outlook" };
    private static ImmutableArray<string>? Addresses(Party? party) => party is null ? null :
        [.. party.Addresses.Select(item => item.Email).Append(party.Email).Select(MailImportService.CanonicalAddress).Where(item => item is not null).Select(item => MailIdentity.Hash(item!)).Distinct()];
    private sealed class PartySource(ImportedMailViewSource owner, Party party) : IMailViewSource
    {
        public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default) => owner.Folders(party, cancellationToken);
        public Task<ImmutableArray<MailListItem>> GetMessagesAsync(MailFolderKey folder, string search, CancellationToken cancellationToken = default) => owner.Messages(folder, search, party, cancellationToken);
        public Task<MailContent?> GetMessageAsync(MailMessageKey message, CancellationToken cancellationToken = default) => owner.Content(message, party, cancellationToken);
    }
}
