using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Audio;

/// <summary>The SIP account lease is held before call recovery and pending recording delivery are invoked.</summary>
public sealed class RecordingRecoverySipPersistence(SipCallPersistence calls, IRecordingJournalRecovery recordings) : ISipCallPersistence
{
    public Task<CallSnapshot> SaveAsync(CallSnapshot snapshot, CancellationToken cancellationToken = default) => calls.SaveAsync(snapshot, cancellationToken);
    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        await calls.RecoverAsync(cancellationToken).ConfigureAwait(false);
        await recordings.RecoverAsync(cancellationToken).ConfigureAwait(false);
    }
}
