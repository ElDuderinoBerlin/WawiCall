using System.Collections.Concurrent;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class SentPcmRecordingBindingTests
{
    private static readonly SentPcmTimelineAnchor Anchor = new(17, 1000, 8000, TimeSpan.Zero);
    [TestMethod]
    public async Task TimestampWrapAndExtrasGapsRemainOnTheExplicitRecordingTimeline()
    {
        var source = new Source(); var session = new Session();
        await using var binding = new SentPcmRecordingBindingFactory().Attach(source, session, Anchor with { Timestamp = uint.MaxValue - 159, RecordingOffset = TimeSpan.FromMilliseconds(10) });
        Assert.IsTrue(source.Emit(new(17, uint.MaxValue - 159, 8000, 160), 8000, new short[160]));
        Assert.IsTrue(source.Emit(new(17, 160, 8000, 160), 8000, new short[160]));
        CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(50) }, session.Blocks.Select(block => block.Start).ToArray());
        Assert.AreEqual(TimeSpan.FromMilliseconds(70), binding.LastWindowEnd); Assert.IsNull(binding.Failure);
    }

    [TestMethod]
    public async Task TwoSecondsAreSplitIntoBoundedChunksWithoutDriftAt192Khz()
    {
        var source = new Source(); var session = new Session();
        await using var binding = new SentPcmRecordingBindingFactory().Attach(source, session, Anchor);
        Assert.IsTrue(source.Emit(new(17, 1000, 8000, 16000), 192000, new short[384000]));
        Assert.AreEqual(20, session.Blocks.Count); Assert.AreEqual(TimeSpan.FromSeconds(2), binding.LastWindowEnd);
        var blocks = session.Blocks.ToArray();
        for (var index = 0; index < blocks.Length; index++)
        { Assert.AreEqual(TimeSpan.FromMilliseconds(index * 100), blocks[index].Start); Assert.AreEqual(19200, blocks[index].Samples.Length); }
    }

    [TestMethod]
    public async Task NegativeAnchorTrimsOnlyTheActualPcmSamplesBeforeRecordingStarts()
    {
        var source = new Source(); var session = new Session();
        await using var binding = new SentPcmRecordingBindingFactory().Attach(source, session, Anchor with { RecordingOffset = TimeSpan.FromMilliseconds(-5) });
        var samples = Enumerable.Range(0, 320).Select(index => (short)index).ToArray();
        Assert.IsTrue(source.Emit(new(17, 1000, 8000, 160), 16000, samples));
        Assert.AreEqual(80L, binding.TrimmedSamples); Assert.AreEqual(TimeSpan.Zero, session.Blocks.Single().Start);
        CollectionAssert.AreEqual(samples[80..], session.Blocks.Single().Samples);
    }

    [TestMethod]
    public async Task EntireWindowBeforeRecordingCanBeSkippedWithoutCreatingNegativeBlocks()
    {
        var source = new Source(); var session = new Session();
        await using var binding = new SentPcmRecordingBindingFactory().Attach(source, session, Anchor with { RecordingOffset = TimeSpan.FromSeconds(-2) });
        Assert.IsTrue(source.Emit(new(17, 1000, 8000, 160), 8000, new short[160]));
        Assert.AreEqual(0, session.Blocks.Count); Assert.AreEqual(160L, binding.TrimmedSamples); Assert.AreEqual(TimeSpan.Zero, binding.LastWindowEnd);
    }

    [TestMethod]
    [DataRow("source", SentPcmRecordingFailure.SourceChanged)]
    [DataRow("clock", SentPcmRecordingFailure.ClockChanged)]
    [DataRow("backward", SentPcmRecordingFailure.TimestampDiscontinuity)]
    [DataRow("jump", SentPcmRecordingFailure.TimestampDiscontinuity)]
    [DataRow("overlap", SentPcmRecordingFailure.Overlap)]
    [DataRow("duplicate", SentPcmRecordingFailure.Overlap)]
    [DataRow("duration", SentPcmRecordingFailure.InvalidPcm)]
    [DataRow("stereo", SentPcmRecordingFailure.InvalidPcm)]
    public async Task InvalidSourceWindowFailsBothBindingAndRecordingImmediately(string kind, SentPcmRecordingFailure expected)
    {
        var source = new Source(); var session = new Session();
        await using var binding = new SentPcmRecordingBindingFactory().Attach(source, session, Anchor);
        Assert.IsTrue(source.Emit(new(17, 1000, 8000, 160), 8000, new short[160]));
        var position = kind switch
        {
            "source" => new SentRtpPcmPosition(19, 1160, 8000, 160),
            "clock" => new(17, 1160, 16000, 320),
            "backward" => new(17, 900, 8000, 160),
            "jump" => new(17, 1000 + 8000 * 301, 8000, 160),
            "overlap" => new(17, 1080, 8000, 160),
            "duplicate" => new(17, 1000, 8000, 160),
            "duration" => new(17, 1160, 8000, 320),
            _ => new(17, 1160, 8000, 160)
        };
        Assert.IsFalse(source.Emit(position, 8000, new short[160], kind == "stereo" ? 2 : 1));
        Assert.AreEqual(expected, binding.Failure); Assert.AreEqual(PcmRecordingState.Failed, session.State);
        Assert.AreEqual(1, session.Blocks.Count); Assert.AreEqual(expected, await binding.Completion);
    }

    [TestMethod]
    public async Task PcmRateCanChangeWhileTheSameRtpClockRemainsUnambiguous()
    {
        var source = new Source(); var session = new Session();
        await using var binding = new SentPcmRecordingBindingFactory().Attach(source, session, Anchor);
        Assert.IsTrue(source.Emit(new(17, 1000, 8000, 160), 8000, new short[160]));
        Assert.IsTrue(source.Emit(new(17, 1160, 8000, 160), 16000, new short[320]));
        Assert.AreEqual(TimeSpan.FromMilliseconds(40), binding.LastWindowEnd);
        CollectionAssert.AreEqual(new[] { 8000, 16000 }, session.Blocks.Select(block => block.Rate).ToArray());
    }

    [TestMethod]
    public async Task NativeRtpRoundingCannotHideAnOverlapInTheActualPcmDuration()
    {
        var source = new Source(); var session = new Session();
        await using var binding = new SentPcmRecordingBindingFactory().Attach(source, session, Anchor);
        Assert.IsTrue(source.Emit(new(17, 1000, 8000, 160), 16000, new short[321]));
        Assert.IsFalse(source.Emit(new(17, 1160, 8000, 160), 16000, new short[320]));
        Assert.AreEqual(SentPcmRecordingFailure.Overlap, binding.Failure);
    }

    [TestMethod]
    public async Task FailureInTheRecordingKeepsItsFirstCauseAndDoesNotEscapeTheSource()
    {
        var source = new Source(); var session = new Session { RejectAppend = true };
        await using var binding = new SentPcmRecordingBindingFactory().Attach(source, session, Anchor);
        Assert.IsFalse(source.Emit(new(17, 1000, 8000, 160), 8000, new short[160]));
        Assert.AreEqual(PcmRecordingFailure.BufferOverflow, session.Failure); Assert.AreEqual(SentPcmRecordingFailure.RecordingRejected, binding.Failure);
    }

    [TestMethod]
    public async Task ConcurrentSourceCallbacksFailRatherThanInterleaveTheirRecordingChunks()
    {
        var source = new Source(); var session = new Session(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        session.AppendAction = () => { entered.Set(); release.Wait(); };
        await using var binding = new SentPcmRecordingBindingFactory().Attach(source, session, Anchor);
        var first = Task.Run(() => source.Emit(new(17, 1000, 8000, 160), 8000, new short[160]));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
            Assert.IsFalse(source.Emit(new(17, 1160, 8000, 160), 8000, new short[160]));
            Assert.AreEqual(SentPcmRecordingFailure.ConcurrentInput, binding.Failure); Assert.AreEqual(PcmRecordingState.Failed, session.State);
        }
        finally { release.Set(); }
        Assert.IsFalse(await first);
    }

    [TestMethod]
    [DataRow(-2001)]
    [DataRow(28800000)]
    public void InvalidAnchorIsRejectedBeforeSubscribing(int milliseconds)
    {
        var source = new Source(); var session = new Session();
        Assert.ThrowsExactly<ArgumentException>(() => new SentPcmRecordingBindingFactory().Attach(source, session, Anchor with { RecordingOffset = TimeSpan.FromMilliseconds(milliseconds) }));
        Assert.IsFalse(source.Subscribed); Assert.AreEqual(PcmRecordingState.Capturing, session.State);
    }

    [TestMethod]
    public async Task WindowsBeyondTheEightHourRecordingBoundFailBeforeAppending()
    {
        var source = new Source(); var session = new Session();
        await using var binding = new SentPcmRecordingBindingFactory().Attach(source, session, Anchor with { RecordingOffset = TimeSpan.FromHours(8) - TimeSpan.FromMilliseconds(10) });
        Assert.IsFalse(source.Emit(new(17, 1000, 8000, 160), 8000, new short[160]));
        Assert.AreEqual(0, session.Blocks.Count); Assert.AreEqual(SentPcmRecordingFailure.InvalidPcm, binding.Failure);
    }

    private sealed class Source : IConversationPcmFailureSource
    {
        private IConversationPcmObserver? _observer; private readonly Sub _subscription = new();
        internal bool Subscribed => _observer is not null;
        public long FailureCount => 0;
        public PcmObservationFailure? LastFailure => null;
        public IConversationPcmSubscription Subscribe(IConversationPcmObserver observer) { _observer = observer; return _subscription; }
        internal bool Emit(SentRtpPcmPosition position, int rate, short[] samples, int channels = 1)
        {
            if (!_subscription.Enter()) return false;
            try
            {
                var result = ((ISentRtpPcmObserver)_observer!).TryObserveSent(position, rate, channels, samples);
                if (!result)
                {
                    ((IConversationPcmFailureObserver)_observer!).OnPcmObservationFailed(PcmObservationFailure.ObserverRejected);
                    _subscription.Retire(PcmObservationFailure.ObserverRejected);
                }
                return result;
            }
            finally { _subscription.Exit(); }
        }
        private sealed class Sub : IConversationPcmSubscription
        {
            internal TaskCompletionSource<PcmObservationFailure?> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly object _gate = new(); private int _inflight; private bool _closed; private PcmObservationFailure? _reason;
            public Task<PcmObservationFailure?> Completion => Done.Task;
            internal bool Enter() { lock (_gate) { if (_closed) return false; _inflight++; return true; } }
            internal void Exit() { lock (_gate) { _inflight--; if (_closed && _inflight == 0) Done.TrySetResult(_reason); } }
            internal void Retire(PcmObservationFailure? failure) { lock (_gate) { _closed = true; _reason ??= failure; if (_inflight == 0) Done.TrySetResult(_reason); } }
            public ValueTask DisposeAsync() { Retire(null); return new(Done.Task); }
        }
    }
    private sealed record Block(TimeSpan Start, int Rate, short[] Samples);
    private sealed class Session : IPcmRecordingSession, IPcmRecordingInputGuard
    {
        internal readonly ConcurrentQueue<Block> Blocks = new(); internal bool RejectAppend; internal Action? AppendAction;
        public PcmRecordingState State { get; private set; } = PcmRecordingState.Capturing;
        public PcmRecordingFailure? Failure { get; private set; }
        public string SegmentDirectory => "unused";
        public Task CaptureCompletion => Task.CompletedTask;
        public bool TryAppend(ConversationAudioSide side, TimeSpan start, int rate, ReadOnlySpan<short> samples)
        {
            Assert.AreEqual(ConversationAudioSide.Sent, side); AppendAction?.Invoke();
            if (RejectAppend) { RejectInput(PcmRecordingFailure.BufferOverflow); return false; }
            if (State == PcmRecordingState.Failed) return false;
            Blocks.Enqueue(new(start, rate, samples.ToArray())); return true;
        }
        public void RejectInput(PcmRecordingFailure failure) { Failure ??= failure; State = PcmRecordingState.Failed; }
        public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken token = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
