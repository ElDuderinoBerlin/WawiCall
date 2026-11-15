using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using CSCore;
using Microsoft.Extensions.DependencyInjection;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows.CSCore;
using WawiCallPro.Audio;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Telephony.Sip;
using SIPSorcery.SIP.App;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed partial class ReceivedRtpPcmTests
{
    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task NativeRtpDispatchReachesTheActualCscoreSinkAndSingleDecoderWithExactHeadersAndSamples(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); var timed = new Observer(); var passive = new PassiveObserver();
        await using var timedSubscription = fixture.Tap.Subscribe(timed); await using var plainSubscription = fixture.Tap.Subscribe(passive);
        var inputEncoder = new AudioEncoder(); var baseline = new AudioEncoder(); var expected = new List<short>();
        for (var index = 0; index < 3; index++)
        {
            var input = Samples(fixture.Format.ClockRate / 50, index);
            var encoded = inputEncoder.EncodeAudio(input, fixture.Format); var decoded = baseline.DecodeAudio(encoded, fixture.Format); expected.AddRange(decoded);
            var position = new RtpPcmPacketPosition(17, unchecked((ushort)(ushort.MaxValue + index)), unchecked(uint.MaxValue - 159 + (uint)(index * 160)), 8000);
            fixture.Dispatch(position, encoded);
            var frame = timed.Frames.Last(); Assert.AreEqual(position, frame.Position); Assert.AreEqual(fixture.Format.ClockRate, frame.Rate); Assert.AreEqual(1, frame.Channels);
            CollectionAssert.AreEqual(decoded, frame.Samples);
        }
        Assert.AreEqual(3, fixture.Decoder.Decodes); Assert.AreEqual(3, passive.Frames.Count); Assert.AreEqual(3, timed.Frames.Count);
        Assert.IsNull(fixture.Tap.LastFailure); CollectionAssert.AreEqual(expected.ToArray(), fixture.ReadPlayback(expected.Count));
        fixture.Tap.EncodeAudio(Samples(fixture.Format.ClockRate / 50, 4), fixture.Format);
        Assert.AreEqual(1, timed.Sent); Assert.AreEqual(4, passive.Frames.Count);
    }

    [TestMethod]
    public async Task PayloadOrFormatMismatchRetiresOnlyRtpObserversAndPreservesOriginalPlayback()
    {
        await using var fixture = new Fixture(); var timed = new Observer(); var plain = new PassiveObserver();
        await using var subscription = fixture.Tap.Subscribe(timed); await using var passive = fixture.Tap.Subscribe(plain);
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Frame(new(0, fixture.Format, 20, payload));
        fixture.Packet(new(17, 10, 1000, 8000), payload.ToArray()); // Equal bytes, but not the native packet's payload instance.
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(0, timed.Frames.Count); Assert.AreEqual(1, plain.Frames.Count); Assert.AreEqual(1, fixture.Decoder.Decodes);
        fixture.Dispatch(new(17, 11, 1160, 8000), payload); Assert.AreEqual(2, plain.Frames.Count); Assert.AreEqual(2, fixture.Decoder.Decodes);
        await using var later = fixture.Tap.Subscribe(new Observer()); Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await later.Completion);
    }

    [TestMethod]
    public async Task MissingHeaderDoesNotGrowAQueueOrInventARecordingTimestamp()
    {
        await using var fixture = new Fixture(); var timed = new Observer(); var plain = new PassiveObserver();
        await using var subscription = fixture.Tap.Subscribe(timed); await using var passive = fixture.Tap.Subscribe(plain);
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Frame(new(0, fixture.Format, 20, payload)); fixture.Frame(new(0, fixture.Format, 20, payload));
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(1, plain.Frames.Count); Assert.AreEqual(0, timed.Frames.Count);
        fixture.Packet(new(17, 11, 1160, 8000), payload); Assert.AreEqual(2, plain.Frames.Count);
    }

    [TestMethod]
    public async Task DirectDecodeHasNoPacketContextAndCannotMasqueradeAsTimedReceivePcm()
    {
        await using var fixture = new Fixture(); var timed = new Observer(); await using var subscription = fixture.Tap.Subscribe(timed);
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Tap.DecodeAudio(payload, fixture.Format);
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion); Assert.AreEqual(0, timed.Frames.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RtpObserverRejectionAndExceptionsRemainIsolatedFromTheExistingSink(bool throws)
    {
        await using var fixture = new Fixture(); var plain = new PassiveObserver(); var timed = new Observer { Behavior = () => throws ? throw new IOException("private-consumer-detail") : false };
        await using var subscription = fixture.Tap.Subscribe(timed); await using var passive = fixture.Tap.Subscribe(plain);
        fixture.Dispatch(new(17, 10, 1000, 8000), new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format));
        Assert.AreEqual(throws ? PcmObservationFailure.ObserverFailed : PcmObservationFailure.ObserverRejected, await subscription.Completion);
        Assert.AreEqual(1, plain.Frames.Count); Assert.AreEqual(1, fixture.Decoder.Decodes); Assert.AreEqual(160, fixture.ReadPlayback(160).Length);
    }

    [TestMethod]
    public async Task NativeCodecFailureRetiresTimedCaptureAndKeepsTheOriginalException()
    {
        await using var fixture = new Fixture(); await using var subscription = fixture.Tap.Subscribe(new Observer());
        var failure = new IOException("private-codec-detail"); fixture.Decoder.Error = failure;
        Assert.AreSame(failure, Assert.ThrowsExactly<IOException>(() => fixture.Dispatch(new(17, 10, 1000, 8000), [1, 2])));
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
    }

    [TestMethod]
    public async Task DisposingTheBindingWaitsForTheCurrentSinkAndRejectsFurtherPackets()
    {
        await using var fixture = new Fixture(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var observer = new Observer { Behavior = () => { entered.Set(); release.Wait(); return true; } };
        await using var subscription = fixture.Tap.Subscribe(observer);
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        var dispatch = Task.Run(() => fixture.Dispatch(new(17, 10, 1000, 8000), payload));
        Task stop;
        try { Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10))); stop = fixture.Bridge.DisposeAsync().AsTask(); Assert.IsFalse(stop.IsCompleted); }
        finally { release.Set(); }
        await dispatch; await stop; fixture.Dispatch(new(17, 11, 1160, 8000), payload);
        Assert.AreEqual(1, fixture.Decoder.Decodes); Assert.AreEqual(1, observer.Frames.Count);
    }

    [TestMethod]
    public async Task ClosingWithAnUnmatchedFrameFailsTimedCaptureAndReleasesThePendingBuffer()
    {
        await using var fixture = new Fixture(); await using var subscription = fixture.Tap.Subscribe(new Observer());
        fixture.Frame(new(0, fixture.Format, 20, [1, 2])); await fixture.Bridge.DisposeAsync();
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion); Assert.AreEqual(0, fixture.Decoder.Decodes);
    }

    [TestMethod]
    public async Task BindingValidationPreventsDoubleDecodingAndClosedOrForeignSinks()
    {
        await using var fixture = new Fixture(); var factory = CreateFactory();
        Assert.ThrowsExactly<InvalidOperationException>(() => factory.Create(fixture.Media, fixture.Endpoint, fixture.Tap));
        await using var other = new Fixture(); Assert.ThrowsExactly<ArgumentException>(() => factory.Create(fixture.Media, other.Endpoint, fixture.Tap));
        fixture.Media.Close("Synthetic test"); Assert.ThrowsExactly<ArgumentException>(() => factory.Create(fixture.Media, fixture.Endpoint, fixture.Tap));
    }

    [TestMethod]
    public async Task ParallelNativeCallbacksKeepTheirOwnRtpContext()
    {
        await using var fixture = new Fixture(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var observer = new Observer { Behavior = () => { entered.Set(); release.Wait(); return true; } };
        await using var subscription = fixture.Tap.Subscribe(observer); var encoder = new AudioEncoder();
        var first = encoder.EncodeAudio(Samples(160, 0), fixture.Format); var second = encoder.EncodeAudio(Samples(160, 2), fixture.Format);
        var positions = new[] { new RtpPcmPacketPosition(17, 10, 1000, 8000), new RtpPcmPacketPosition(17, 11, 1160, 8000) };
        var firstDispatch = Task.Run(() => fixture.Dispatch(positions[0], first));
        Task? secondDispatch = null;
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
            using var started = new ManualResetEventSlim();
            secondDispatch = Task.Run(() => { started.Set(); fixture.Dispatch(positions[1], second); });
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(10)));
            Assert.IsFalse(secondDispatch.IsCompleted); Assert.AreEqual(1, fixture.Decoder.Decodes);
        }
        finally { release.Set(); }
        await firstDispatch; await secondDispatch!; Assert.AreEqual(2, fixture.Decoder.Decodes); Assert.AreEqual(2, observer.Frames.Count);
        foreach (var frame in observer.Frames)
        {
            var index = Array.IndexOf(positions, frame.Position); Assert.IsTrue(index >= 0);
            CollectionAssert.AreEqual(new AudioEncoder().DecodeAudio(index == 0 ? first : second, fixture.Format), frame.Samples);
        }
        Assert.IsFalse(subscription.Completion.IsCompleted);
    }

    [TestMethod]
    public async Task PendingFrameSlotsAreBoundedWhenNativeHeadersAreMissing()
    {
        await using var fixture = new Fixture(); await using var subscription = fixture.Tap.Subscribe(new Observer());
        var threads = Enumerable.Range(0, 9).Select(_ => new Thread(() => fixture.Frame(new(0, fixture.Format, 20, [1, 2])))).ToArray();
        foreach (var thread in threads) thread.Start(); foreach (var thread in threads) Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(1, fixture.Decoder.Decodes, "The ninth orphan is played un-timed, without growing pending storage.");
        await fixture.Bridge.DisposeAsync(); Assert.AreEqual(1, fixture.Decoder.Decodes);
    }

    [TestMethod]
    public async Task NonAudioRtpDoesNotConsumeThePendingAudioFrame()
    {
        await using var fixture = new Fixture(); var observer = new Observer(); await using var subscription = fixture.Tap.Subscribe(observer);
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format); var position = new RtpPcmPacketPosition(17, 10, 1000, 8000);
        fixture.Frame(new(0, fixture.Format, 20, payload)); fixture.NonAudioPacket(position, payload);
        Assert.AreEqual(0, fixture.Decoder.Decodes); fixture.Packet(position, payload);
        Assert.AreEqual(position, observer.Frames.Single().Position); Assert.AreEqual(1, fixture.Decoder.Decodes);
    }

    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task ActualUdpLoopbackNegotiatesAndDeliversMatchingRtpAndCscorePcm(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); var received = new Observer();
        await using var subscription = fixture.Tap.Subscribe(received);
        var headers = new ConcurrentQueue<RtpPcmPacketPosition>(); var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Media.OnRtpPacketReceived += (_, type, packet) =>
        {
            if (type != SDPMediaTypesEnum.audio) return;
            headers.Enqueue(new(packet.Header.SyncSource, packet.Header.SequenceNumber, packet.Header.Timestamp, fixture.Format.RtpClockRate));
            if (headers.Count == 3) completed.TrySetResult();
        };
        using var sender = new RTPSession(false, false, false, IPAddress.Loopback);
        sender.addTrack(new MediaStreamTrack(new List<AudioFormat> { fixture.Format }));
        Assert.AreEqual(SetDescriptionResultEnum.OK, fixture.Media.SetRemoteDescription(SdpType.offer, sender.CreateOffer(IPAddress.Loopback)));
        Assert.AreEqual(SetDescriptionResultEnum.OK, sender.SetRemoteDescription(SdpType.answer, fixture.Media.CreateAnswer(IPAddress.Loopback)));
        await fixture.Media.Start(); await sender.Start();
        var initialTimestamp = sender.AudioStream.LocalTrack.Timestamp;
        var encoder = new AudioEncoder(); var baseline = new AudioEncoder(); var expected = new List<short>();
        for (var index = 0; index < 3; index++)
        {
            var payload = encoder.EncodeAudio(Samples(fixture.Format.ClockRate / 50, index), fixture.Format);
            expected.AddRange(baseline.DecodeAudio(payload, fixture.Format)); sender.AudioStream.SendAudioFrame(160, fixture.Format.FormatID, payload);
        }
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        CollectionAssert.AreEqual(headers.ToArray(), received.Frames.Select(frame => frame.Position).ToArray());
        CollectionAssert.AreEqual(new[] { initialTimestamp, unchecked(initialTimestamp + 160), unchecked(initialTimestamp + 320) }, received.Frames.Select(frame => frame.Position.Timestamp).ToArray());
        CollectionAssert.AreEqual(expected.ToArray(), fixture.ReadPlayback(expected.Count)); Assert.AreEqual(3, fixture.Decoder.Decodes);
        Assert.AreEqual(fixture.Format.ClockRate, received.Frames.First().Rate); Assert.IsNull(fixture.Tap.LastFailure);
    }

    [TestMethod]
    public async Task MediaOwnerDrainsTheReceiveBindingBeforeRetiringObserversAndClosingNativeMedia()
    {
        await using var fixture = new Fixture(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var observer = new Observer { Behavior = () => { entered.Set(); release.Wait(); return true; } };
        await using var subscription = fixture.Tap.Subscribe(observer); var inner = new MediaOwner(fixture.Media);
        var owner = new PcmSipMediaSession(inner, fixture.Tap, fixture.Bridge); var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        var packet = Task.Run(() => fixture.Dispatch(new(17, 10, 1000, 8000), payload)); Task stopping;
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10))); stopping = owner.DisposeAsync().AsTask();
            Assert.IsFalse(stopping.IsCompleted); Assert.IsFalse(inner.Closed); Assert.IsFalse(subscription.Completion.IsCompleted);
        }
        finally { release.Set(); }
        await packet; await stopping; await owner.DisposeAsync(); Assert.IsTrue(inner.Closed); Assert.IsNull(await subscription.Completion);
        Assert.AreEqual(1, observer.Frames.Count);
    }

    [TestMethod]
    public async Task DeferredExecutionContextCannotReuseACompletedPacketsTimestamp()
    {
        await using var fixture = new Fixture(); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); Task? deferred = null;
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        var observer = new Observer { Behavior = () =>
        {
            deferred = Task.Run(async () => { await release.Task; fixture.Tap.DecodeAudio(payload, fixture.Format); });
            return true;
        } };
        await using var subscription = fixture.Tap.Subscribe(observer);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); Assert.AreEqual(1, observer.Frames.Count);
        release.TrySetResult(); await deferred!;
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(1, observer.Frames.Count); Assert.AreEqual(2, fixture.Decoder.Decodes);
    }

    [TestMethod]
    public async Task ClosedNativeSinkCannotSilentlyLeaveTimedCaptureSuccessful()
    {
        await using var fixture = new Fixture(); await using var subscription = fixture.Tap.Subscribe(new Observer());
        await fixture.Endpoint.CloseAudioSink();
        fixture.Dispatch(new(17, 10, 1000, 8000), new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format));
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion); Assert.AreEqual(0, fixture.Decoder.Decodes);
    }

    [TestMethod]
    public async Task MismatchedPayloadTypeDoesNotChangeTheOriginalG722FrameOrInventMetadata()
    {
        await using var fixture = new Fixture(SDPWellKnownMediaFormatsEnum.G722); var plain = new PassiveObserver();
        await using var subscription = fixture.Tap.Subscribe(new Observer()); await using var passive = fixture.Tap.Subscribe(plain);
        var payload = new AudioEncoder().EncodeAudio(Samples(320, 0), fixture.Format); var position = new RtpPcmPacketPosition(17, 10, 1000, 8000);
        fixture.Frame(new(0, fixture.Format, 20, payload)); fixture.PacketWithPayloadType(position, payload, 8);
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(1, fixture.Decoder.Decodes); CollectionAssert.AreEqual(new AudioEncoder().DecodeAudio(payload, fixture.Format), plain.Frames.Single());
    }

    [TestMethod]
    public async Task SinkOnlyMediaCanBindBeforeSdpCreatesAnAudioStream()
    {
        await using var fixture = new Fixture();
        var media = new VoIPMediaSession(new MediaEndPoints { AudioSink = fixture.Endpoint }, IPAddress.Loopback, 0);
        try
        {
            Assert.IsNull(media.AudioStream);
            await using var bridge = CreateFactory().Create(media, fixture.Endpoint, fixture.Tap);
        }
        finally { media.Close("Synthetic test"); }
    }

    [TestMethod]
    public async Task ConsoleHostRegistersTheSameStatelessBridgeFactoryWithoutStartingNativeWork()
    {
        using var host = ConsoleCompositionRoot.Build(loadPlugins: false); var factory = host.Services.GetRequiredService<ISipRtpPcmBridgeFactory>();
        Assert.IsInstanceOfType<SipRtpPcmBridgeFactory>(factory); Assert.AreSame(factory, host.Services.GetRequiredService<ISipRtpPcmBridgeFactory>());
        await using var fixture = new Fixture();
    }

    private static short[] Samples(int count, int block) => Enumerable.Range(block * count, count).Select(index => (short)(Math.Sin(index * 0.17) * 10000)).ToArray();
    private static SipRtpPcmBridgeFactory CreateFactory(TimeProvider? clock = null)
    {
        clock ??= TimeProvider.System;
        return new(new WawiCallPro.Core.Audio.RtpAudioPacketBufferFactory(clock), clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SipRtpPcmBridgeFactory>.Instance);
    }
    private sealed record Frame(RtpPcmPacketPosition Position, int Rate, int Channels, short[] Samples);
    private sealed class Observer : IReceivedRtpPcmObserver
    {
        internal ConcurrentQueue<Frame> Frames { get; } = new(); internal int Sent; internal Func<bool>? Behavior;
        public bool TryObserve(ConversationAudioSide side, int rate, int channels, ReadOnlySpan<short> samples)
        { Assert.AreEqual(ConversationAudioSide.Sent, side); Interlocked.Increment(ref Sent); return true; }
        public bool TryObserveReceived(RtpPcmPacketPosition packet, int rate, int channels, ReadOnlySpan<short> samples)
        { Frames.Enqueue(new(packet, rate, channels, samples.ToArray())); return Behavior?.Invoke() ?? true; }
    }
    private sealed class PassiveObserver : IConversationPcmObserver
    {
        internal ConcurrentQueue<short[]> Frames { get; } = new();
        public bool TryObserve(ConversationAudioSide side, int rate, int channels, ReadOnlySpan<short> samples) { Frames.Enqueue(samples.ToArray()); return true; }
    }
    private sealed class CountingEncoder : IAudioEncoder
    {
        private readonly AudioEncoder _inner = new(); internal int Decodes; internal Exception? Error;
        public List<AudioFormat> SupportedFormats => _inner.SupportedFormats;
        public byte[] EncodeAudio(short[] pcm, AudioFormat format) => _inner.EncodeAudio(pcm, format);
        public short[] DecodeAudio(byte[] encoded, AudioFormat format) { Interlocked.Increment(ref Decodes); if (Error is { } error) throw error; return _inner.DecodeAudio(encoded, format); }
    }
    private sealed class MediaOwner(VoIPMediaSession media) : ISipMediaSession
    {
        internal bool Closed;
        public IMediaSession Session => media;
        public void ApplyPlaybackSettings() { }
        public ValueTask DisposeAsync() { Closed = true; media.Close("Synthetic test"); return ValueTask.CompletedTask; }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        internal AudioFormat Format { get; }
        internal CountingEncoder Decoder { get; } = new();
        internal PcmObservingAudioEncoder Tap { get; }
        internal WindowsAudioEndPoint Endpoint { get; }
        internal VoIPMediaSession Media { get; }
        internal IAsyncDisposable Bridge { get; }
        private readonly IWaveSource _playback;
        internal Fixture(SDPWellKnownMediaFormatsEnum codec = SDPWellKnownMediaFormatsEnum.PCMA, ISipRtpPcmBridgeFactory? bridges = null)
        {
            Format = new(codec); Tap = new(Decoder);
            // Never call the device-resolving CSCore constructor. Exercise its real sink with only a memory buffer.
            Endpoint = (WindowsAudioEndPoint)RuntimeHelpers.GetUninitializedObject(typeof(WindowsAudioEndPoint));
            var playbackField = typeof(WindowsAudioEndPoint).GetField("_bufferingSource", BindingFlags.Instance | BindingFlags.NonPublic)!;
            _playback = (IWaveSource)Activator.CreateInstance(playbackField.FieldType, new WaveFormat(Format.ClockRate, 16, 1), Format.ClockRate * 10)!;
            typeof(WindowsAudioEndPoint).GetField("_audioEncoder", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Endpoint, Tap);
            playbackField.SetValue(Endpoint, _playback);
            var source = new AudioExtrasSource(); source.RestrictFormats(format => format.Codec == Format.Codec); source.SetSource(AudioSourcesEnum.None);
            Media = new VoIPMediaSession(new MediaEndPoints { AudioSource = source, AudioSink = Endpoint }, IPAddress.Loopback, 0);
            Bridge = (bridges ?? CreateFactory()).Create(Media, Endpoint, Tap);
        }
        internal void Dispatch(RtpPcmPacketPosition position, byte[] payload)
        {
            var packet = CreatePacket(position, payload);
            var format = Media.AudioStream.LocalTrack.Capabilities.Single(candidate => candidate.ID == Format.FormatID);
            Invoke(typeof(AudioStream).GetMethod("ProcessRtpPacket", BindingFlags.Instance | BindingFlags.NonPublic)!, Media.AudioStream, [new IPEndPoint(IPAddress.Loopback, 20000), packet, format]);
        }
        internal void Frame(EncodedAudioFrame frame) => Invoke(Bridge.GetType().GetMethod("FrameReceived", BindingFlags.Instance | BindingFlags.NonPublic)!, Bridge, [frame]);
        internal void Packet(RtpPcmPacketPosition position, byte[] payload) => Invoke(Bridge.GetType().GetMethod("PacketReceived", BindingFlags.Instance | BindingFlags.NonPublic)!, Bridge,
            [new IPEndPoint(IPAddress.Loopback, 20000), SDPMediaTypesEnum.audio, CreatePacket(position, payload)]);
        internal void NonAudioPacket(RtpPcmPacketPosition position, byte[] payload) => Invoke(Bridge.GetType().GetMethod("PacketReceived", BindingFlags.Instance | BindingFlags.NonPublic)!, Bridge,
            [new IPEndPoint(IPAddress.Loopback, 20000), SDPMediaTypesEnum.video, CreatePacket(position, payload)]);
        internal void PacketWithPayloadType(RtpPcmPacketPosition position, byte[] payload, int payloadType)
        {
            var packet = CreatePacket(position, payload); packet.Header.PayloadType = payloadType;
            Invoke(Bridge.GetType().GetMethod("PacketReceived", BindingFlags.Instance | BindingFlags.NonPublic)!, Bridge, [new IPEndPoint(IPAddress.Loopback, 20000), SDPMediaTypesEnum.audio, packet]);
        }
        private RTPPacket CreatePacket(RtpPcmPacketPosition position, byte[] payload)
        {
            var packet = new RTPPacket(payload.Length) { Payload = payload };
            packet.Header.SyncSource = position.SynchronizationSource; packet.Header.SequenceNumber = position.SequenceNumber;
            packet.Header.Timestamp = position.Timestamp; packet.Header.PayloadType = Format.FormatID; return packet;
        }
        internal short[] ReadPlayback(int count)
        {
            var bytes = new byte[count * 2]; Assert.AreEqual(bytes.Length, _playback.Read(bytes, 0, bytes.Length));
            return Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2, 2))).ToArray();
        }
        public async ValueTask DisposeAsync() { await Bridge.DisposeAsync(); await Tap.DisposeAsync(); Media.Close("Synthetic test"); await Endpoint.CloseAudioSink(); }
        private static void Invoke(MethodInfo method, object target, object[] arguments)
        {
            try { method.Invoke(target, arguments); }
            catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
        }
    }
}
