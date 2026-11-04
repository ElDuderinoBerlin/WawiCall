using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Database.Contracts;

public sealed record StoredCompany(Company Company, long Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

/// <summary>Revision zero with no refresh time denotes a registered company whose cache has never been replaced.</summary>
public sealed record PartyCacheState(CompanyKey Company, long Revision, DateTimeOffset? RefreshedAtUtc)
{
    public string? ParserVersion { get; init; }
    public string? ContextVersion { get; init; }
}

/// <summary>Version is the persistence compare-and-swap token, separate from telephony revision and event sequence.</summary>
public sealed record StoredCall(CallHistory History, long Version);

public sealed record StoredCallNote(CallNote Note, long Version);

public sealed record StoredRecording(Recording Recording, long Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

/// <summary>A source-data error, retained independently of subsequent cache replacement. Never an operational log message.</summary>
public sealed record NumberParseFailure
{
    public NumberParseFailure(Guid id, PartyKey party, SourceField source, PhoneKind kind,
        string originalValue, string code, DateTimeOffset occurredAtUtc)
    {
        _ = DatabaseId.Encode(id);
        StorageContract.PartyKey(party);
        StorageContract.Source(source);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentNullException.ThrowIfNull(originalValue);
        StorageContract.Text(originalValue, 2048, nameof(originalValue));
        StorageContract.Code(code);
        StorageContract.Utc(occurredAtUtc);
        Id = id;
        Party = party;
        Source = source;
        Kind = kind;
        OriginalValue = originalValue;
        Code = code;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; }
    public PartyKey Party { get; }
    public SourceField Source { get; }
    public PhoneKind Kind { get; }
    public string OriginalValue { get; }
    public string Code { get; }
    public DateTimeOffset OccurredAtUtc { get; }
}

public sealed record CompanyRepositoryQuery(PageRequest Page, LexwareProductLine? ProductLine = null);

/// <summary>UTC bounds are inclusive/exclusive; ordering is occurrence descending, then canonical ID ascending.</summary>
public sealed record NumberParseLogQuery(CompanyKey Company, PageRequest Page, DateTimeOffset? FromUtc = null, DateTimeOffset? ToUtc = null);
