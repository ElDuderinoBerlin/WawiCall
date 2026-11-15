using System.Buffers.Binary;
using System.Net;
using CSCore.Codecs.WAV;
using Microsoft.Extensions.DependencyInjection;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

public sealed partial class SentRtpPcmTests
{
    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task ActualSourceAndOrderedUdpReceiveReachOneStereoWaveUsingExplicitPairedAnchors(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); fixture.EnableReceiveBuffer(); await fixture.Connect();
        using var files = new RecordingFiles(); using var host = ConsoleCompositionRoot.Build(loadPlugins: false);
        var rate = fixture.Format.ClockRate; var block = rate / 50;
        await using var session = await host.Services.GetRequiredService<IPcmRecordingSessionFactory>().StartAsync(files.Request(rate));
        var factory = host.Services.GetRequiredService<ISentPcmRecordingBindingFactory>();
        Assert.IsInstanceOfType<SentPcmRecordingBindingFactory>(factory); Assert.AreSame(factory, host.Services.GetRequiredService<ISentPcmRecordingBindingFactory>());
        await using var sent = factory.Attach(fixture.Tap, session, Anchor(fixture));
        await using var received = await host.Services.GetRequiredService<IReceivedPcmRecordingBindingFactory>().AttachAsync(
            fixture.Tap, session, new(fixture.RemoteSsrc, 100, 5000, 8000, TimeSpan.FromMilliseconds(10)));
        var arrived = 0; var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Media.OnRtpPacketReceived += (_, type, _) => { if (type == SDPMediaTypesEnum.audio && Interlocked.Increment(ref arrived) == 3) completed.TrySetResult(); };
        var inputs = new[] { Samples(block, 0), Samples(block, 1) };
        fixture.Capture(inputs[0]); fixture.EmitExtras(160, new AudioEncoder().EncodeAudio(Samples(block, 2), fixture.Format)); fixture.Capture(inputs[1]);
        await fixture.Packets(3);
        var encoder = new AudioEncoder(); var baseline = new AudioEncoder();
        var payloads = Enumerable.Range(0, 3).Select(index => encoder.EncodeAudio(Samples(block, index + 5), fixture.Format)).ToArray();
        var receiveSamples = payloads.SelectMany(payload => baseline.DecodeAudio(payload, fixture.Format)).ToArray();
        foreach (var index in new[] { 0, 2, 1 }) fixture.SendRemote((ushort)(100 + index), (uint)(5000 + index * 160), payloads[index]);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await sent.DisposeAsync(); await received.DisposeAsync(); Assert.IsNull(await sent.Completion); Assert.IsNull(await received.Completion);
        Assert.AreEqual(TimeSpan.FromMilliseconds(60), sent.LastWindowEnd); Assert.AreEqual(TimeSpan.FromMilliseconds(70), received.LastPacketEnd);
        var result = await session.StopAsync(TimeSpan.FromMilliseconds(80)); Assert.AreEqual(block * 4L, result.Wave.FrameCount);
        var pcm = files.Read(rate, block * 4);
        for (var frame = 0; frame < block * 4; frame++)
        {
            var left = frame < block ? inputs[0][frame] : frame >= block * 2 && frame < block * 3 ? inputs[1][frame - block * 2] : (short)0;
            var offset = frame - rate / 100; var right = offset >= 0 && offset < receiveSamples.Length ? receiveSamples[offset] : (short)0;
            Assert.AreEqual(left, pcm[frame * 2]); Assert.AreEqual(right, pcm[frame * 2 + 1]);
        }
        Assert.AreEqual(2, fixture.Codec.Encodes);
    }

    [TestMethod]
    public async Task RecordingCanBeginInsideASourceG722WindowWithoutMixingUpRtpAndPcmRates()
    {
        await using var fixture = new Fixture(SDPWellKnownMediaFormatsEnum.G722); await fixture.Connect(); using var files = new RecordingFiles();
        await using var session = await Sessions().StartAsync(files.Request(16000));
        await using var binding = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, session, Anchor(fixture) with { RecordingOffset = TimeSpan.FromMilliseconds(-5) });
        var input = Samples(320, 0); fixture.Capture(input); await fixture.Packets(1); await binding.DisposeAsync();
        Assert.AreEqual(80L, binding.TrimmedSamples); await session.StopAsync(TimeSpan.FromMilliseconds(20));
        var pcm = files.Read(16000, 320);
        for (var frame = 0; frame < 320; frame++) { Assert.AreEqual(frame < 240 ? input[frame + 80] : (short)0, pcm[frame * 2]); Assert.AreEqual((short)0, pcm[frame * 2 + 1]); }
    }

    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task AFragmentedSourceBlockIsRecordedAsOneContinuousPcmWindow(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); await fixture.Connect(); using var files = new RecordingFiles();
        await using var session = await Sessions().StartAsync(files.Request(fixture.Format.ClockRate));
        await using var binding = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, session, Anchor(fixture));
        var input = Samples(fixture.Format.ClockRate / 2, 0); fixture.Capture(input);
        await binding.DisposeAsync(); await session.StopAsync(TimeSpan.FromMilliseconds(500)); var pcm = files.Read(fixture.Format.ClockRate, input.Length);
        for (var frame = 0; frame < input.Length; frame++) { Assert.AreEqual(input[frame], pcm[frame * 2]); Assert.AreEqual((short)0, pcm[frame * 2 + 1]); }
        Assert.AreEqual(1, fixture.Codec.Encodes); Assert.AreEqual(TimeSpan.FromMilliseconds(500), binding.LastWindowEnd);
    }

    [TestMethod]
    public async Task WrappedNativeSendTimestampsProduceContiguousActualWaveSamples()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        typeof(MediaStreamTrack).GetProperty("Timestamp")!.SetValue(fixture.Media.AudioStream.LocalTrack, uint.MaxValue - 159);
        await using var session = await Sessions().StartAsync(files.Request(8000));
        await using var binding = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, session, Anchor(fixture));
        var first = Samples(160, 0); var second = Samples(160, 1); fixture.Capture(first); fixture.Capture(second);
        await fixture.Packets(2); await binding.DisposeAsync(); await session.StopAsync(TimeSpan.FromMilliseconds(40));
        var expected = first.Concat(second).ToArray(); var pcm = files.Read(8000, 320);
        for (var frame = 0; frame < 320; frame++) Assert.AreEqual(expected[frame], pcm[frame * 2]);
    }

    [TestMethod]
    [DataRow("source", SentPcmRecordingFailure.SourceChanged)]
    [DataRow("clock", SentPcmRecordingFailure.ClockChanged)]
    [DataRow("future", SentPcmRecordingFailure.TimestampDiscontinuity)]
    [DataRow("duration", SentPcmRecordingFailure.MetadataUnavailable)]
    [DataRow("codec", SentPcmRecordingFailure.MetadataUnavailable)]
    public async Task InvalidSendInputSynchronouslyPreventsPartialRecordingPublication(string kind, SentPcmRecordingFailure expected)
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        await using var session = await Sessions().StartAsync(files.Request(8000));
        var anchor = Anchor(fixture);
        anchor = kind switch { "source" => anchor with { SynchronizationSource = anchor.SynchronizationSource ^ 1 }, "clock" => anchor with { RtpClockRate = 16000 }, "future" => anchor with { Timestamp = unchecked(anchor.Timestamp + 160) }, _ => anchor };
        await using var binding = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, session, anchor);
        if (kind == "duration") { var payload = fixture.Tap.EncodeAudio(Samples(160, 0), fixture.Format); fixture.Emit(320, payload); }
        else if (kind == "codec")
        {
            var error = new IOException("private-codec-detail"); fixture.Codec.Error = error;
            Assert.AreSame(error, Assert.ThrowsExactly<IOException>(() => fixture.Capture(Samples(160, 0))));
        }
        else fixture.Capture(Samples(160, 0));
        Assert.AreEqual(expected, binding.Failure); Assert.AreEqual(PcmRecordingState.Failed, session.State);
        // Stop immediately; the guard must not depend on a scheduled Completion continuation.
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(40)));
        Assert.IsFalse(File.Exists(files.Output)); await binding.DisposeAsync(); Assert.AreEqual(expected, await binding.Completion);
    }

    [TestMethod]
    public async Task FailedSourceSubscriptionRejectsTheRecordingAndDoesNotConsumeTheSource()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        await using var session = await Sessions().StartAsync(files.Request(8000)); await fixture.Tap.DisposeAsync();
        Assert.ThrowsExactly<ObjectDisposedException>(() => new SentPcmRecordingBindingFactory().Attach(fixture.Tap, session, Anchor(fixture)));
        Assert.AreEqual(PcmRecordingState.Failed, session.State);
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(20))); Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task NormalSourceClosureCompletesTheBindingAfterTheMediaBridgeDrains()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        await using var session = await Sessions().StartAsync(files.Request(8000));
        await using var binding = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, session, Anchor(fixture));
        var input = Samples(160, 0); fixture.Capture(input); await fixture.Packets(1);
        await fixture.Bridge.DisposeAsync(); await fixture.Tap.DisposeAsync(); Assert.IsNull(await binding.Completion);
        Assert.AreEqual(PcmRecordingState.Capturing, session.State); await session.StopAsync(TimeSpan.FromMilliseconds(20));
        var pcm = files.Read(8000, 160); for (var frame = 0; frame < 160; frame++) Assert.AreEqual(input[frame], pcm[frame * 2]);
    }

    [TestMethod]
    public async Task DetachKeepsTheBorrowedSourceAndRecordingAliveAndExcludesFurtherWindows()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        await using var session = await Sessions().StartAsync(files.Request(8000));
        await using var binding = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, session, Anchor(fixture));
        var input = Samples(160, 0); fixture.Capture(input); await binding.DisposeAsync(); await binding.DisposeAsync();
        fixture.Capture(Samples(160, 1)); await fixture.Packets(2); Assert.AreEqual(PcmRecordingState.Capturing, session.State);
        await session.StopAsync(TimeSpan.FromMilliseconds(40)); var pcm = files.Read(8000, 320);
        for (var frame = 0; frame < 320; frame++) Assert.AreEqual(frame < 160 ? input[frame] : (short)0, pcm[frame * 2]);
        Assert.IsNull(await binding.Completion); Assert.AreEqual(TimeSpan.FromMilliseconds(20), binding.LastWindowEnd);
    }

    [TestMethod]
    public async Task DetachWaitsForTheActiveRecordingAppendBeforeItsCompletion()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        await using var session = await Sessions().StartAsync(files.Request(8000)); using var blocked = new BlockingInput(session);
        await using var binding = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, blocked, Anchor(fixture));
        var input = Samples(160, 0); var capture = Task.Run(() => fixture.Capture(input)); Task? detach = null;
        try
        {
            Assert.IsTrue(blocked.Entered.Wait(TimeSpan.FromSeconds(10))); detach = binding.DisposeAsync().AsTask();
            Assert.IsFalse(detach.IsCompleted); Assert.IsFalse(binding.Completion.IsCompleted); Assert.AreEqual(PcmRecordingState.Capturing, session.State);
        }
        finally { blocked.Release.Set(); }
        await capture; await detach!; Assert.IsNull(await binding.Completion);
        fixture.Capture(Samples(160, 1)); await fixture.Packets(2); Assert.AreEqual(1, blocked.Appends);
        await session.StopAsync(TimeSpan.FromMilliseconds(40)); var pcm = files.Read(8000, 320);
        for (var frame = 0; frame < 320; frame++) Assert.AreEqual(frame < 160 ? input[frame] : (short)0, pcm[frame * 2]);
    }

    [TestMethod]
    public async Task RecordingPreservesItsOriginalFailureWhenTheSendBindingRejectsFurtherInput()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        await using var session = await Sessions().StartAsync(files.Request(8000));
        await using var binding = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, session, Anchor(fixture));
        ((IPcmRecordingInputGuard)session).RejectInput(PcmRecordingFailure.BufferOverflow); fixture.Capture(Samples(160, 0));
        Assert.AreEqual(PcmRecordingFailure.BufferOverflow, session.Failure); Assert.AreEqual(SentPcmRecordingFailure.RecordingRejected, binding.Failure);
        await binding.DisposeAsync(); var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.AreEqual(PcmRecordingFailure.BufferOverflow, error.Failure); Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task AlreadyFailedSendMetadataRejectsANewRecordingBeforeAttachReturns()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        await using var observer = fixture.Tap.Subscribe(new Observer());
        var payload = fixture.Tap.EncodeAudio(Samples(160, 0), fixture.Format); fixture.Emit(160, payload.ToArray());
        Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await observer.Completion);
        await using var session = await Sessions().StartAsync(files.Request(8000));
        await using var binding = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, session, Anchor(fixture));
        Assert.AreEqual(SentPcmRecordingFailure.MetadataUnavailable, binding.Failure); Assert.AreEqual(PcmRecordingState.Failed, session.State);
        await binding.DisposeAsync(); await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task ReceiveFailureBlocksPublicationOfTheSharedDuplexRecordingAndFurtherSendInput()
    {
        await using var fixture = new Fixture(); fixture.EnableReceiveBuffer(); await fixture.Connect(); using var files = new RecordingFiles();
        await using var session = await Sessions().StartAsync(files.Request(8000));
        await using var sent = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, session, Anchor(fixture));
        await using var received = await new ReceivedPcmRecordingBindingFactory(new RtpPcmRecordingFeedFactory()).AttachAsync(
            fixture.Tap, session, new(fixture.RemoteSsrc, 100, 5000, 8000, TimeSpan.Zero));
        fixture.Capture(Samples(160, 0));
        fixture.Tap.DecodeAudio(new AudioEncoder().EncodeAudio(Samples(160, 3), fixture.Format), fixture.Format);
        Assert.AreEqual(PcmRecordingState.Failed, session.State); Assert.AreEqual(ReceivedPcmRecordingFailure.MetadataUnavailable, received.Failure);
        fixture.Capture(Samples(160, 1)); Assert.AreEqual(SentPcmRecordingFailure.RecordingRejected, sent.Failure);
        await sent.DisposeAsync(); await received.DisposeAsync();
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(40))); Assert.IsFalse(File.Exists(files.Output));
    }

    private sealed class BlockingInput(IPcmRecordingSession inner) : IPcmRecordingSession, IPcmRecordingInputGuard, IDisposable
    {
        internal readonly ManualResetEventSlim Entered = new(), Release = new(); internal int Appends;
        public PcmRecordingState State => inner.State;
        public PcmRecordingFailure? Failure => inner.Failure;
        public string SegmentDirectory => inner.SegmentDirectory;
        public Task CaptureCompletion => inner.CaptureCompletion;
        public bool TryAppend(ConversationAudioSide side, TimeSpan start, int rate, ReadOnlySpan<short> samples)
        { Interlocked.Increment(ref Appends); Entered.Set(); Release.Wait(); return inner.TryAppend(side, start, rate, samples); }
        public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken token = default) => inner.StopAsync(duration, token);
        public void RejectInput(PcmRecordingFailure failure) => ((IPcmRecordingInputGuard)inner).RejectInput(failure);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Dispose() { Release.Set(); Entered.Dispose(); Release.Dispose(); }
    }

    private static SentPcmTimelineAnchor Anchor(Fixture fixture) => new(fixture.Media.AudioStream.LocalTrack.Ssrc, fixture.Media.AudioStream.LocalTrack.Timestamp, 8000, TimeSpan.Zero);
    private static IPcmRecordingSessionFactory Sessions() => new PcmRecordingSessionFactory(new CscorePcmSegmentSinkFactory(), new ConversationWaveMixer(new ConversationWaveRenderer()));
    private sealed class RecordingFiles : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-sent-binding-" + Guid.NewGuid().ToString("N"));
        internal string Output => Path.Combine(_root, "recorded.wav");
        internal RecordingFiles() => Directory.CreateDirectory(_root);
        internal PcmRecordingRequest Request(int rate) => new(Guid.NewGuid(), Guid.NewGuid(), Output, rate);
        internal short[] Read(int rate, int frames)
        {
            using var reader = new WaveFileReader(Output); Assert.AreEqual(rate, reader.WaveFormat.SampleRate); Assert.AreEqual(2, reader.WaveFormat.Channels);
            Assert.AreEqual(frames * 4L, reader.Length); var bytes = new byte[reader.Length]; Assert.AreEqual(bytes.Length, reader.Read(bytes, 0, bytes.Length));
            return Enumerable.Range(0, bytes.Length / 2).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2, 2))).ToArray();
        }
        public void Dispose()
        {
            foreach (var directory in Directory.GetDirectories(_root)) { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
            foreach (var file in Directory.GetFiles(_root)) File.Delete(file); Directory.Delete(_root);
        }
    }
}
