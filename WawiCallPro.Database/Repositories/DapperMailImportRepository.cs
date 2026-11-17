using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;

namespace WawiCallPro.Database.Repositories;

/// <summary>Commits a mail page and its cursor together; incomplete snapshots never remove unseen messages.</summary>
public sealed class DapperMailImportRepository(DatabaseRepositoryContext context) : IMailImportRepository
{
    public Task<MailSyncState?> GetStateAsync(MailImportAccount account, string folderHash, CancellationToken cancellationToken = default)
    {
        ValidateAccount(account); Hash(folderHash);
        return context.RunAsync(false, async db =>
        {
            await CheckAccount(db, account, false, default);
            var row = await db.OneAsync($"SELECT * FROM {db.T("mailsyncstate")} WHERE id=@id", new { id = MailIdentity.State(account.Id, folderHash) });
            return row is null ? null : State(row);
        }, cancellationToken);
    }

    public Task<MailSyncState> ApplyAsync(MailImportWrite write, CancellationToken cancellationToken = default)
    {
        Validate(write);
        return context.RunAsync(true, async db =>
        {
            await CheckAccount(db, write.Account, true, write.ChangedAtUtc);
            var id = MailIdentity.State(write.Account.Id, write.FolderHash);
            var row = await db.OneAsync($"SELECT * FROM {db.T("mailsyncstate")} WHERE id=@id", new { id });
            var previous = row is null ? null : State(row);
            if (previous is not null && previous.Version - 1 == (write.ExpectedVersion ?? 0) && previous.LastPageHash == write.PageHash && previous.Generation == write.Generation)
                return previous;
            RepositorySession.Expected(row, write.ExpectedVersion);
            if (previous is not null) RepositoryData.Chronology(write.ChangedAtUtc, previous.UpdatedAtUtc);
            var account = DatabaseId.Encode(write.Account.Id); var ticks = DatabaseUtc.Encode(write.ChangedAtUtc);
            foreach (var message in write.Messages) await PutMessage(db, write, message);
            foreach (var removed in write.RemovedSourceHashes)
            {
                var removedRows = await db.RowsAsync($"SELECT id FROM {db.T("mailmessages")} WHERE accountid=@account AND sourcehash=@removed", new { account, removed });
                foreach (var removedRow in removedRows)
                    await db.ExecuteAsync($"DELETE FROM {db.T("mailmemberships")} WHERE messageid=@message", new { message = removedRow.Text("id") });
            }
            if (write.CompleteSnapshot)
                await db.ExecuteAsync($"DELETE FROM {db.T("mailmemberships")} WHERE folderhash=@folder AND generation<>@generation AND messageid IN (SELECT id FROM {db.T("mailmessages")} WHERE accountid=@account)",
                    new { folder = write.FolderHash, generation = write.Generation.ToString("N"), account });
            await db.ExecuteAsync($"UPDATE {db.T("mailmessages")} SET hidden=1, updatedatutc=@ticks WHERE accountid=@account AND hidden=0 AND NOT EXISTS (SELECT 1 FROM {db.T("mailmemberships")} f WHERE f.messageid={db.T("mailmessages")}.id)", new { account, ticks });
            var version = previous is null ? 1 : RepositorySession.Next(previous.Version);
            var values = new Dictionary<string, object?>
            {
                ["accountid"] = account, ["folderhash"] = write.FolderHash, ["checkpoint"] = write.Checkpoint,
                ["nextpage"] = write.NextPage, ["generation"] = write.Generation.ToString("N"), ["snapshot"] = write.Snapshot,
                ["lastpagehash"] = write.PageHash, ["writeversion"] = version, ["updatedatutc"] = ticks
            };
            if (row is null) { values.Add("id", id); await db.InsertAsync("mailsyncstate", values); }
            else await db.UpdateAsync("mailsyncstate", "id", id, write.ExpectedVersion!.Value, values);
            return new MailSyncState(version, write.Checkpoint, write.NextPage, write.Generation, write.Snapshot, write.PageHash, write.ChangedAtUtc);
        }, cancellationToken);
    }

