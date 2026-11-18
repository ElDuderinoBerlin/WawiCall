using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Audio;

/// <summary>Requests a nonblocking file audit after the existing call/completion recovery under the account lease.</summary>
public sealed class ReconciledRecordingSipPersistence(InterruptedRecordingSipPersistence calls, IRecordingArtifactAuditScheduler artifacts) : ISipCallPersistence
{
    private readonly IRecordingConversionQueue? _conversions;
    public ReconciledRecordingSipPersistence(InterruptedRecordingSipPersistence calls, IRecordingArtifactAuditScheduler artifacts,
        IRecordingConversionQueue conversions) : this(calls, artifacts) => _conversions = conversions;
    public async Task<CallSnapshot> SaveAsync(CallSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var saved = await calls.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
        if (saved.State is CallState.Ended or CallState.Failed) _conversions?.Request();
        return saved;
    }
    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        await calls.RecoverAsync(cancellationToken).ConfigureAwait(false);
        artifacts.Request();
        _conversions?.Request();
    }
}
