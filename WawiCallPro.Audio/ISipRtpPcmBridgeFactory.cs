using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;

namespace WawiCallPro.Audio;

/// <summary>Owns replacement of VoIPMediaSession's immediate sink callback until the media session is closed.
/// The owner drains this binding before retiring PCM observers and releasing the native endpoint.</summary>
public interface ISipRtpPcmBridgeFactory
{
    IAsyncDisposable Create(VoIPMediaSession media, IAudioSink sink, PcmObservingAudioEncoder encoder);
}
