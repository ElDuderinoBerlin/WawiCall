using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

public sealed partial class ReceivedRtpPcmTests
{
    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task NativeReceiveOrdersAndDeduplicatesBeforeTheExistingStatefulDecoder(SDPWellKnownMediaFormatsEnum codec)
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(codec, CreateFactory(clock));
        var observer = new Observer(); await using var subscription = fixture.Tap.Subscribe(observer);
        var encoder = new AudioEncoder(); var baseline = new AudioEncoder();
        var payloads = Enumerable.Range(0, 3).Select(index => encoder.EncodeAudio(Samples(fixture.Format.ClockRate / 50, index), fixture.Format)).ToArray();
        var expected = payloads.SelectMany(payload => baseline.DecodeAudio(payload, fixture.Format)).ToArray();
        RtpPcmPacketPosition Position(int index) => new(17, unchecked((ushort)(ushort.MaxValue + index)), unchecked(uint.MaxValue - 159 + (uint)(index * 160)), 8000);
        fixture.Dispatch(Position(0), payloads[0]); fixture.Dispatch(Position(2), payloads[2]); fixture.Dispatch(Position(2), payloads[2]);
        Assert.AreEqual(1, fixture.Decoder.Decodes);
        fixture.Dispatch(Position(1), payloads[1]); fixture.Dispatch(Position(0), payloads[0]); clock.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(3, fixture.Decoder.Decodes); CollectionAssert.AreEqual(expected, fixture.ReadPlayback(expected.Length));
        CollectionAssert.AreEqual(Enumerable.Range(0, 3).Select(Position).ToArray(), observer.Frames.Select(frame => frame.Position).ToArray());
        Assert.IsFalse(subscription.Completion.IsCompleted);
    }

    [TestMethod]
    public async Task MissingPacketExpiresWithoutAnotherArrivalAndKeepsOriginalMediaTimestamp()
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(bridges: CreateFactory(clock));
        var observer = new Observer(); await using var subscription = fixture.Tap.Subscribe(observer);
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); fixture.Dispatch(new(17, 12, 1320, 8000), payload);
        clock.Advance(TimeSpan.FromMilliseconds(59)); Assert.AreEqual(1, fixture.Decoder.Decodes);
        clock.Advance(TimeSpan.FromMilliseconds(1)); Assert.AreEqual(2, fixture.Decoder.Decodes);
        Assert.AreEqual(1320u, observer.Frames.Last().Position.Timestamp);
        fixture.Dispatch(new(17, 11, 1160, 8000), payload); Assert.AreEqual(2, fixture.Decoder.Decodes);
        Assert.IsFalse(subscription.Completion.IsCompleted);
    }

    [TestMethod]
    public async Task FurtherPacketsDoNotExtendTheFirstMissingPacketsDeadline()
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(bridges: CreateFactory(clock));
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); fixture.Dispatch(new(17, 12, 1320, 8000), payload);
        clock.Advance(TimeSpan.FromMilliseconds(50)); fixture.Dispatch(new(17, 13, 1480, 8000), payload);
        clock.Advance(TimeSpan.FromMilliseconds(10)); Assert.AreEqual(3, fixture.Decoder.Decodes);
    }

    [TestMethod]
    public async Task QueuedPayloadIsOwnedAndRetainsItsNegotiatedCodec()
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(bridges: CreateFactory(clock));
        var observer = new Observer(); await using var subscription = fixture.Tap.Subscribe(observer);
        var first = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        var g722 = new AudioFormat(SDPWellKnownMediaFormatsEnum.G722); var payload = new AudioEncoder().EncodeAudio(Samples(320, 1), g722);
        var expected = new AudioEncoder().DecodeAudio(payload, g722);
        fixture.Dispatch(new(17, 10, 1000, 8000), first);
        fixture.Frame(new(0, g722, 20, payload)); fixture.PacketWithPayloadType(new(17, 12, 1320, 8000), payload, g722.FormatID);
        Array.Fill(payload, (byte)0); clock.Advance(TimeSpan.FromMilliseconds(60));
        var frame = observer.Frames.Last(); Assert.AreEqual(16000, frame.Rate); CollectionAssert.AreEqual(expected, frame.Samples);
        Assert.AreEqual(2, fixture.Decoder.Decodes); Assert.IsFalse(subscription.Completion.IsCompleted);
    }

    [TestMethod]
    [DataRow("source")]
    [DataRow("clock")]
    [DataRow("conflict")]
    [DataRow("overflow")]
    public async Task BufferFailureFailsTimedObservationAndKeepsPlaybackAvailable(string failure)
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(bridges: CreateFactory(clock));
        var observer = new Observer(); var passive = new PassiveObserver();
        await using var subscription = fixture.Tap.Subscribe(observer); await using var plain = fixture.Tap.Subscribe(passive);
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload);
        switch (failure)
        {
            case "source": fixture.Dispatch(new(19, 11, 1160, 8000), payload); break;
            case "clock":
                var changed = new AudioFormat(8, "PCMA", 8000, 16000, 1, null);
                fixture.Frame(new(0, changed, 20, payload)); fixture.Packet(new(17, 11, 1160, 16000), payload); break;
            case "conflict":
                fixture.Dispatch(new(17, 12, 1320, 8000), payload); fixture.Dispatch(new(17, 12, 9999, 8000), payload); break;
            case "overflow":
                for (ushort sequence = 12; sequence <= 76; sequence++) fixture.Dispatch(new(17, sequence, (uint)(1000 + (sequence - 10) * 160), 8000), payload);
                break;
        }
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(1, observer.Frames.Count); Assert.AreEqual(2, fixture.Decoder.Decodes);
        clock.Advance(TimeSpan.FromSeconds(1)); Assert.AreEqual(2, fixture.Decoder.Decodes);
        fixture.Dispatch(new(17, 77, 11720, 8000), payload); Assert.AreEqual(3, fixture.Decoder.Decodes); Assert.AreEqual(3, passive.Frames.Count);
    }

    [TestMethod]
    public async Task DisposalDrainsQueuedPacketsBeforeObserversAndNativeMediaClose()
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(bridges: CreateFactory(clock));
        var observer = new Observer(); await using var subscription = fixture.Tap.Subscribe(observer);
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); fixture.Dispatch(new(17, 12, 1320, 8000), payload);
        var inner = new MediaOwner(fixture.Media); var owner = new PcmSipMediaSession(inner, fixture.Tap, fixture.Bridge);
        await owner.DisposeAsync(); await owner.DisposeAsync();
        Assert.AreEqual(2, observer.Frames.Count); Assert.AreEqual(2, fixture.Decoder.Decodes); Assert.IsTrue(inner.Closed);
        Assert.IsNull(await subscription.Completion); Assert.IsTrue(clock.TimerDisposed);
        clock.Advance(TimeSpan.FromSeconds(1)); Assert.AreEqual(2, fixture.Decoder.Decodes);
    }

    [TestMethod]
    public async Task TimerCodecFailureIsContainedAndReportedWithoutNativeDetails()
    {
        var clock = new DeadlineClock(); var log = new ReceiverLog();
        var factory = new SipRtpPcmBridgeFactory(new RtpAudioPacketBufferFactory(clock), clock, log);
        await using var fixture = new Fixture(bridges: factory); await using var subscription = fixture.Tap.Subscribe(new Observer());
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); fixture.Dispatch(new(17, 12, 1320, 8000), payload);
        fixture.Decoder.Error = new IOException("private-native-detail"); clock.Advance(TimeSpan.FromMilliseconds(60));
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(1, log.Messages.Count); Assert.IsFalse(log.Messages.Single().Contains("private-native-detail"));
        clock.Advance(TimeSpan.FromSeconds(1)); Assert.AreEqual(2, fixture.Decoder.Decodes);
    }

    [TestMethod]
    public async Task ReentrantDeliveryCannotDeadlockOrRecursivelyEnterTheStatefulDecoder()
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(bridges: CreateFactory(clock));
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        var observer = new Observer { Behavior = () => { fixture.Dispatch(new(17, 11, 1160, 8000), payload); return true; } };
        await using var subscription = fixture.Tap.Subscribe(observer);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload);
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(1, fixture.Decoder.Decodes);
    }

    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722)]
    public async Task UdpReceiveReordersRealRtpBeforeCscoreDecodes(SDPWellKnownMediaFormatsEnum codec)
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(codec, CreateFactory(clock));
        var observer = new Observer(); await using var subscription = fixture.Tap.Subscribe(observer);
        var arrived = 0; var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Media.OnRtpPacketReceived += (_, type, _) => { if (type == SDPMediaTypesEnum.audio && Interlocked.Increment(ref arrived) == 4) complete.TrySetResult(); };
        using var sender = new RTPSession(false, false, false, IPAddress.Loopback);
        sender.addTrack(new MediaStreamTrack(new List<AudioFormat> { fixture.Format }));
        Assert.AreEqual(SetDescriptionResultEnum.OK, fixture.Media.SetRemoteDescription(SdpType.offer, sender.CreateOffer(IPAddress.Loopback)));
        Assert.AreEqual(SetDescriptionResultEnum.OK, sender.SetRemoteDescription(SdpType.answer, fixture.Media.CreateAnswer(IPAddress.Loopback)));
        await fixture.Media.Start(); await sender.Start();
        var encoder = new AudioEncoder(); var baseline = new AudioEncoder();
        var payloads = Enumerable.Range(0, 3).Select(index => encoder.EncodeAudio(Samples(fixture.Format.ClockRate / 50, index), fixture.Format)).ToArray();
        var expected = payloads.SelectMany(payload => baseline.DecodeAudio(payload, fixture.Format)).ToArray();
        foreach (var index in new[] { 0, 2, 2, 1 })
            sender.SendRtpRaw(SDPMediaTypesEnum.audio, payloads[index], (uint)(1000 + index * 160), 0, fixture.Format.FormatID, (ushort)(100 + index));
        await complete.Task.WaitAsync(TimeSpan.FromSeconds(10));
        CollectionAssert.AreEqual(new ushort[] { 100, 101, 102 }, observer.Frames.Select(frame => frame.Position.SequenceNumber).ToArray());
        CollectionAssert.AreEqual(expected, fixture.ReadPlayback(expected.Length)); Assert.AreEqual(3, fixture.Decoder.Decodes);
        Assert.IsFalse(subscription.Completion.IsCompleted);
    }

    [TestMethod]
    public async Task ClosingWaitsForAnActiveSystemTimerCallbackBeforeRetiringObservers()
    {
        await using var fixture = new Fixture(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var calls = 0; var observer = new Observer { Behavior = () =>
        {
            if (Interlocked.Increment(ref calls) == 2) { entered.Set(); release.Wait(); }
            return true;
        } };
        await using var subscription = fixture.Tap.Subscribe(observer);
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); fixture.Dispatch(new(17, 12, 1320, 8000), payload);
        var inner = new MediaOwner(fixture.Media); var owner = new PcmSipMediaSession(inner, fixture.Tap, fixture.Bridge);
        Task? stopping = null;
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
            stopping = owner.DisposeAsync().AsTask(); Assert.IsFalse(stopping.IsCompleted);
            Assert.IsFalse(inner.Closed); Assert.IsFalse(subscription.Completion.IsCompleted);
        }
        finally { release.Set(); }
        await stopping!; Assert.IsTrue(inner.Closed); Assert.IsNull(await subscription.Completion);
        fixture.Dispatch(new(17, 13, 1480, 8000), payload); Assert.AreEqual(2, fixture.Decoder.Decodes);
    }

    [TestMethod]
    public async Task FailureWhileDrainingStillReleasesTimerObserversAndNativeMedia()
    {
        var clock = new DeadlineClock(); var fixture = new Fixture(bridges: CreateFactory(clock));
        await using var subscription = fixture.Tap.Subscribe(new Observer());
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); fixture.Dispatch(new(17, 12, 1320, 8000), payload);
        var failure = new IOException("private-native-detail"); fixture.Decoder.Error = failure;
        var inner = new MediaOwner(fixture.Media); var owner = new PcmSipMediaSession(inner, fixture.Tap, fixture.Bridge);
        try
        {
            Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<IOException>(() => owner.DisposeAsync().AsTask()));
            Assert.IsTrue(inner.Closed); Assert.IsTrue(clock.TimerDisposed);
            Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
            Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<IOException>(() => owner.DisposeAsync().AsTask()));
            clock.Advance(TimeSpan.FromSeconds(1)); Assert.AreEqual(2, fixture.Decoder.Decodes);
        }
        finally
        {
            // The cached failed close remains observable. Native endpoint cleanup is independent of that failure.
            await Assert.ThrowsExactlyAsync<IOException>(() => fixture.Bridge.DisposeAsync().AsTask());
            await fixture.Endpoint.CloseAudioSink();
        }
    }

    [TestMethod]
    public async Task SinkClosedWhileWaitingForAGapCannotProduceSuccessfulTimedPcm()
    {
        var clock = new DeadlineClock(); await using var fixture = new Fixture(bridges: CreateFactory(clock));
        await using var subscription = fixture.Tap.Subscribe(new Observer());
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload); fixture.Dispatch(new(17, 12, 1320, 8000), payload);
        await fixture.Endpoint.CloseAudioSink(); clock.Advance(TimeSpan.FromMilliseconds(60));
        Assert.AreEqual(PcmObservationFailure.ReceivedMetadataUnavailable, await subscription.Completion);
        Assert.AreEqual(1, fixture.Decoder.Decodes);
    }

    [TestMethod]
    public async Task DeadlineFiringBeforeTheReceiveGateIsReleasedCannotLoseTheLastPacket()
    {
        var clock = new DeadlineClock { FireImmediatelyAtZero = true };
        await using var fixture = new Fixture(bridges: CreateFactory(clock));
        var payload = new AudioEncoder().EncodeAudio(Samples(160, 0), fixture.Format);
        fixture.Dispatch(new(17, 10, 1000, 8000), payload);
        // The deadline crosses between the unsuccessful dequeue and arming the timer.
        clock.ExpireOnThirdRead = true;
        fixture.Dispatch(new(17, 12, 1320, 8000), payload); Assert.AreEqual(1, fixture.Decoder.Decodes);
        clock.Advance(TimeSpan.FromMilliseconds(1)); Assert.AreEqual(2, fixture.Decoder.Decodes);
    }

    private sealed class ReceiverLog : ILogger<SipRtpPcmBridgeFactory>
    {
        internal ConcurrentQueue<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Assert.IsNull(exception); Messages.Enqueue(formatter(state, exception)); }
    }

    private sealed class DeadlineClock : TimeProvider
    {
        private long _ticks; private ManualTimer? _timer; private int _reads;
        internal bool FireImmediatelyAtZero, ExpireOnThirdRead;
        internal bool TimerDisposed => _timer?.Disposed == true;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp()
        {
            if (ExpireOnThirdRead && ++_reads == 3) { _ticks += TimeSpan.FromMilliseconds(60).Ticks; ExpireOnThirdRead = false; }
            return _ticks;
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.IsNull(_timer); _timer = new(this, callback, state); _timer.Change(dueTime, period); return _timer;
        }
        internal void Advance(TimeSpan elapsed)
        {
            _ticks += elapsed.Ticks;
            if (_timer is { Disposed: false } timer && timer.Due <= _ticks) { timer.Due = long.MaxValue; timer.Fire(); }
        }
        private sealed class ManualTimer(DeadlineClock clock, TimerCallback callback, object? state) : ITimer
        {
            internal long Due = long.MaxValue; internal bool Disposed;
            internal void Fire() => callback(state);
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                Assert.AreEqual(Timeout.InfiniteTimeSpan, period);
                if (Disposed) throw new ObjectDisposedException(nameof(ManualTimer));
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : clock._ticks + dueTime.Ticks;
                if (dueTime == TimeSpan.Zero && clock.FireImmediatelyAtZero) { Due = long.MaxValue; Fire(); }
                return true;
            }
            public void Dispose() { Disposed = true; Due = long.MaxValue; }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
