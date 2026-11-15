using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Pairs the CSCore codec result with its synchronous local RTP submission. Never creates another encoder.</summary>
internal sealed class SipRtpPcmSender : IAsyncDisposable
{
    private readonly VoIPMediaSession _media;
    private readonly IAudioSource _source;
    private readonly PcmObservingAudioEncoder _pcm;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _serial = new(1, 1);
    private readonly Dictionary<int, Pending> _pending = new();
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closed;
    private int _inflight, _sendThread;

    internal SipRtpPcmSender(VoIPMediaSession media, PcmObservingAudioEncoder pcm)
    {
        _media = media; _source = media.Media.AudioSource; _pcm = pcm;
        pcm.AttachSender(this);
        _source.OnAudioSourceEncodedSample -= media.SendAudio;
        _source.OnAudioSourceEncodedSample += Send;
        if (!ReferenceEquals(_source, media.AudioExtrasSource))
        {
            media.AudioExtrasSource.OnAudioSourceEncodedSample -= media.SendAudio;
            media.AudioExtrasSource.OnAudioSourceEncodedSample += SendExtras;
        }
    }

    internal void Remember(short[] samples, AudioFormat format, byte[] payload)
    {
        if (!_pcm.HasSentObservers) return;
        var valid = format.ClockRate is >= 8000 and <= 192000 && format.RtpClockRate is >= 1 and <= 192000
            && format.ChannelCount is 1 or 2 && samples.Length > 0 && samples.Length % format.ChannelCount == 0
            && samples.Length <= format.ClockRate * format.ChannelCount * 2 && payload.Length is > 0 and <= 65536;
        lock (_gate)
        {
            if (_closed || !valid || _pending.ContainsKey(Environment.CurrentManagedThreadId) || _pending.Count >= 8)
            { Fail(); return; }
            _pending.Add(Environment.CurrentManagedThreadId, new(format, payload, samples.ToArray()));
        }
    }

    private void Send(uint duration, byte[] payload) => Submit(duration, payload, extras: false);
    private void SendExtras(uint duration, byte[] payload) => Submit(duration, payload, extras: true);
    private void Submit(uint duration, byte[] payload, bool extras)
    {
        Pending? pending = null;
        lock (_gate)
        {
            if (_closed) return;
            _inflight++;
            if (!extras) _pending.Remove(Environment.CurrentManagedThreadId, out pending);
        }
        var entered = false;
        try
        {
            if (Volatile.Read(ref _sendThread) == Environment.CurrentManagedThreadId)
            { Fail(); _media.SendAudio(duration, payload); return; }
            _serial.Wait(); entered = true;
            Volatile.Write(ref _sendThread, Environment.CurrentManagedThreadId);
            var track = _media.AudioStream?.LocalTrack;
            var timestamp = track?.Timestamp ?? 0; var ssrc = track?.Ssrc ?? 0;
            _media.SendAudio(duration, payload);
            if (extras || !_pcm.HasSentObservers) return;
            var stream = _media.AudioStream;
            if (pending is null || track is null || stream is null || !ReferenceEquals(track, stream.LocalTrack)
                || !ReferenceEquals(pending.Payload, payload) || duration == 0 || track.Ssrc != ssrc
                || unchecked(track.Timestamp - timestamp) != duration)
            { Fail(); return; }
            var selected = stream.NegotiatedFormat.ToAudioFormat();
            if (selected.FormatID != pending.Format.FormatID || selected.Codec != pending.Format.Codec
                || selected.ClockRate != pending.Format.ClockRate || selected.RtpClockRate != pending.Format.RtpClockRate
                || selected.ChannelCount != pending.Format.ChannelCount
                || (long)pending.Samples.Length / selected.ChannelCount * selected.RtpClockRate / selected.ClockRate != duration)
            { Fail(); return; }
            _pcm.PublishSent(new(ssrc, timestamp, selected.RtpClockRate, duration), pending.Format, pending.Samples);
        }
        catch { Fail(); throw; }
        finally
        {
            if (entered) { Volatile.Write(ref _sendThread, 0); _serial.Release(); }
            lock (_gate)
            {
                _inflight--;
                if (_closed && _inflight == 0) _drained.TrySetResult();
            }
        }
    }

    private void Fail()
    {
        lock (_gate) _pending.Clear();
        _pcm.RejectSentMetadata();
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_closed) return new(_drained.Task);
            _closed = true;
            _source.OnAudioSourceEncodedSample -= Send;
            if (!ReferenceEquals(_source, _media.AudioExtrasSource)) _media.AudioExtrasSource.OnAudioSourceEncodedSample -= SendExtras;
            _pcm.DetachSender(this);
            if (_pending.Count != 0) Fail();
            _pending.Clear();
            if (_inflight == 0) _drained.TrySetResult();
            return new(_drained.Task);
        }
    }

    private sealed record Pending(AudioFormat Format, byte[] Payload, short[] Samples);
}
