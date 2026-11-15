using System.Buffers.Binary;
using CSCore.Codecs.WAV;
using Microsoft.Extensions.DependencyInjection;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class RtpAudioPacketBufferIntegrationTests
{
    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task ReorderingPrecedesTheActualStatefulDecoderAndProducesBitIdenticalRecordedPcm(SDPWellKnownMediaFormatsEnum codec)
    {
        var format = new AudioFormat(codec); var encoder = new AudioEncoder(); var baseline = new AudioEncoder();
        var encoded = Enumerable.Range(0, 5).Select(index => encoder.EncodeAudio(Input(format.ClockRate / 50, index), format)).ToArray();
        var expected = encoded.SelectMany(bytes => baseline.DecodeAudio(bytes, format)).ToArray();
        using var files = new Files(); await using var session = await Sessions().StartAsync(files.Request(format.ClockRate));
        var anchor = new RtpPcmTimelineAnchor(17, ushort.MaxValue - 1, uint.MaxValue - 159, 8000, TimeSpan.Zero);
        using var buffer = new RtpAudioPacketBufferFactory(new Clock()).Create(new(17, anchor.SequenceNumber, 8000));
        await using var feed = new RtpPcmRecordingFeedFactory().Create(session, ConversationAudioSide.Received, anchor);
        await using var decoder = new PcmObservingAudioEncoder(new AudioEncoder());
        var observed = new Observer(); await using var subscription = decoder.Subscribe(observed);
        RtpPcmPacketPosition Position(int index) => new(17, unchecked((ushort)(anchor.SequenceNumber + index)), unchecked(anchor.Timestamp + (uint)(index * 160)), 8000);
        RtpAudioEnqueueResult Enqueue(int index) => buffer.TryEnqueue(Position(index), format.FormatID, encoded[index]);
        void DecodeAvailable()
        {
            while (buffer.TryDequeue(out var packet))
            {
                Assert.AreEqual(format.FormatID, packet.PayloadType);
                var pcm = decoder.DecodeAudio(packet.Payload.ToArray(), format);
                Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(packet.Position, format.ClockRate, pcm));
            }
        }
        Enqueue(2); Enqueue(0); Enqueue(4); DecodeAvailable(); Assert.AreEqual(1, observed.Calls);
        Assert.AreEqual(RtpAudioEnqueueResult.Duplicate, Enqueue(2)); Enqueue(1); DecodeAvailable(); Assert.AreEqual(3, observed.Calls);
        Assert.AreEqual(RtpAudioEnqueueResult.Duplicate, Enqueue(4)); Enqueue(3); DecodeAvailable(); Assert.AreEqual(5, observed.Calls);
        Assert.AreEqual(RtpAudioEnqueueResult.Late, Enqueue(0)); DecodeAvailable(); Assert.AreEqual(5, observed.Calls);
        Assert.AreEqual(2L, buffer.DuplicatePackets); Assert.AreEqual(1L, buffer.LatePackets); Assert.AreEqual(0L, buffer.MissingPackets);
        Assert.IsNull(buffer.Failure); Assert.IsNull(feed.Failure);
        buffer.CompleteInput(); await feed.DisposeAsync(); await session.StopAsync(TimeSpan.FromMilliseconds(100));
        CollectionAssert.AreEqual(expected, ReadRightChannel(files.Output, format.ClockRate, expected.Length));
    }

    [TestMethod]
    public async Task DeadlineSkipsMissingEncodedPacketsWhileOriginalRtpTimeProducesSilenceInTheWave()
    {
        var format = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMA); var encoder = new AudioEncoder(); var baseline = new AudioEncoder();
        var first = encoder.EncodeAudio(Input(160, 0), format); var third = encoder.EncodeAudio(Input(160, 2), format);
        var expectedFirst = baseline.DecodeAudio(first, format); var expectedThird = baseline.DecodeAudio(third, format);
        var clock = new Clock(); using var buffer = new RtpAudioPacketBufferFactory(clock).Create(new(17, 10, 8000));
        using var files = new Files(); await using var session = await Sessions().StartAsync(files.Request(8000));
        await using var feed = new RtpPcmRecordingFeedFactory().Create(session, ConversationAudioSide.Received, new(17, 10, 1000, 8000, TimeSpan.Zero));
        var decoder = new AudioEncoder(); var decoded = 0;
        void DecodeAvailable()
        {
            while (buffer.TryDequeue(out var packet))
            {
                var pcm = decoder.DecodeAudio(packet.Payload.ToArray(), format); decoded++;
                Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(packet.Position, format.ClockRate, pcm));
            }
        }
        buffer.TryEnqueue(new(17, 10, 1000, 8000), format.FormatID, first); DecodeAvailable();
        buffer.TryEnqueue(new(17, 12, 1320, 8000), format.FormatID, third); DecodeAvailable(); Assert.AreEqual(1, decoded);
        clock.Advance(TimeSpan.FromMilliseconds(60)); DecodeAvailable(); Assert.AreEqual(2, decoded); Assert.AreEqual(1L, buffer.MissingPackets);
        Assert.AreEqual(RtpAudioEnqueueResult.Late, buffer.TryEnqueue(new(17, 11, 1160, 8000), format.FormatID, first));
        await feed.DisposeAsync(); await session.StopAsync(TimeSpan.FromMilliseconds(60));
        CollectionAssert.AreEqual(expectedFirst.Concat(new short[160]).Concat(expectedThird).ToArray(), ReadRightChannel(files.Output, 8000, 480));
    }

    [TestMethod]
    public void BufferedPacketsRetainTheirOwnPayloadTypesAcrossNegotiatedCodecChanges()
    {
        var formats = new[] { new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMA), new AudioFormat(SDPWellKnownMediaFormatsEnum.G722), new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMU) };
        var encoder = new AudioEncoder(); var baseline = new AudioEncoder(); var decoder = new AudioEncoder();
        var encoded = formats.Select((format, index) => encoder.EncodeAudio(Input(format.ClockRate / 50, index), format)).ToArray();
        var expected = formats.Select((format, index) => baseline.DecodeAudio(encoded[index], format)).ToArray();
        var clock = new Clock(); using var buffer = new RtpAudioPacketBufferFactory(clock).Create(new(17, 10, 8000));
        foreach (var index in new[] { 2, 0, 1 }) buffer.TryEnqueue(new(17, (ushort)(10 + index), (uint)(1000 + index * 160), 8000), formats[index].FormatID, encoded[index]);
        for (var index = 0; index < 3; index++)
        {
            Assert.IsTrue(buffer.TryDequeue(out var packet)); var format = formats.Single(candidate => candidate.FormatID == packet.PayloadType);
            var actual = decoder.DecodeAudio(packet.Payload.ToArray(), format);
            Assert.AreEqual(formats[index].ClockRate, format.ClockRate); CollectionAssert.AreEqual(expected[index], actual);
        }
        Assert.IsFalse(buffer.TryDequeue(out _));
    }

    [TestMethod]
    public async Task PacketBufferFailureMustBeForwardedBeforeARecordingCanBeStopped()
    {
        using var files = new Files(); await using var session = await Sessions().StartAsync(files.Request(8000));
        await using var feed = new RtpPcmRecordingFeedFactory().Create(session, ConversationAudioSide.Received, new(17, 10, 1000, 8000, TimeSpan.Zero));
        using var buffer = new RtpAudioPacketBufferFactory(new Clock()).Create(new(17, 10, 8000, Capacity: 1));
        var format = new AudioFormat(SDPWellKnownMediaFormatsEnum.PCMA); var encoder = new AudioEncoder(); var decoder = new AudioEncoder();
        var encoded = encoder.EncodeAudio(Input(160, 0), format); buffer.TryEnqueue(new(17, 10, 1000, 8000), format.FormatID, encoded);
        Assert.IsTrue(buffer.TryDequeue(out var packet)); feed.TryAppend(packet.Position, format.ClockRate, decoder.DecodeAudio(packet.Payload.ToArray(), format));
        buffer.TryEnqueue(new(17, 12, 1320, 8000), format.FormatID, encoded);
        Assert.AreEqual(RtpAudioEnqueueResult.Failed, buffer.TryEnqueue(new(17, 13, 1480, 8000), format.FormatID, encoded));
        Assert.AreEqual(RtpAudioBufferFailure.Overflow, buffer.Failure);
        // The future live pipeline owns this relay; the independent buffer never owns a recording session.
        ((IPcmRecordingInputGuard)session).RejectInput(PcmRecordingFailure.TimelineConflict);
        await feed.DisposeAsync(); var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(80)));
        Assert.AreEqual(PcmRecordingFailure.TimelineConflict, error.Failure); Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public void BothHostRegistrationAndAnExplicitDeadlineClockRemainAvailableWithoutNativeWork()
    {
        using var host = ConsoleCompositionRoot.Build(loadPlugins: false);
        var factory = host.Services.GetRequiredService<IRtpAudioPacketBufferFactory>(); Assert.IsInstanceOfType<RtpAudioPacketBufferFactory>(factory);
        Assert.AreSame(factory, host.Services.GetRequiredService<IRtpAudioPacketBufferFactory>());
        var clock = new Clock(); var services = new ServiceCollection(); services.AddSingleton<TimeProvider>(clock); services.AddAudioConversion();
        using var provider = services.BuildServiceProvider(); Assert.AreSame(clock, provider.GetRequiredService<TimeProvider>());
        using var buffer = provider.GetRequiredService<IRtpAudioPacketBufferFactory>().Create(new(17, 10, 8000));
        buffer.TryEnqueue(new(17, 11, 1160, 8000), 8, [1]); clock.Advance(TimeSpan.FromMilliseconds(60)); Assert.IsTrue(buffer.TryDequeue(out _));
    }

    private static IPcmRecordingSessionFactory Sessions() => new PcmRecordingSessionFactory(new CscorePcmSegmentSinkFactory(), new ConversationWaveMixer(new ConversationWaveRenderer()));
    private static short[] Input(int count, int block) => Enumerable.Range(block * count, count).Select(index => (short)(Math.Sin(2 * Math.PI * index / 29) * 10000)).ToArray();
    private static short[] ReadRightChannel(string path, int rate, int frames)
    {
        using var reader = new WaveFileReader(path); Assert.AreEqual(rate, reader.WaveFormat.SampleRate); Assert.AreEqual(2, reader.WaveFormat.Channels);
        Assert.AreEqual(frames * 4L, reader.Length); var bytes = new byte[reader.Length]; Assert.AreEqual(bytes.Length, reader.Read(bytes, 0, bytes.Length));
        for (var index = 0; index < frames; index++) Assert.AreEqual((short)0, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 4, 2)));
        return Enumerable.Range(0, frames).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 4 + 2, 2))).ToArray();
    }
    private sealed class Observer : IConversationPcmObserver
    {
        internal int Calls;
        public bool TryObserve(ConversationAudioSide side, int sampleRate, int channels, ReadOnlySpan<short> samples)
        { Assert.AreEqual(ConversationAudioSide.Received, side); Calls++; return true; }
    }
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }
    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "WawiCallPro-rtp-order-" + Guid.NewGuid().ToString("N"));
        internal string Output => Path.Combine(Root, "recorded.wav");
        internal Files() => Directory.CreateDirectory(Root);
        internal PcmRecordingRequest Request(int rate) => new(Guid.NewGuid(), Guid.NewGuid(), Output, SampleRate: rate);
        public void Dispose()
        {
            foreach (var child in Directory.GetDirectories(Root)) { foreach (var file in Directory.GetFiles(child)) File.Delete(file); Directory.Delete(child); }
            foreach (var file in Directory.GetFiles(Root)) File.Delete(file); Directory.Delete(Root);
        }
    }
}
