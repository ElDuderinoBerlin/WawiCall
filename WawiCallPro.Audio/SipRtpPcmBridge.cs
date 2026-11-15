using System.Net;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

internal sealed class SipRtpPcmBridge : IAsyncDisposable
{
    private readonly VoIPMediaSession _media;
    private readonly PcmObservingAudioEncoder _encoder;
    private readonly SipRtpAudioReceiver _receiver;
    private readonly SipRtpPcmSender? _sender;
    private readonly Action _release;
    private readonly object _gate = new();
    private readonly Dictionary<int, EncodedAudioFrame> _pending = new();
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closed;
    private int _inflight;
    private Task? _dispose;

    internal SipRtpPcmBridge(VoIPMediaSession media, IAudioSink sink, PcmObservingAudioEncoder encoder,
        SipRtpAudioReceiver receiver, Action release)
    {
        _media = media; _encoder = encoder; _receiver = receiver; _release = release;
        try { if (media.Media.AudioSource is not null) _sender = new(media, encoder); }
        catch { receiver.DisposeAsync().AsTask().GetAwaiter().GetResult(); throw; }
        // In pinned SIPSorcery 10.0.17 a frame and its RTP event run synchronously on the same dispatch thread.
        // Keep the original frame/format and delay only its existing sink call, never decode another copy.
        media.OnAudioFrameReceived -= sink.GotEncodedMediaFrame;
        media.OnAudioFrameReceived += FrameReceived;
        media.OnRtpPacketReceived += PacketReceived;
    }

    private void FrameReceived(EncodedAudioFrame frame)
    {
        EncodedAudioFrame? orphan = null;
        var forwardPlain = false;
        lock (_gate)
        {
            if (_closed) return;
            _inflight++;
            var thread = Environment.CurrentManagedThreadId;
            if (_pending.Remove(thread, out var previous)) orphan = previous;
            if (_pending.Count >= 8) forwardPlain = true;
            else _pending.Add(thread, frame);
        }
        try
        {
            if (orphan is not null || forwardPlain) _encoder.RejectReceivedMetadata();
            if (orphan is not null) _receiver.ReceiveWithoutMetadata(orphan);
            if (forwardPlain) _receiver.ReceiveWithoutMetadata(frame);
        }
        finally { Exit(); }
    }

    private void PacketReceived(IPEndPoint remote, SDPMediaTypesEnum mediaType, RTPPacket packet)
    {
        if (mediaType != SDPMediaTypesEnum.audio) return;
        EncodedAudioFrame frame;
        lock (_gate)
        {
            if (_closed || !_pending.Remove(Environment.CurrentManagedThreadId, out frame!)) return;
            _inflight++;
        }
        try
        {
            var format = frame.AudioFormat;
            if (!ReferenceEquals(frame.EncodedAudio, packet.Payload) || format.FormatID != packet.Header.PayloadType || format.RtpClockRate is < 1 or > 192000)
            {
                _encoder.RejectReceivedMetadata();
                _receiver.ReceiveWithoutMetadata(frame);
                return;
            }
            _receiver.Receive(new(packet.Header.SyncSource, packet.Header.SequenceNumber, packet.Header.Timestamp, format.RtpClockRate), frame);
        }
        catch { _encoder.RejectReceivedMetadata(); throw; }
        finally { Exit(); }
    }

    private void Exit()
    {
        lock (_gate)
        {
            _inflight--;
            if (_closed && _inflight == 0) _drained.TrySetResult();
        }
    }
    public ValueTask DisposeAsync() { lock (_gate) return new(_dispose ??= DisposeCoreAsync()); }
    private async Task DisposeCoreAsync()
    {
        _closed = true;
        _media.OnAudioFrameReceived -= FrameReceived;
        _media.OnRtpPacketReceived -= PacketReceived;
        if (_pending.Count != 0) _encoder.RejectReceivedMetadata();
        _pending.Clear();
        if (_inflight == 0) _drained.TrySetResult();
        await _drained.Task.ConfigureAwait(false);
        try { if (_sender is not null) await _sender.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            try { await _receiver.DisposeAsync().ConfigureAwait(false); }
            finally { _release(); }
        }
    }
}
