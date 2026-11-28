using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using CSCore;
using Microsoft.Extensions.Logging.Abstractions;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows.CSCore;
using WawiCallPro.Audio;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed partial class SentRtpPcmTests
{
    [TestMethod]
    public async Task CaptureCallbackAfterRtpCloseDoesNotInvalidatePreviouslySentPcm()
    {
        await using var fixture = new Fixture();
        await fixture.Connect();
        using var files = new RecordingFiles();
        await using var recording = await Sessions().StartAsync(files.Request(8000));
        await using var binding = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, recording, Anchor(fixture));
        var samples = Samples(160, 0);
        fixture.Capture(samples);
        await fixture.Packets(1);

        // The capture callback can run while call teardown has already closed RTP.
        var late = fixture.Tap.EncodeAudio(Samples(160, 1), fixture.Format);
        fixture.Media.Close("Synthetic hangup");
        fixture.Emit(160, late);

        await binding.DisposeAsync();
        Assert.IsNull(await binding.Completion);
        await recording.StopAsync(binding.LastWindowEnd);
        CollectionAssert.AreEqual(samples, files.Read(8000, 160).Where((_, index) => index % 2 == 0).ToArray());
    }

    [TestMethod]
    public async Task RtpDtmfReachesThePeerWhileCaptureKeepsItsSentPcmSubscription()
    {
        await using var fixture = new Fixture();
        await fixture.Connect();
        using var files = new RecordingFiles();
        await using var recording = await Sessions().StartAsync(files.Request(8000));
        await using var binding = new SentPcmRecordingBindingFactory().Attach(fixture.Tap, recording, Anchor(fixture));
        var observer = new Observer();
        await using var subscription = fixture.Tap.Subscribe(observer);
        var first = Samples(160, 0);
        var last = Samples(160, 2);
        fixture.Capture(first);
        var before = (await fixture.Packets(1)).Single();

        var tone = fixture.Media.SendDtmf(5, default);
        Assert.IsFalse(tone.IsCompleted);
        fixture.Capture(Samples(160, 1));
        await tone;
        Assert.AreEqual((byte)5, await fixture.DtmfReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)));

        fixture.Capture(last);
        var after = (await fixture.Packets(1)).Single();
        Assert.AreEqual(unchecked(before.Header.Timestamp + 160 + RTPSession.DTMF_EVENT_DURATION), after.Header.Timestamp);
        Assert.AreEqual(2, observer.Frames.Count);
        Assert.IsFalse(subscription.Completion.IsCompleted);
        await binding.DisposeAsync();
        Assert.IsNull(await binding.Completion);
        var duration = binding.LastWindowEnd;
        await recording.StopAsync(duration);
        var pcm = files.Read(8000, checked((int)(duration.Ticks * 8000 / TimeSpan.TicksPerSecond)));
        var sent = pcm.Where((_, index) => index % 2 == 0).ToArray();
        CollectionAssert.AreEqual(first, sent[..160]);
        Assert.IsTrue(sent[160..^160].All(sample => sample == 0));
        CollectionAssert.AreEqual(last, sent[^160..]);
    }

    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task ActualCscoreCaptureMatchesTheLocalRtpWindowAndRealUdpPayload(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); await fixture.Connect(); var observer = new Observer(); var passive = new Passive();
        await using var subscription = fixture.Tap.Subscribe(observer); await using var plain = fixture.Tap.Subscribe(passive);
        var inputs = Enumerable.Range(0, 3).Select(index => Samples(fixture.Format.ClockRate / 50, index)).ToArray();
        var initial = fixture.Media.AudioStream.LocalTrack.Timestamp; var baseline = new AudioEncoder();
        for (var index = 0; index < inputs.Length; index++) fixture.Capture(inputs[index]);
        var packets = await fixture.Packets(3);
        Assert.AreEqual(3, observer.Frames.Count); Assert.AreEqual(3, passive.Count); Assert.AreEqual(3, fixture.Codec.Encodes);
        for (var index = 0; index < 3; index++)
        {
            var frame = observer.Frames.ElementAt(index); var packet = packets[index];
            Assert.AreEqual(new SentRtpPcmPosition(packet.Header.SyncSource, packet.Header.Timestamp, 8000, 160), frame.Position);
            Assert.AreEqual(unchecked(initial + (uint)(index * 160)), frame.Position.Timestamp);
            Assert.AreEqual(fixture.Format.ClockRate, frame.Rate); CollectionAssert.AreEqual(inputs[index], frame.Samples);
            CollectionAssert.AreEqual(baseline.EncodeAudio(inputs[index], fixture.Format), packet.Payload);
        }
        Assert.IsFalse(subscription.Completion.IsCompleted); Assert.IsNull(fixture.Tap.LastFailure);
    }

    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task FragmentedSourceBlockRetainsOnePcmWindowWithoutInventingPacketSequenceNumbers(SDPWellKnownMediaFormatsEnum codec)
    {
        await using var fixture = new Fixture(codec); await fixture.Connect(); var observer = new Observer();
        await using var subscription = fixture.Tap.Subscribe(observer);
        var input = Samples(fixture.Format.ClockRate / 2, 0); var timestamp = fixture.Media.AudioStream.LocalTrack.Timestamp;
        fixture.Capture(input);
        var maxPayload = (int)typeof(RTPSession).GetField("RTP_MAX_PAYLOAD", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        var packetCount = (fixture.Codec.LastPayload!.Length + maxPayload - 1) / maxPayload;
        var packets = await fixture.Packets(packetCount); Assert.IsTrue(packetCount > 1);
        var frame = observer.Frames.Single(); Assert.AreEqual(timestamp, frame.Position.Timestamp); Assert.AreEqual(4000u, frame.Position.DurationRtpUnits);
        Assert.AreEqual(timestamp, packets[0].Header.Timestamp); CollectionAssert.AreEqual(input, frame.Samples);
        CollectionAssert.AreEqual(fixture.Codec.LastPayload, packets.SelectMany(packet => packet.Payload).ToArray());
        Assert.AreEqual(1, fixture.Codec.Encodes);
    }

    [TestMethod]
    public async Task CapturingBeforeTheRtpClockAdvancesFailsOnlyTimedSendObservation()
    {
        await using var fixture = new Fixture(); var observer = new Observer(); var passive = new Passive();
        await using var subscription = fixture.Tap.Subscribe(observer); await using var plain = fixture.Tap.Subscribe(passive);
        Assert.ThrowsExactly<ArgumentException>(() => fixture.Capture(Samples(160, 0)));
        Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(1, passive.Count); Assert.AreEqual(0, observer.Frames.Count);
        await fixture.Connect(); fixture.Capture(Samples(160, 1)); await fixture.Packets(1); Assert.AreEqual(2, passive.Count);
        await using var later = fixture.Tap.Subscribe(new Observer()); Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await later.Completion);
    }

    [TestMethod]
    public async Task StandaloneCodecCannotLabelItsSourceSamplesAsRtpTimedPcm()
    {
        await using var tap = new PcmObservingAudioEncoder(new AudioEncoder()); await using var subscription = tap.Subscribe(new Observer());
        tap.EncodeAudio(Samples(160, 0), new(SDPWellKnownMediaFormatsEnum.PCMA));
        Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await subscription.Completion);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TimedObserverRejectionOrFailureDoesNotChangeNativeCaptureOrSend(bool throws)
    {
        await using var fixture = new Fixture(); await fixture.Connect();
        var observer = new Observer { Behavior = () => throws ? throw new IOException("private-observer-detail") : false };
        await using var subscription = fixture.Tap.Subscribe(observer);
        var input = Samples(160, 0); fixture.Capture(input); var packets = await fixture.Packets(1);
        Assert.AreEqual(throws ? PcmObservationFailure.ObserverFailed : PcmObservationFailure.ObserverRejected, await subscription.Completion);
        CollectionAssert.AreEqual(new AudioEncoder().EncodeAudio(input, fixture.Format), packets.Single().Payload);
        Assert.AreEqual(1, observer.Failures.Count);
    }

    [TestMethod]
    public async Task MissingOrWrongEncodedPayloadCannotReuseAnotherFramesPcm()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); var observer = new Observer();
        await using var subscription = fixture.Tap.Subscribe(observer);
        var payload = fixture.Tap.EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Emit(160, payload.ToArray()); await fixture.Packets(1);
        Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await subscription.Completion); Assert.AreEqual(0, observer.Frames.Count);
    }

    [TestMethod]
    public async Task MissingSourceEventIsReportedWhenTheMediaBindingCloses()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); var observer = new Observer();
        await using var subscription = fixture.Tap.Subscribe(observer);
        fixture.Tap.EncodeAudio(Samples(160, 0), fixture.Format);
        await fixture.Bridge.DisposeAsync(); Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(0, observer.Frames.Count);
    }

    [TestMethod]
    public async Task CodecFailureKeepsItsOriginalExceptionAndSynchronouslyNotifiesTheFailureBoundary()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); var observer = new Observer();
        await using var subscription = fixture.Tap.Subscribe(observer); var failure = new IOException("private-codec-detail"); fixture.Codec.Error = failure;
        Assert.AreSame(failure, Assert.ThrowsExactly<IOException>(() => fixture.Capture(Samples(160, 0))));
        Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, observer.Failures.Single());
        Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await subscription.Completion);
    }

    [TestMethod]
    public async Task NativeTimestampWrapIsPreservedInObservedPcmAndActualUdpHeaders()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); var observer = new Observer();
        await using var subscription = fixture.Tap.Subscribe(observer);
        typeof(MediaStreamTrack).GetProperty("Timestamp")!.SetValue(fixture.Media.AudioStream.LocalTrack, uint.MaxValue - 159);
        fixture.Capture(Samples(160, 0)); fixture.Capture(Samples(160, 1)); var packets = await fixture.Packets(2);
        CollectionAssert.AreEqual(new[] { uint.MaxValue - 159, 0u }, observer.Frames.Select(frame => frame.Position.Timestamp).ToArray());
        CollectionAssert.AreEqual(packets.Select(packet => packet.Header.Timestamp).ToArray(), observer.Frames.Select(frame => frame.Position.Timestamp).ToArray());
        Assert.IsFalse(subscription.Completion.IsCompleted);
    }

    [TestMethod]
    public async Task ExtrasAdvanceTheNativeClockWithoutInventingMicrophonePcmOrCompressingTheGap()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); var observer = new Observer();
        await using var subscription = fixture.Tap.Subscribe(observer); var initial = fixture.Media.AudioStream.LocalTrack.Timestamp;
        fixture.Capture(Samples(160, 0));
        fixture.EmitExtras(160, new AudioEncoder().EncodeAudio(Samples(160, 1), fixture.Format));
        fixture.Capture(Samples(160, 2)); var packets = await fixture.Packets(3);
        CollectionAssert.AreEqual(new[] { initial, unchecked(initial + 320) }, observer.Frames.Select(frame => frame.Position.Timestamp).ToArray());
        Assert.AreEqual(3, packets.Length); Assert.AreEqual(2, fixture.Codec.Encodes); Assert.IsFalse(subscription.Completion.IsCompleted);
    }

    [TestMethod]
    public async Task SuppressedNativeSendDoesNotBecomeATimedPcmFrame()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); var observer = new Observer();
        await using var subscription = fixture.Tap.Subscribe(observer); fixture.Media.AudioStream.IsClosed = true;
        fixture.Capture(Samples(160, 0));
        Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await subscription.Completion); Assert.AreEqual(0, observer.Frames.Count);
    }

    [TestMethod]
    public async Task WrongRtpDurationCannotLabelTheCapturedSamplesWithTheWrongWindow()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); var observer = new Observer();
        await using var subscription = fixture.Tap.Subscribe(observer);
        var payload = fixture.Tap.EncodeAudio(Samples(160, 0), fixture.Format); fixture.Emit(320, payload); await fixture.Packets(1);
        Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await subscription.Completion); Assert.AreEqual(0, observer.Frames.Count);
    }

    [TestMethod]
    public async Task PendingCaptureSlotsAreBoundedAndFailWithoutInventingSendPositions()
    {
        await using var fixture = new Fixture(); var observer = new Observer(); await using var subscription = fixture.Tap.Subscribe(observer);
        var threads = Enumerable.Range(0, 9).Select(index => new Thread(() => fixture.Tap.EncodeAudio(Samples(160, index), fixture.Format))).ToArray();
        foreach (var thread in threads) thread.Start(); foreach (var thread in threads) Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await subscription.Completion); Assert.AreEqual(0, observer.Frames.Count);
    }

    [TestMethod]
    public async Task ConcurrentSourceSubmissionsKeepTheOwnedPcmAndNativeRtpWindowsTogether()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); var observer = new Observer(); using var ready = new Barrier(2);
        await using var subscription = fixture.Tap.Subscribe(observer); var inputs = new[] { Samples(160, 0), Samples(160, 1) };
        var calls = inputs.Select(input => Task.Factory.StartNew(() =>
        {
            var payload = fixture.Tap.EncodeAudio(input, fixture.Format);
            Assert.IsTrue(ready.SignalAndWait(TimeSpan.FromSeconds(10))); fixture.Emit(160, payload);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
        await Task.WhenAll(calls); var packets = await fixture.Packets(2); Assert.AreEqual(2, fixture.Codec.Encodes);
        foreach (var packet in packets)
        {
            var frame = observer.Frames.Single(frame => frame.Position.Timestamp == packet.Header.Timestamp);
            CollectionAssert.AreEqual(new AudioEncoder().EncodeAudio(frame.Samples, fixture.Format), packet.Payload);
        }
        Assert.IsFalse(subscription.Completion.IsCompleted);
    }

    [TestMethod]
    public async Task ClosingTheMediaBindingWaitsForAnActiveSentObserver()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var observer = new Observer { Behavior = () => { entered.Set(); release.Wait(); return true; } };
        await using var subscription = fixture.Tap.Subscribe(observer);
        var capture = Task.Run(() => fixture.Capture(Samples(160, 0))); Task? close = null;
        try { Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10))); close = fixture.Bridge.DisposeAsync().AsTask(); Assert.IsFalse(close.IsCompleted); }
        finally { release.Set(); }
        await capture; await close!; await fixture.Bridge.DisposeAsync(); await fixture.Packets(1); Assert.AreEqual(1, observer.Frames.Count);
    }

    [TestMethod]
    public async Task PendingSendOwnsItsPcmUntilTheNativeSourceEvent()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); var observer = new Observer();
        await using var subscription = fixture.Tap.Subscribe(observer);
        var input = Samples(160, 0); var expected = input.ToArray(); var payload = fixture.Tap.EncodeAudio(input, fixture.Format);
        Array.Fill(input, (short)0); fixture.Emit(160, payload); await fixture.Packets(1);
        CollectionAssert.AreEqual(expected, observer.Frames.Single().Samples); Assert.IsFalse(subscription.Completion.IsCompleted);
    }

    [TestMethod]
    public async Task OneEncodedBlockCannotBeObservedTwiceAsTimedSourcePcm()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); var observer = new Observer();
        await using var subscription = fixture.Tap.Subscribe(observer);
        var payload = fixture.Tap.EncodeAudio(Samples(160, 0), fixture.Format); fixture.Emit(160, payload); fixture.Emit(160, payload);
        await fixture.Packets(2); Assert.AreEqual(1, observer.Frames.Count);
        Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await subscription.Completion);
    }

    [TestMethod]
    public async Task SendMetadataFailureLeavesReceiveOnlyAndBasicObserversSubscribed()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); var receive = new ReceiveOnly();
        await using var received = fixture.Tap.Subscribe(receive); await using var sent = fixture.Tap.Subscribe(new Observer());
        var payload = fixture.Tap.EncodeAudio(Samples(160, 0), fixture.Format); fixture.Emit(160, payload.ToArray());
        await fixture.Packets(1); Assert.AreEqual(PcmObservationFailure.SentMetadataUnavailable, await sent.Completion);
        fixture.Capture(Samples(160, 1)); await fixture.Packets(1);
        Assert.AreEqual(2, receive.Sent); Assert.IsFalse(received.Completion.IsCompleted);
    }

    private sealed class ReceiveOnly : IReceivedRtpPcmObserver
    {
        internal int Sent;
        public bool TryObserve(ConversationAudioSide side, int rate, int channels, ReadOnlySpan<short> samples) { Assert.AreEqual(ConversationAudioSide.Sent, side); Sent++; return true; }
        public bool TryObserveReceived(RtpPcmPacketPosition packet, int rate, int channels, ReadOnlySpan<short> samples) => true;
    }

    private static short[] Samples(int count, int block) => Enumerable.Range(block * count, count).Select(index => (short)(Math.Sin(index * 0.19) * 9000)).ToArray();
    private sealed record Frame(SentRtpPcmPosition Position, int Rate, short[] Samples);
    private sealed class Observer : ISentRtpPcmObserver, IConversationPcmFailureObserver
    {
        internal ConcurrentQueue<Frame> Frames { get; } = new(); internal ConcurrentQueue<PcmObservationFailure> Failures { get; } = new(); internal Func<bool>? Behavior;
        public bool TryObserve(ConversationAudioSide side, int rate, int channels, ReadOnlySpan<short> samples) { Assert.AreEqual(ConversationAudioSide.Received, side); return true; }
        public bool TryObserveSent(SentRtpPcmPosition position, int rate, int channels, ReadOnlySpan<short> samples)
        { Assert.AreEqual(1, channels); Frames.Enqueue(new(position, rate, samples.ToArray())); return Behavior?.Invoke() ?? true; }
        public void OnPcmObservationFailed(PcmObservationFailure failure) => Failures.Enqueue(failure);
    }
    private sealed class Passive : IConversationPcmObserver
    { internal int Count; public bool TryObserve(ConversationAudioSide side, int rate, int channels, ReadOnlySpan<short> samples) { Count++; return true; } }
    private sealed class CountingCodec : IAudioEncoder
    {
        private readonly AudioEncoder _inner = new(); internal int Encodes; internal byte[]? LastPayload; internal Exception? Error;
        public List<AudioFormat> SupportedFormats => _inner.SupportedFormats;
        public byte[] EncodeAudio(short[] input, AudioFormat format) { Interlocked.Increment(ref Encodes); if (Error is { } error) throw error; return LastPayload = _inner.EncodeAudio(input, format); }
        public short[] DecodeAudio(byte[] input, AudioFormat format) => _inner.DecodeAudio(input, format);
    }
    private sealed class Fixture : IAsyncDisposable
    {
        internal AudioFormat Format { get; }
        internal CountingCodec Codec { get; } = new();
        internal PcmObservingAudioEncoder Tap { get; }
        internal WindowsAudioEndPoint Endpoint { get; }
        internal VoIPMediaSession Media { get; }
        internal IAsyncDisposable Bridge { get; }
        private readonly Input _input;
        private readonly RTPSession _receiver = new(false, false, false, IPAddress.Loopback);
        private readonly ConcurrentQueue<RTPPacket> _packets = new();
        private readonly SemaphoreSlim _arrived = new(0);
        internal TaskCompletionSource<byte> DtmfReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Fixture(SDPWellKnownMediaFormatsEnum codec = SDPWellKnownMediaFormatsEnum.PCMA)
        {
            Format = new(codec); Tap = new(Codec); _input = new(Format.ClockRate);
            Endpoint = (WindowsAudioEndPoint)RuntimeHelpers.GetUninitializedObject(typeof(WindowsAudioEndPoint));
            var manager = new MediaFormatManager<AudioFormat>([Format]); manager.SetSelectedFormat(Format);
            Field("_audioEncoder").SetValue(Endpoint, Tap); Field("_inputWaveSource").SetValue(Endpoint, _input);
            Field("_waveSourceFormat").SetValue(Endpoint, _input.WaveFormat); Field("_audioFormatManager").SetValue(Endpoint, manager);
            var sessionType = typeof(SipRtpPcmBridgeFactory).Assembly.GetType("WawiCallPro.Audio.ConversationVoipMediaSession", throwOnError: true)!;
            Media = (VoIPMediaSession)Activator.CreateInstance(sessionType, new MediaEndPoints { AudioSource = Endpoint, AudioSink = Endpoint }, IPAddress.Loopback)!;
            Bridge = new SipRtpPcmBridgeFactory(new RtpAudioPacketBufferFactory(TimeProvider.System), TimeProvider.System,
                NullLogger<SipRtpPcmBridgeFactory>.Instance).Create(Media, Endpoint, Tap);
            _receiver.addTrack(new MediaStreamTrack(new List<AudioFormat> { Format }));
            _receiver.OnRtpPacketReceived += (_, type, packet) => { if (type == SDPMediaTypesEnum.audio) { _packets.Enqueue(packet); _arrived.Release(); } };
            _receiver.OnRtpEvent += (_, tone, _) => { if (tone.EndOfEvent) DtmfReceived.TrySetResult(tone.EventID); };
        }
        internal async Task Connect()
        {
            Assert.AreEqual(SetDescriptionResultEnum.OK, _receiver.SetRemoteDescription(SdpType.offer, Media.CreateOffer(IPAddress.Loopback)));
            Assert.AreEqual(SetDescriptionResultEnum.OK, Media.SetRemoteDescription(SdpType.answer, _receiver.CreateAnswer(IPAddress.Loopback)));
            await _receiver.Start(); await Media.Start();
        }
        internal void Capture(short[] samples) { _input.Add(samples); Invoke(typeof(WindowsAudioEndPoint).GetMethod("LocalAudioSampleAvailable", BindingFlags.Instance | BindingFlags.NonPublic)!, Endpoint, [null!, null!]); }
        internal void Emit(uint duration, byte[] payload)
        {
            var handler = (EncodedSampleDelegate)Field("OnAudioSourceEncodedSample").GetValue(Endpoint)!; handler(duration, payload);
        }
        internal void EmitExtras(uint duration, byte[] payload)
        {
            var field = typeof(AudioExtrasSource).GetField("OnAudioSourceEncodedSample", BindingFlags.Instance | BindingFlags.NonPublic)!;
            ((EncodedSampleDelegate)field.GetValue(Media.AudioExtrasSource)!)(duration, payload);
        }
        internal uint RemoteSsrc => _receiver.AudioStream.LocalTrack.Ssrc;
        internal void SendRemote(ushort sequence, uint timestamp, byte[] payload) =>
            _receiver.SendRtpRaw(SDPMediaTypesEnum.audio, payload, timestamp, 0, Format.FormatID, sequence);
        internal void EnableReceiveBuffer()
        {
            var field = Field("_bufferingSource");
            field.SetValue(Endpoint, Activator.CreateInstance(field.FieldType, new WaveFormat(Format.ClockRate, 16, 1), Format.ClockRate * 10));
        }
        internal async Task<RTPPacket[]> Packets(int count)
        {
            var packets = new List<RTPPacket>();
            for (var index = 0; index < count; index++)
            { Assert.IsTrue(await _arrived.WaitAsync(TimeSpan.FromSeconds(10))); Assert.IsTrue(_packets.TryDequeue(out var packet)); packets.Add(packet!); }
            return packets.ToArray();
        }
        public async ValueTask DisposeAsync() { await Bridge.DisposeAsync(); await Tap.DisposeAsync(); Media.Close("Synthetic test"); _receiver.Close("Synthetic test"); await Endpoint.CloseAudio(); }
        private static FieldInfo Field(string name) => typeof(WindowsAudioEndPoint).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static void Invoke(MethodInfo method, object target, object[] args)
        { try { method.Invoke(target, args); } catch (TargetInvocationException error) when (error.InnerException is not null) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); } }
    }
    private sealed class Input(int rate) : IWaveSource
    {
        private readonly Queue<short[]> _chunks = new();
        internal void Add(short[] samples) => _chunks.Enqueue(samples);
        public WaveFormat WaveFormat { get; } = new(rate, 16, 1);
        public bool CanSeek => false;
        public long Length => throw new NotSupportedException();
        public long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public int Read(byte[] buffer, int offset, int count)
        {
            if (!_chunks.TryDequeue(out var samples)) return 0;
            Assert.IsTrue(samples.Length * 2 <= count);
            for (var index = 0; index < samples.Length; index++) BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(offset + index * 2, 2), samples[index]);
            return samples.Length * 2;
        }
        public void Dispose() { }
    }
}
