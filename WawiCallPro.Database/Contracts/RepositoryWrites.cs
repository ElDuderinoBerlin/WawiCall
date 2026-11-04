using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Database.Contracts;

/// <summary>Null expected version means insert only; an existing record requires its last observed positive version.</summary>
public sealed record CompanyWrite
{
    public CompanyWrite(Company company, long? expectedVersion, DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(company);
        StorageContract.CompanyKey(company.Key);
        StorageContract.Text(company.DisplayName, 512, nameof(company), required: true);
        StorageContract.ExpectedVersion(expectedVersion);
        StorageContract.Utc(observedAtUtc);
        Company = company;
        ExpectedVersion = expectedVersion;
        ObservedAtUtc = observedAtUtc;
    }

    public Company Company { get; }
    public long? ExpectedVersion { get; }
    public DateTimeOffset ObservedAtUtc { get; }
}

/// <summary>A complete company cache, committed atomically only after all source pages were successfully loaded.</summary>
public sealed record PartyCacheReplacement
{
    public PartyCacheReplacement(CompanyKey company, long expectedRevision, ImmutableArray<Party> parties, DateTimeOffset refreshedAtUtc)
    {
        StorageContract.CompanyKey(company);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
        StorageContract.Utc(refreshedAtUtc);
        StorageContract.Array(parties);
        var keys = new HashSet<PartyKey>();
        foreach (var party in parties)
        {
            StorageContract.Party(party);
            if (party.Key.Company != company || !keys.Add(party.Key))
                throw new ArgumentException("Cache entries must have unique source identities in the specified company.", nameof(parties));
        }
        Company = company;
        ExpectedRevision = expectedRevision;
        Parties = parties;
        RefreshedAtUtc = refreshedAtUtc;
    }

    public CompanyKey Company { get; }
    public long ExpectedRevision { get; }
    public ImmutableArray<Party> Parties { get; }
    public DateTimeOffset RefreshedAtUtc { get; }
}

/// <summary>
/// One transaction replaces the call snapshot/candidates and appends immutable events. Existing notes and recordings
/// are not part of this write. Null expected version is insert only, never an unconditional upsert.
/// </summary>
public sealed record CallWrite
{
    public CallWrite(CallSnapshot call, long? expectedVersion, ImmutableArray<CallPartySnapshot> parties,
        ImmutableArray<CallEvent> events, CallOrigin origin)
        : this(call, expectedVersion, parties, events, origin, null) { }

    public CallWrite(CallSnapshot call, long? expectedVersion, ImmutableArray<CallPartySnapshot> parties,
        ImmutableArray<CallEvent> events, CallOrigin origin, CallerLookupSnapshot? lookup)
    {
        StorageContract.Call(call, parties, events);
        StorageContract.Lookup(lookup);
        if (lookup is not null)
        {
            var scope = lookup.SearchCompanies.Select(company => company.Company).ToHashSet();
            if (parties.Any(party => party.Match is not null && !scope.Contains(party.Party.Company)))
                throw new ArgumentException("Automatic match evidence must belong to the recorded search scope.", nameof(lookup));
        }
        StorageContract.ExpectedVersion(expectedVersion);
        ArgumentNullException.ThrowIfNull(origin);
        if (call.Origin is not null && call.Origin != origin)
            throw new ArgumentException("Call origin must match the captured snapshot.", nameof(origin));
        Call = call with { Origin = origin };
        Origin = origin;
        ExpectedVersion = expectedVersion;
        Parties = parties;
        Events = events;
        Lookup = lookup;
    }

    public CallSnapshot Call { get; }
    public CallOrigin Origin { get; }
    public long? ExpectedVersion { get; }
    public ImmutableArray<CallPartySnapshot> Parties { get; }
    public ImmutableArray<CallEvent> Events { get; }
    public CallerLookupSnapshot? Lookup { get; }
}

/// <summary>Creation time is assigned on insert and retained on edits. Ownership and persisted version are checked in the transaction.</summary>
public sealed record CallNoteWrite
{
    public CallNoteWrite(Guid callId, Guid noteId, string text, long? expectedVersion, DateTimeOffset changedAtUtc)
    {
        _ = DatabaseId.Encode(callId);
        _ = DatabaseId.Encode(noteId);
        StorageContract.Text(text, 32768, nameof(text), required: true, multiline: true);
        StorageContract.ExpectedVersion(expectedVersion);
        StorageContract.Utc(changedAtUtc);
        CallId = callId;
        NoteId = noteId;
        Text = text;
        ExpectedVersion = expectedVersion;
        ChangedAtUtc = changedAtUtc;
    }

    public Guid CallId { get; }
    public Guid NoteId { get; }
    public string Text { get; }
    public long? ExpectedVersion { get; }
    public DateTimeOffset ChangedAtUtc { get; }
}

/// <summary>Metadata only; committing this request never creates, deletes or confirms the contents of an audio file.</summary>
public sealed record RecordingWrite
{
    public RecordingWrite(Recording recording, long? expectedVersion, DateTimeOffset changedAtUtc)
    {
        StorageContract.Recording(recording);
        StorageContract.ExpectedVersion(expectedVersion);
        StorageContract.Utc(changedAtUtc);
        Recording = recording;
        ExpectedVersion = expectedVersion;
        ChangedAtUtc = changedAtUtc;
    }

    public Recording Recording { get; }
    public long? ExpectedVersion { get; }
    public DateTimeOffset ChangedAtUtc { get; }
}
