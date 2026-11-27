using System.Collections.Immutable;
using CSCore.CoreAudioAPI;
using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public async Task AudioBufferProbeMeasuresLoopbackDropoutsAndImpulses()
    {
        if (Environment.GetEnvironmentVariable("WCP_VERIFY_AUDIO_BUFFER_PROBE") != "1")
            Assert.Inconclusive("Requires permission to play a short, quiet reference tone.");

        var audio = new WindowsAudioSelfTest(TimeProvider.System);
        var captureId = FindNativeEndpoint(DataFlow.Capture);
        var playbackId = FindNativeEndpoint(DataFlow.Render);
        var loopback = new CscorePlaybackCaptureFactory(TimeProvider.System);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(65));
        const int rate = 16000;
        const int frequency = 1000;
        var tone = Enumerable.Range(0, rate * 2).Select(i =>
        {
            var edge = Math.Min(i, rate * 2 - 1 - i);
            var fade = Math.Min(1.0, edge / (rate * 0.04));
            return (short)(4000 * fade * Math.Sin(i * (2 * Math.PI * frequency / rate)));
        }).ToArray();
        var reference = new AudioSelfTestSample(tone, rate, 4000, AudioSelfTestProbe.Device);
        var validMeasurements = 0;
        foreach (var (captureBuffer, playbackBuffer) in new[] { (0, 0), (20, 20), (40, 40), (70, 70), (70, 110) })
        {
            var settings = new AudioDeviceSettings
            {
                CaptureDeviceId = captureId,
                PlaybackDeviceId = playbackId,
                CaptureBufferMilliseconds = captureBuffer,
                PlaybackBufferMilliseconds = playbackBuffer
            };
            var input = await audio.RecordAsync(settings, new Progress<ImmutableArray<int>>(), timeout.Token);
            var trace = new AudioBufferProbeTrace();
            var playback = await loopback.StartAsync(playbackId, DateTimeOffset.UtcNow, trace, timeout.Token);
            AudioSelfTestDevice output;
            try
            {
                output = await audio.PlayAsync(settings, reference, new(12), new Progress<AudioSelfTestDevice>(), timeout.Token);
                await Task.Delay(150, timeout.Token);
            }
            finally { await playback.DisposeAsync(); }

            var capturedSeconds = input.Samples.Length / (double)input.SampleRate;
            var result = trace.Measure(frequency);
            if (result.SignalFound) validMeasurements++;
            Console.WriteLine($"Requested {captureBuffer}/{playbackBuffer} ms; actual {input.Device.BufferMs:0.#}/{output.BufferMs:0.#} ms; "
                + $"input {capturedSeconds:0.000} s of 4 s; render {result}; native discontinuity: {playback.Completion.IsFaulted}; "
                + $"Windows {output.WindowsVolume}%{(output.Muted ? " muted" : "")}.");
        }
        Assert.IsGreaterThan(0, validMeasurements, "The reference tone was not present in the Windows loopback.");
    }
    [TestMethod]
    public async Task AudioBufferProbeAutomaticallyRecommendsAConfirmedConfiguration()
    {
        if (Environment.GetEnvironmentVariable("WCP_VERIFY_AUDIO_BUFFER_PROBE") != "1")
            Assert.Inconclusive("Requires permission to play a short, quiet reference tone.");

        var audio = new WindowsAudioSelfTest(TimeProvider.System);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(80));
        var settings = new AudioDeviceSettings
        {
            CaptureDeviceId = FindNativeEndpoint(DataFlow.Capture),
            PlaybackDeviceId = FindNativeEndpoint(DataFlow.Render),
            CaptureBufferMilliseconds = 70,
            PlaybackBufferMilliseconds = 110
        };
        var measurements = new List<AudioBufferMeasurement>();
        var result = await audio.CheckBuffersAsync(settings, new BufferProbeProgress(measurements), timeout.Token);
        Console.WriteLine($"Recommendation: {result}");
        Assert.IsNotNull(result.CaptureMilliseconds);
        Assert.IsNotNull(result.PlaybackMilliseconds);
        Assert.IsNotNull(result.MeasuredCaptureMilliseconds);
        Assert.IsNotNull(result.MeasuredPlaybackMilliseconds);
        Assert.HasCount(2, measurements.Where(value => value.RequestedMilliseconds == result.CaptureMilliseconds && value.CaptureStable));
        Assert.HasCount(2, measurements.Where(value => value.RequestedMilliseconds == result.PlaybackMilliseconds && value.PlaybackStable));
    }

    [TestMethod]
    public void AudioBufferProbeDetectsAnInjectedImpulseAndDropout()
    {
        const int sampleRate = 16000;
        const int frequency = 1000;
        var samples = Enumerable.Range(0, sampleRate * 2).Select(i =>
        {
            var fade = Math.Min(1, Math.Min(i, sampleRate * 2 - 1 - i) / (sampleRate * 0.04));
            return (short)(480 * fade * Math.Sin(i * (2 * Math.PI * frequency / sampleRate)));
        }).ToArray();
        var clean = new AudioBufferProbeTrace();
        clean.TryObserve(TimeSpan.Zero, sampleRate, samples);
        Assert.IsTrue(clean.Measure(frequency).IsClean);

        samples[sampleRate / 2 + 1] = 1200;
        Array.Clear(samples, sampleRate, sampleRate / 25);
        var impaired = new AudioBufferProbeTrace();
        impaired.TryObserve(TimeSpan.Zero, sampleRate, samples);
        var result = impaired.Measure(frequency);
        Assert.IsGreaterThan(0, result.Low10MsBlocks);
        Assert.IsGreaterThan(0, result.ImpulseGroups);
    }

    private sealed class BufferProbeProgress(List<AudioBufferMeasurement> measurements) : IProgress<AudioBufferMeasurement>
    {
        public void Report(AudioBufferMeasurement value) => measurements.Add(value);
    }
}
