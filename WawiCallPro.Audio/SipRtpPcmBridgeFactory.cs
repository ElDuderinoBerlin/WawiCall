using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Creates per-call RTP/PCM bindings without opening devices or starting media.</summary>
public sealed class SipRtpPcmBridgeFactory(IRtpAudioPacketBufferFactory buffers, TimeProvider clock,
    ILogger<SipRtpPcmBridgeFactory> logger) : ISipRtpPcmBridgeFactory
{
    private static readonly object Gate = new();
    private static readonly ConditionalWeakTable<VoIPMediaSession, object> Bindings = new();

    public IAsyncDisposable Create(VoIPMediaSession media, IAudioSink sink, PcmObservingAudioEncoder encoder)
    {
        ArgumentNullException.ThrowIfNull(media); ArgumentNullException.ThrowIfNull(sink); ArgumentNullException.ThrowIfNull(encoder);
        if (media.IsClosed || !ReferenceEquals(media.Media.AudioSink, sink))
            throw new ArgumentException("Die RTP-/PCM-Bindung benötigt den eigenen Audio-Sink einer offenen Mediensitzung.");
        lock (Gate)
        {
            if (Bindings.TryGetValue(media, out _)) throw new InvalidOperationException("Diese Mediensitzung besitzt bereits eine RTP-/PCM-Bindung.");
            Bindings.Add(media, new object());
            try { return new SipRtpPcmBridge(media, sink, encoder,
                new SipRtpAudioReceiver(buffers, clock, sink, encoder, logger),
                () => { lock (Gate) Bindings.Remove(media); }); }
            catch { Bindings.Remove(media); throw; }
        }
    }
}
