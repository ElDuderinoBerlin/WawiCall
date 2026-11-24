using System.Buffers.Binary;
using CSCore;
using CSCore.Codecs.WAV;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class PlaybackLoopbackTests
{
    [TestMethod]
    public async Task SelectedNativeLoopbackReceivesKnownOutputAndCanReopen()
    {
        var deviceName = Environment.GetEnvironmentVariable("WCP_LOOPBACK_OUTPUT_DEVICE");
        if (string.IsNullOrWhiteSpace(deviceName))
            Assert.Inconclusive("Set WCP_LOOPBACK_OUTPUT_DEVICE to opt in to the audible WASAPI loopback test.");

        var clock = TimeProvider.System;
        var catalog = await new WindowsAudioDeviceCatalog(new StartupExecutionMode(false)).ReadAsync();
        var speaker = catalog.Playback.Single(device => string.Equals(device.DisplayName, deviceName, StringComparison.OrdinalIgnoreCase));
        using var fixture = new Fixture();
        var file = Path.Combine(fixture.Root, "tone.wav");
        const int sampleRate = 48000;
        using (var writer = new WaveWriter(file, new WaveFormat(sampleRate, 16, 1)))
        {
            var bytes = new byte[2 * sampleRate * sizeof(short)];
            for (var index = 0; index < bytes.Length / sizeof(short); index++)
            {
                var envelope = Math.Min(1, Math.Min(index, 2 * sampleRate - index) / 480.0);
                var value = (short)(6000 * envelope * Math.Sin(ToneProbe.AngularFrequency * index / sampleRate));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * sizeof(short)), value);
            }
            writer.Write(bytes, 0, bytes.Length);
        }

        var factory = new CscorePlaybackCaptureFactory(clock);
        var audio = fixture.Services.GetRequiredService<IAnnouncementAudio>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        for (var trial = 1; trial <= 2; trial++)
        {
            var probe = new ToneProbe();
            var capture = await factory.StartAsync(speaker.Id, clock.GetUtcNow(), probe, timeout.Token);
            try
            {
                await audio.PreviewAsync(file, new() { PlaybackDeviceId = speaker.Id, PlaybackVolumePercent = 15 }, timeout.Token);
            }
            finally { await capture.DisposeAsync(); }
            await capture.Completion.WaitAsync(timeout.Token);
            Console.WriteLine($"{speaker.DisplayName}, Lauf {trial}: {probe.MatchingDuration.TotalSeconds:F2} s Testton aus WASAPI-Loopback.");
            Assert.IsGreaterThan(TimeSpan.FromSeconds(1), probe.MatchingDuration);
        }
    }

    [TestMethod]
    public void DeviceClockPreservesSilenceAndDoesNotFollowPacketArrivalJitter()
    {
        var timeline = new PlaybackPacketTimeline(48000, 100_000_000, TimeSpan.Zero);
        Assert.AreEqual(TimeSpan.Zero, timeline.Map(1000, 100_000_000, 480, true));
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), timeline.Map(1960, 100_210_000, 480, false));
        Assert.AreEqual(TimeSpan.FromMilliseconds(30), timeline.Map(2440, 100_290_000, 480, false));
    }

    [TestMethod]
    [DataRow("overlap")]
    [DataRow("discontinuity")]
    [DataRow("clock")]
    [DataRow("negative")]
    public void DeviceClockRejectsAmbiguousOrLostPositions(string reason)
    {
        var timeline = new PlaybackPacketTimeline(48000, 100_000_000, TimeSpan.Zero);
        timeline.Map(1000, 100_000_000, 480, false);
        Assert.Throws<PcmRecordingException>(() => timeline.Map(reason == "overlap" ? 1200 : reason == "negative" ? -1 : 1480,
            reason == "clock" ? 110_000_000 : 100_100_000, 480, reason == "discontinuity"));
    }

    [TestMethod]
    public void DeviceClockRejectsABackwardsQpcTimestampInsideTheDriftTolerance()
    {
        var timeline = new PlaybackPacketTimeline(48000, 100_000_000, TimeSpan.Zero);
        timeline.Map(0, 100_000_000, 480, false);
        timeline.Map(480, 100_100_000, 480, false);
        var error = Assert.Throws<PcmRecordingException>(() => timeline.Map(960, 100_090_000, 480, false));
        Assert.AreEqual(PcmRecordingFailure.TimelineConflict, error.Failure);
    }

    [TestMethod]
    [DataRow(16)]
    [DataRow(24)]
    [DataRow(32)]
    public void NativePcmPacketsDownmixAllChannelsWithoutOverflow(int bits)
    {
        var format = new WaveFormat(48000, bits, 2, AudioEncoding.Pcm);
        byte[] input = bits == 16 ? [0xff, 0x7f, 0xff, 0x7f, 0, 0x80, 0, 0x80]
            : bits == 24 ? [0xff, 0xff, 0x7f, 0xff, 0xff, 0x7f, 0, 0, 0x80, 0, 0, 0x80]
            : [0xff, 0xff, 0xff, 0x7f, 0xff, 0xff, 0xff, 0x7f, 0, 0, 0, 0x80, 0, 0, 0, 0x80];
        short[] output = new short[2];
        PlaybackPacketPcm.Decode(input, format, output, false);
        CollectionAssert.AreEqual(new short[] { short.MaxValue, short.MinValue }, output);
        PlaybackPacketPcm.Decode(input, format, output, true);
        CollectionAssert.AreEqual(new short[2], output);
    }

    [TestMethod]
    public void FloatPacketsAreClippedDownmixedAndRejectNanOrIncompleteFrames()
    {
        var format = new WaveFormat(48000, 32, 2, AudioEncoding.IeeeFloat);
        var input = BitConverter.GetBytes(0.5f).Concat(BitConverter.GetBytes(-0.25f)).ToArray();
        short[] output = new short[1];
        PlaybackPacketPcm.Decode(input, format, output, false);
        Assert.AreEqual((short)4096, output[0]);
        PlaybackPacketPcm.Decode(BitConverter.GetBytes(2f).Concat(BitConverter.GetBytes(2f)).ToArray(), format, output, false);
        Assert.AreEqual(short.MaxValue, output[0]);
        Assert.Throws<PcmRecordingException>(() => PlaybackPacketPcm.Decode(BitConverter.GetBytes(float.NaN).Concat(BitConverter.GetBytes(0f)).ToArray(), format, output, false));
        Assert.Throws<PcmRecordingException>(() => PlaybackPacketPcm.Decode(new byte[3], format, output, false));
    }

    [TestMethod]
    public async Task PlaybackAndSentPcmProduceOneStereoWaveWithExactSilencePositions()
    {
        using var fixture = new Fixture();
        var sent = new SentSource();
        var request = fixture.Request();
        var factory = fixture.Services.GetRequiredService<IDuplexPcmRecordingFactory>();
        var recording = await factory.StartAsync(new PlaybackRecordingSource(sent, "synthetic-render"), request);
        try
        {
            sent.Send(1000, Enumerable.Repeat((short)100, 80).ToArray());
            fixture.Capture.Push(TimeSpan.Zero, Enumerable.Repeat((short)200, 80).ToArray());
            fixture.Capture.Push(TimeSpan.FromMilliseconds(20), Enumerable.Repeat((short)300, 80).ToArray());
            var wave = await recording.StopAsync(TimeSpan.FromMilliseconds(30));
            Assert.AreEqual(PcmRecordingState.Completed, recording.State);
            Assert.AreEqual("synthetic-render", fixture.Capture.DeviceId);
            Assert.AreEqual(1, fixture.Capture.Starts);
            Assert.IsTrue(fixture.Capture.Stopped);
            Assert.AreEqual(0, sent.Subscribers);
            using var reader = new WaveFileReader(request.Recording.WavePath);
            byte[] data = new byte[960];
            Assert.AreEqual(960, reader.Read(data, 0, data.Length));
            Assert.AreEqual((short)100, BinaryPrimitives.ReadInt16LittleEndian(data));
            Assert.AreEqual((short)200, BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(2)));
            Assert.AreEqual((short)0, BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(80 * 4 + 2)));
            Assert.AreEqual((short)300, BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(160 * 4 + 2)));
            Assert.IsNotNull(wave);
        }
        finally { await recording.DisposeAsync(); }
    }

    [TestMethod]
    public async Task DefaultRtpRecordingNeverStartsDeviceCapture()
    {
        using var fixture = new Fixture();
        var factory = fixture.Services.GetRequiredService<IDuplexPcmRecordingFactory>();
        var recording = await factory.StartAsync(new SentSource(), fixture.Request());
        try { await recording.StopAsync(TimeSpan.FromMilliseconds(20)); Assert.AreEqual(0, fixture.Capture.Starts); }
        finally { await recording.DisposeAsync(); }
    }

    [TestMethod]
    public async Task CallSnapshotsThePlaybackDeviceAndDoesNotOpenCaptureWhenConfigured()
    {
        var source = new SentSource();
        var native = new Media(source);
        var options = new AudioDeviceSettings { PlaybackDeviceId = "synthetic-render-a", RecordingReceiveSource = RecordingReceiveSource.PlaybackLoopback };
        var media = new RecordingInputMediaSession(native, options);
        var selected = (PlaybackRecordingSource)media.PcmSource;
        options = options with { PlaybackDeviceId = "synthetic-render-b", RecordingReceiveSource = RecordingReceiveSource.Rtp };
        Assert.AreEqual("synthetic-render-a", selected.DeviceId);
        Assert.AreSame(source, selected.Sent);
        Assert.AreEqual(0, source.Subscribers);
        await media.DisposeAsync();
        Assert.AreEqual(1, native.Disposals);
    }

    [TestMethod]
    public async Task CancelledStartupDoesNotSubscribeOrStartACapture()
    {
        using var fixture = new Fixture();
        var sent = new SentSource(); var request = fixture.Request();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Services.GetRequiredService<IDuplexPcmRecordingFactory>()
            .StartAsync(new PlaybackRecordingSource(sent, null), request, cancellation.Token));
        Assert.AreEqual(0, sent.Subscribers);
        Assert.AreEqual(0, fixture.Capture.Starts);
        Assert.IsFalse(File.Exists(request.Recording.WavePath));
    }

    [TestMethod]
    public async Task DeviceStartupFailureRollsBackSentSubscriptionAndPublishesNoWave()
    {
        using var fixture = new Fixture();
        fixture.Capture.FailStartup = true;
        var sent = new SentSource(); var request = fixture.Request();
        await Assert.ThrowsAsync<PcmRecordingException>(() => fixture.Services.GetRequiredService<IDuplexPcmRecordingFactory>()
            .StartAsync(new PlaybackRecordingSource(sent, "synthetic-render"), request));
        Assert.AreEqual(0, sent.Subscribers);
        Assert.IsFalse(File.Exists(request.Recording.WavePath));
    }

    [TestMethod]
    public async Task AbortStopsDeviceCaptureWithoutPublishingAMix()
    {
        using var fixture = new Fixture();
        var sent = new SentSource(); var request = fixture.Request();
        var recording = await fixture.Services.GetRequiredService<IDuplexPcmRecordingFactory>().StartAsync(new PlaybackRecordingSource(sent, null), request);
        await recording.DisposeAsync(); await recording.DisposeAsync();
        Assert.IsTrue(fixture.Capture.Stopped);
        Assert.AreEqual(0, sent.Subscribers);
        Assert.IsFalse(File.Exists(request.Recording.WavePath));
    }

    [TestMethod]
    public async Task CaptureFailureCannotPublishAnApparentlySuccessfulWave()
    {
        using var fixture = new Fixture();
        var sent = new SentSource(); var request = fixture.Request();
        var recording = await fixture.Services.GetRequiredService<IDuplexPcmRecordingFactory>().StartAsync(new PlaybackRecordingSource(sent, null), request);
        fixture.Capture.Fail();
        Assert.AreEqual(PcmRecordingState.Failed, recording.State);
        await Assert.ThrowsAsync<PcmRecordingException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(20)));
        try { await recording.DisposeAsync(); } catch (PcmRecordingException) { }
        Assert.IsTrue(fixture.Capture.Stopped);
        Assert.AreEqual(0, sent.Subscribers);
        Assert.IsFalse(File.Exists(request.Recording.WavePath));
    }

    [TestMethod]
    public async Task RenderDeviceChangeRejectsAnActiveLoopbackSegmentAndNextSegmentUsesReplacement()
    {
        using var fixture = new Fixture();
        using var replaced = new CancellationTokenSource();
        var binding = new PlaybackDeviceBinding("old-speaker", replaced.Token);
        var source = PlaybackRecordingSource.ForActivePlayback(new SentSource(), () => binding);
        var factory = fixture.Services.GetRequiredService<IDuplexPcmRecordingFactory>();
        var request = fixture.Request();
        var first = await factory.StartAsync(source, request);
        Assert.AreEqual("old-speaker", fixture.Capture.DeviceId);
        binding = new("new-speaker", CancellationToken.None);
        await replaced.CancelAsync();
        Assert.AreEqual(PcmRecordingState.Failed, first.State);
        await Assert.ThrowsAsync<PcmRecordingException>(() => first.StopAsync(TimeSpan.FromMilliseconds(20)));
        try { await first.DisposeAsync(); } catch (PcmRecordingException) { }
        Assert.IsFalse(File.Exists(request.Recording.WavePath));

        using var nextFixture = new Fixture();
        await using var next = await nextFixture.Services.GetRequiredService<IDuplexPcmRecordingFactory>()
            .StartAsync(source.WithSent(new SentSource()), nextFixture.Request());
        Assert.AreEqual("new-speaker", nextFixture.Capture.DeviceId);
    }

    [TestMethod]
    public async Task VersionElevenDefaultsToRtpAndVersionTwelveRoundTripsLoopback()
    {
        using var fixture = new Fixture();
        var location = new ConfigurationLocation(fixture.Root);
        var store = new ConfigurationStore(location, new ApplicationOptionsValidator(new WindowsUserSecretProtector()));
        await File.WriteAllTextAsync(location.FilePath, """{"formatVersion":11,"settings":{"devices":{"playbackDeviceId":"synthetic-render"}}}""");
        var before = await store.LoadAsync();
        Assert.AreEqual(RecordingReceiveSource.Rtp, before.Options.Devices.RecordingReceiveSource);
        await store.SaveAsync(before.Options with { Devices = before.Options.Devices with { RecordingReceiveSource = RecordingReceiveSource.PlaybackLoopback } }, before);
        var after = await store.LoadAsync();
        Assert.AreEqual(ConfigurationFormat.CurrentVersion, after.FormatVersion);
        Assert.AreEqual(RecordingReceiveSource.PlaybackLoopback, after.Options.Devices.RecordingReceiveSource);
        Assert.AreEqual("synthetic-render", after.Options.Devices.PlaybackDeviceId);
        Assert.Throws<ConfigurationException>(() => new ApplicationOptionsValidator(new WindowsUserSecretProtector()).Validate(
            after.Options with { Devices = after.Options.Devices with { RecordingReceiveSource = (RecordingReceiveSource)99 } }));
    }

    [TestMethod]
    public async Task PortableSettingsDoNotTransferLoopbackConsentAndPreserveTheLocalChoice()
    {
        using var fixture = new Fixture();
        var validator = new ApplicationOptionsValidator(new WindowsUserSecretProtector());
        var source = new ConfigurationStore(new ConfigurationLocation(fixture.Root), validator);
        await source.SaveAsync(new() { Devices = new() { RecordingReceiveSource = RecordingReceiveSource.PlaybackLoopback } });
        var path = Path.Combine(fixture.Root, "portable.json");
        await source.ExportPortableAsync(path);
        var json = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain("recordingReceiveSource", json);
        Assert.DoesNotContain("PlaybackLoopback", json);
        var target = new ConfigurationStore(new ConfigurationLocation(Path.Combine(fixture.Root, "target")), validator);
        var imported = await target.ImportPortableAsync(path, await target.LoadAsync());
        Assert.AreEqual(RecordingReceiveSource.Rtp, imported.Options.Devices.RecordingReceiveSource);
        await target.SaveAsync(imported.Options with { Devices = imported.Options.Devices with { RecordingReceiveSource = RecordingReceiveSource.PlaybackLoopback } }, imported);
        imported = await target.ImportPortableAsync(path, await target.LoadAsync());
        Assert.AreEqual(RecordingReceiveSource.PlaybackLoopback, imported.Options.Devices.RecordingReceiveSource);
    }

    private sealed class ToneProbe : IPlaybackPcmObserver
    {
        internal const double AngularFrequency = 2 * Math.PI * 1000;
        internal TimeSpan MatchingDuration { get; private set; }

        public bool TryObserve(TimeSpan position, int sampleRate, ReadOnlySpan<short> samples)
        {
            double inPhase = 0, quadrature = 0, power = 0;
            for (var index = 0; index < samples.Length; index++)
            {
                var phase = AngularFrequency * index / sampleRate;
                int sample = samples[index];
                inPhase += sample * Math.Cos(phase);
                quadrature += sample * Math.Sin(phase);
                power += sample * sample;
            }

            if (power / samples.Length > 25 && 2 * (inPhase * inPhase + quadrature * quadrature) / (samples.Length * power) > 0.8)
                MatchingDuration += TimeSpan.FromSeconds((double)samples.Length / sampleRate);
            return true;
        }

        public void OnFailure() { }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "WawiCallPro-loopback-" + Guid.NewGuid().ToString("N"));
        internal readonly CaptureFactory Capture = new();
        internal readonly ServiceProvider Services;
        internal Fixture()
        {
            Directory.CreateDirectory(Root);
            var services = new ServiceCollection(); services.AddLogging(); services.AddAudioConversion();
            services.AddSingleton(new ConfigurationStore(new ConfigurationLocation(Root), new ApplicationOptionsValidator(new WindowsUserSecretProtector())));
            services.AddSingleton<IPlaybackPcmCaptureFactory>(Capture);
            Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        }
        internal DuplexPcmRecordingRequest Request() => new(new(Guid.NewGuid(), Guid.NewGuid(), Path.Combine(Root, "recording.wav"), 8000),
            new(7, 1000, 8000, TimeSpan.Zero), new(9, 0, 1000, 8000, TimeSpan.Zero)) { RecordingEpochUtc = DateTimeOffset.UtcNow.AddSeconds(-1) };
        public void Dispose() { Services.Dispose(); Directory.Delete(Root, true); }
    }

    private sealed class CaptureFactory : IPlaybackPcmCaptureFactory, IPlaybackPcmCapture
    {
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IPlaybackPcmObserver? _observer;
        internal int Starts;
        internal bool FailStartup, Stopped;
        internal string? DeviceId;
        public Task Completion => _completed.Task;
        public Task<IPlaybackPcmCapture> StartAsync(string? deviceId, DateTimeOffset epoch, IPlaybackPcmObserver observer, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested(); Starts++; DeviceId = deviceId;
            if (FailStartup) throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
            _observer = observer; return Task.FromResult<IPlaybackPcmCapture>(this);
        }
        internal void Push(TimeSpan start, short[] samples) => Assert.IsTrue(_observer!.TryObserve(start, 8000, samples));
        internal void Fail() { _observer!.OnFailure(); _completed.TrySetException(new IOException("synthetic-device-error")); }
        public ValueTask DisposeAsync() { Stopped = true; _observer = null; _completed.TrySetResult(); return ValueTask.CompletedTask; }
    }

    private sealed class SentSource : IConversationPcmFailureSource, IConversationPcmSubscription
    {
        private IConversationPcmObserver? _observer;
        private readonly TaskCompletionSource<PcmObservationFailure?> _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Subscribers => _observer is null ? 0 : 1;
        public long FailureCount => 0;
        public PcmObservationFailure? LastFailure => null;
        public Task<PcmObservationFailure?> Completion => _completed.Task;
        public IConversationPcmSubscription Subscribe(IConversationPcmObserver observer) { _observer = observer; return this; }
        internal void Send(uint timestamp, short[] samples) => Assert.IsTrue(((ISentRtpPcmObserver)_observer!).TryObserveSent(new(7, timestamp, 8000, (uint)samples.Length), 8000, 1, samples));
        public ValueTask DisposeAsync() { _observer = null; _completed.TrySetResult(null); return ValueTask.CompletedTask; }
    }

    private sealed class Media(IConversationPcmSource source) : WawiCallPro.Telephony.Sip.ISipPcmMediaSession
    {
        internal int Disposals;
        public SIPSorcery.SIP.App.IMediaSession Session => throw new NotSupportedException();
        public IConversationPcmSource PcmSource => source;
        public void ApplyPlaybackSettings() { }
        public ValueTask DisposeAsync() { Disposals++; return ValueTask.CompletedTask; }
    }
}
