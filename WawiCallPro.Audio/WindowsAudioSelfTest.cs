using System.Collections.Immutable;
using System.Runtime.InteropServices;
using CSCore;
using CSCore.Codecs.RAW;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.SoundOut;
using CSCore.Streams;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Offline audio diagnostics. Each action owns its WASAPI stream and keeps samples in memory.</summary>
public interface IAudioSelfTest
{
    Task<AudioBufferRecommendation> CheckBuffersAsync(AudioDeviceSettings settings, IProgress<AudioBufferMeasurement> progress, CancellationToken token);
    Task MonitorAsync(AudioDeviceSettings settings, AudioPlaybackVolume volume, CancellationToken token);
    Task<AudioSelfTestDevice> PlayAsync(AudioDeviceSettings settings, AudioSelfTestSample? sample,
        AudioPlaybackVolume volume, IProgress<AudioSelfTestDevice> opened, CancellationToken token);
    Task<AudioSelfTestSample> RecordAsync(AudioDeviceSettings settings, IProgress<ImmutableArray<int>> spectrum, CancellationToken token);
    Task<IReadOnlyList<AudioSelfTestCodec>> CheckCodecsAsync(AudioSelfTestSample sample, CancellationToken token);
}

public sealed record AudioSelfTestDevice(string Name, int SampleRate, int Channels, int Bits, double BufferMs, int WindowsVolume, bool Muted);
public sealed record AudioSelfTestSample(short[] Samples, int SampleRate, int Peak, AudioSelfTestDevice Device);
public sealed record AudioSelfTestCodec(string Name, int Packets, bool Passed);
public sealed record AudioBufferRecommendation(int? CaptureMilliseconds, int? PlaybackMilliseconds)
{
    public double? MeasuredCaptureMilliseconds { get; init; }
    public double? MeasuredPlaybackMilliseconds { get; init; }
}
public sealed record AudioBufferMeasurement(int RequestedMilliseconds, double? CaptureMilliseconds, double? PlaybackMilliseconds,
    bool CaptureStable, bool PlaybackStable, int Dropouts, int Impulses);

internal sealed class WindowsAudioSelfTest(TimeProvider clock) : IAudioSelfTest
{
    private static readonly TimeSpan CaptureDuration = TimeSpan.FromSeconds(4);

    public async Task<AudioBufferRecommendation> CheckBuffersAsync(AudioDeviceSettings settings, IProgress<AudioBufferMeasurement> progress,
        CancellationToken token)
    {
        // Resolve system defaults once so the measurement compares buffers on the same endpoints.
        settings = await Task.Run(() =>
        {
            using var enumerator = new MMDeviceEnumerator();
            using var capture = Resolve(enumerator, settings.CaptureDeviceId, DataFlow.Capture);
            using var output = Resolve(enumerator, settings.PlaybackDeviceId, DataFlow.Render);
            return settings with { CaptureDeviceId = capture.DeviceID, PlaybackDeviceId = output.DeviceID };
        }, token).ConfigureAwait(false);

        int? captureChoice = null, playbackChoice = null;
        double? measuredCapture = null, measuredPlayback = null;
        foreach (var milliseconds in new[] { 0, 20, 40, 70, 110 })
        {
            var probe = settings with
            {
                CaptureBufferMilliseconds = milliseconds,
                PlaybackBufferMilliseconds = milliseconds,
                MicrophoneMuted = true
            };
            var first = await MeasureBuffersAsync(probe, captureChoice is null, playbackChoice is null, token).ConfigureAwait(false);
            progress.Report(first);
            if (!first.CaptureStable && !first.PlaybackStable) continue;

            // A recommendation must pass twice. Input and output can qualify at different sizes.
            var repeat = await MeasureBuffersAsync(probe, first.CaptureStable, first.PlaybackStable, token).ConfigureAwait(false);
            progress.Report(repeat);
            if (repeat.CaptureStable)
            {
                captureChoice = milliseconds;
                measuredCapture = repeat.CaptureMilliseconds;
            }
            if (repeat.PlaybackStable)
            {
                playbackChoice = milliseconds;
                measuredPlayback = repeat.PlaybackMilliseconds;
            }
            if (captureChoice is not null && playbackChoice is not null) break;
        }
        return new(captureChoice, playbackChoice)
        {
            MeasuredCaptureMilliseconds = measuredCapture,
            MeasuredPlaybackMilliseconds = measuredPlayback
        };
    }