    private static async Task PutMessage(RepositorySession db, MailImportWrite write, MailImportRecord message)
    {
        var id = MailIdentity.Message(write.Account.Id, message.StableHash); var account = DatabaseId.Encode(write.Account.Id);
        var old = await db.OneAsync($"SELECT * FROM {db.T("mailmessages")} WHERE id=@id", new { id });
        if (old is not null) RepositoryData.Chronology(write.ChangedAtUtc, old.Utc("updatedatutc"));
        var values = new Dictionary<string, object?>
        {
            ["accountid"] = account, ["stablehash"] = message.StableHash, ["sourcehash"] = message.SourceHash,
            ["revisionhash"] = message.RevisionHash, ["receivedatutc"] = DatabaseUtc.Encode(message.ReceivedAtUtc),
            ["payload"] = old is not null && old.Text("revisionhash") == message.RevisionHash ? old.Text("payload") : message.EncryptedPayload,
            ["hidden"] = message.Hidden, ["createdatutc"] = old?.Long("createdatutc") ?? DatabaseUtc.Encode(write.ChangedAtUtc),
            ["updatedatutc"] = DatabaseUtc.Encode(write.ChangedAtUtc)
        };
        if (old is null) { values.Add("id", id); await db.InsertAsync("mailmessages", values); }
        else await db.ExecuteAsync($"UPDATE {db.T("mailmessages")} SET {string.Join(',', values.Keys.Select(key => db.Q(key) + "=@" + key))} WHERE id=@id",
            RepositorySession.Parameters(new(values) { ["id"] = id }));
        await db.ExecuteAsync($"DELETE FROM {db.T("mailaddresses")} WHERE messageid=@id", new { id });
        foreach (var hash in message.AddressHashes)
            await db.InsertAsync("mailaddresses", new() { ["messageid"] = id, ["addresshash"] = hash });
        var existing = await db.RowsAsync($"SELECT folderhash, generation FROM {db.T("mailmemberships")} WHERE messageid=@id", new { id });
        await db.ExecuteAsync($"DELETE FROM {db.T("mailmemberships")} WHERE messageid=@id", new { id });
        if (message.Hidden) return;
        foreach (var folder in message.FolderHashes)
        {
            var generation = folder == write.FolderHash ? write.Generation.ToString("N") :
                existing.FirstOrDefault(item => item.Text("folderhash") == folder)?.Text("generation") ?? write.Generation.ToString("N");
            await db.InsertAsync("mailmemberships", new() { ["messageid"] = id, ["folderhash"] = folder, ["generation"] = generation });
        }
    }

    public Task<Page<StoredMailRecord>> GetPageAsync(MailReadQuery query, CancellationToken cancellationToken = default)
    {
        _ = DatabaseId.Encode(query.AccountId); Hashes(query.AllowedFolderHashes, 1024);
        if (query.FolderHash is not null) Hash(query.FolderHash);
        if (query.AddressHashes is { } filter) Hashes(filter, 2048);
        if (query.AfterId is not null) Hash(query.AfterId);
        var page = new PageRequest(0, query.Limit);
        if (query.AllowedFolderHashes.Length == 0 || query.AddressHashes is { Length: 0 } || query.FolderHash is { } folder && !query.AllowedFolderHashes.Contains(folder))
            return Task.FromResult(new Page<StoredMailRecord>([], false));
        return context.RunAsync(false, async db =>
        {
            var select = $"SELECT m.* FROM {db.T("mailmessages")} m WHERE m.accountid=@account AND m.hidden=0 AND m.id>@after " +
                $"AND EXISTS (SELECT 1 FROM {db.T("mailmemberships")} f WHERE f.messageid=m.id AND {db.In("f.folderhash", "@folders")})";
            if (query.AddressHashes is not null) select += $" AND EXISTS (SELECT 1 FROM {db.T("mailaddresses")} a WHERE a.messageid=m.id AND {db.In("a.addresshash", "@addresses")})";
            var rows = await db.RowsAsync(db.PageSql(select, "m.id", page), new { account = DatabaseId.Encode(query.AccountId), after = query.AfterId ?? "", folders = query.FolderHash is null ? query.AllowedFolderHashes.ToArray() : [query.FolderHash], addresses = query.AddressHashes?.ToArray() });
            return new Page<StoredMailRecord>([.. rows.Take(query.Limit).Select(Read)], rows.Count > query.Limit);
        }, cancellationToken);
    }

