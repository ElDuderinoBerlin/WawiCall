using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Interfaces.History;

/// <summary>Captures the configured installation ID and the effective local machine/account at call creation.</summary>
public interface ICallOriginProvider
{
    Task<CallOrigin> CaptureAsync(CancellationToken cancellationToken = default);
}

/// <summary>Persists a new call with its actual origin. An existing ID is never overwritten.</summary>
public interface ICallJournal
{
    Task<CallSnapshot> BeginAsync(CallSnapshot call, CancellationToken cancellationToken = default);
}
