using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class RtpAudioPacketBufferTests
{
    private static RtpPcmPacketPosition Position(ushort sequence, uint timestamp = 1000) => new(17, sequence, timestamp, 8000);
    private static IRtpAudioPacketBuffer Create(Clock clock, RtpAudioPacketBufferOptions? options = null) =>
        new RtpAudioPacketBufferFactory(clock).Create(options ?? new(17, 10, 8000));

    [TestMethod]
    public void ReorderedPayloadsAreReleasedOnlyInSequenceOrderWithTheirOwnMetadata()
    {
        var clock = new Clock(); using var buffer = Create(clock);
        Assert.AreEqual(RtpAudioEnqueueResult.Accepted, buffer.TryEnqueue(Position(12, 1320), 8, [12]));
        Assert.AreEqual(RtpAudioEnqueueResult.Accepted, buffer.TryEnqueue(Position(10, 1000), 0, [10]));
        Assert.IsTrue(buffer.TryDequeue(out var first)); Assert.AreEqual((ushort)10, first.Position.SequenceNumber);
        Assert.AreEqual(0, first.PayloadType); Assert.AreEqual((byte)10, first.Payload.Span[0]);
        Assert.IsFalse(buffer.TryDequeue(out _)); Assert.AreEqual(TimeSpan.FromMilliseconds(60), buffer.NextPacketDelay);
        clock.Advance(TimeSpan.FromMilliseconds(59)); buffer.TryEnqueue(Position(11, 1160), 9, [11]);
        Assert.AreEqual(TimeSpan.Zero, buffer.NextPacketDelay);
        Assert.IsTrue(buffer.TryDequeue(out var second)); Assert.AreEqual((ushort)11, second.Position.SequenceNumber); Assert.AreEqual(9, second.PayloadType);
        Assert.IsTrue(buffer.TryDequeue(out var third)); Assert.AreEqual(1320u, third.Position.Timestamp); Assert.AreEqual(8, third.PayloadType);
        Assert.IsNull(buffer.NextPacketDelay); Assert.AreEqual(0L, buffer.MissingPackets);
    }

    [TestMethod]
    public void RolloverUsesModularSequenceOrderWithoutChangingRtpTimestamps()
    {
        var clock = new Clock(); using var buffer = Create(clock, new(17, ushort.MaxValue, 8000));
        buffer.TryEnqueue(Position(1, 160), 8, [3]); buffer.TryEnqueue(Position(0, 0), 8, [2]); buffer.TryEnqueue(Position(ushort.MaxValue, uint.MaxValue - 159), 8, [1]);
        var packets = Drain(buffer); CollectionAssert.AreEqual(new ushort[] { ushort.MaxValue, 0, 1 }, packets.Select(packet => packet.Position.SequenceNumber).ToArray());
        CollectionAssert.AreEqual(new uint[] { uint.MaxValue - 159, 0, 160 }, packets.Select(packet => packet.Position.Timestamp).ToArray());
        Assert.AreEqual(0L, buffer.MissingPackets);
    }

    [TestMethod]
    public void MissingInitialPacketsExpireWithoutAnotherArrivalAndLatePacketsNeverReachTheDecoder()
    {
        var clock = new Clock(); using var buffer = Create(clock);
        buffer.TryEnqueue(Position(12, 1320), 8, [12]); Assert.IsFalse(buffer.TryDequeue(out _));
        clock.Advance(TimeSpan.FromMilliseconds(59)); Assert.IsFalse(buffer.TryDequeue(out _));
        clock.Advance(TimeSpan.FromMilliseconds(1)); Assert.AreEqual(TimeSpan.Zero, buffer.NextPacketDelay);
        Assert.IsTrue(buffer.TryDequeue(out var packet)); Assert.AreEqual((ushort)12, packet.Position.SequenceNumber); Assert.AreEqual(2L, buffer.MissingPackets);
        Assert.AreEqual(RtpAudioEnqueueResult.Late, buffer.TryEnqueue(Position(10), 8, [10]));
        Assert.AreEqual(RtpAudioEnqueueResult.Late, buffer.TryEnqueue(Position(11, 1160), 8, [11]));
        Assert.AreEqual(2L, buffer.LatePackets); Assert.AreEqual(0, buffer.Count); Assert.IsFalse(buffer.TryDequeue(out _));
    }

    [TestMethod]
    public void AdditionalArrivalsAndWallClockChangesDoNotExtendAnExistingGapDeadline()
    {
        var clock = new Clock(); using var buffer = Create(clock);
        buffer.TryEnqueue(Position(12, 1320), 8, [12]); clock.Advance(TimeSpan.FromMilliseconds(40));
        buffer.TryEnqueue(Position(13, 1480), 8, [13]); clock.WallClock = DateTimeOffset.UnixEpoch.AddYears(20);
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), buffer.NextPacketDelay);
        clock.Advance(TimeSpan.FromMilliseconds(20)); CollectionAssert.AreEqual(new ushort[] { 12, 13 }, Drain(buffer).Select(packet => packet.Position.SequenceNumber).ToArray());
        Assert.AreEqual(2L, buffer.MissingPackets);
    }

    [TestMethod]
    public void ASubsequentGapKeepsTheAgeOfPacketsAlreadyWaiting()
    {
        var clock = new Clock(); using var buffer = Create(clock);
        buffer.TryEnqueue(Position(12, 1320), 8, [12]); clock.Advance(TimeSpan.FromMilliseconds(40));
        buffer.TryEnqueue(Position(10), 8, [10]); Assert.IsTrue(buffer.TryDequeue(out _));
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), buffer.NextPacketDelay);
        clock.Advance(TimeSpan.FromMilliseconds(20)); Assert.IsTrue(buffer.TryDequeue(out _)); Assert.AreEqual(1L, buffer.MissingPackets);
    }

    [TestMethod]
    public void ZeroDelaySkipsGapsWithoutIntroducingAWait()
    {
        var clock = new Clock(); using var buffer = Create(clock, new(17, 10, 8000, ReorderDelay: TimeSpan.Zero));
        buffer.TryEnqueue(Position(11), 8, [11]); Assert.AreEqual(TimeSpan.Zero, buffer.NextPacketDelay);
        Assert.IsTrue(buffer.TryDequeue(out _)); Assert.AreEqual(1L, buffer.MissingPackets);
    }

    [TestMethod]
    public void CompletingInputFlushesInSequenceOrderAndRejectsFurtherInput()
    {
        var clock = new Clock(); using var buffer = Create(clock);
        buffer.TryEnqueue(Position(15), 8, [15]); buffer.TryEnqueue(Position(11), 8, [11]); buffer.TryEnqueue(Position(13), 8, [13]);
        buffer.CompleteInput(); buffer.CompleteInput(); Assert.AreEqual(TimeSpan.Zero, buffer.NextPacketDelay);
        Assert.AreEqual(RtpAudioEnqueueResult.Closed, buffer.TryEnqueue(Position(10), 8, [10]));
        CollectionAssert.AreEqual(new ushort[] { 11, 13, 15 }, Drain(buffer).Select(packet => packet.Position.SequenceNumber).ToArray());
        Assert.AreEqual(3L, buffer.MissingPackets); Assert.IsNull(buffer.NextPacketDelay); Assert.IsNull(buffer.Failure);
    }

    [TestMethod]
    public void QueuedDuplicatesDoNotUseCapacityOrExtendTheDeadline()
    {
        var clock = new Clock(); using var buffer = Create(clock, new(17, 10, 8000, Capacity: 1));
        buffer.TryEnqueue(Position(11, 1160), 8, [1, 2]); clock.Advance(TimeSpan.FromMilliseconds(50));
        Assert.AreEqual(RtpAudioEnqueueResult.Duplicate, buffer.TryEnqueue(Position(11, 1160), 8, [1, 2]));
        Assert.AreEqual(1, buffer.Count); Assert.AreEqual(1L, buffer.DuplicatePackets); Assert.AreEqual(TimeSpan.FromMilliseconds(10), buffer.NextPacketDelay);
        clock.Advance(TimeSpan.FromMilliseconds(10)); Assert.IsTrue(buffer.TryDequeue(out _));
        Assert.AreEqual(RtpAudioEnqueueResult.Late, buffer.TryEnqueue(Position(11, 1160), 8, [1, 2]));
    }

    [TestMethod]
    [DataRow("timestamp")]
    [DataRow("payload-type")]
    [DataRow("payload")]
    public void ConflictingCopiesOfAQueuedSequenceFailInsteadOfSelectingAnArbitraryCodecInput(string mode)
    {
        var clock = new Clock(); using var buffer = Create(clock); buffer.TryEnqueue(Position(11), 8, [1]);
        Assert.AreEqual(RtpAudioEnqueueResult.Failed, buffer.TryEnqueue(Position(11, mode == "timestamp" ? 2000u : 1000u), mode == "payload-type" ? 9 : 8, mode == "payload" ? [2] : [1]));
        Assert.AreEqual(RtpAudioBufferFailure.SequenceConflict, buffer.Failure); Assert.AreEqual(0, buffer.Count); Assert.IsFalse(buffer.TryDequeue(out _));
    }

    [TestMethod]
    public void OverflowIsTerminalClearsPayloadsAndPreservesItsFirstCause()
    {
        var clock = new Clock(); using var buffer = Create(clock, new(17, 10, 8000, Capacity: 2));
        buffer.TryEnqueue(Position(11), 8, [11]); buffer.TryEnqueue(Position(12), 8, [12]);
        Assert.AreEqual(RtpAudioEnqueueResult.Failed, buffer.TryEnqueue(Position(13), 8, [13]));
        Assert.AreEqual(RtpAudioBufferFailure.Overflow, buffer.Failure); Assert.AreEqual(0, buffer.Count);
        buffer.CompleteInput(); buffer.Dispose(); Assert.AreEqual(RtpAudioEnqueueResult.Failed, buffer.TryEnqueue(Position(10), -1, []));
        Assert.AreEqual(RtpAudioBufferFailure.Overflow, buffer.Failure); Assert.IsNull(buffer.NextPacketDelay); Assert.IsFalse(buffer.TryDequeue(out _));
    }

    [TestMethod]
    public void PayloadIsCopiedBeforeReturnAndTransferredPacketsOutliveBufferDisposal()
    {
        var clock = new Clock(); var buffer = Create(clock); var payload = new byte[] { 1, 2, 3 };
        buffer.TryEnqueue(Position(10), 8, payload); payload[0] = 99;
        Assert.IsTrue(buffer.TryDequeue(out var packet)); buffer.TryEnqueue(Position(12), 8, [12]); buffer.Dispose(); buffer.Dispose();
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, packet.Payload.ToArray()); Assert.AreEqual(0, buffer.Count); Assert.IsNull(buffer.NextPacketDelay);
        Assert.AreEqual(RtpAudioEnqueueResult.Closed, buffer.TryEnqueue(Position(11), 8, [11])); Assert.IsFalse(buffer.TryDequeue(out _));
    }

    [TestMethod]
    [DataRow("empty", RtpAudioBufferFailure.InvalidPacket)]
    [DataRow("large", RtpAudioBufferFailure.InvalidPacket)]
    [DataRow("type-negative", RtpAudioBufferFailure.InvalidPacket)]
    [DataRow("type-large", RtpAudioBufferFailure.InvalidPacket)]
    [DataRow("rate", RtpAudioBufferFailure.InvalidPacket)]
    [DataRow("source", RtpAudioBufferFailure.SourceChanged)]
    [DataRow("clock", RtpAudioBufferFailure.ClockChanged)]
    [DataRow("half-sequence", RtpAudioBufferFailure.SequenceConflict)]
    [DataRow("jump", RtpAudioBufferFailure.SequenceConflict)]
    public void InvalidInputFailsBeforeDecodingAndDiscardsQueuedData(string mode, RtpAudioBufferFailure expected)
    {
        var clock = new Clock(); using var buffer = Create(clock); buffer.TryEnqueue(Position(11), 8, [11]);
        var position = Position(mode == "half-sequence" ? (ushort)32778 : mode == "jump" ? (ushort)1035 : (ushort)10) with
        { SynchronizationSource = mode == "source" ? 18u : 17u, RtpClockRate = mode == "rate" ? 0 : mode == "clock" ? 16000 : 8000 };
        Assert.AreEqual(RtpAudioEnqueueResult.Failed, buffer.TryEnqueue(position, mode == "type-negative" ? -1 : mode == "type-large" ? 128 : 8, new byte[mode == "empty" ? 0 : mode == "large" ? 4097 : 1]));
        Assert.AreEqual(expected, buffer.Failure); Assert.AreEqual(0, buffer.Count); Assert.IsFalse(buffer.TryDequeue(out _));
    }

    [TestMethod]
    public void MaximumCapacityAndPayloadAreAcceptedWithoutAnUnboundedQueue()
    {
        var clock = new Clock(); using var buffer = Create(clock, new(17, 10, 192000, Capacity: 256, ReorderDelay: TimeSpan.FromSeconds(1)));
        var payload = new byte[4096];
        for (var index = 0; index < 256; index++) Assert.AreEqual(RtpAudioEnqueueResult.Accepted, buffer.TryEnqueue(new(17, (ushort)(11 + index), (uint)index, 192000), 127, payload));
        Assert.AreEqual(256, buffer.Count); Assert.AreEqual(RtpAudioEnqueueResult.Failed, buffer.TryEnqueue(new(17, 267, 256, 192000), 127, payload));
        Assert.AreEqual(RtpAudioBufferFailure.Overflow, buffer.Failure);
    }

    [TestMethod]
    public async Task ConcurrentEnqueuesHaveOneOwnedPayloadAndCountAllDuplicates()
    {
        var clock = new Clock(); using var buffer = Create(clock, new(17, 10, 8000, Capacity: 1));
        var results = await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => buffer.TryEnqueue(Position(10), 8, [1]))));
        Assert.AreEqual(1, results.Count(result => result == RtpAudioEnqueueResult.Accepted)); Assert.AreEqual(99L, buffer.DuplicatePackets); Assert.AreEqual(1, buffer.Count);
        Assert.IsTrue(buffer.TryDequeue(out _)); Assert.IsFalse(buffer.TryDequeue(out _));
    }

    [TestMethod]
    public void InvalidOptionsFailWithoutCreatingTimersOrAcquiringOtherResources()
    {
        var factory = new RtpAudioPacketBufferFactory(new Clock()); var options = new RtpAudioPacketBufferOptions(17, 10, 8000);
        foreach (var invalid in new[] { options with { Capacity = 0 }, options with { Capacity = 257 }, options with { RtpClockRate = 0 },
            options with { RtpClockRate = 192001 }, options with { ReorderDelay = TimeSpan.FromTicks(-1) }, options with { ReorderDelay = TimeSpan.FromSeconds(1) + TimeSpan.FromTicks(1) } })
            Assert.ThrowsExactly<ArgumentException>(() => factory.Create(invalid));
        Assert.ThrowsExactly<ArgumentNullException>(() => factory.Create(null!));
    }

    private static List<BufferedRtpAudioPacket> Drain(IRtpAudioPacketBuffer buffer)
    {
        var packets = new List<BufferedRtpAudioPacket>(); while (buffer.TryDequeue(out var packet)) packets.Add(packet); return packets;
    }
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        internal DateTimeOffset WallClock { get; set; } = DateTimeOffset.UnixEpoch;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => WallClock;
        internal void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => throw new AssertFailedException("The passive packet buffer must not create timers.");
    }
}
