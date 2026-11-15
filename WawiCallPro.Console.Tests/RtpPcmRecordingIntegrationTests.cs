using System.Buffers.Binary;
using CSCore.Codecs.WAV;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Audio;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class RtpPcmRecordingIntegrationTests
{
    [TestMethod]
    public async Task HostFeedsPreserveRtpGapsWrapsAndTheSharedRecordingEpochInActualWaveSamples()
    {
        using var files = new Files(); using var host = ConsoleCompositionRoot.Build(loadPlugins: false);
        var sessions = host.Services.GetRequiredService<IPcmRecordingSessionFactory>();
        var feeds = host.Services.GetRequiredService<IRtpPcmRecordingFeedFactory>(); Assert.IsInstanceOfType<RtpPcmRecordingFeedFactory>(feeds);
        Assert.AreSame(feeds, host.Services.GetRequiredService<IRtpPcmRecordingFeedFactory>());
        await using var session = await sessions.StartAsync(files.Request());
        var anchor = new RtpPcmTimelineAnchor(17, ushort.MaxValue, uint.MaxValue - 159, 8000, TimeSpan.Zero);
        await using var sent = feeds.Create(session, ConversationAudioSide.Sent, anchor);
        await using var received = feeds.Create(session, ConversationAudioSide.Received, new(91, 100, 8000, 8000, TimeSpan.FromMilliseconds(10)));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, sent.TryAppend(new(17, ushort.MaxValue, anchor.Timestamp, 8000), 8000, Samples(160, 1000)));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, received.TryAppend(new(91, 100, 8000, 8000), 8000, Samples(160, 2000)));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, sent.TryAppend(new(17, 0, 160, 8000), 8000, Samples(160, 3000)));
        Assert.AreEqual(RtpPcmAppendResult.Duplicate, sent.TryAppend(new(17, 0, 160, 8000), 8000, Samples(160, 3000)));
        await sent.DisposeAsync(); await received.DisposeAsync();
        var result = await session.StopAsync(TimeSpan.FromMilliseconds(100)); Assert.AreEqual(800L, result.Wave.FrameCount);
        var pcm = Read(files.Output, 8000, 2, 800);
        for (var frame = 0; frame < 800; frame++)
        {
            Assert.AreEqual(frame < 160 ? (short)1000 : frame is >= 320 and < 480 ? (short)3000 : (short)0, pcm[frame * 2]);
            Assert.AreEqual(frame is >= 80 and < 240 ? (short)2000 : (short)0, pcm[frame * 2 + 1]);
        }
        Assert.AreEqual(TimeSpan.FromMilliseconds(60), sent.LastPacketEnd); Assert.AreEqual(1L, sent.DuplicatePackets);
    }

    [TestMethod]
    public async Task G722PcmWithAnExplicitMidPacketAnchorBeginsAtTheCorrectSample()
    {
        using var files = new Files(); await using var session = await Sessions().StartAsync(files.Request() with { SampleRate = 16000 });
        await using var feed = new RtpPcmRecordingFeedFactory().Create(session, ConversationAudioSide.Received,
            new(17, 10, 1000, 8000, TimeSpan.FromMilliseconds(-5)));
        var input = Enumerable.Range(0, 320).Select(value => (short)value).ToArray();
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(new(17, 10, 1000, 8000), 16000, input));
        await feed.DisposeAsync(); await session.StopAsync(TimeSpan.FromMilliseconds(20));
        var pcm = Read(files.Output, 16000, 2, 320);
        for (var frame = 0; frame < 320; frame++) { Assert.AreEqual((short)0, pcm[frame * 2]); Assert.AreEqual(frame < 240 ? (short)(80 + frame) : (short)0, pcm[frame * 2 + 1]); }
        Assert.AreEqual(80L, feed.TrimmedSamples);
    }

    [TestMethod]
    public async Task TwoSecondPcmBlocksReachTheRecordingThroughBoundedChunks()
    {
        using var files = new Files(); await using var session = await Sessions().StartAsync(files.Request() with { SampleRate = 16000 });
        await using var feed = new RtpPcmRecordingFeedFactory().Create(session, ConversationAudioSide.Sent, new(17, 10, 1000, 8000, TimeSpan.Zero));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(new(17, 10, 1000, 8000), 16000, Samples(32000, 1234)));
        await feed.DisposeAsync(); var result = await session.StopAsync(TimeSpan.FromSeconds(2));
        Assert.AreEqual(32000L, result.Wave.FrameCount); Assert.IsNull(feed.Failure);
        var pcm = Read(files.Output, 16000, 2, 32000);
        for (var frame = 0; frame < 32000; frame++) { Assert.AreEqual((short)1234, pcm[frame * 2]); Assert.AreEqual((short)0, pcm[frame * 2 + 1]); }
    }

    [TestMethod]
    public async Task TimestampConflictPreventsTheActualSessionFromPublishingAPartialRecording()
    {
        using var files = new Files(); await using var session = await Sessions().StartAsync(files.Request());
        await using var feed = new RtpPcmRecordingFeedFactory().Create(session, ConversationAudioSide.Sent, new(17, 10, 1000, 8000, TimeSpan.Zero));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(new(17, 10, 1000, 8000), 8000, Samples(160, 1234)));
        Assert.AreEqual(RtpPcmAppendResult.Failed, feed.TryAppend(new(17, 11, 1080, 8000), 8000, Samples(160, 1234)));
        await feed.DisposeAsync();
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(50)));
        Assert.AreEqual(PcmRecordingFailure.TimelineConflict, error.Failure); Assert.AreEqual(RtpPcmFeedFailure.Overlap, feed.Failure);
        Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task BufferOverflowKeepsItsOriginalRecordingFailureThroughTheFeed()
    {
        using var files = new Files(); using var sinks = new BlockingSinks();
        var sessions = new PcmRecordingSessionFactory(sinks, new ConversationWaveMixer(new ConversationWaveRenderer()));
        await using var session = await sessions.StartAsync(files.Request() with { QueueCapacity = 1 });
        await using var feed = new RtpPcmRecordingFeedFactory().Create(session, ConversationAudioSide.Sent, new(17, 10, 1000, 8000, TimeSpan.Zero));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(new(17, 10, 1000, 8000), 8000, Samples(800, 1234)));
        try
        {
            await sinks.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(RtpPcmAppendResult.Failed, feed.TryAppend(new(17, 11, 1800, 8000), 8000, Samples(16000, 1234)));
            Assert.AreEqual(RtpPcmFeedFailure.RecordingRejected, feed.Failure); Assert.AreEqual(PcmRecordingFailure.BufferOverflow, session.Failure);
        }
        finally { sinks.Release.Set(); }
        await feed.DisposeAsync();
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromSeconds(3)));
        Assert.AreEqual(PcmRecordingFailure.BufferOverflow, error.Failure); Assert.IsFalse(File.Exists(files.Output));
    }

    private static IPcmRecordingSessionFactory Sessions() => new PcmRecordingSessionFactory(new CscorePcmSegmentSinkFactory(), new ConversationWaveMixer(new ConversationWaveRenderer()));
    private static short[] Samples(int count, int value) => Enumerable.Repeat((short)value, count).ToArray();
    private static short[] Read(string path, int rate, int channels, int frames)
    {
        using var reader = new WaveFileReader(path); Assert.AreEqual(rate, reader.WaveFormat.SampleRate); Assert.AreEqual(channels, reader.WaveFormat.Channels);
        Assert.AreEqual(frames * channels * 2L, reader.Length); var bytes = new byte[reader.Length]; Assert.AreEqual(bytes.Length, reader.Read(bytes, 0, bytes.Length));
        return Enumerable.Range(0, bytes.Length / 2).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2, 2))).ToArray();
    }
    private sealed class BlockingSinks : IPcmSegmentSinkFactory, IDisposable
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new();
        public IPcmSegmentSink Open(string path, int rate) => new Sink(new CscorePcmSegmentSinkFactory().Open(path, rate), this);
        private sealed class Sink(IPcmSegmentSink inner, BlockingSinks owner) : IPcmSegmentSink
        {
            public void Write(ReadOnlySpan<short> samples) { owner.Entered.TrySetResult(); owner.Release.Wait(); inner.Write(samples); }
            public void Complete() => inner.Complete();
            public void Dispose() => inner.Dispose();
        }
        public void Dispose() { Release.Set(); Release.Dispose(); }
    }
    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "WawiCallPro-rtp-feed-" + Guid.NewGuid().ToString("N"));
        internal string Output => Path.Combine(Root, "recorded.wav");
        internal Files() => Directory.CreateDirectory(Root);
        internal PcmRecordingRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(), Output, SampleRate: 8000);
        public void Dispose()
        {
            foreach (var child in Directory.GetDirectories(Root)) { foreach (var file in Directory.GetFiles(child)) File.Delete(file); Directory.Delete(child); }
            foreach (var file in Directory.GetFiles(Root)) File.Delete(file); Directory.Delete(Root);
        }
    }
}
