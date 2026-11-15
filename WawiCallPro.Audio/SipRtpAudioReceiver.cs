using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Owns one bounded packet queue and deadline timer. All sink/decoder calls are serialized.</summary>
internal sealed class SipRtpAudioReceiver : IAsyncDisposable
{
    private readonly IRtpAudioPacketBufferFactory _buffers;
    private readonly IAudioSink _sink;
    private readonly PcmObservingAudioEncoder _encoder;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _serial = new(1, 1);
    private readonly object _disposeGate = new();
    private readonly Dictionary<ushort, FrameFormat> _formats = new();
    private readonly ITimer _timer;
    private IRtpAudioPacketBuffer? _buffer;
    private bool _failed;
    private int _closing, _deliveryThread;
    private Task? _dispose;

    internal SipRtpAudioReceiver(IRtpAudioPacketBufferFactory buffers, TimeProvider clock,
        IAudioSink sink, PcmObservingAudioEncoder encoder, ILogger logger)
    {
        _buffers = buffers; _sink = sink; _encoder = encoder; _logger = logger;
        _timer = clock.CreateTimer(DeadlineReached, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    internal void Receive(RtpPcmPacketPosition position, EncodedAudioFrame frame)
    {
        if (!Enter()) return;
        try
        {
            if (_failed) { _sink.GotEncodedMediaFrame(frame); return; }
            _buffer ??= _buffers.Create(new(position.SynchronizationSource, position.SequenceNumber, position.RtpClockRate));
            var result = _buffer.TryEnqueue(position, frame.AudioFormat.FormatID, frame.EncodedAudio);
            if (result is RtpAudioEnqueueResult.Failed or RtpAudioEnqueueResult.Closed)
            {
                Fail();
                _sink.GotEncodedMediaFrame(frame);
                return;
            }
            if (result == RtpAudioEnqueueResult.Accepted)
                _formats.Add(position.SequenceNumber, new(frame.MediaStreamIndex, frame.AudioFormat, frame.DurationMilliSeconds));
            Drain();
            ArmDeadline();
        }
        catch { Fail(); throw; }
        finally { Exit(); }
    }

    internal void ReceiveWithoutMetadata(EncodedAudioFrame frame)
    {
        if (!Enter()) return;
        try { Fail(); _sink.GotEncodedMediaFrame(frame); }
        finally { Exit(); }
    }

    private bool Enter()
    {
        if (Volatile.Read(ref _closing) != 0) return false;
        // A sink/observer must not recursively drive the same stateful decoder.
        if (Volatile.Read(ref _deliveryThread) == Environment.CurrentManagedThreadId) { Fail(); return false; }
        _serial.Wait();
        if (Volatile.Read(ref _closing) != 0) { _serial.Release(); return false; }
        Volatile.Write(ref _deliveryThread, Environment.CurrentManagedThreadId);
        return true;
    }

    private void Exit() { Volatile.Write(ref _deliveryThread, 0); _serial.Release(); }

    private void Drain()
    {
        while (_buffer is not null && _buffer.TryDequeue(out var packet))
        {
            var format = _formats[packet.Position.SequenceNumber];
            _formats.Remove(packet.Position.SequenceNumber);
            // The buffer owns its copy. The sink borrows it synchronously; retain the packet's own format.
            var frame = new EncodedAudioFrame(format.StreamIndex, format.Format, format.Duration, packet.Payload.ToArray());
            _encoder.DeliverReceivedFrame(packet.Position, frame, _sink);
        }
    }

    private void ArmDeadline()
    {
        if (Volatile.Read(ref _closing) != 0) return;
        var delay = _buffer?.NextPacketDelay;
        _timer.Change(delay ?? Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void DeadlineReached(object? state)
    {
        if (Volatile.Read(ref _closing) != 0) return;
        // The timer can fire after arming a zero deadline but before Receive releases the gate.
        // Retry with this same one-shot timer; dropping that wake-up could strand the last packet.
        if (!_serial.Wait(0))
        {
            if (Volatile.Read(ref _closing) == 0)
            {
                try { _timer.Change(TimeSpan.FromMilliseconds(1), Timeout.InfiniteTimeSpan); }
                catch (ObjectDisposedException) { }
            }
            return;
        }
        Volatile.Write(ref _deliveryThread, Environment.CurrentManagedThreadId);
        try
        {
            if (Volatile.Read(ref _closing) != 0 || _failed) return;
            Drain();
            ArmDeadline();
        }
        catch
        {
            Fail();
            // Timer exceptions cannot escape onto the thread pool. Never log payloads or native details.
            try { _logger.LogWarning("RTP-Audioempfang: Die gepufferte Dekodierung ist fehlgeschlagen; zeitabhängige PCM-Beobachtung wurde gesperrt."); }
            catch { }
        }
        finally { Exit(); }
    }

    private void Fail()
    {
        _failed = true;
        _buffer?.Dispose();
        _formats.Clear();
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _encoder.RejectReceivedMetadata();
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate)
        {
            if (_dispose is not null) return new(_dispose);
            Volatile.Write(ref _closing, 1);
            return new(_dispose = DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _serial.WaitAsync().ConfigureAwait(false);
        try
        {
            await _timer.DisposeAsync().ConfigureAwait(false);
            if (!_failed) { _buffer?.CompleteInput(); Drain(); }
        }
        catch { _encoder.RejectReceivedMetadata(); throw; }
        finally { _buffer?.Dispose(); _formats.Clear(); Exit(); }
    }

    private sealed record FrameFormat(int StreamIndex, AudioFormat Format, uint Duration);
}
