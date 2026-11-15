namespace WawiCallPro.Interfaces.Audio;

/// <summary>Explicit recording access for an established call. Does not open devices or start recording automatically.</summary>
public interface ICallRecordingService
{
    /// <summary>The request's CallId must identify the currently connected call. Both anchors must share a
    /// caller-established recording epoch. The returned controller supports explicit Stop/abort; the call
    /// also owns its cleanup. Call end or host shutdown aborts an unstopped recording without publishing a mix.</summary>
    Task<IDuplexPcmRecording> StartRecordingAsync(DuplexPcmRecordingRequest request, CancellationToken cancellationToken = default);
}
