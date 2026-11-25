using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CSCore;
using CSCore.CoreAudioAPI;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>CSCore WASAPI loopback with the original capture packet clock. No device is opened by construction.</summary>
public sealed class CscorePlaybackCaptureFactory(TimeProvider clock) : IPlaybackPcmCaptureFactory
{
    // The CSCore enum in use defines TimestampError as 0x3 (Discontinuity | Silent).
    // WASAPI's AUDCLNT_BUFFERFLAGS_TIMESTAMP_ERROR is the independent bit 0x4.
    private const AudioClientBufferFlags WasapiTimestampError = (AudioClientBufferFlags)0x4;

    public async Task<IPlaybackPcmCapture> StartAsync(string? deviceId, DateTimeOffset epoch, IPlaybackPcmObserver observer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observer);
        if (epoch.Offset != TimeSpan.Zero || epoch > clock.GetUtcNow()) throw new ArgumentException("Eine gültige UTC-Aufnahmeepoche ist erforderlich.");
        cancellationToken.ThrowIfCancellationRequested();
        var capture = new Capture(deviceId, epoch, observer, clock, cancellationToken);
        try { await capture.Ready.WaitAsync(cancellationToken).ConfigureAwait(false); return capture; }
        catch
        {
            await capture.DisposeAsync().ConfigureAwait(false);
            try { await capture.Completion.ConfigureAwait(false); } catch { }
            throw;
        }
    }

    private sealed class Capture : IPlaybackPcmCapture
    {
        private readonly CancellationTokenSource _stopping;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _worker;
        private readonly object _gate = new();
        private Task? _dispose;
        public Task Ready => _ready.Task;
        public Task Completion => _completion.Task;
        internal Capture(string? deviceId, DateTimeOffset epoch, IPlaybackPcmObserver observer, TimeProvider clock, CancellationToken token)
        {
            _stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
            _worker = Task.Factory.StartNew(() => Run(deviceId, epoch, observer, clock), CancellationToken.None,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void Run(string? deviceId, DateTimeOffset epoch, IPlaybackPcmObserver observer, TimeProvider clock)
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                using var device = deviceId is null ? MMDeviceEnumerator.DefaultAudioEndpoint(DataFlow.Render, Role.Multimedia) : enumerator.GetDevice(deviceId);
                using var client = AudioClient.FromMMDevice(device);
                var format = client.GetMixFormat();
                _ = PlaybackPacketPcm.GetEncoding(format);
                client.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.StreamFlagsLoopback, 2_000_000, 0, format, Guid.Empty);
                using var packets = AudioCaptureClient.FromAudioClient(client);
                var first = Stopwatch.GetTimestamp();
                var referenceUtc = clock.GetUtcNow();
                var last = Stopwatch.GetTimestamp();
                var referenceQpc = (long)(((Int128)first + last) * TimeSpan.TicksPerSecond / (2 * Stopwatch.Frequency));
                var timeline = new PlaybackPacketTimeline(format.SampleRate, referenceQpc, referenceUtc - epoch);
                var started = false;
                try
                {
                    _stopping.Token.ThrowIfCancellationRequested();
                    client.Start(); started = true; _ready.TrySetResult();
                    while (!_stopping.IsCancellationRequested)
                    {
                        while (!_stopping.IsCancellationRequested && packets.GetNextPacketSize() > 0)
                        {
                            byte[]? bytes = null;
                            try
                            {
                                var pointer = packets.GetBuffer(out var frames, out var flags, out var devicePosition, out var qpc);
                                var silent = (flags & AudioClientBufferFlags.Silent) != 0;
                                int length;
                                try
                                {
                                    if (frames is < 1 || frames > format.SampleRate / 2 || (flags & WasapiTimestampError) != 0)
                                        throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
                                    if (!silent && pointer == IntPtr.Zero) throw new PcmRecordingException(PcmRecordingFailure.InvalidBlock);
                                    length = checked(frames * format.BlockAlign);
                                    bytes = ArrayPool<byte>.Shared.Rent(length);
                                    if (!silent) Marshal.Copy(pointer, bytes, 0, length);
                                }
                                finally { packets.ReleaseBuffer(frames); }

                                // Only the owned copy is needed for timeline checks, decoding and the observer.
                                var start = timeline.Map(devicePosition, qpc, frames, (flags & AudioClientBufferFlags.DataDiscontinuity) != 0);
                                var pcm = ArrayPool<short>.Shared.Rent(frames);
                                try
                                {
                                    PlaybackPacketPcm.Decode(bytes.AsSpan(0, length), format, pcm.AsSpan(0, frames), silent);
                                    var offset = start.Ticks < 0 ? (int)Int128.Min(frames, ((Int128)(-start.Ticks) * format.SampleRate + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond) : 0;
                                    while (offset < frames && !_stopping.IsCancellationRequested)
                                    {
                                        var count = Math.Min(format.SampleRate / 10, frames - offset);
                                        var position = start + TimeSpan.FromTicks((long)((Int128)offset * TimeSpan.TicksPerSecond / format.SampleRate));
                                        if (!observer.TryObserve(position, format.SampleRate, pcm.AsSpan(offset, count)))
                                            throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
                                        offset += count;
                                    }
                                }
                                finally { ArrayPool<short>.Shared.Return(pcm, true); }
                            }
                            finally { if (bytes is not null) ArrayPool<byte>.Shared.Return(bytes, true); }
                        }
                        _stopping.Token.WaitHandle.WaitOne(5);
                    }
                }
                finally { if (started) client.Stop(); }
                _completion.TrySetResult();
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            { _ready.TrySetCanceled(_stopping.Token); _completion.TrySetResult(); }
            catch
            {
                try { observer.OnFailure(); } catch { }
                var safe = new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
                _ready.TrySetException(safe); _completion.TrySetException(safe);
            }
        }
        public ValueTask DisposeAsync() { lock (_gate) return new(_dispose ??= DisposeCoreAsync()); }
        private async Task DisposeCoreAsync()
        {
            try { await _stopping.CancelAsync().ConfigureAwait(false); await _worker.ConfigureAwait(false); }
            finally { _stopping.Dispose(); }
        }
    }
}