    private async Task<AudioBufferMeasurement> MeasureBuffersAsync(AudioDeviceSettings settings, bool checkCapture, bool checkPlayback,
        CancellationToken token)
    {
        double? captureMs = null, playbackMs = null;
        var captureStable = false;
        var playbackStable = false;
        var dropouts = 0;
        var impulses = 0;
        if (checkCapture)
        {
            try
            {
                var sample = await RecordAsync(settings, new Progress<ImmutableArray<int>>(), token).ConfigureAwait(false);
                captureMs = sample.Device.BufferMs;
                // Starting and stopping mid-period may discard a final partial device buffer.
                var minimumSeconds = CaptureDuration.TotalSeconds - Math.Max(0.04, captureMs.Value / 1000);
                captureStable = sample.Samples.Length / (double)sample.SampleRate >= minimumSeconds;
            }
            catch (Exception) when (!token.IsCancellationRequested) { }
        }
        if (checkPlayback)
        {
            try
            {
                const int rate = 16000;
                const int frequency = 1000;
                var samples = new short[rate * 2];
                for (var i = 0; i < samples.Length; i++)
                {
                    var fade = Math.Min(1, Math.Min(i, samples.Length - i - 1) / (rate * 0.04));
                    samples[i] = (short)(4000 * fade * Math.Sin(i * (2 * Math.PI * frequency / rate)));
                }
                var reference = new AudioSelfTestSample(samples, rate, 4000, new("", rate, 1, 16, 0, 0, false));
                var trace = new AudioBufferProbeTrace();
                var loopback = await new CscorePlaybackCaptureFactory(clock).StartAsync(
                    settings.PlaybackDeviceId, clock.GetUtcNow(), trace, token).ConfigureAwait(false);
                AudioSelfTestDevice output;
                try
                {
                    output = await PlayAsync(settings, reference, new(12), new Progress<AudioSelfTestDevice>(), token).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromMilliseconds(150), clock, token).ConfigureAwait(false);
                }
                finally { await loopback.DisposeAsync().ConfigureAwait(false); }
                playbackMs = output.BufferMs;
                var result = trace.Measure(frequency);
                dropouts = result.Low10MsBlocks + (result.MissingFrames > 0 ? 1 : 0);
                impulses = result.ImpulseGroups;
                playbackStable = result.IsClean && loopback.Completion.IsCompletedSuccessfully
                    && !output.Muted && output.WindowsVolume > 0;
                _ = loopback.Completion.Exception;
            }
            catch (Exception) when (!token.IsCancellationRequested) { }
        }
        return new(settings.PlaybackBufferMilliseconds, captureMs, playbackMs, captureStable, playbackStable, dropouts, impulses);
    }

    public Task MonitorAsync(AudioDeviceSettings settings, AudioPlaybackVolume volume, CancellationToken token) => Task.Run(async () =>
    {
        using var enumerator = new MMDeviceEnumerator();
        using var microphone = Resolve(enumerator, settings.CaptureDeviceId, DataFlow.Capture);
        using var speakers = Resolve(enumerator, settings.PlaybackDeviceId, DataFlow.Render);
        var inputBuffer = settings.CaptureBufferMilliseconds;
        var outputBuffer = settings.PlaybackBufferMilliseconds;
        using var capture = new WasapiCapture(inputBuffer == 0, AudioClientShareMode.Shared, inputBuffer == 0 ? 100 : inputBuffer)
            { Device = microphone };
        capture.Initialize();
        var format = capture.WaveFormat;
        // Use a short, bounded queue and insert silence when capture has not produced a block yet.
        var bufferFrames = format.SampleRate * Math.Max(60, Math.Max(inputBuffer, outputBuffer)) / 1000;
        using var input = new SoundInSource(capture, bufferFrames * format.BlockAlign) { FillWithZeros = true };
        using var source = new MonitorVolumeSource(input.ToSampleSource(), volume).ToWaveSource(16);
        using var output = new WasapiOut(outputBuffer == 0, AudioClientShareMode.Shared, outputBuffer == 0 ? 100 : outputBuffer)
            { Device = speakers };
        var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        capture.Stopped += (_, e) => stopped.TrySetResult(e.Exception);
        output.Stopped += (_, e) => stopped.TrySetResult(e.Exception);
        output.Initialize(source);
        token.ThrowIfCancellationRequested();
        capture.Start();
        try
        {
            token.ThrowIfCancellationRequested();
            output.Play();
            var failure = await stopped.Task.WaitAsync(token).ConfigureAwait(false);
            throw new InvalidOperationException("The microphone monitor stopped.", failure);
        }
        finally
        {
            try { output.Stop(); }
            finally { capture.Stop(); }
        }
    }, token);

    private sealed class MonitorVolumeSource(ISampleSource source, AudioPlaybackVolume level) : VolumeSource(source)
    {
        public override float Volume => level.Percent / 100f;
    }

    public Task<AudioSelfTestDevice> PlayAsync(AudioDeviceSettings settings, AudioSelfTestSample? sample,
        AudioPlaybackVolume volume, IProgress<AudioSelfTestDevice> opened, CancellationToken token) => Task.Run(() =>
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = Resolve(enumerator, settings.PlaybackDeviceId, DataFlow.Render);
        var buffer = settings.PlaybackBufferMilliseconds;
        using var source = OpenPcm(sample?.Samples ?? CreateTone(), sample?.SampleRate ?? 16000);
        using var output = new WasapiOut(buffer == 0, AudioClientShareMode.Shared, buffer == 0 ? 100 : buffer) { Device = device };
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.Stopped += (_, e) =>
        {
            if (e.Exception is { } error) stopped.TrySetException(error);
            else stopped.TrySetResult();
        };
        output.Initialize(source);
        var info = Describe(device, output.OutputFormat, output.BufferDuration!.Value);
        opened.Report(info);
        var appliedVolume = volume.Percent;
        output.Volume = appliedVolume / 100f;
        token.ThrowIfCancellationRequested();
        output.Play();
        try
        {
            while (!stopped.Task.IsCompleted)
            {
                token.ThrowIfCancellationRequested();
                var current = volume.Percent;
                if (current != appliedVolume)
                {
                    appliedVolume = current;
                    output.Volume = current / 100f;
                }
                Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromMilliseconds(40), clock, token)).GetAwaiter().GetResult();
            }
            stopped.Task.GetAwaiter().GetResult();
            return info;
        }
        finally { output.Stop(); }
    }, token);

    public Task<AudioSelfTestSample> RecordAsync(AudioDeviceSettings settings, IProgress<ImmutableArray<int>> spectrum, CancellationToken token) =>
        Task.Run(async () =>
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = Resolve(enumerator, settings.CaptureDeviceId, DataFlow.Capture);
            var buffer = settings.CaptureBufferMilliseconds;
            using var capture = new WasapiCapture(buffer == 0, AudioClientShareMode.Shared, buffer == 0 ? 100 : buffer)
                { Device = device };
            capture.Initialize();
            var format = capture.WaveFormat;
            var info = Describe(device, format, capture.BufferDuration!.Value);
            _ = PlaybackPacketPcm.GetEncoding(format);
            var analyzer = new CscoreSpectrumAnalyzer(clock);
            var pcm = new short[format.SampleRate * (int)CaptureDuration.TotalSeconds];
            var written = 0;
            var peak = 0;
            var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            capture.Stopped += (_, e) => stopped.TrySetResult(e.Exception);
            capture.DataAvailable += (_, e) =>
            {
                if (token.IsCancellationRequested || stopped.Task.IsCompleted) return;
                try
                {
                    var frames = Math.Min(pcm.Length - written, e.ByteCount / format.BlockAlign);
                    if (frames == 0) return;
                    // The decoder accepts at most half a second per block.
                    for (var offset = 0; offset < frames;)
                    {
                        var count = Math.Min(frames - offset, format.SampleRate / 2);
                        var block = pcm.AsSpan(written + offset, count);
                        PlaybackPacketPcm.Decode(e.Data.AsSpan(e.Offset + offset * format.BlockAlign, count * format.BlockAlign), format, block, false);
                        for (var i = 0; i < block.Length; i++)
                        {
                            block[i] = (short)(block[i] * settings.MicrophoneVolumePercent / 100);
                            if (settings.MicrophoneMuted) block[i] = 0;
                            peak = Math.Max(peak, Math.Abs((int)block[i]));
                        }
                        analyzer.Add(format.SampleRate, 1, block);
                        offset += count;
                    }
                    written += frames;
                    spectrum.Report(analyzer.Read());
                }
                catch (Exception error) { stopped.TrySetResult(error); }
            };
            using var duration = CancellationTokenSource.CreateLinkedTokenSource(token);
            token.ThrowIfCancellationRequested();
            capture.Start();
            try
            {
                var limit = Task.Delay(CaptureDuration, clock, duration.Token);
                if (await Task.WhenAny(limit, stopped.Task).ConfigureAwait(false) == stopped.Task)
                    throw new InvalidOperationException("Capture stopped before the sample ended.", await stopped.Task.ConfigureAwait(false));
                await limit.ConfigureAwait(false);
            }
            finally
            {
                duration.Cancel();
                capture.Stop();
            }
            if (await stopped.Task.ConfigureAwait(false) is { } failure)
                throw new InvalidOperationException("Capture failed.", failure);
            if (written == 0) throw new InvalidOperationException("The capture device returned no frames.");
            Array.Resize(ref pcm, written);
            return new AudioSelfTestSample(pcm, format.SampleRate, peak, info);
        }, token);

    public Task<IReadOnlyList<AudioSelfTestCodec>> CheckCodecsAsync(AudioSelfTestSample sample, CancellationToken token) =>
        Task.Run<IReadOnlyList<AudioSelfTestCodec>>(() =>
        [
            CheckCodec(sample, AudioCodecsEnum.G722, "G.722", token),
            CheckCodec(sample, AudioCodecsEnum.PCMA, "G.711 A-law", token),
            CheckCodec(sample, AudioCodecsEnum.PCMU, "G.711 μ-law", token)
        ], token);

    private static AudioSelfTestCodec CheckCodec(AudioSelfTestSample sample, AudioCodecsEnum codec, string name, CancellationToken token)
    {
        // Encode the same 20 ms frame sizes used for the SIP media path.
        using var encoder = new AudioEncoder();
        using var decoder = new AudioEncoder();
        var format = encoder.SupportedFormats.First(candidate => candidate.Codec == codec);
        using var source = OpenPcm(sample.Samples, sample.SampleRate).ChangeSampleRate(format.ClockRate);
        var bytes = new byte[format.ClockRate / 50 * sizeof(short)];
        var frames = new short[bytes.Length / sizeof(short)];
        var packets = 0;
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var filled = 0;
                int read;
                while (filled < bytes.Length && (read = source.Read(bytes, filled, bytes.Length - filled)) > 0) filled += read;
                if (filled == 0) break;
                bytes.AsSpan(filled).Clear();
                MemoryMarshal.Cast<byte, short>(bytes).CopyTo(frames);
                var payload = encoder.EncodeAudio(frames, format);
                if (payload is not { Length: 160 } || decoder.DecodeAudio(payload, format).Length != frames.Length)
                    return new(name, packets, false);
                packets++;
            }
        }
        catch (Exception) when (!token.IsCancellationRequested) { return new(name, packets, false); }
        return new(name, packets, packets > 0);
    }

    private static MMDevice Resolve(MMDeviceEnumerator enumerator, string? id, DataFlow direction) =>
        id is null ? enumerator.GetDefaultAudioEndpoint(direction, Role.Multimedia) : enumerator.GetDevice(id);

    private static AudioSelfTestDevice Describe(MMDevice device, WaveFormat format, TimeSpan buffer)
    {
        using var volume = AudioEndpointVolume.FromDevice(device);
        return new(device.FriendlyName, format.SampleRate, format.Channels, format.BitsPerSample, buffer.TotalMilliseconds,
            (int)Math.Round(volume.MasterVolumeLevelScalar * 100), volume.IsMuted);
    }

    private static RawDataReader OpenPcm(short[] samples, int sampleRate) =>
        new(new MemoryStream(MemoryMarshal.AsBytes(samples.AsSpan()).ToArray(), writable: false), new WaveFormat(sampleRate, 16, 1));

    private static short[] CreateTone()
    {
        const int rate = 16000;
        var samples = new short[rate * 3];
        for (var i = 0; i < samples.Length; i++)
        {
            var phase = i % rate;
            if (phase >= rate / 2) continue;
            var fade = Math.Min(1, Math.Min(phase, rate / 2 - phase) / (rate / 50.0));
            samples[i] = (short)(5000 * fade * Math.Sin(2 * Math.PI * 520 * i / rate));
        }
        return samples;
    }
}
