using System.Collections.Immutable;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Interfaces.History;

/// <summary>A matched participant with the name/number at call time, independent of subsequent cache changes.</summary>
public sealed record CallPartySnapshot(PartyKey Party, string DisplayName, string? Number, bool IsSelected);

/// <summary>Note IDs remain stable when text is edited. Timestamps are UTC.</summary>
public sealed record CallNote(Guid Id, Guid CallId, string Text, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

/// <summary>Sequence orders events within a call; Code is a stable technical code, not a localized message.</summary>
public sealed record CallEvent(Guid Id, Guid CallId, long Sequence, DateTimeOffset OccurredAtUtc, string Code, CallState State);

public enum RecordingAvailability
{
    Pending = 0,
    Available = 1,
    Missing = 2,
    Failed = 3
}

/// <summary>RelativePath is relative to the configured recordings root, never a source-machine absolute path.</summary>
public sealed record Recording(
    Guid Id,
    Guid CallId,
    string RelativePath,
    string Format,
    TimeSpan? Duration,
    RecordingAvailability Availability);

/// <summary>Detached history including all candidates, not just the selected participant.</summary>
public sealed record CallHistory
{
    public required CallSnapshot Call { get; init; }
    public ImmutableArray<CallPartySnapshot> Parties { get; init; } = [];
    public ImmutableArray<CallNote> Notes { get; init; } = [];
    public ImmutableArray<CallEvent> Events { get; init; } = [];
    public ImmutableArray<Recording> Recordings { get; init; } = [];
}

/// <summary>
/// Company=null includes all companies and unassigned calls. Date bounds are UTC, from inclusive/to exclusive.
/// A party filter uses its full source identity. Results are ordered by start descending, then call ID.
/// </summary>
public sealed record CallHistoryQuery(
    PageRequest Page,
    CompanyKey? Company = null,
    PartyKey? Party = null,
    DateTimeOffset? StartedFromUtc = null,
    DateTimeOffset? StartedToUtc = null)
{
    public Guid? WorkstationId { get; init; }
    public string? MachineName { get; init; }
    public string? WindowsUserName { get; init; }
}
