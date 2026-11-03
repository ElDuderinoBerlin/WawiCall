using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Contracts;

/// <summary>Only the owned communications database. Source identities are immutable; writes never reset a company's cache revision.</summary>
public interface ICompanyRepository
{
    Task<StoredCompany?> GetAsync(CompanyKey key, CancellationToken cancellationToken = default);
    Task<Page<StoredCompany>> GetPageAsync(CompanyRepositoryQuery query, CancellationToken cancellationToken = default);
    Task<StoredCompany> WriteAsync(CompanyWrite write, CancellationToken cancellationToken = default);
}

/// <summary>Cache replacement preserves history and parse errors. A failed/conflicting replacement leaves the entire previous cache intact.</summary>
public interface IPartyCacheRepository
{
    Task<PartyCacheState> GetStateAsync(CompanyKey company, CancellationToken cancellationToken = default);
    Task<Party?> GetAsync(PartyKey key, CancellationToken cancellationToken = default);
    Task<PartyPage> GetPageAsync(PartyQuery query, CancellationToken cancellationToken = default);

    /// <summary>Exact pre-normalized number, all phone kinds, distinct parties in canonical source-hash order. No unique-number assumption.</summary>
    Task<PartyPage> FindByE164Async(CompanyKey company, string e164, PageRequest page, CancellationToken cancellationToken = default);
    Task<PartyCacheState> ReplaceAsync(PartyCacheReplacement replacement, CancellationToken cancellationToken = default);
}

/// <summary>Call writes compare persistence versions and never regress state revision. Aggregate reads are transactionally consistent.</summary>
public interface ICallRepository
{
    Task<StoredCall?> GetAsync(Guid callId, CancellationToken cancellationToken = default);
    Task<Page<CallSnapshot>> GetPageAsync(CallHistoryQuery query, CancellationToken cancellationToken = default);
    Task<StoredCall> WriteAsync(CallWrite write, CancellationToken cancellationToken = default);
}

/// <summary>Stable note IDs, immutable call ownership and optimistic edits independent of call-state persistence.</summary>
public interface ICallNoteRepository
{
    Task<StoredCallNote?> GetAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<StoredCallNote> WriteAsync(CallNoteWrite write, CancellationToken cancellationToken = default);
}

/// <summary>Manages only metadata; a missing file does not make a recording row disappear.</summary>
public interface IRecordingRepository
{
    Task<StoredRecording?> GetAsync(Guid recordingId, CancellationToken cancellationToken = default);
    Task<StoredRecording> WriteAsync(RecordingWrite write, CancellationToken cancellationToken = default);
}

/// <summary>Append-only source errors; identical ID/content retries are idempotent, changed content under the same ID conflicts.</summary>
public interface INumberParseLogRepository
{
    Task AppendAsync(NumberParseFailure failure, CancellationToken cancellationToken = default);
    Task<Page<NumberParseFailure>> GetPageAsync(NumberParseLogQuery query, CancellationToken cancellationToken = default);
}
