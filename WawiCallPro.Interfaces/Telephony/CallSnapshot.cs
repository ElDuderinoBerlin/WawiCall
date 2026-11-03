using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Interfaces.Telephony;

public enum CallDirection
{
    Incoming = 1,
    Outgoing = 2
}

/// <summary>Shared state vocabulary; legal transitions and concurrency policy are defined in the next plan item.</summary>
public enum CallState
{
    Created = 1,
    Dialing = 2,
    Ringing = 3,
    Connected = 4,
    Held = 5,
    Ended = 6,
    Failed = 7
}

public enum CallEndReason
{
    Completed = 1,
    Missed = 2,
    Rejected = 3,
    Busy = 4,
    Cancelled = 5,
    RemoteHangup = 6,
    NetworkFailure = 7,
    Error = 8,
    Interrupted = 9,
    Transferred = 10
}

public enum NumberPresentation
{
    Unknown = 0,
    Available = 1,
    Withheld = 2
}

/// <summary>
/// A dialled or signalled address, including internal numbers and SIP URIs that have no E.164 value.
/// Withheld/unknown numbers may have no original value; they must not be replaced by a fabricated number.
/// </summary>
public sealed record CallAddress(
    NumberPresentation Presentation,
    string? OriginalValue = null,
    string? E164 = null,
    string? Extension = null);

/// <summary>
/// Stable application call ID independent of SIP dialog IDs. Revision orders snapshots of the same call.
/// Company may be unknown at arrival. All timestamps are UTC; duration starts at AnsweredAtUtc.
/// Snapshots contain no credentials, media endpoints or UI objects.
/// </summary>
public sealed record CallSnapshot
{
    public required Guid Id { get; init; }
    public required long Revision { get; init; }
    public CompanyKey? Company { get; init; }
    public required CallDirection Direction { get; init; }
    public required CallState State { get; init; }
    public required CallAddress LocalAddress { get; init; }
    public required CallAddress RemoteAddress { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? AnsweredAtUtc { get; init; }
    public DateTimeOffset? EndedAtUtc { get; init; }
    public CallEndReason? EndReason { get; init; }
}
