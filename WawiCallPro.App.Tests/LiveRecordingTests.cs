using System.Buffers.Binary;
using CSCore.Codecs.WAV;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class LiveRecordingTests
{
    [TestMethod]
    public async Task LiveLoopbackKeepsTheSelectedEndpointAndDrainsCaptureBeforeChoosingDuration()
    {
        using var fixture = new Fixture(); var capture = new CaptureDouble();
        using var services = new ServiceCollection().AddLogging().AddSingleton<TimeProvider>(fixture.Clock)
            .AddAudioConversion().AddSingleton<IPlaybackPcmCaptureFactory>(capture).BuildServiceProvider();
        var factory = new LivePcmRecordingFactory(new ForwardFactory(services.GetRequiredService<IDuplexPcmRecordingFactory>()), fixture.Clock);
        var start = factory.StartAsync(new PlaybackRecordingSource(fixture.Source, "synthetic-playback"),
            fixture.Request() with { Layout = ConversationWaveLayout.MixedMono });
        fixture.Source.Send(1000, 100); fixture.Source.Receive(1, 2000, 200);
        await using var recording = await start;
        fixture.Clock.Advance(40); await recording.StopAsync();
        Assert.AreEqual("synthetic-playback", capture.Endpoint); Assert.AreEqual(1, capture.Stops);
        using var wave = new WaveFileReader(fixture.Path);
        Assert.AreEqual(1, wave.WaveFormat.Channels);
        byte[] bytes = new byte[320];
        Assert.AreEqual(bytes.Length, wave.Read(bytes, 0, bytes.Length));
        Assert.AreEqual((short)300, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(160)));
    }

    [TestMethod]
    public async Task ImmediateStopDrainsTheLastWholeFrameInsteadOfCuttingItOrInventingAFutureUtcEnd()
    {
        using var fixture = new Fixture();
        var paths = new ApplicationPaths(new ConfigurationLocation(fixture.Root));
        var request = fixture.Request() with { WavePath = Path.Combine(paths.RecordingsDirectory, "live.wav") };
        var journal = new RecordingJournalDouble();
        var persisted = new PersistedDuplexPcmRecordingFactory(fixture.Services.GetRequiredService<IDuplexPcmRecordingFactory>(),
            journal, paths, TimeProvider.System);
        var factory = new LivePcmRecordingFactory(persisted, TimeProvider.System);
        var start = factory.StartAsync(fixture.Source, request);
        fixture.Source.Send(1000, 100); fixture.Source.Receive(1, 2000, 200);
        await using var recording = await start;
        var result = await recording.StopAsync();
        Assert.IsTrue(result.Wave.FrameCount >= 160);
        Assert.IsTrue(File.Exists(request.WavePath)); Assert.AreEqual(0, fixture.Source.Subscribers);
        Assert.AreEqual(PcmRecordingState.Completed, journal.Saved?.Capture.Capture.State);
        Assert.IsEmpty(result.Segments);
        Assert.IsFalse(Directory.EnumerateDirectories(paths.RecordingsDirectory, ".wawi-record-*").Any());
    }

    [TestMethod]
    public async Task RealRtpCounterAndSequenceWrapRemainValidOnTheLocalTimeline()
    {
        using var fixture = new Fixture();
        var start = fixture.Factory.StartAsync(fixture.Source, fixture.Request());
        fixture.Source.Send(uint.MaxValue - 159, 100); fixture.Source.Receive(65535, uint.MaxValue - 159, 200);
        await using var recording = await start;
        fixture.Clock.Advance(20); fixture.Source.Send(0, 300); fixture.Source.Receive(0, 0, 400);
        fixture.Clock.Advance(40); await recording.StopAsync();
        Assert.AreEqual(PcmRecordingState.Completed, recording.State);
    }

    [TestMethod]
    public async Task LiveRtpRecordingPublishesStereoWaveAfterTheObservedG722Overlap()
    {
        using var fixture = new Fixture();
        using var encoder = new SIPSorcery.Media.AudioEncoder();
        var format = encoder.SupportedFormats.Single(candidate => candidate.Codec == SIPSorceryMedia.Abstractions.AudioCodecsEnum.G722);
        var first = Decode(512, 1200);
        var second = Decode(320, 730);
        var third = Decode(320, 480);
        var start = fixture.Factory.StartAsync(fixture.Source, fixture.Request() with { SampleRate = 16000 });
        fixture.Source.Send(1000, 1234);
        fixture.Source.ReceiveWideband(10, 1000, first);
        await using var recording = await start;
        fixture.Clock.Advance(20);
        fixture.Source.ReceiveWideband(11, 1160, second);
        fixture.Clock.Advance(20);
        fixture.Source.ReceiveWideband(12, 1320, third);
        fixture.Clock.Advance(20);
        await recording.StopAsync();

        using var wave = new WaveFileReader(fixture.Path);
        Assert.AreEqual(2, wave.WaveFormat.Channels);
        Assert.AreEqual(16000, wave.WaveFormat.SampleRate);
        byte[] bytes = new byte[960 * 4];
        Assert.AreEqual(bytes.Length, wave.Read(bytes, 0, bytes.Length));
        Assert.AreNotEqual((short)0, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(64 * 4)));
        Assert.AreEqual(first[511], ReadReceived(511));
        Assert.AreEqual(second[192], ReadReceived(512));
        Assert.AreEqual(second[319], ReadReceived(639));
        Assert.AreEqual(third[0], ReadReceived(640));

        short ReadReceived(int frame) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(frame * 4 + 2));
        short[] Decode(int sampleCount, int frequency)
        {
            var pcm = Enumerable.Range(0, sampleCount)
                .Select(frame => (short)(12000 * Math.Sin(Math.Tau * frequency * frame / 16000))).ToArray();
            var decoded = encoder.DecodeAudio(encoder.EncodeAudio(pcm, format), format);
            Assert.HasCount(sampleCount, decoded);
            return decoded;
        }
    }

    [TestMethod]
    public async Task ExplicitPendingStartCancellationKeepsTheConversationMediaAlive()
    {
        using var fixture = new Fixture(); var native = new NativeMedia(fixture.Source);
        await using var media = new LiveRecordingMediaSession(native, fixture.Persisted, fixture.Factory, new(new ConfigurationLocation(fixture.Root)));
        var start = media.StartLiveAsync(Guid.NewGuid(), CancellationToken.None);
        media.CancelPendingStart();
        await Assert.ThrowsAsync<OperationCanceledException>(() => start);
        Assert.AreEqual(0, native.Disposals); Assert.AreEqual(0, fixture.Source.Subscribers);
        Assert.AreEqual(LiveRecordingState.Failed, media.RecordingStatus.State);
    }

    [TestMethod]
    public async Task LiveRecordingMissingSecondSignalTimesOutAndReleasesTheInput()
    {
        using var fixture = new Fixture();
        var native = new NativeMedia(fixture.Source);
        var paths = new ApplicationPaths(new ConfigurationLocation(fixture.Root));
        await using var media = new LiveRecordingMediaSession(native, fixture.Persisted, fixture.Factory, paths);
        var start = media.StartLiveAsync(Guid.NewGuid(), CancellationToken.None);
        fixture.Source.Send(1000, 300);
        await Assert.ThrowsExactlyAsync<TimeoutException>(() => start);
        Assert.AreEqual(LiveRecordingState.Failed, media.RecordingStatus.State);
        Assert.AreEqual(LiveRecordingFailure.SignalMissing, media.RecordingStatus.Failure);
        Assert.AreEqual(0, fixture.Source.Subscribers);
        Assert.AreEqual(0, native.Disposals);
        Assert.IsFalse(Directory.Exists(paths.RecordingsDirectory));
    }

    [TestMethod]
    public async Task LiveRecordingInputFailureRetiresCaptureWhileKeepingMediaOpen()
    {
        using var fixture = new Fixture();
        var native = new NativeMedia(fixture.Source);
        var paths = new ApplicationPaths(new ConfigurationLocation(fixture.Root));
        Directory.CreateDirectory(paths.RecordingsDirectory);
        await using var media = new LiveRecordingMediaSession(native, fixture.Persisted, fixture.Factory, paths);
        var failure = new TaskCompletionSource<LiveRecordingSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        media.RecordingChanged += (_, args) =>
        {
            if (args.Snapshot.State == LiveRecordingState.Failed) failure.TrySetResult(args.Snapshot);
        };
        var start = media.StartLiveAsync(Guid.NewGuid(), CancellationToken.None);
        fixture.Source.Send(1000, 100);
        fixture.Source.Receive(1, 2000, 200);
        await start;
        fixture.Clock.Advance(40);
        fixture.Source.Fail();
        var status = await failure.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.AreEqual(LiveRecordingFailure.InputFailed, status.Failure);
        Assert.AreEqual(0, fixture.Source.Subscribers);
        Assert.AreEqual(0, native.Disposals);
        Assert.IsFalse(Directory.EnumerateFiles(paths.RecordingsDirectory, "*.wav").Any());
    }

    [TestMethod]
    public async Task MultipleExplicitSegmentsUseDifferentIdsAndDoNotDisposeTheConversation()
    {
        using var fixture = new Fixture(); var native = new NativeMedia(fixture.Source);
        var paths = new ApplicationPaths(new ConfigurationLocation(fixture.Root)); Directory.CreateDirectory(paths.RecordingsDirectory);
        await using var media = new LiveRecordingMediaSession(native, fixture.Persisted, fixture.Factory, paths);
        var call = Guid.NewGuid();
        for (var segment = 0; segment < 2; segment++)
        {
            var start = media.StartLiveAsync(call, CancellationToken.None);
            for (var wait = 0; fixture.Source.Subscribers == 0 && wait < 200; wait++) await Task.Delay(5);
            Assert.AreEqual(1, fixture.Source.Subscribers);
            fixture.Source.Send(1000, 100); fixture.Source.Receive(1, 2000, 200); await start;
            fixture.Clock.Advance(40); await media.StopLiveAsync(CancellationToken.None);
        }
        Assert.AreEqual(2, Directory.EnumerateFiles(paths.RecordingsDirectory, "*.wav").Count());
        Assert.AreEqual(0, native.Disposals); Assert.AreEqual(0, fixture.Source.Subscribers);
    }

    [TestMethod]
    public async Task LocalReferencesUseOneMonotonicEpochAndRealRtpMetadataWithoutClaimingRemoteSynchronization()
    {
        var source = new LiveRecordingSourceDouble(); var clock = new LiveRecordingClock();
        await using var preparation = new LiveRecordingPreparation(source, clock);
        clock.Advance(10); source.Send(0xfffffff0, 100);
        clock.Advance(15); source.Receive(65535, 2000, 200);
        await preparation.Ready;
        var request = preparation.CreateRequest(new(Guid.NewGuid(), Guid.NewGuid(), "unused.wav"));
        Assert.AreEqual(RecordingTimelineBasis.LocalCodecObservation, request.TimelineBasis);
        Assert.AreEqual(TimeSpan.FromMilliseconds(10), request.Sent.RecordingOffset);
        Assert.AreEqual(TimeSpan.FromMilliseconds(25), request.Received.RecordingOffset);
        Assert.AreEqual(0xfffffff0u, request.Sent.Timestamp); Assert.AreEqual((ushort)65535, request.Received.SequenceNumber);
        clock.UtcShift = TimeSpan.FromHours(1);
        Assert.AreEqual(TimeSpan.FromMilliseconds(25), preparation.GetElapsed());
        Assert.AreEqual(new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.Zero), request.RecordingEpochUtc);
    }

    [TestMethod]
    public async Task StartAndStopCreateBothWaveChannelsPreserveRtpGapsAndDrainOnce()
    {
        using var fixture = new Fixture();
        var start = fixture.Factory.StartAsync(fixture.Source, fixture.Request());
        fixture.Source.Send(1000, 100); fixture.Source.Receive(1, 2000, 200);
        await using var recording = await start;
        fixture.Clock.Advance(40); fixture.Source.Send(1320, 300); fixture.Source.Receive(3, 2320, 400);
        fixture.Clock.Advance(40);
        var first = recording.StopAsync(); Assert.AreSame(first, recording.StopAsync());
        await first;
        Assert.AreEqual(0, fixture.Source.Subscribers);
        Assert.AreEqual(PcmRecordingState.Completed, recording.State);
        using var reader = new WaveFileReader(fixture.Path);
        Assert.AreEqual(2, reader.WaveFormat.Channels);
        byte[] bytes = new byte[(int)reader.Length]; reader.Read(bytes, 0, bytes.Length);
        Assert.AreEqual((short)100, BinaryPrimitives.ReadInt16LittleEndian(bytes));
        Assert.AreEqual((short)200, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)));
        Assert.AreEqual((short)0, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(160 * 4)));
        Assert.AreEqual((short)300, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(320 * 4)));
        Assert.AreEqual((short)400, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(320 * 4 + 2)));
    }

    [TestMethod]
    public async Task CancellationWhileWaitingForASecondSideDetachesAndCreatesNoWave()
    {
        using var fixture = new Fixture(); using var stopping = new CancellationTokenSource();
        var start = fixture.Factory.StartAsync(fixture.Source, fixture.Request(), stopping.Token);
        fixture.Source.Send(1000, 100); stopping.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => start);
        Assert.AreEqual(0, fixture.Source.Subscribers); Assert.IsFalse(File.Exists(fixture.Path));
    }

    [TestMethod]
    public async Task StartupBufferIsBoundedAndRejectsInsteadOfSilentlyDroppingOneSide()
    {
        using var fixture = new Fixture();
        var start = fixture.Factory.StartAsync(fixture.Source, fixture.Request());
        for (uint i = 0; i < 140 && fixture.Source.Subscribers > 0; i++) fixture.Source.Send(1000 + i * 160, 100);
        await Assert.ThrowsAsync<PcmRecordingException>(() => start);
        Assert.AreEqual(0, fixture.Source.Subscribers); Assert.IsFalse(File.Exists(fixture.Path));
    }

    [TestMethod]
    public async Task SourceLossAndInvalidMetadataCannotProduceASuccessfulWave()
    {
        using var fixture = new Fixture();
        var start = fixture.Factory.StartAsync(fixture.Source, fixture.Request());
        fixture.Source.Send(1000, 100); fixture.Source.Receive(1, 2000, 200);
        var recording = await start; fixture.Clock.Advance(40);
        fixture.Source.Fail();
        await Assert.ThrowsAsync<PcmRecordingException>(() => recording.StopAsync());
        try { await recording.DisposeAsync(); } catch (PcmRecordingException) { }
        Assert.IsFalse(File.Exists(fixture.Path)); Assert.AreEqual(0, fixture.Source.Subscribers);
    }

    [TestMethod]
    public async Task LateLocalFramesBeyondBoundedClockToleranceFailWithoutPublishing()
    {
        using var fixture = new Fixture();
        var start = fixture.Factory.StartAsync(fixture.Source, fixture.Request());
        fixture.Source.Send(1000, 100); fixture.Source.Receive(1, 2000, 200);
        var recording = await start;
        fixture.Source.Send(9000, 100);
        await Assert.ThrowsAsync<PcmRecordingException>(() => recording.StopAsync());
        try { await recording.DisposeAsync(); } catch (PcmRecordingException) { }
        Assert.IsFalse(File.Exists(fixture.Path));
    }

    [TestMethod]
    [DataRow(160L, 40)]
    [DataRow(640L, 80)]
    [DataRow(4800L, 0)]
    public async Task SlowInputDrainDoesNotMoveTheStopTimeOrHideFutureFrames(long inputFrames, int expectedMilliseconds)
    {
        var clock = new LiveRecordingClock();
        var source = new LiveRecordingSourceDouble();
        await using var preparation = new LiveRecordingPreparation(source, clock);
        var request = new PcmRecordingRequest(Guid.NewGuid(), Guid.NewGuid(), "unused.wav", 8000);
        var inner = new SlowDrainRecording(request, clock, inputFrames);
        var recording = new LivePcmRecording(preparation, inner, request, new CancellationTokenSource());
        clock.Advance(40);

        if (expectedMilliseconds == 0)
        {
            await Assert.ThrowsAsync<PcmRecordingException>(() => recording.StopAsync());
            await Assert.ThrowsAsync<PcmRecordingException>(() => recording.DisposeAsync().AsTask());
            Assert.IsNull(inner.StopDuration);
        }
        else
        {
            await using (recording)
            {
                var result = await recording.StopAsync();
                Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), result.Wave.Duration);
            }
        }
        Assert.IsTrue(inner.Disposed);
        Assert.AreEqual(0, source.Subscribers);
    }

    [TestMethod]
    public async Task MediaTerminationFinalizesBeforeClosingNativeMediaAndRejectsParallelStarts()
    {
        using var fixture = new Fixture();
        var native = new NativeMedia(fixture.Source);
        var paths = new ApplicationPaths(new ConfigurationLocation(fixture.Root)); Directory.CreateDirectory(paths.RecordingsDirectory);
        var media = new LiveRecordingMediaSession(native, fixture.Persisted, fixture.Factory, paths);
        var states = new List<LiveRecordingState>(); media.RecordingChanged += (_, e) => states.Add(e.Snapshot.State);
        var id = Guid.NewGuid(); var start = media.StartLiveAsync(id, CancellationToken.None);
        fixture.Source.Send(1000, 100); fixture.Source.Receive(1, 2000, 200); await start;
        await Assert.ThrowsAsync<WawiCallPro.Interfaces.Telephony.TelephonyException>(() => media.StartLiveAsync(id, CancellationToken.None));
        fixture.Clock.Advance(40);
        native.BeforeDispose = () => Assert.IsTrue(Directory.EnumerateFiles(paths.RecordingsDirectory, "*.wav").Any());
        var close = media.DisposeAsync().AsTask(); await close; Assert.AreSame(close, media.DisposeAsync().AsTask());
        Assert.AreEqual(1, native.Disposals); Assert.AreEqual(LiveRecordingState.Completed, media.RecordingStatus.State);
        CollectionAssert.AreEqual(new[] { LiveRecordingState.Starting, LiveRecordingState.Recording, LiveRecordingState.Stopping, LiveRecordingState.Completed }, states);
        Assert.AreEqual(0, fixture.Source.Subscribers);
    }

    [TestMethod]
    public async Task MediaCloseCancelsAnUnprimedStartBeforeReleasingNativeResources()
    {
        using var fixture = new Fixture(); var native = new NativeMedia(fixture.Source);
        var media = new LiveRecordingMediaSession(native, fixture.Persisted, fixture.Factory, new(new ConfigurationLocation(fixture.Root)));
        var start = media.StartLiveAsync(Guid.NewGuid(), CancellationToken.None);
        await media.DisposeAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => start);
        Assert.AreEqual(0, fixture.Source.Subscribers); Assert.AreEqual(1, native.Disposals);
    }

    [TestMethod]
    public async Task AbortingDoesNotPublishAndAnotherExplicitSegmentCanStart()
    {
        using var fixture = new Fixture();
        var start = fixture.Factory.StartAsync(fixture.Source, fixture.Request());
        fixture.Source.Send(1000, 100); fixture.Source.Receive(1, 2000, 200);
        var recording = await start; await recording.DisposeAsync();
        Assert.IsFalse(File.Exists(fixture.Path)); Assert.AreEqual(0, fixture.Source.Subscribers);
        start = fixture.Factory.StartAsync(fixture.Source, fixture.Request());
        fixture.Source.Send(2000, 100); fixture.Source.Receive(2, 3000, 200);
        recording = await start; fixture.Clock.Advance(40); await recording.StopAsync(); await recording.DisposeAsync();
        Assert.IsTrue(File.Exists(fixture.Path));
    }

    private sealed class SlowDrainRecording(PcmRecordingRequest request, LiveRecordingClock clock, long inputFrames)
        : IDuplexPcmRecording, IRecordingInputDrain
    {
        public PcmRecordingState State => PcmRecordingState.Capturing;
        public PcmRecordingFailure? Failure => null;
        public Task InputCompletion => Task.CompletedTask;
        internal bool Disposed { get; private set; }
        internal TimeSpan? StopDuration { get; private set; }

        public Task<long> DrainInputAsync()
        {
            clock.Advance(1000);
            return Task.FromResult(inputFrames);
        }

        public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            StopDuration = duration;
            var frames = duration.Ticks * request.SampleRate / TimeSpan.TicksPerSecond;
            return Task.FromResult(new PcmRecordingResult(new(request.CallId, request.RecordingId, frames, request.SampleRate, 2, 0), []));
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingJournalDouble : IRecordingJournal
    {
        internal StoredRecordingJournal? Saved;
        public Task<StoredRecordingJournal> BeginAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default)
            => WriteAsync(recording, capture, cancellationToken);
        public Task<StoredRecordingJournal> WriteAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default)
            => Task.FromResult(Saved = new StoredRecordingJournal(
                new(recording.Recording, (recording.ExpectedVersion ?? 0) + 1, recording.ChangedAtUtc, recording.ChangedAtUtc),
                new(capture.Capture, (capture.ExpectedVersion ?? 0) + 1, capture.ChangedAtUtc)));
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly string Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WawiCallPro-live-" + Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(Root, "live.wav");
        internal readonly LiveRecordingClock Clock = new();
        internal readonly LiveRecordingSourceDouble Source = new();
        internal readonly ServiceProvider Services;
        internal readonly LivePcmRecordingFactory Factory;
        internal readonly IPersistedDuplexPcmRecordingFactory Persisted;
        internal Fixture()
        {
            Directory.CreateDirectory(Root); var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<TimeProvider>(Clock); services.AddAudioConversion();
            services.AddSingleton(new ConfigurationStore(new ConfigurationLocation(Root), new ApplicationOptionsValidator(new WindowsUserSecretProtector())));
            Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
            Persisted = new ForwardFactory(Services.GetRequiredService<IDuplexPcmRecordingFactory>());
            Factory = new(Persisted, Clock);
        }
        internal PcmRecordingRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(), Path, 8000);
        public void Dispose() { Services.Dispose(); Directory.Delete(Root, true); }
    }
    internal sealed class ForwardFactory(IDuplexPcmRecordingFactory inner) : IPersistedDuplexPcmRecordingFactory
    {
        public Task<IDuplexPcmRecording> StartAsync(IConversationPcmFailureSource source, DuplexPcmRecordingRequest request, CancellationToken token = default) => inner.StartAsync(source, request, token);
    }
    internal sealed class NativeMedia(IConversationPcmSource source) : ISipPcmMediaSession
    {
        internal int Disposals; internal Action? BeforeDispose;
        public SIPSorcery.SIP.App.IMediaSession Session => throw new NotSupportedException();
        public IConversationPcmSource PcmSource => source;
        public void ApplyPlaybackSettings() { }
        public ValueTask DisposeAsync() { BeforeDispose?.Invoke(); Disposals++; return ValueTask.CompletedTask; }
    }
    private sealed class CaptureDouble : IPlaybackPcmCaptureFactory, IPlaybackPcmCapture
    {
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal string? Endpoint; internal int Stops;
        public Task Completion => _completed.Task;
        public Task<IPlaybackPcmCapture> StartAsync(string? id, DateTimeOffset epoch, IPlaybackPcmObserver observer, CancellationToken token = default)
        {
            Endpoint = id; Assert.IsTrue(observer.TryObserve(TimeSpan.Zero, 8000, Enumerable.Repeat((short)200, 160).ToArray()));
            return Task.FromResult<IPlaybackPcmCapture>(this);
        }
        public ValueTask DisposeAsync() { Stops++; _completed.TrySetResult(); return ValueTask.CompletedTask; }
    }
}

