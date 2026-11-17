using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Audio;

/// <summary>Requests a nonblocking file audit after the existing call/completion recovery under the account lease.</summary>
public sealed class ReconciledRecordingSipPersistence(InterruptedRecordingSipPersistence calls, IRecordingArtifactAuditScheduler artifacts) : ISipCallPersistence
{
    public Task<CallSnapshot> SaveAsync(CallSnapshot snapshot, CancellationToken cancellationToken = default) => calls.SaveAsync(snapshot, cancellationToken);
    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        await calls.RecoverAsync(cancellationToken).ConfigureAwait(false);
        artifacts.Request();
    }
}
