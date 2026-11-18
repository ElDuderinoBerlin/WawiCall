using System.Collections.Immutable;
using System.Security.Cryptography;
using WawiCallPro.Database.Cache;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Office;

public interface IMailCacheMaintenance
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);
    Task RebuildAsync(IProgress<MailSyncProgress>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>Owns one local SQLite cache. A rebuild publishes a completed staging file while retaining the previous cache on failure.</summary>
public sealed class MailCacheService(ConfigurationStore settings, ApplicationPaths paths, MailAccounts accounts,
    IMailConnectorFactory connectors, ProtectedMailData encryption, IMailBlobProtector blobs, DirectMailViewSource direct)
    : IMailViewSource, IMailSynchronization, IMailCacheMaintenance
{
    private readonly SemaphoreSlim _gate = new(1);
    private string FilePath => Path.Combine(paths.RootDirectory, "mail-cache-v1.db");
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default) => (await settings.LoadAsync(cancellationToken)).Options.MailCache.Enabled;
    public IMailViewSource ForParty(Party party) => new Customer(this, party);
    public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default) => Folders(null, cancellationToken);
    public Task<ImmutableArray<MailListItem>> GetMessagesAsync(MailFolderKey folder, string search, CancellationToken cancellationToken = default) => Messages(folder, search, null, cancellationToken);
    public Task<MailContent?> GetMessageAsync(MailMessageKey message, CancellationToken cancellationToken = default) => Content(message, null, cancellationToken);

    private async Task<MailFolderCatalog> Folders(Party? party, CancellationToken token)
    {
        if (!await IsEnabledAsync(token)) return await Source(direct, party).GetFoldersAsync(token);
        return await WithStore(async (store, policy) =>
        {
            var result = await Source(new ImportedMailViewSource(accounts, store.Messages, encryption), party).GetFoldersAsync(token);
            return result with { Status = result.Folders.IsEmpty ? result.Status : "SQLite-Mailcache · Nachrichten der letzten " + policy.Days + " Tage." };
        }, token);
    }
    private async Task<ImmutableArray<MailListItem>> Messages(MailFolderKey folder, string search, Party? party, CancellationToken token)
    {
        if (!await IsEnabledAsync(token)) return await Source(direct, party).GetMessagesAsync(folder, search, token);
        return await WithStore((store, _) => Source(new ImportedMailViewSource(accounts, store.Messages, encryption), party).GetMessagesAsync(folder, search, token), token);
    }
    private async Task<MailContent?> Content(MailMessageKey message, Party? party, CancellationToken token)
    {
        if (!await IsEnabledAsync(token)) return await Source(direct, party).GetMessageAsync(message, token);
        return await WithStore((store, _) => Source(new ImportedMailViewSource(accounts, store.Messages, encryption), party).GetMessageAsync(message, token), token);
    }
    private static IMailViewSource Source(IMailViewSource source, Party? party) => party is null ? source : source switch
    { ImportedMailViewSource cached => cached.ForParty(party), DirectMailViewSource live => live.ForParty(party), _ => throw new InvalidOperationException() };

    public async Task SynchronizeAsync(Guid accountId, IProgress<MailSyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(cancellationToken)) return;
        await WithStore(async (store, policy) =>
        {
            var account = await accounts.GetAsync(accountId, cancellationToken);
            var importer = new MailImportService(accounts, new RetentionConnectors(connectors, policy.Days), new RetentionRepository(store.Messages, policy.Days), encryption);
            await importer.SynchronizeAsync(accountId, progress, cancellationToken);
            await store.PruneAsync(Cutoff(policy), cancellationToken);
            await FillAttachments(store, account, policy, cancellationToken);
            await VerifyCacheScopeAsync(policy, account, cancellationToken);
            return true;
        }, cancellationToken);
    }
    public async Task RebuildAsync(IProgress<MailSyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        string? stage = null;
        try
        {
            using var lease = await Lease(cancellationToken);
            var options = (await settings.LoadAsync(cancellationToken)).Options;
            var policy = options.MailCache;
            if (!policy.Enabled) throw new MailException(MailFailure.Configuration);
            var selected = options.MailAccounts.Where(item => item.Enabled && !item.FolderIds.IsEmpty).ToArray();
            if (File.Exists(FilePath)) _ = await SqliteMailCacheStore.OpenAsync(FilePath, false, cancellationToken);
            stage = StagePath(); var store = await SqliteMailCacheStore.OpenAsync(stage, true, cancellationToken);
            await store.SetPolicyAsync(policy.Days, cancellationToken);
            foreach (var account in selected)
            {
                await VerifyRebuildScopeAsync(policy, selected, cancellationToken);
                var importer = new MailImportService(accounts, new RetentionConnectors(connectors, policy.Days), new RetentionRepository(store.Messages, policy.Days), encryption);
                await importer.SynchronizeAsync(account.Id, progress, cancellationToken);
                await store.PruneAsync(Cutoff(policy), cancellationToken);
                await FillAttachments(store, account, policy, cancellationToken);
            }
            await VerifyRebuildScopeAsync(policy, selected, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); CheckPath(FilePath);
            if (File.Exists(FilePath)) File.Replace(stage, FilePath, null); else File.Move(stage, FilePath);
            stage = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (MailException) { throw; }
        catch (Exception) { throw new MailException(MailFailure.ProviderUnavailable); }
        finally { if (stage is not null) CleanupStage(stage); _gate.Release(); }
    }
    public async Task<byte[]> GetAttachmentAsync(MailMessageKey message, string attachmentId, CancellationToken token)
    {
        if (!await IsEnabledAsync(token)) return await Download(message, attachmentId, token);
        return await WithStore(async (store, _) =>
        {
            if (!Guid.TryParseExact(message.Account, "N", out var id)) throw new MailException(MailFailure.AccessDenied);
            var content = await new ImportedMailViewSource(accounts, store.Messages, encryption).GetMessageAsync(message, token);
            if (content is null || !content.Attachments.Any(item => item.Id == attachmentId)) throw new MailException(MailFailure.AccessDenied);
            var record = await store.Messages.GetAsync(id, MailIdentity.Hash(message.Id), token) ?? throw new MailException(MailFailure.AccessDenied);
            var hash = MailIdentity.Hash(attachmentId); var saved = await store.ReadBlobAsync(record.Id, hash, record.RevisionHash, token);
            var purpose = BlobPurpose(record, hash);
            if (saved is not null) return blobs.Unprotect(saved, purpose);
            var bytes = await Download(message, attachmentId, token);
            await store.WriteBlobAsync(record.Id, hash, record.RevisionHash, blobs.Protect(bytes, purpose), token); return bytes;
        }, token);
    }
    private async Task<byte[]> Download(MailMessageKey key, string attachment, CancellationToken token)
    {
        if (await direct.GetMessageAsync(key, token) is not { } message || !message.Attachments.Any(item => item.Id == attachment)) throw new MailException(MailFailure.AccessDenied);
        var account = await accounts.GetAsync(Guid.ParseExact(key.Account, "N"), token);
        var bytes = await connectors.Create(account).GetAttachmentAsync(key.Id, attachment, token);
        try { await MailAccountScope.VerifyAsync(accounts, account, token); return bytes; }
        catch { CryptographicOperations.ZeroMemory(bytes); throw; }
    }
    private async Task FillAttachments(SqliteMailCacheStore store, MailAccountSettings account, MailCacheSettings policy, CancellationToken token)
    {
        string? after = null;
        do
        {
            await VerifyCacheScopeAsync(policy, account, token);
            var page = await store.Messages.GetPageAsync(new(account.Id, [.. account.FolderIds.Select(MailIdentity.Hash)], AfterId: after), token);
            foreach (var record in page.Items)
            {
                var envelope = MailImportService.Decode(record, account, encryption);
                foreach (var attachment in envelope.Content.Attachments)
                {
                    await VerifyCacheScopeAsync(policy, account, token);
                    if (attachment.Id is not { } attachmentId) throw new MailException(MailFailure.NotSupported);
                    var hash = MailIdentity.Hash(attachmentId);
                    if (await store.ReadBlobAsync(record.Id, hash, record.RevisionHash, token) is not null) continue;
                    var bytes = await connectors.Create(account).GetAttachmentAsync(envelope.Content.Key.Id, attachmentId, token);
                    try
                    {
                        await VerifyCacheScopeAsync(policy, account, token);
                        await store.WriteBlobAsync(record.Id, hash, record.RevisionHash, blobs.Protect(bytes, BlobPurpose(record, hash)), token);
                    }
                    finally { CryptographicOperations.ZeroMemory(bytes); }
                }
            }
            if (!page.HasMore) break; after = page.Items[^1].Id;
        } while (true);
    }
    private async Task VerifyCacheScopeAsync(MailCacheSettings policy, MailAccountSettings account, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var options = (await settings.LoadAsync(token)).Options;
        if (options.MailCache != policy) throw new MailException(MailFailure.Conflict);
        MailAccountScope.Verify(account, options.MailAccounts);
    }
    private async Task VerifyRebuildScopeAsync(MailCacheSettings policy, MailAccountSettings[] selected, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var options = (await settings.LoadAsync(token)).Options;
        var current = options.MailAccounts.Where(item => item.Enabled && !item.FolderIds.IsEmpty).OrderBy(item => item.Id).Select(MailAccountScope.Key);
        if (options.MailCache != policy || !selected.OrderBy(item => item.Id).Select(MailAccountScope.Key).SequenceEqual(current))
            throw new MailException(MailFailure.Conflict);
    }
    private async Task<T> WithStore<T>(Func<SqliteMailCacheStore, MailCacheSettings, Task<T>> action, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            var policy = (await settings.LoadAsync(token)).Options.MailCache;
            if (!policy.Enabled) throw new MailException(MailFailure.Conflict);
            using var lease = await Lease(token);
            if (!File.Exists(FilePath))
            {
                var staging = StagePath();
                try { _ = await SqliteMailCacheStore.OpenAsync(staging, true, token); token.ThrowIfCancellationRequested(); File.Move(staging, FilePath); }
                finally { CleanupStage(staging); }
            }
            var store = await SqliteMailCacheStore.OpenAsync(FilePath, false, token);
            await store.SetPolicyAsync(policy.Days, token); await store.PruneAsync(Cutoff(policy), token);
            return await action(store, policy);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (MailException) { throw; }
        catch (Exception) { throw new MailException(MailFailure.ProviderUnavailable); }
        finally { _gate.Release(); }
    }
    private async Task<FileStream> Lease(CancellationToken token)
    {
        CheckPath(paths.RootDirectory); Directory.CreateDirectory(paths.RootDirectory); CheckPath(FilePath);
        var file = Path.Combine(paths.RootDirectory, "mail-cache-v1.lock"); CheckPath(file);
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { return new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose); }
            catch (IOException) when (attempt < 300) { await Task.Delay(100, token); }
        }
    }
    private string StagePath() => Path.Combine(paths.RootDirectory, ".mail-cache-build-" + Guid.NewGuid().ToString("N") + ".db");
    private void CleanupStage(string path)
    {
        var root = Path.GetFullPath(paths.RootDirectory) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(path).StartsWith(".mail-cache-build-", StringComparison.Ordinal)) throw new MailException(MailFailure.Configuration);
        foreach (var suffix in new[] { "", "-journal", "-wal", "-shm", ".migration.lock" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
    }
    private static void CheckPath(string path)
    { if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new MailException(MailFailure.Configuration); }
    private static DateTimeOffset Cutoff(MailCacheSettings policy) => DateTimeOffset.UtcNow.AddDays(-policy.Days);
    private static string BlobPurpose(StoredMailRecord message, string attachment) => message.AccountId.ToString("N") + ":" + message.StableHash + ":" + message.RevisionHash + ":" + attachment;
    private sealed class RetentionConnectors(IMailConnectorFactory inner, int days) : IMailConnectorFactory
    { public IMailConnector Create(MailAccountSettings account) => inner.Create(account with { ImportDays = account.ImportDays == 0 ? days : Math.Min(days, account.ImportDays) }); }
    private sealed class RetentionRepository(IMailImportRepository inner, int days) : IMailImportRepository
    {
        public Task<MailSyncState> ApplyAsync(MailImportWrite write, CancellationToken cancellationToken = default) => inner.ApplyAsync(write with { Messages = [.. write.Messages.Where(item => item.ReceivedAtUtc >= DateTimeOffset.UtcNow.AddDays(-days))] }, cancellationToken);
        public Task<MailSyncState?> GetStateAsync(MailImportAccount account, string folderHash, CancellationToken cancellationToken = default) => inner.GetStateAsync(account, folderHash, cancellationToken);
        public Task<Page<StoredMailRecord>> GetPageAsync(MailReadQuery query, CancellationToken cancellationToken = default) => inner.GetPageAsync(query, cancellationToken);
        public Task<StoredMailRecord?> GetAsync(Guid accountId, string sourceHash, CancellationToken cancellationToken = default) => inner.GetAsync(accountId, sourceHash, cancellationToken);
        public Task<StoredMailRecord?> GetStableAsync(Guid accountId, string stableHash, CancellationToken cancellationToken = default) => inner.GetStableAsync(accountId, stableHash, cancellationToken);
    }
    private sealed class Customer(MailCacheService owner, Party party) : IMailViewSource
    {
        public Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default) => owner.Folders(party, cancellationToken);
        public Task<ImmutableArray<MailListItem>> GetMessagesAsync(MailFolderKey folder, string search, CancellationToken cancellationToken = default) => owner.Messages(folder, search, party, cancellationToken);
        public Task<MailContent?> GetMessageAsync(MailMessageKey message, CancellationToken cancellationToken = default) => owner.Content(message, party, cancellationToken);
    }
}