internal sealed class LiveRecordingClock : TimeProvider
{
    private long _ticks;
    internal TimeSpan UtcShift;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.Zero).AddTicks(_ticks) + UtcShift;
    internal void Advance(int milliseconds) => _ticks += TimeSpan.FromMilliseconds(milliseconds).Ticks;
}

internal sealed class LiveRecordingSourceDouble : IConversationPcmFailureSource
{
    private Lease? _lease;
    internal int Subscribers => _lease is null ? 0 : 1;
    public long FailureCount => 0;
    public PcmObservationFailure? LastFailure => null;
    public IConversationPcmSubscription Subscribe(IConversationPcmObserver observer)
    { if (_lease is not null) throw new InvalidOperationException(); return _lease = new(this, observer); }
    internal void Send(uint timestamp, short sample) => (_lease?.Observer as ISentRtpPcmObserver)?.TryObserveSent(new(7, timestamp, 8000, 160), 8000, 1, Enumerable.Repeat(sample, 160).ToArray());
    internal void Receive(ushort sequence, uint timestamp, short sample) => (_lease?.Observer as IReceivedRtpPcmObserver)?.TryObserveReceived(new(9, sequence, timestamp, 8000), 8000, 1, Enumerable.Repeat(sample, 160).ToArray());
    internal void ReceiveWideband(ushort sequence, uint timestamp, short[] samples) =>
        (_lease?.Observer as IReceivedRtpPcmObserver)?.TryObserveReceived(new(9, sequence, timestamp, 8000), 16000, 1, samples);
    internal void Fail()
    {
        var lease = _lease;
        (lease?.Observer as IConversationPcmFailureObserver)?.OnPcmObservationFailed(PcmObservationFailure.SentMetadataUnavailable);
        lease?.Completed.TrySetResult(PcmObservationFailure.SentMetadataUnavailable);
    }
    private sealed class Lease(LiveRecordingSourceDouble owner, IConversationPcmObserver observer) : IConversationPcmSubscription
    {
        internal IConversationPcmObserver Observer = observer;
        internal readonly TaskCompletionSource<PcmObservationFailure?> Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<PcmObservationFailure?> Completion => Completed.Task;
        public ValueTask DisposeAsync() { owner._lease = null; Completed.TrySetResult(null); return ValueTask.CompletedTask; }
    }
}