    public Task<StoredMailRecord?> GetAsync(Guid accountId, string sourceHash, CancellationToken cancellationToken = default) => Get(accountId, "sourcehash", sourceHash, false, cancellationToken);
    public Task<StoredMailRecord?> GetStableAsync(Guid accountId, string stableHash, CancellationToken cancellationToken = default) => Get(accountId, "stablehash", stableHash, true, cancellationToken);
    private Task<StoredMailRecord?> Get(Guid accountId, string column, string hash, bool includeHidden, CancellationToken token)
    {
        var account = DatabaseId.Encode(accountId); Hash(hash);
        return context.RunAsync(false, async db =>
        {
            var rows = await db.RowsAsync($"SELECT * FROM {db.T("mailmessages")} WHERE accountid=@account AND {db.Q(column)}=@hash" + (includeHidden ? "" : " AND hidden=0"), new { account, hash });
            return rows.Count switch { 0 => null, 1 => Read(rows[0]), _ => throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch) };
        }, token);
    }
    private static async Task CheckAccount(RepositorySession db, MailImportAccount account, bool create, DateTimeOffset time)
    {
        var id = DatabaseId.Encode(account.Id); var row = await db.OneAsync($"SELECT * FROM {db.T("mailaccounts")} WHERE id=@id", new { id });
        if (row is not null && (row.Text("binding") != account.Binding || row.Long("provider") != (int)account.Provider))
            throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
        if (row is null && create) await db.InsertAsync("mailaccounts", new() { ["id"] = id, ["provider"] = (int)account.Provider, ["binding"] = account.Binding, ["createdatutc"] = DatabaseUtc.Encode(time) });
    }
    private static StoredMailRecord Read(RepositoryRow row) => new(row.Text("id"), row.Id("accountid"), row.Text("stablehash"), row.Text("sourcehash"), row.Text("revisionhash"), row.Utc("receivedatutc"), row.Text("payload"), row.Bool("hidden"), row.Utc("createdatutc"), row.Utc("updatedatutc"));
    private static MailSyncState State(RepositoryRow row) => new(row.Long("writeversion"), row.OptionalText("checkpoint"), row.OptionalText("nextpage"), row.Id("generation"), row.Bool("snapshot"), row.Text("lastpagehash"), row.Utc("updatedatutc"));
    private static void Hash(string value)
    {
        if (value is null || value.Length != 64 || value.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))) throw new ArgumentException("Invalid mail identity.");
    }
    private static void Hashes(ImmutableArray<string> values, int maximum)
    {
        if (values.IsDefault || values.Length > maximum || values.Distinct(StringComparer.Ordinal).Count() != values.Length) throw new ArgumentException("Invalid mail scope.");
        foreach (var value in values) Hash(value);
    }
    private static void ValidateAccount(MailImportAccount account)
    { _ = DatabaseId.Encode(account.Id); Hash(account.Binding); if (!Enum.IsDefined(account.Provider)) throw new ArgumentException("Invalid provider."); }
    private static void Validate(MailImportWrite write)
    {
        ValidateAccount(write.Account); Hash(write.FolderHash); Hash(write.PageHash); _ = DatabaseId.Encode(write.Generation); StorageContract.Utc(write.ChangedAtUtc);
        if (write.ExpectedVersion is <= 0 || write.Messages.IsDefault || write.Messages.Length > 100000 || write.Messages.Select(item => item.StableHash).Distinct().Count() != write.Messages.Length ||
            write.Messages.Sum(item => (long)item.EncryptedPayload.Length) > 67108864 || write.Checkpoint?.Length > 262144 || write.NextPage?.Length > 262144 || write.CompleteSnapshot && (!write.Snapshot || write.NextPage is not null)) throw new ArgumentException("Invalid mail page.");
        Hashes(write.RemovedSourceHashes, 100000);
        foreach (var message in write.Messages)
        {
            Hash(message.StableHash); Hash(message.SourceHash); Hash(message.RevisionHash); StorageContract.Utc(message.ReceivedAtUtc);
            if (string.IsNullOrWhiteSpace(message.EncryptedPayload) || message.EncryptedPayload.Length > 4194304) throw new ArgumentException("Invalid protected message.");
            Hashes(message.AddressHashes, 2048); Hashes(message.FolderHashes, 1024);
        }
    }
}
