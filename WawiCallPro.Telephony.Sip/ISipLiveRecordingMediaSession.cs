using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Telephony.Sip;

/// <summary>The media owner finalizes live recordings before disposing its PCM source and native endpoint.</summary>
public interface ISipLiveRecordingMediaSession : ISipRecordingMediaSession
{
    LiveRecordingSnapshot RecordingStatus { get; }
    event EventHandler<LiveRecordingChangedEventArgs>? RecordingChanged;
    void CancelPendingStart();
    Task StartLiveAsync(Guid callId, CancellationToken cancellationToken);
    Task StopLiveAsync(CancellationToken cancellationToken);
}
