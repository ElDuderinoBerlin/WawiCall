using System.Collections.Concurrent;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class RtpPcmRecordingFeedTests
{
    private static RtpPcmTimelineAnchor Anchor => new(17, 10, 1000, 8000, TimeSpan.Zero);
    private static RtpPcmPacketPosition Packet(ushort sequence, uint timestamp) => new(17, sequence, timestamp, 8000);

    [TestMethod]
    public async Task G722UsesRtpTicksForTimeAndActualPcmRateForSamplesAndKeepsGaps()
    {
        var session = new Session(); await using var feed = Create(session);
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(10, 1000), 16000, Samples(320)));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(12, 1480), 16000, Samples(320)));
        var blocks = session.Blocks.ToArray(); Assert.AreEqual(2, blocks.Length);
        Assert.AreEqual(TimeSpan.Zero, blocks[0].Start); Assert.AreEqual(TimeSpan.FromMilliseconds(60), blocks[1].Start);
        Assert.AreEqual(16000, blocks[1].Rate); Assert.AreEqual(320, blocks[1].Samples.Length); Assert.AreEqual(TimeSpan.FromMilliseconds(80), feed.LastPacketEnd);
    }

    [TestMethod]
    public async Task LiveG722KeepsEarlierSamplesAndClipsOnlyTheOverlappingPrefix()
    {
        var session = new Session();
        await using var feed = new RtpPcmRecordingFeedFactory().Create(session, ConversationAudioSide.Received,
            Anchor with { TrimShortG722Overlap = true });
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(10, 1000), 16000, Samples(512)));
        var input = Enumerable.Range(0, 320).Select(value => (short)value).ToArray();
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(11, 1160), 16000, input));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(13, 1480), 16000, Samples(320)));
        var blocks = session.Blocks.ToArray();
        Assert.AreEqual(512, blocks[0].Samples.Length);
        Assert.AreEqual(TimeSpan.FromMilliseconds(32), blocks[1].Start);
        CollectionAssert.AreEqual(input[192..], blocks[1].Samples);
        Assert.AreEqual(192L, feed.TrimmedSamples);
        Assert.AreEqual(TimeSpan.FromMilliseconds(60), blocks[2].Start);
        Assert.AreEqual(TimeSpan.FromMilliseconds(80), feed.LastPacketEnd);
    }

    [TestMethod]
    [DataRow(true, 11, 160, 16_000, 700, 640)]
    [DataRow(true, 11, 160, 16_000, 512, 192)]
    [DataRow(true, 12, 160, 16_000, 512, 320)]
    [DataRow(true, 11, 0, 16_000, 512, 640)]
    [DataRow(true, 11, 160, 8_000, 512, 320)]
    [DataRow(false, 11, 160, 16_000, 512, 320)]
    public async Task G722KeepsRejectingAmbiguousOrLargeOverlaps(bool allowTrim, int sequence, int delta, int rate,
        int precedingSamples, int currentSamples)
    {
        var session = new Session();
        await using var feed = new RtpPcmRecordingFeedFactory().Create(session, ConversationAudioSide.Received,
            Anchor with { TrimShortG722Overlap = allowTrim });
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(10, 1000), 16000, Samples(precedingSamples)));
        Assert.AreEqual(RtpPcmAppendResult.Failed, feed.TryAppend(Packet((ushort)sequence, (uint)(1000 + delta)), rate, Samples(currentSamples)));
        Assert.AreEqual(RtpPcmFeedFailure.Overlap, feed.Failure);
        Assert.AreEqual(PcmRecordingFailure.TimelineConflict, session.Failure);
        Assert.HasCount(1, session.Blocks);
    }

    [TestMethod]
    public async Task TwoSidesUseTheSameExplicitRecordingEpochWithoutConsultingArrivalTime()
    {
        var session = new Session(); var factory = new RtpPcmRecordingFeedFactory();
        await using var sent = factory.Create(session, ConversationAudioSide.Sent, Anchor with { RecordingOffset = TimeSpan.FromMilliseconds(10) });
        await using var received = factory.Create(session, ConversationAudioSide.Received, new(91, 301, 999900, 16000, TimeSpan.FromMilliseconds(40)));
        sent.TryAppend(Packet(10, 1000), 8000, Samples(160));
        received.TryAppend(new(91, 301, 999900, 16000), 16000, Samples(320));
        var blocks = session.Blocks.ToArray(); Assert.AreEqual(TimeSpan.FromMilliseconds(10), blocks[0].Start);
        Assert.AreEqual(TimeSpan.FromMilliseconds(40), blocks[1].Start); Assert.AreEqual(ConversationAudioSide.Received, blocks[1].Side);
    }

    [TestMethod]
    public async Task MissingInitialPacketsAndTimestampWrapPreserveTheirExactPositions()
    {
        var session = new Session(); var anchor = Anchor with { SequenceNumber = ushort.MaxValue, Timestamp = uint.MaxValue - 159 };
        await using var feed = Create(session, anchor);
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(0, 0), 8000, Samples(160)));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(1, 160), 8000, Samples(160)));
        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(40) }, session.Blocks.Select(block => block.Start).ToArray());
        Assert.AreEqual(TimeSpan.FromMilliseconds(60), feed.LastPacketEnd);
    }

    [TestMethod]
    public async Task MultipleTimestampWrapsAreUnwrappedFromBoundedForwardDeltas()
    {
        var session = new Session(); var anchor = new RtpPcmTimelineAnchor(17, 10, uint.MaxValue - 99, 192000, TimeSpan.Zero);
        await using var feed = Create(session, anchor);
        for (var index = 0; index <= 100; index++)
        {
            var timestamp = unchecked(anchor.Timestamp + (uint)((long)index * 192000 * 240));
            Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(new(17, (ushort)(10 + index), timestamp, 192000), 192000, Samples(1920)));
        }
        Assert.AreEqual(TimeSpan.FromSeconds(24000) + TimeSpan.FromMilliseconds(10), feed.LastPacketEnd);
    }

    [TestMethod]
    public async Task DuplicateAndLatePacketsAreReportedAndNeverAppendedTwice()
    {
        var session = new Session(); await using var feed = Create(session);
        feed.TryAppend(Packet(10, 1000), 8000, Samples(160)); feed.TryAppend(Packet(11, 1160), 8000, Samples(160));
        Assert.AreEqual(RtpPcmAppendResult.Duplicate, feed.TryAppend(Packet(11, 1160), 8000, Samples(160)));
        Assert.AreEqual(RtpPcmAppendResult.Late, feed.TryAppend(Packet(10, 1000), 8000, Samples(160)));
        Assert.AreEqual(1L, feed.DuplicatePackets); Assert.AreEqual(1L, feed.LatePackets); Assert.AreEqual(2, session.Blocks.Count);
        Assert.IsNull(feed.Failure); Assert.AreEqual(TimeSpan.FromMilliseconds(40), feed.LastPacketEnd);
    }

    [TestMethod]
    public async Task PacketOlderThanTheExplicitAnchorIsIgnoredWithoutInventingANewAnchor()
    {
        var session = new Session(); await using var feed = Create(session);
        Assert.AreEqual(RtpPcmAppendResult.Late, feed.TryAppend(Packet(9, 840), 8000, Samples(160)));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(10, 1000), 8000, Samples(160)));
        Assert.AreEqual(TimeSpan.Zero, session.Blocks.Single().Start);
    }

    [TestMethod]
    public async Task RecordingInsideAPacketTrimsOnlyPreRecordingSamples()
    {
        var session = new Session(); await using var feed = Create(session, Anchor with { RecordingOffset = TimeSpan.FromMilliseconds(-5) });
        var input = Enumerable.Range(0, 320).Select(value => (short)value).ToArray();
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(10, 1000), 16000, input));
        var block = session.Blocks.Single(); Assert.AreEqual(TimeSpan.Zero, block.Start); Assert.AreEqual(240, block.Samples.Length);
        Assert.AreEqual((short)80, block.Samples[0]); Assert.AreEqual(80L, feed.TrimmedSamples); Assert.AreEqual(TimeSpan.FromMilliseconds(15), feed.LastPacketEnd);
    }

    [TestMethod]
    public async Task EntirePacketsBeforeRecordingAdvanceTheTimelineWithoutAppendingPcm()
    {
        var session = new Session(); await using var feed = Create(session, Anchor with { RecordingOffset = TimeSpan.FromMilliseconds(-40) });
        Assert.AreEqual(RtpPcmAppendResult.BeforeRecording, feed.TryAppend(Packet(10, 1000), 8000, Samples(160)));
        Assert.AreEqual(RtpPcmAppendResult.BeforeRecording, feed.TryAppend(Packet(11, 1160), 8000, Samples(160)));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(12, 1320), 8000, Samples(160)));
        Assert.AreEqual(320L, feed.TrimmedSamples); Assert.AreEqual(TimeSpan.Zero, session.Blocks.Single().Start);
    }

    [TestMethod]
    public async Task LargeBlocksSplitIntoExactHundredMillisecondChunksWithoutRoundingDrift()
    {
        var session = new Session(); await using var feed = Create(session, Anchor with { RtpClockRate = 44100 });
        var input = Enumerable.Range(0, 88200).Select(index => (short)(index % 12345)).ToArray();
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(new(17, 10, 1000, 44100), 44100, input));
        var blocks = session.Blocks.ToArray(); Assert.AreEqual(20, blocks.Length);
        for (var index = 0; index < blocks.Length; index++) { Assert.AreEqual(4410, blocks[index].Samples.Length); Assert.AreEqual(TimeSpan.FromMilliseconds(index * 100), blocks[index].Start); }
        CollectionAssert.AreEqual(input, blocks.SelectMany(block => block.Samples).ToArray()); Assert.AreEqual(TimeSpan.FromSeconds(2), feed.LastPacketEnd);
    }

    [TestMethod]
    public async Task PcmRateChangesDoNotChangeTheRtpTimebase()
    {
        var session = new Session(); await using var feed = Create(session);
        feed.TryAppend(Packet(10, 1000), 8000, Samples(160));
        Assert.AreEqual(RtpPcmAppendResult.Accepted, feed.TryAppend(Packet(11, 1160), 16000, Samples(320)));
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), session.Blocks.Last().Start); Assert.AreEqual(TimeSpan.FromMilliseconds(40), feed.LastPacketEnd);
    }

    [TestMethod]
    [DataRow("source", RtpPcmFeedFailure.SourceChanged)]
    [DataRow("clock", RtpPcmFeedFailure.ClockChanged)]
    [DataRow("same-sequence", RtpPcmFeedFailure.SequenceConflict)]
    [DataRow("sequence-backward-time-forward", RtpPcmFeedFailure.SequenceConflict)]
    [DataRow("ambiguous-sequence", RtpPcmFeedFailure.SequenceConflict)]
    [DataRow("time-backward", RtpPcmFeedFailure.TimestampDiscontinuity)]
    [DataRow("large-jump", RtpPcmFeedFailure.TimestampDiscontinuity)]
    [DataRow("overlap", RtpPcmFeedFailure.Overlap)]
    public async Task ContradictoryMetadataFailsTheRecordingInsteadOfProducingPartialSuccess(string mode, RtpPcmFeedFailure expected)
    {
        var session = new Session(); await using var feed = Create(session); feed.TryAppend(Packet(10, 1000), 8000, Samples(160));
        var packet = mode switch
        {
            "source" => new(18, (ushort)11, 1160u, 8000), "clock" => new(17, (ushort)11, 1160u, 16000),
            "same-sequence" => Packet(10, 1160), "sequence-backward-time-forward" => Packet(9, 1160),
            "ambiguous-sequence" => Packet(32778, 840),
            "time-backward" => Packet(11, 999), "large-jump" => Packet(11, 1000 + 8000 * 301), _ => Packet(11, 1080)
        };
        Assert.AreEqual(RtpPcmAppendResult.Failed, feed.TryAppend(packet, 8000, Samples(160))); Assert.AreEqual(expected, feed.Failure);
        Assert.AreEqual(PcmRecordingState.Failed, session.State); Assert.AreEqual(PcmRecordingFailure.TimelineConflict, session.Failure);
        Assert.AreEqual(1, session.Blocks.Count); Assert.AreEqual(RtpPcmAppendResult.Failed, feed.TryAppend(Packet(12, 1320), 8000, Samples(160)));
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("rate")]
    [DataRow("size")]
    [DataRow("rtp-rate")]
    [DataRow("window")]
    public async Task InvalidBlocksAreRejectedBeforeAppending(string mode)
    {
        var session = new Session(); await using var feed = Create(session, mode == "window" ? Anchor with { RecordingOffset = TimeSpan.FromHours(8) - TimeSpan.FromMilliseconds(10) } : Anchor);
        Assert.AreEqual(RtpPcmAppendResult.Failed, feed.TryAppend(mode == "rtp-rate" ? Packet(10, 1000) with { RtpClockRate = 0 } : Packet(10, 1000),
            mode == "rate" ? 0 : 8000, Samples(mode == "empty" ? 0 : mode == "size" ? 16001 : 160)));
        Assert.AreEqual(RtpPcmFeedFailure.InvalidPacket, feed.Failure); Assert.AreEqual(PcmRecordingFailure.InvalidBlock, session.Failure); Assert.AreEqual(0, session.Blocks.Count);
    }

    [TestMethod]
    public async Task AnIncorrectFirstAnchorPacketDoesNotSilentlyRebaseTheRecording()
    {
        var session = new Session(); await using var feed = Create(session);
        Assert.AreEqual(RtpPcmAppendResult.Failed, feed.TryAppend(Packet(10, 1160), 8000, Samples(160)));
        Assert.AreEqual(RtpPcmFeedFailure.SequenceConflict, feed.Failure);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RecordingRejectionAndForeignExceptionsNeverEscapeTheMediaCallback(bool throws)
    {
        var session = new Session { Reject = true, Throws = throws }; await using var feed = Create(session);
        Assert.AreEqual(RtpPcmAppendResult.Failed, feed.TryAppend(Packet(10, 1000), 8000, Samples(160)));
        Assert.AreEqual(RtpPcmFeedFailure.RecordingRejected, feed.Failure); Assert.AreEqual(PcmRecordingState.Failed, session.State);
    }

    [TestMethod]
    public async Task ConcurrentInputFailsWithoutWaitingAndDisposalDrainsTheOriginalCallback()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var session = new Session { BeforeAppend = () => { entered.Set(); release.Wait(); } }; var feed = Create(session);
        var first = Task.Run(() => feed.TryAppend(Packet(10, 1000), 8000, Samples(160)));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.AreEqual(RtpPcmAppendResult.Failed, feed.TryAppend(Packet(11, 1160), 8000, Samples(160)));
            Assert.AreEqual(RtpPcmFeedFailure.ConcurrentInput, feed.Failure); Assert.IsFalse(feed.DisposeAsync().IsCompleted);
        }
        finally { release.Set(); }
        Assert.AreEqual(RtpPcmAppendResult.Failed, await first); await feed.DisposeAsync(); Assert.AreEqual(0, session.Blocks.Count);
    }

    [TestMethod]
    public async Task NormalDisposalRetiresInputAndKeepsTheBorrowedSessionOpen()
    {
        var session = new Session(); var feed = Create(session); await feed.DisposeAsync(); await feed.DisposeAsync();
        Assert.AreEqual(RtpPcmAppendResult.Closed, feed.TryAppend(Packet(10, 1000), 8000, Samples(160)));
        Assert.AreEqual(PcmRecordingState.Capturing, session.State); Assert.IsNull(session.Failure);
    }

    [TestMethod]
    public async Task AStoppingSessionRejectsLateInputWithoutTurningANormalStopIntoFailure()
    {
        var session = new Session(); await using var feed = Create(session); session.CurrentState = PcmRecordingState.Stopping;
        Assert.AreEqual(RtpPcmAppendResult.Closed, feed.TryAppend(Packet(10, 1000), 8000, Samples(160))); Assert.IsNull(feed.Failure); Assert.IsNull(session.Failure);
    }

    [TestMethod]
    public void InvalidAnchorsAndUnguardedSessionsFailBeforeAnyPcmIsAccepted()
    {
        var factory = new RtpPcmRecordingFeedFactory(); var session = new Session();
        foreach (var anchor in new[] { Anchor with { RtpClockRate = 0 }, Anchor with { RtpClockRate = 192001 },
            Anchor with { RecordingOffset = TimeSpan.FromSeconds(-3) }, Anchor with { RecordingOffset = TimeSpan.FromHours(8) } })
            Assert.ThrowsExactly<ArgumentException>(() => factory.Create(session, ConversationAudioSide.Sent, anchor));
        Assert.ThrowsExactly<ArgumentException>(() => factory.Create(session, (ConversationAudioSide)77, Anchor));
        session.CurrentState = PcmRecordingState.Completed; Assert.ThrowsExactly<ArgumentException>(() => factory.Create(session, ConversationAudioSide.Sent, Anchor));
        Assert.ThrowsExactly<ArgumentException>(() => factory.Create(new UnguardedSession(), ConversationAudioSide.Sent, Anchor));
        Assert.AreEqual(0, session.Blocks.Count); Assert.AreEqual(nameof(RtpPcmTimelineAnchor), Anchor.ToString());
    }

    private static IRtpPcmRecordingFeed Create(Session session, RtpPcmTimelineAnchor? anchor = null) => new RtpPcmRecordingFeedFactory().Create(session, ConversationAudioSide.Sent, anchor ?? Anchor);
    private static short[] Samples(int count) => Enumerable.Repeat((short)1234, count).ToArray();
    private sealed record Block(ConversationAudioSide Side, TimeSpan Start, int Rate, short[] Samples);
    private class UnguardedSession : IPcmRecordingSession
    {
        internal PcmRecordingState CurrentState;
        public PcmRecordingState State => CurrentState;
        public PcmRecordingFailure? Failure { get; protected set; }
        public string SegmentDirectory => "";
        public Task CaptureCompletion => Task.CompletedTask;
        public virtual bool TryAppend(ConversationAudioSide side, TimeSpan start, int rate, ReadOnlySpan<short> samples) => throw new NotSupportedException();
        public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class Session : UnguardedSession, IPcmRecordingInputGuard
    {
        internal ConcurrentQueue<Block> Blocks { get; } = new(); internal bool Reject, Throws; internal Action? BeforeAppend;
        public override bool TryAppend(ConversationAudioSide side, TimeSpan start, int rate, ReadOnlySpan<short> samples)
        {
            BeforeAppend?.Invoke(); if (Throws) throw new IOException("synthetic-private-detail");
            if (Reject || State != PcmRecordingState.Capturing) return false;
            Blocks.Enqueue(new(side, start, rate, samples.ToArray())); return true;
        }
        public void RejectInput(PcmRecordingFailure failure) { Failure ??= failure; CurrentState = PcmRecordingState.Failed; }
    }
}
