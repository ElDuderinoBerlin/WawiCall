using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Telephony.Sip;

public sealed partial class SipTelephonyService : ICallRecordingService
{
    public async Task<IDuplexPcmRecording> StartRecordingAsync(DuplexPcmRecordingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Recording);
        IDuplexPcmRecording? recording = null;
        await CommandAsync(request.Recording.CallId, agent => EnqueueAsync(async () =>
        {
            // The event queue also owns terminal call cleanup. Recheck after queuing so a preceding BYE wins.
            cancellationToken.ThrowIfCancellationRequested();
            if (_stopping || !ReferenceEquals(_agent, agent) || agent.State?.Current.State != CallState.Connected)
                throw new TelephonyException("CallNotConnected");
            if (agent.Media is not ISipRecordingMediaSession media)
                throw new TelephonyException("RecordingUnavailable");
            recording = await media.StartRecordingAsync(request, cancellationToken).ConfigureAwait(false);
        }), cancellationToken).ConfigureAwait(false);
        return recording!;
    }
}
