using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Telephony;

/// <summary>A sequenced observation from the call's serialized event queue, not an unconfirmed UI command.</summary>
public sealed record CallStateChange(long Sequence, CallState State, DateTimeOffset OccurredAtUtc, CallEndReason? EndReason = null);

public enum CallChangeResult
{
    Applied,
    Stale,
    Duplicate,
    Terminal,
    InvalidTransition,
    InvalidTime,
    InvalidEndReason
}

/// <summary>
/// Thread-safe state and timing rules for one call. No events, I/O or UI callbacks run inside the state lock.
/// The caller persists and publishes accepted snapshots in queue order, outside this object.
/// </summary>
public sealed class CallStateMachine
{
    private readonly object _gate = new();
    private CallSnapshot _current;

    /// <summary>Accepts a new or persisted snapshot; invalid persisted state is rejected rather than repaired silently.</summary>
    public CallStateMachine(CallSnapshot initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ValidateSnapshot(initial);
        _current = initial with
        {
            StartedAtUtc = initial.StartedAtUtc.ToUniversalTime(),
            StateChangedAtUtc = initial.StateChangedAtUtc.ToUniversalTime(),
            AnsweredAtUtc = initial.AnsweredAtUtc?.ToUniversalTime(),
            EndedAtUtc = initial.EndedAtUtc?.ToUniversalTime()
        };
    }

    public CallSnapshot Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>
    /// Applies only newer, legal observations. Rejected observations leave both snapshot and revision unchanged.
    /// OccurredAtUtc must not precede the previous accepted observation; offsets are normalized to UTC.
    /// </summary>
    public CallChangeResult Apply(CallStateChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(change.Sequence);
        if (!Enum.IsDefined(change.State))
            throw new ArgumentOutOfRangeException(nameof(change));

        lock (_gate)
        {
            if (change.Sequence <= _current.Revision) return CallChangeResult.Stale;
            if (IsTerminal(_current.State)) return CallChangeResult.Terminal;
            if (change.State == _current.State) return CallChangeResult.Duplicate;
            if (!CanTransition(_current.Direction, _current.State, change.State)) return CallChangeResult.InvalidTransition;
            var occurredAtUtc = change.OccurredAtUtc.ToUniversalTime();
            if (occurredAtUtc < _current.StateChangedAtUtc) return CallChangeResult.InvalidTime;
            var answeredAtUtc = _current.AnsweredAtUtc;
            if (change.State == CallState.Connected) answeredAtUtc ??= occurredAtUtc;
            if (!IsValidEndReason(_current.Direction, change.State, answeredAtUtc.HasValue, change.EndReason))
                return CallChangeResult.InvalidEndReason;

            _current = _current with
            {
                Revision = change.Sequence,
                State = change.State,
                StateChangedAtUtc = occurredAtUtc,
                AnsweredAtUtc = answeredAtUtc,
                EndedAtUtc = IsTerminal(change.State) ? occurredAtUtc : null,
                EndReason = change.EndReason
            };
            return CallChangeResult.Applied;
        }
    }

    private static bool IsTerminal(CallState state) => state is CallState.Ended or CallState.Failed;

    private static bool CanTransition(CallDirection direction, CallState from, CallState to)
    {
        if (IsTerminal(to)) return true;
        return (from, to) switch
        {
            (CallState.Created, CallState.Dialing) => direction == CallDirection.Outgoing,
            (CallState.Created, CallState.Ringing) => direction == CallDirection.Incoming,
            (CallState.Dialing, CallState.Ringing or CallState.Connected) => true,
            (CallState.Ringing, CallState.Connected) => true,
            (CallState.Connected, CallState.Held) => true,
            (CallState.Held, CallState.Connected) => true,
            _ => false
        };
    }

    private static bool IsValidEndReason(CallDirection direction, CallState state, bool answered, CallEndReason? reason)
    {
        if (!IsTerminal(state)) return reason is null;
        if (state == CallState.Failed)
            return reason is CallEndReason.NetworkFailure or CallEndReason.Error or CallEndReason.Interrupted;
        return reason switch
        {
            CallEndReason.Completed or CallEndReason.Transferred => answered,
            CallEndReason.Missed => direction == CallDirection.Incoming && !answered,
            CallEndReason.Cancelled => direction == CallDirection.Outgoing && !answered,
            CallEndReason.Rejected or CallEndReason.Busy => !answered,
            CallEndReason.RemoteHangup => true,
            _ => false
        };
    }

    private static void ValidateSnapshot(CallSnapshot snapshot)
    {
        if (snapshot.Id == Guid.Empty || snapshot.Revision < 0 ||
            !Enum.IsDefined(snapshot.Direction) || !Enum.IsDefined(snapshot.State))
            throw new ArgumentException("Invalid call identity, revision or state.", nameof(snapshot));
        ArgumentNullException.ThrowIfNull(snapshot.LocalAddress);
        ArgumentNullException.ThrowIfNull(snapshot.RemoteAddress);
        if (snapshot.Direction == CallDirection.Incoming && snapshot.State == CallState.Dialing)
            throw new ArgumentException("An incoming call cannot be dialing.", nameof(snapshot));
        var established = snapshot.State is CallState.Connected or CallState.Held;
        if ((established && snapshot.AnsweredAtUtc is null) ||
            (!established && !IsTerminal(snapshot.State) && snapshot.AnsweredAtUtc is not null) ||
            snapshot.AnsweredAtUtc < snapshot.StartedAtUtc ||
            snapshot.StateChangedAtUtc < (snapshot.AnsweredAtUtc ?? snapshot.StartedAtUtc) ||
            IsTerminal(snapshot.State) != snapshot.EndedAtUtc.HasValue ||
            (snapshot.EndedAtUtc.HasValue && snapshot.EndedAtUtc != snapshot.StateChangedAtUtc) ||
            snapshot.EndedAtUtc < (snapshot.AnsweredAtUtc ?? snapshot.StartedAtUtc) ||
            !IsValidEndReason(snapshot.Direction, snapshot.State, snapshot.AnsweredAtUtc.HasValue, snapshot.EndReason))
            throw new ArgumentException("Call state, timestamps and end reason are inconsistent.", nameof(snapshot));
    }
}