/// <summary>Uses the device frame counter after its first QPC anchor; arrival scheduling does not move samples.</summary>
public sealed class PlaybackPacketTimeline(int sampleRate, long referenceQpc, TimeSpan referenceOffset)
{
    private bool _started;
    private long _firstDevice, _firstQpc, _lastQpc, _end;
    public TimeSpan Map(long devicePosition, long qpc, int frames, bool discontinuity)
    {
        if (sampleRate is < 8000 or > 192000 || frames is < 1 || frames > sampleRate / 2 || devicePosition < 0 || qpc <= 0
            || _started && (discontinuity || devicePosition < _end || qpc < _lastQpc))
            throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
        if (!_started) { _firstDevice = devicePosition; _firstQpc = qpc; _started = true; }
        var elapsed = (Int128)(devicePosition - _firstDevice) * TimeSpan.TicksPerSecond / sampleRate;
        if (Int128.Abs((Int128)qpc - _firstQpc - elapsed) > TimeSpan.FromMilliseconds(50).Ticks)
            throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
        var ticks = (Int128)referenceOffset.Ticks + _firstQpc - referenceQpc + elapsed;
        if (ticks < -TimeSpan.FromSeconds(2).Ticks || ticks + (Int128)frames * TimeSpan.TicksPerSecond / sampleRate > TimeSpan.FromHours(8).Ticks)
            throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
        _end = checked(devicePosition + frames);
        _lastQpc = qpc;
        return TimeSpan.FromTicks((long)ticks);
    }
}

/// <summary>Bounded render-packet conversion. Every output frame is one downmixed mono PCM16 sample.</summary>
public static class PlaybackPacketPcm
{
    public static AudioEncoding GetEncoding(WaveFormat format)
    {
        var encoding = format.WaveFormatTag;
        if (format is WaveFormatExtensible extended)
            encoding = extended.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71") ? AudioEncoding.IeeeFloat
                : extended.SubFormat == new Guid("00000001-0000-0010-8000-00aa00389b71") ? AudioEncoding.Pcm : AudioEncoding.Unknown;
        if (format.SampleRate is < 8000 or > 192000 || format.Channels is < 1 or > 8
            || format.BlockAlign != format.Channels * format.BitsPerSample / 8
            || !(encoding == AudioEncoding.IeeeFloat && format.BitsPerSample == 32 || encoding == AudioEncoding.Pcm && format.BitsPerSample is 16 or 24 or 32))
            throw new PcmRecordingException(PcmRecordingFailure.InvalidBlock);
        return encoding;
    }
    public static void Decode(ReadOnlySpan<byte> input, WaveFormat format, Span<short> output, bool silent)
    {
        var encoding = GetEncoding(format);
        if (input.Length % format.BlockAlign != 0 || output.Length != input.Length / format.BlockAlign || output.Length > format.SampleRate / 2)
            throw new PcmRecordingException(PcmRecordingFailure.InvalidBlock);
        if (silent) { output.Clear(); return; }
        var bytes = format.BitsPerSample / 8;
        for (var frame = 0; frame < output.Length; frame++)
        {
            double sum = 0;
            for (var channel = 0; channel < format.Channels; channel++)
            {
                var data = input.Slice(frame * format.BlockAlign + channel * bytes, bytes);
                var value = encoding == AudioEncoding.IeeeFloat ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data)) * 32768.0
                    : bytes == 2 ? BinaryPrimitives.ReadInt16LittleEndian(data)
                    : bytes == 3 ? ((data[0] << 8 | data[1] << 16 | data[2] << 24) >> 8) / 256.0
                    : BinaryPrimitives.ReadInt32LittleEndian(data) / 65536.0;
                if (!double.IsFinite(value)) throw new PcmRecordingException(PcmRecordingFailure.InvalidBlock);
                sum += value;
            }
            output[frame] = (short)Math.Clamp(Math.Round(sum / format.Channels, MidpointRounding.AwayFromZero), short.MinValue, short.MaxValue);
        }
    }
}
