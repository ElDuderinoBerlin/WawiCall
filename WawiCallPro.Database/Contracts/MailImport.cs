using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Office;

namespace WawiCallPro.Database.Contracts;

public sealed record MailImportAccount(Guid Id, MailProviderKind Provider, string Binding);
public sealed record MailSyncState(long Version, string? Checkpoint, string? NextPage, Guid Generation, bool Snapshot, string LastPageHash, DateTimeOffset UpdatedAtUtc);
public sealed record MailImportRecord(string StableHash, string SourceHash, string RevisionHash, DateTimeOffset ReceivedAtUtc,
    string EncryptedPayload, bool Hidden, ImmutableArray<string> AddressHashes, ImmutableArray<string> FolderHashes);
public sealed record MailImportWrite(MailImportAccount Account, string FolderHash, long? ExpectedVersion, Guid Generation,
    bool Snapshot, string? Checkpoint, string? NextPage, string PageHash, bool CompleteSnapshot,
    ImmutableArray<MailImportRecord> Messages, ImmutableArray<string> RemovedSourceHashes, DateTimeOffset ChangedAtUtc);
public sealed record StoredMailRecord(string Id, Guid AccountId, string StableHash, string SourceHash, string RevisionHash,
    DateTimeOffset ReceivedAtUtc, string EncryptedPayload, bool Hidden, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
/// <summary>Null addresses means all mail in the explicit folder scope; an empty address filter always means no matches.</summary>
public sealed record MailReadQuery(Guid AccountId, ImmutableArray<string> AllowedFolderHashes, string? FolderHash = null,
    ImmutableArray<string>? AddressHashes = null, string? AfterId = null, int Limit = 100);

public interface IMailImportRepository
{
    Task<MailSyncState?> GetStateAsync(MailImportAccount account, string folderHash, CancellationToken cancellationToken = default);
    Task<MailSyncState> ApplyAsync(MailImportWrite write, CancellationToken cancellationToken = default);
    Task<Page<StoredMailRecord>> GetPageAsync(MailReadQuery query, CancellationToken cancellationToken = default);
    Task<StoredMailRecord?> GetAsync(Guid accountId, string sourceHash, CancellationToken cancellationToken = default);
    Task<StoredMailRecord?> GetStableAsync(Guid accountId, string stableHash, CancellationToken cancellationToken = default);
}

public static class MailIdentity
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public static string Message(Guid account, string stableHash) => Hash(account.ToString("N") + "\n" + stableHash);
    public static string State(Guid account, string folderHash) => Hash(account.ToString("N") + "\n" + folderHash);
}
