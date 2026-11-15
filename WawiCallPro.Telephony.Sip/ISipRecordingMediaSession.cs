using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Telephony.Sip;

/// <summary>Optional owned recording capability. Disposing the media session drains its recording first.</summary>
public interface ISipRecordingMediaSession : ISipPcmMediaSession
{
    Task<IDuplexPcmRecording> StartRecordingAsync(DuplexPcmRecordingRequest request, CancellationToken cancellationToken = default);
}
