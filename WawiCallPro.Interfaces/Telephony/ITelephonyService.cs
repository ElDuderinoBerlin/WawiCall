using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Interfaces.Telephony;

public enum RegistrationState
{
    Stopped = 0,
    Registering = 1,
    Registered = 2,
    Reconnecting = 3,
    Failed = 4
}

/// <summary>Status exposes a safe error code, never credentials or raw authentication headers.</summary>
public sealed record RegistrationSnapshot(RegistrationState State, DateTimeOffset ChangedAtUtc, string? ErrorCode = null);

/// <summary>
/// Asynchronous SIP operations implemented exclusively by SIPSorcery in phase 7.
/// Events can originate on background threads; subscribers marshal UI work through IUiDispatcher.
/// Failed/unsupported operations throw; cancellation does not imply that a remote SIP action was undone.
/// </summary>
public interface ITelephonyService
{
    RegistrationSnapshot Registration { get; }
    ImmutableArray<CallSnapshot> GetCalls();
    event EventHandler<RegistrationChangedEventArgs>? RegistrationChanged;
    event EventHandler<CallChangedEventArgs>? CallChanged;

    Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken cancellationToken = default);
    Task AnswerAsync(Guid callId, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid callId, CancellationToken cancellationToken = default);
    Task HangUpAsync(Guid callId, CancellationToken cancellationToken = default);
    Task HoldAsync(Guid callId, CancellationToken cancellationToken = default);
    Task ResumeAsync(Guid callId, CancellationToken cancellationToken = default);
    Task TransferAsync(Guid callId, string destination, CancellationToken cancellationToken = default);

    /// <summary>Sends one of 0–9, *, # or A–D during an established call.</summary>
    Task SendDtmfAsync(Guid callId, char digit, CancellationToken cancellationToken = default);
}

public sealed class RegistrationChangedEventArgs(RegistrationSnapshot registration) : EventArgs
{
    public RegistrationSnapshot Registration { get; } = registration;
}

public sealed class CallChangedEventArgs(CallSnapshot call) : EventArgs
{
    public CallSnapshot Call { get; } = call;
}
