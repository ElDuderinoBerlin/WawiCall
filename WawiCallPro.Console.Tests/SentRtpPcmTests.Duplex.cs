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
    public async Task OwnedDuplexRecordingStartsAndStopsBothDirectionsInOneSampleExactWave(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); fixture.EnableReceiveBuffer(); await fixture.Connect(); using var files = new RecordingFiles();
        using var host = ConsoleCompositionRoot.Build(loadPlugins: false); var factory = host.Services.GetRequiredService<IDuplexPcmRecordingFactory>();
        Assert.IsInstanceOfType<DuplexPcmRecordingFactory>(factory); Assert.AreSame(factory, host.Services.GetRequiredService<IDuplexPcmRecordingFactory>());
        await using var recording = await factory.StartAsync(fixture.Tap, DuplexRequest(fixture, files));
        var rate = fixture.Format.ClockRate; var block = rate / 50; var inputs = new[] { Samples(block, 0), Samples(block, 1) };
        fixture.Capture(inputs[0]); fixture.EmitExtras(160, new AudioEncoder().EncodeAudio(Samples(block, 2), fixture.Format)); fixture.Capture(inputs[1]);
        await fixture.Packets(3);
        var arrived = 0; var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Media.OnRtpPacketReceived += (_, type, _) => { if (type == SDPMediaTypesEnum.audio && Interlocked.Increment(ref arrived) == 3) complete.TrySetResult(); };
        var encoder = new AudioEncoder(); var decoder = new AudioEncoder();
        var payloads = Enumerable.Range(0, 3).Select(index => encoder.EncodeAudio(Samples(block, index + 5), fixture.Format)).ToArray();
        var expectedReceive = payloads.SelectMany(payload => decoder.DecodeAudio(payload, fixture.Format)).ToArray();
        foreach (var index in new[] { 0, 2, 1 }) fixture.SendRemote((ushort)(100 + index), (uint)(5000 + index * 160), payloads[index]);
        await complete.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var stop = recording.StopAsync(TimeSpan.FromMilliseconds(80)); Assert.AreSame(stop, recording.StopAsync(TimeSpan.FromMilliseconds(100)));
        var result = await stop; Assert.AreEqual(block * 4L, result.Wave.FrameCount); Assert.AreEqual(PcmRecordingState.Completed, recording.State);
        Assert.IsTrue(recording.InputCompletion.IsCompleted); var pcm = files.Read(rate, block * 4);
        for (var frame = 0; frame < block * 4; frame++)
        {
            var left = frame < block ? inputs[0][frame] : frame >= block * 2 && frame < block * 3 ? inputs[1][frame - block * 2] : (short)0;
            var offset = frame - rate / 100; var right = offset >= 0 && offset < expectedReceive.Length ? expectedReceive[offset] : (short)0;
            Assert.AreEqual(left, pcm[frame * 2]); Assert.AreEqual(right, pcm[frame * 2 + 1]);
        }
        var saved = File.ReadAllBytes(files.Output); fixture.Capture(Samples(block, 9)); await fixture.Packets(1);
        CollectionAssert.AreEqual(saved, File.ReadAllBytes(files.Output));
    }

    [TestMethod]
    public async Task DisposingAnActiveOwnedRecordingAbortsWithoutPublishingAWave()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        var recording = await DuplexFactory().StartAsync(fixture.Tap, DuplexRequest(fixture, files)); fixture.Capture(Samples(160, 0));
        await recording.DisposeAsync(); await recording.DisposeAsync();
        Assert.AreEqual(PcmRecordingState.Aborted, recording.State); Assert.IsTrue(recording.InputCompletion.IsCompleted); Assert.IsFalse(File.Exists(files.Output));
        fixture.Capture(Samples(160, 1)); await fixture.Packets(2);
    }

    [TestMethod]
    public async Task CanceledOwnedStopPublishesNoWaveAndRemainsObservableOnDispose()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        var recording = await DuplexFactory().StartAsync(fixture.Tap, DuplexRequest(fixture, files)); fixture.Capture(Samples(160, 0));
        var stop = recording.StopAsync(TimeSpan.FromMilliseconds(20), new CancellationToken(true));
        await Assert.ThrowsAsync<OperationCanceledException>(() => stop);
        await Assert.ThrowsAsync<OperationCanceledException>(() => recording.DisposeAsync().AsTask());
        Assert.AreEqual(PcmRecordingState.Aborted, recording.State); Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task MediaFailureIsAppliedToTheOwnedRecordingBeforeAnImmediateStop()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        var recording = await DuplexFactory().StartAsync(fixture.Tap, DuplexRequest(fixture, files)); fixture.Capture(Samples(160, 0));
        fixture.Tap.DecodeAudio(new AudioEncoder().EncodeAudio(Samples(160, 1), fixture.Format), fixture.Format);
        Assert.AreEqual(PcmRecordingState.Failed, recording.State);
        var failure = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(40)));
        Assert.AreEqual(PcmRecordingFailure.TimelineConflict, failure.Failure); Assert.IsFalse(File.Exists(files.Output));
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.DisposeAsync().AsTask());
    }

    [TestMethod]
    public async Task SourceClosureDrainsInputAndStillRequiresAnExplicitOwnedStop()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        await using var recording = await DuplexFactory().StartAsync(fixture.Tap, DuplexRequest(fixture, files)); var input = Samples(160, 0);
        fixture.Capture(input); await fixture.Bridge.DisposeAsync(); await fixture.Tap.DisposeAsync(); await recording.InputCompletion;
        Assert.AreEqual(PcmRecordingState.Capturing, recording.State); Assert.IsFalse(File.Exists(files.Output));
        await recording.StopAsync(TimeSpan.FromMilliseconds(20)); var pcm = files.Read(8000, 160);
        for (var frame = 0; frame < 160; frame++) Assert.AreEqual(input[frame], pcm[frame * 2]);
    }

    [TestMethod]
    public async Task StartupAgainstAClosedSourceRollsBackWithoutPublishingAMix()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles(); await fixture.Tap.DisposeAsync();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => DuplexFactory().StartAsync(fixture.Tap, DuplexRequest(fixture, files)));
        Assert.IsFalse(File.Exists(files.Output));
        foreach (var path in Directory.GetFiles(Path.GetDirectoryName(files.Output)!, "*.wav", SearchOption.AllDirectories))
        { using var reader = new WaveFileReader(path); Assert.IsTrue(reader.Length >= 0); }
    }

    [TestMethod]
    public async Task InvalidAnchorCreatesNoSegmentDirectoryAndLeavesTheSourceUsable()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles(); var request = DuplexRequest(fixture, files);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => DuplexFactory().StartAsync(fixture.Tap, request with { Sent = request.Sent with { RtpClockRate = 0 } }));
        Assert.AreEqual(0, Directory.GetDirectories(Path.GetDirectoryName(files.Output)!).Length); Assert.IsFalse(File.Exists(files.Output));
        fixture.Capture(Samples(160, 0)); await fixture.Packets(1);
    }

    [TestMethod]
    public async Task InvalidWavDurationDoesNotDetachAnActualRecording()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles(); var request = DuplexRequest(fixture, files);
        await using var recording = await DuplexFactory().StartAsync(fixture.Tap, request with { Recording = request.Recording with { SampleRate = 48000 } });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => recording.StopAsync(TimeSpan.FromTicks(1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => recording.StopAsync(TimeSpan.FromHours(8)));
        Assert.AreEqual(PcmRecordingState.Capturing, recording.State); fixture.Capture(Samples(160, 0));
        await recording.DisposeAsync(); Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task FailedPublicationKeepsTheExistingDestinationAndReleasesTheOwnedRecording()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles(); var original = new byte[] { 1, 3, 7 };
        var recording = await DuplexFactory().StartAsync(fixture.Tap, DuplexRequest(fixture, files)); fixture.Capture(Samples(160, 0));
        File.WriteAllBytes(files.Output, original); // A competing writer creates the destination after startup.
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.AreEqual(PcmRecordingFailure.MixingFailed, error.Failure); Assert.AreEqual(PcmRecordingState.Failed, recording.State);
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.DisposeAsync().AsTask()); CollectionAssert.AreEqual(original, File.ReadAllBytes(files.Output));
    }

    [TestMethod]
    public async Task OneFreeLiveObserverSlotIsEnoughForAnEntireDuplexRecording()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles();
        var observers = Enumerable.Range(0, 7).Select(_ => fixture.Tap.Subscribe(new Passive())).ToArray();
        try
        {
            await using var recording = await DuplexFactory().StartAsync(fixture.Tap, DuplexRequest(fixture, files)); fixture.Capture(Samples(160, 0));
            await recording.StopAsync(TimeSpan.FromMilliseconds(20)); Assert.AreEqual(PcmRecordingState.Completed, recording.State); files.Read(8000, 160);
        }
        finally { foreach (var observer in observers) await observer.DisposeAsync(); }
    }

    [TestMethod]
    public async Task LifetimeCancellationCanBeDrainedByOwnedAbortWithoutPublishing()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new RecordingFiles(); using var cancellation = new CancellationTokenSource();
        var recording = await DuplexFactory().StartAsync(fixture.Tap, DuplexRequest(fixture, files), cancellation.Token);
        fixture.Capture(Samples(160, 0)); cancellation.Cancel(); await recording.DisposeAsync();
        Assert.AreEqual(PcmRecordingState.Aborted, recording.State); Assert.IsTrue(recording.InputCompletion.IsCompleted); Assert.IsFalse(File.Exists(files.Output));
    }

    private static IDuplexPcmRecordingFactory DuplexFactory() => new DuplexPcmRecordingFactory(Sessions(), new SentPcmRecordingBindingFactory(), new ReceivedPcmRecordingBindingFactory(new RtpPcmRecordingFeedFactory()));
    private static DuplexPcmRecordingRequest DuplexRequest(Fixture fixture, RecordingFiles files) => new(files.Request(fixture.Format.ClockRate),
        Anchor(fixture), new(fixture.RemoteSsrc, 100, 5000, 8000, TimeSpan.FromMilliseconds(10)));
}
