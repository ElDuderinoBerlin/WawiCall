using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Audio;

/// <summary>Call recovery and protected completion replay precede any interruption of captures without files.</summary>
public sealed class InterruptedRecordingSipPersistence(RecordingRecoverySipPersistence calls,
    IInterruptedRecordingRecovery interrupted) : ISipCallPersistence
{
    public Task<CallSnapshot> SaveAsync(CallSnapshot snapshot, CancellationToken cancellationToken = default) => calls.SaveAsync(snapshot, cancellationToken);
    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        await calls.RecoverAsync(cancellationToken).ConfigureAwait(false);
        await interrupted.RecoverAsync(cancellationToken).ConfigureAwait(false);
    }
}
