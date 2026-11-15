using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using CSCore.Codecs.WAV;
using Microsoft.Extensions.DependencyInjection;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

public sealed partial class ReceivedRtpPcmTests
{
    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task NativeReceiveBindingPreservesOrderedSamplesGapsAndExplicitOffsetInStereoWave(SDPWellKnownMediaFormatsEnum codec)
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(codec, CreateFactory(clock));
        using var files = new RecordedFiles(); using var host = ConsoleCompositionRoot.Build(loadPlugins: false);
        await using var session = await host.Services.GetRequiredService<IPcmRecordingSessionFactory>().StartAsync(files.Request(fixture.Format.ClockRate));
        var bindings = host.Services.GetRequiredService<IReceivedPcmRecordingBindingFactory>();
        Assert.IsInstanceOfType<ReceivedPcmRecordingBindingFactory>(bindings);
        Assert.AreSame(bindings, host.Services.GetRequiredService<IReceivedPcmRecordingBindingFactory>());
        var timestamp = uint.MaxValue - 159;
        await using var binding = await bindings.AttachAsync(fixture.Tap, session, new(17, ushort.MaxValue, timestamp, 8000, TimeSpan.FromMilliseconds(10)));
        var encoder = new AudioEncoder(); var baseline = new AudioEncoder(); var rate = fixture.Format.ClockRate; var block = rate / 50;
        var payloads = Enumerable.Range(0, 5).Select(index => encoder.EncodeAudio(Samples(block, index), fixture.Format)).ToArray();
        var decoded = new short[5][];
        foreach (var index in new[] { 0, 1, 2, 4 }) decoded[index] = baseline.DecodeAudio(payloads[index], fixture.Format);
        RtpPcmPacketPosition Position(int index) => new(17, unchecked((ushort)(ushort.MaxValue + index)), unchecked(timestamp + (uint)(index * 160)), 8000);
        // The left channel's explicitly positioned synthetic input is independent of the native send clock.
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, rate, Enumerable.Repeat((short)1234, block).ToArray()));
        foreach (var index in new[] { 0, 2, 1, 2, 4 }) fixture.Dispatch(Position(index), payloads[index]);
        clock.Advance(TimeSpan.FromMilliseconds(60));
        await binding.DisposeAsync(); Assert.IsNull(await binding.Completion);
        Assert.AreEqual(TimeSpan.FromMilliseconds(110), binding.LastPacketEnd);
        var result = await session.StopAsync(TimeSpan.FromMilliseconds(120));
        Assert.AreEqual(rate * 120 / 1000L, result.Wave.FrameCount);
        var pcm = files.Read(rate, rate * 120 / 1000);
        for (var frame = 0; frame < pcm.Length / 2; frame++)
        {
            Assert.AreEqual(frame < block ? (short)1234 : (short)0, pcm[frame * 2]);
            var offset = frame - rate / 100; var packet = offset < 0 ? -1 : offset / block;
            var expected = packet is >= 0 and <= 4 && packet != 3 ? decoded[packet][offset % block] : (short)0;
            Assert.AreEqual(expected, pcm[frame * 2 + 1], "Receive sample " + frame);
        }
        Assert.AreEqual(4, fixture.Decoder.Decodes); Assert.IsNull(binding.Failure);
    }

    [TestMethod]
    public async Task ReceiveBindingTrimsRecordingStartInsideAnActualG722Packet()
    {
        await using var fixture = new Fixture(SDPWellKnownMediaFormatsEnum.G722); using var files = new RecordedFiles();
        await using var session = await RecordingSessions().StartAsync(files.Request(16000));
        await using var binding = await RecordingBindings().AttachAsync(fixture.Tap, session, new(17, 10, 1000, 8000, TimeSpan.FromMilliseconds(-5)));
        var payload = new AudioEncoder().EncodeAudio(Samples(320, 0), fixture.Format); var decoded = new AudioEncoder().DecodeAudio(payload, fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); await binding.DisposeAsync(); await session.StopAsync(TimeSpan.FromMilliseconds(20));
        var pcm = files.Read(16000, 320);
        for (var frame = 0; frame < 320; frame++)
        { Assert.AreEqual((short)0, pcm[frame * 2]); Assert.AreEqual(frame < 240 ? decoded[frame + 80] : (short)0, pcm[frame * 2 + 1]); }
    }

    [TestMethod]
    [DataRow("metadata")]
    [DataRow("source")]
    [DataRow("codec")]
    [DataRow("anchor")]
    public async Task MediaFailureSynchronouslyPreventsTheActualRecordingFromPublishingAPartialWave(string kind)
    {
        await using var fixture = new Fixture(); using var files = new RecordedFiles();
        await using var session = await RecordingSessions().StartAsync(files.Request(8000));
        await using var binding = await RecordingBindings().AttachAsync(fixture.Tap, session, new(17, 10, 1000, 8000, TimeSpan.Zero));
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload);
        switch (kind)
        {
            case "metadata": fixture.Tap.DecodeAudio(payload, fixture.Format); break;
            case "source": fixture.Dispatch(new(19, 11, 1160, 8000), payload); break;
            case "codec":
                var error = new IOException("private-native-detail"); fixture.Decoder.Error = error;
                Assert.AreSame(error, Assert.ThrowsExactly<IOException>(() => fixture.Dispatch(new(17, 11, 1160, 8000), payload))); break;
            case "anchor": fixture.Dispatch(new(17, 11, 1080, 8000), payload); break;
        }
        // Deliberately check/stop before awaiting binding Completion: failure cannot depend on a scheduled continuation.
        Assert.AreEqual(PcmRecordingState.Failed, session.State); Assert.IsNotNull(binding.Failure);
        var failure = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(100)));
        Assert.AreEqual(PcmRecordingFailure.TimelineConflict, failure.Failure); Assert.IsFalse(File.Exists(files.Output));
        await binding.DisposeAsync(); Assert.IsNotNull(await binding.Completion);
    }

    [TestMethod]
    public async Task TimerDecodeFailureSynchronouslyFailsTheRecordingBeforeAnyStop()
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(bridges: CreateFactory(clock)); using var files = new RecordedFiles();
        await using var session = await RecordingSessions().StartAsync(files.Request(8000));
        await using var binding = await RecordingBindings().AttachAsync(fixture.Tap, session, new(17, 10, 1000, 8000, TimeSpan.Zero));
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); fixture.Dispatch(new(17, 12, 1320, 8000), payload);
        fixture.Decoder.Error = new IOException("private-native-detail"); clock.Advance(TimeSpan.FromMilliseconds(60));
        Assert.AreEqual(PcmRecordingState.Failed, session.State);
        await binding.DisposeAsync(); await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(100)));
        Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task NativeMediaClosureDrainsLastQueuedPacketBeforeTheRecordingBindingCompletes()
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(bridges: CreateFactory(clock)); using var files = new RecordedFiles();
        await using var session = await RecordingSessions().StartAsync(files.Request(8000));
        await using var binding = await RecordingBindings().AttachAsync(fixture.Tap, session, new(17, 10, 1000, 8000, TimeSpan.Zero));
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); fixture.Dispatch(new(17, 12, 1320, 8000), payload);
        var owner = new PcmSipMediaSession(new MediaOwner(fixture.Media), fixture.Tap, fixture.Bridge);
        await owner.DisposeAsync(); Assert.IsNull(await binding.Completion); Assert.IsNull(binding.Failure);
        Assert.AreEqual(TimeSpan.FromMilliseconds(60), binding.LastPacketEnd); Assert.AreEqual(PcmRecordingState.Capturing, session.State);
        await session.StopAsync(TimeSpan.FromMilliseconds(60)); var pcm = files.Read(8000, 480);
        var expected = new AudioEncoder().DecodeAudio(payload, fixture.Format);
        for (var frame = 0; frame < 480; frame++)
        { Assert.AreEqual((short)0, pcm[frame * 2]); Assert.AreEqual(frame is >= 160 and < 320 ? (short)0 : expected[frame % 160], pcm[frame * 2 + 1]); }
    }

    [TestMethod]
    public async Task DetachingDrainsOnlyItsSubscriptionAndKeepsTheBorrowedSourceAndSessionAlive()
    {
        await using var fixture = new Fixture(); using var files = new RecordedFiles();
        await using var session = await RecordingSessions().StartAsync(files.Request(8000));
        await using var binding = await RecordingBindings().AttachAsync(fixture.Tap, session, new(17, 10, 1000, 8000, TimeSpan.Zero));
        var passive = new PassiveObserver(); await using var subscription = fixture.Tap.Subscribe(passive);
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); await binding.DisposeAsync(); await binding.DisposeAsync();
        fixture.Dispatch(new(17, 11, 1160, 8000), payload); fixture.Tap.EncodeAudio(Samples(160, 2), fixture.Format);
        Assert.AreEqual(3, passive.Frames.Count); Assert.AreEqual(PcmRecordingState.Capturing, session.State);
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), binding.LastPacketEnd); Assert.IsNull(await binding.Completion);
        await session.StopAsync(TimeSpan.FromMilliseconds(40));
        var pcm = files.Read(8000, 320); for (var frame = 160; frame < 320; frame++) Assert.AreEqual((short)0, pcm[frame * 2 + 1]);
    }

    [TestMethod]
    public async Task AlreadyFailedReceiveMetadataFailsANewBindingBeforeAttachReturns()
    {
        await using var fixture = new Fixture(); using var files = new RecordedFiles();
        fixture.Frame(new(0, fixture.Format, 20, [1])); fixture.Frame(new(0, fixture.Format, 20, [2]));
        await using var session = await RecordingSessions().StartAsync(files.Request(8000));
        await using var binding = await RecordingBindings().AttachAsync(fixture.Tap, session, new(17, 10, 1000, 8000, TimeSpan.Zero));
        Assert.AreEqual(PcmRecordingState.Failed, session.State); Assert.AreEqual(ReceivedPcmRecordingFailure.MetadataUnavailable, binding.Failure);
        await binding.DisposeAsync(); await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task FailedSubscriptionSetupReleasesTheFeedAndRejectsTheBorrowedRecording()
    {
        await using var fixture = new Fixture(); using var files = new RecordedFiles();
        await using var session = await RecordingSessions().StartAsync(files.Request(8000)); await fixture.Tap.DisposeAsync();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => RecordingBindings().AttachAsync(fixture.Tap, session, new(17, 10, 1000, 8000, TimeSpan.Zero)));
        Assert.AreEqual(PcmRecordingState.Failed, session.State);
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task UdpReceiveReachesTheRecordingWaveThroughTheExistingDecoderAndBinding(SDPWellKnownMediaFormatsEnum codec)
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(codec, CreateFactory(clock)); using var files = new RecordedFiles();
        await using var session = await RecordingSessions().StartAsync(files.Request(fixture.Format.ClockRate));
        using var sender = new RTPSession(false, false, false, IPAddress.Loopback);
        sender.addTrack(new MediaStreamTrack(new List<AudioFormat> { fixture.Format }));
        Assert.AreEqual(SetDescriptionResultEnum.OK, fixture.Media.SetRemoteDescription(SdpType.offer, sender.CreateOffer(IPAddress.Loopback)));
        Assert.AreEqual(SetDescriptionResultEnum.OK, sender.SetRemoteDescription(SdpType.answer, fixture.Media.CreateAnswer(IPAddress.Loopback)));
        var ssrc = sender.AudioStream.LocalTrack.Ssrc;
        await using var binding = await RecordingBindings().AttachAsync(fixture.Tap, session, new(ssrc, 100, 1000, 8000, TimeSpan.Zero));
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var arrived = 0;
        fixture.Media.OnRtpPacketReceived += (_, type, _) => { if (type == SDPMediaTypesEnum.audio && Interlocked.Increment(ref arrived) == 4) completed.TrySetResult(); };
        await fixture.Media.Start(); await sender.Start();
        var encoder = new AudioEncoder(); var baseline = new AudioEncoder(); var block = fixture.Format.ClockRate / 50;
        var payloads = Enumerable.Range(0, 3).Select(index => encoder.EncodeAudio(Samples(block, index), fixture.Format)).ToArray();
        var expected = payloads.SelectMany(payload => baseline.DecodeAudio(payload, fixture.Format)).ToArray();
        foreach (var index in new[] { 0, 2, 2, 1 })
            sender.SendRtpRaw(SDPMediaTypesEnum.audio, payloads[index], (uint)(1000 + index * 160), 0, fixture.Format.FormatID, (ushort)(100 + index));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10)); await binding.DisposeAsync();
        Assert.IsNull(await binding.Completion); await session.StopAsync(TimeSpan.FromMilliseconds(60));
        var pcm = files.Read(fixture.Format.ClockRate, block * 3);
        for (var frame = 0; frame < expected.Length; frame++) { Assert.AreEqual((short)0, pcm[frame * 2]); Assert.AreEqual(expected[frame], pcm[frame * 2 + 1]); }
        Assert.AreEqual(3, fixture.Decoder.Decodes);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SynchronousFailureBoundaryRunsForRejectedOrThrowingObserversAndIsItselfIsolated(bool throws)
    {
        await using var fixture = new Fixture();
        var observer = new FailureBoundary
        {
            Behavior = () => throws ? throw new IOException("private-observer-detail") : false,
            FailureAction = _ => throw new IOException("private-boundary-detail")
        };
        await using var subscription = fixture.Tap.Subscribe(observer);
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload);
        var reason = throws ? PcmObservationFailure.ObserverFailed : PcmObservationFailure.ObserverRejected;
        Assert.AreEqual(reason, observer.Failures.Single()); Assert.AreEqual(reason, await subscription.Completion);
        CollectionAssert.AreEqual(new AudioEncoder().DecodeAudio(payload, fixture.Format), fixture.ReadPlayback(160));
    }

    [TestMethod]
    public async Task SubscriptionAndSourceCompletionWaitUntilSynchronousFailureNotificationReturns()
    {
        await using var fixture = new Fixture(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var observer = new FailureBoundary { FailureAction = _ => { entered.Set(); release.Wait(); } };
        await using var subscription = fixture.Tap.Subscribe(observer); var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        var publish = Task.Run(() => fixture.Tap.DecodeAudio(payload, fixture.Format)); Task? detach = null, close = null;
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10))); detach = subscription.DisposeAsync().AsTask(); close = fixture.Tap.DisposeAsync().AsTask();
            Assert.IsFalse(subscription.Completion.IsCompleted); Assert.IsFalse(detach.IsCompleted); Assert.IsFalse(close.IsCompleted);
        }
        finally { release.Set(); }
        await publish; await detach!; await close!;
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(1, observer.Failures.Count);
    }

    [TestMethod]
    public async Task FailureDuringAnAlreadyRequestedDetachStillReachesTheSynchronousBoundary()
    {
        await using var fixture = new Fixture(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var observer = new FailureBoundary { Behavior = () => { entered.Set(); release.Wait(); return false; } };
        await using var subscription = fixture.Tap.Subscribe(observer); var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        var publish = Task.Run(() => fixture.Dispatch(new(17, 10, 1000, 8000), payload)); Task? detach = null;
        try { Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10))); detach = subscription.DisposeAsync().AsTask(); Assert.IsFalse(detach.IsCompleted); }
        finally { release.Set(); }
        await publish; await detach!;
        Assert.AreEqual(PcmObservationFailure.ObserverRejected, observer.Failures.Single());
        Assert.AreEqual(PcmObservationFailure.ObserverRejected, await subscription.Completion);
    }

    [TestMethod]
    public async Task StereoReceivePcmFailsBeforeTheMonoRecordingCanAcceptIt()
    {
        await using var fixture = new Fixture(); using var files = new RecordedFiles();
        await using var session = await RecordingSessions().StartAsync(files.Request(8000));
        await using var binding = await RecordingBindings().AttachAsync(fixture.Tap, session, new(17, 10, 1000, 8000, TimeSpan.Zero));
        var stereo = new AudioFormat(8, "PCMA", 8000, 8000, 2, null); var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Frame(new(0, stereo, 20, payload)); fixture.Packet(new(17, 10, 1000, 8000), payload);
        Assert.AreEqual(PcmRecordingFailure.InvalidBlock, session.Failure); Assert.AreEqual(ReceivedPcmRecordingFailure.InvalidPcm, binding.Failure);
        await binding.DisposeAsync(); await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task BindingKeepsTheRecordingsOriginalFailureWhenFurtherInputIsRejected()
    {
        await using var fixture = new Fixture(); using var files = new RecordedFiles();
        await using var session = await RecordingSessions().StartAsync(files.Request(8000));
        await using var binding = await RecordingBindings().AttachAsync(fixture.Tap, session, new(17, 10, 1000, 8000, TimeSpan.Zero));
        ((IPcmRecordingInputGuard)session).RejectInput(PcmRecordingFailure.BufferOverflow);
        fixture.Dispatch(new(17, 10, 1000, 8000), new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format));
        Assert.AreEqual(PcmRecordingFailure.BufferOverflow, session.Failure); Assert.AreEqual(ReceivedPcmRecordingFailure.RecordingRejected, binding.Failure);
        await binding.DisposeAsync(); var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.AreEqual(PcmRecordingFailure.BufferOverflow, error.Failure); Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task DetachAwaitsTheActiveRecordingAppendBeforeCompletingItsBorrowedFeed()
    {
        await using var fixture = new Fixture(); using var files = new RecordedFiles();
        await using var session = await RecordingSessions().StartAsync(files.Request(8000));
        using var blocking = new BlockingRecordingInput(session);
        await using var binding = await RecordingBindings().AttachAsync(fixture.Tap, blocking, new(17, 10, 1000, 8000, TimeSpan.Zero));
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        var dispatch = Task.Run(() => fixture.Dispatch(new(17, 10, 1000, 8000), payload)); Task? detach = null;
        try
        {
            Assert.IsTrue(blocking.Entered.Wait(TimeSpan.FromSeconds(10))); detach = binding.DisposeAsync().AsTask();
            Assert.IsFalse(detach.IsCompleted); Assert.IsFalse(binding.Completion.IsCompleted); Assert.AreEqual(PcmRecordingState.Capturing, session.State);
        }
        finally { blocking.Release.Set(); }
        await dispatch; await detach!; Assert.IsNull(await binding.Completion);
        fixture.Dispatch(new(17, 11, 1160, 8000), payload);
        Assert.AreEqual(1, blocking.Appends); await session.StopAsync(TimeSpan.FromMilliseconds(40));
        var pcm = files.Read(8000, 320);
        var expected = new AudioEncoder().DecodeAudio(payload, fixture.Format);
        for (var frame = 0; frame < 320; frame++) Assert.AreEqual(frame < 160 ? expected[frame] : (short)0, pcm[frame * 2 + 1]);
    }

    private sealed class BlockingRecordingInput(IPcmRecordingSession inner) : IPcmRecordingSession, IPcmRecordingInputGuard, IDisposable
    {
        internal readonly ManualResetEventSlim Entered = new(), Release = new(); internal int Appends;
        public PcmRecordingState State => inner.State;
        public PcmRecordingFailure? Failure => inner.Failure;
        public string SegmentDirectory => inner.SegmentDirectory;
        public Task CaptureCompletion => inner.CaptureCompletion;
        public bool TryAppend(ConversationAudioSide side, TimeSpan start, int sampleRate, ReadOnlySpan<short> samples)
        { Interlocked.Increment(ref Appends); Entered.Set(); Release.Wait(); return inner.TryAppend(side, start, sampleRate, samples); }
        public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken token = default) => inner.StopAsync(duration, token);
        public void RejectInput(PcmRecordingFailure failure) => ((IPcmRecordingInputGuard)inner).RejectInput(failure);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask; // Borrowed; the test owns the actual session.
        public void Dispose() { Release.Set(); Entered.Dispose(); Release.Dispose(); }
    }

    private sealed class FailureBoundary : IReceivedRtpPcmObserver, IConversationPcmFailureObserver
    {
        internal readonly ConcurrentQueue<PcmObservationFailure> Failures = new();
        internal Func<bool>? Behavior; internal Action<PcmObservationFailure>? FailureAction;
        public bool TryObserve(ConversationAudioSide side, int rate, int channels, ReadOnlySpan<short> samples) => Behavior?.Invoke() ?? true;
        public bool TryObserveReceived(RtpPcmPacketPosition packet, int rate, int channels, ReadOnlySpan<short> samples) => Behavior?.Invoke() ?? true;
        public void OnPcmObservationFailed(PcmObservationFailure failure) { Failures.Enqueue(failure); FailureAction?.Invoke(failure); }
    }

    private static IReceivedPcmRecordingBindingFactory RecordingBindings() => new ReceivedPcmRecordingBindingFactory(new RtpPcmRecordingFeedFactory());
    private static IPcmRecordingSessionFactory RecordingSessions() => new PcmRecordingSessionFactory(new CscorePcmSegmentSinkFactory(), new ConversationWaveMixer(new ConversationWaveRenderer()));
    private sealed class RecordedFiles : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-receive-binding-" + Guid.NewGuid().ToString("N"));
        internal string Output => Path.Combine(_root, "recorded.wav");
        internal RecordedFiles() => Directory.CreateDirectory(_root);
        internal PcmRecordingRequest Request(int rate) => new(Guid.NewGuid(), Guid.NewGuid(), Output, rate);
        internal short[] Read(int rate, int frames)
        {
            using var reader = new WaveFileReader(Output); Assert.AreEqual(rate, reader.WaveFormat.SampleRate); Assert.AreEqual(2, reader.WaveFormat.Channels);
            Assert.AreEqual(frames * 4L, reader.Length); var bytes = new byte[reader.Length]; Assert.AreEqual(bytes.Length, reader.Read(bytes, 0, bytes.Length));
            return Enumerable.Range(0, bytes.Length / 2).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2, 2))).ToArray();
        }
        public void Dispose()
        {
            foreach (var directory in Directory.GetDirectories(_root))
            { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
            foreach (var file in Directory.GetFiles(_root)) File.Delete(file); Directory.Delete(_root);
        }
    }
}
