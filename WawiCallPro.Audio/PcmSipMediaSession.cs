using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Audio;

/// <summary>Retires and drains observers before closing native audio; codec ownership stays with the media endpoint.</summary>
public sealed class PcmSipMediaSession(ISipMediaSession inner, PcmObservingAudioEncoder pcm) : ISipPcmMediaSession
{
    private readonly object _gate = new();
    private Task? _dispose;
    private readonly IAsyncDisposable? _receive;
    public PcmSipMediaSession(ISipMediaSession inner, PcmObservingAudioEncoder pcm, IAsyncDisposable receive) : this(inner, pcm) =>
        _receive = receive ?? throw new ArgumentNullException(nameof(receive));
    public SIPSorcery.SIP.App.IMediaSession Session => inner.Session;
    public ISipConversationAudio? ConversationAudio => inner.ConversationAudio;
    public IConversationPcmSource PcmSource => pcm;
    public void ApplyPlaybackSettings() => inner.ApplyPlaybackSettings();
    public ValueTask DisposeAsync()
    {
        lock (_gate) return new(_dispose ??= DisposeCoreAsync());
    }
    private async Task DisposeCoreAsync()
    {
        try { if (_receive is not null) await _receive.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            try { await pcm.DisposeAsync().ConfigureAwait(false); }
            finally { await inner.DisposeAsync().ConfigureAwait(false); }
        }
    }
}
