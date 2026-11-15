using System.Collections.Concurrent;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class DuplexPcmRecordingTests
{
    private static DuplexPcmRecordingRequest Request() => new(new(Guid.NewGuid(), Guid.NewGuid(), "unused.wav", 8000),
        new(17, 1000, 8000, TimeSpan.Zero), new(91, 100, 5000, 8000, TimeSpan.FromMilliseconds(10)));
    private static DuplexPcmRecordingFactory Factory(Sessions sessions, IReceivedPcmRecordingBindingFactory? received = null) =>
        new(sessions, new SentPcmRecordingBindingFactory(), received ?? new ReceivedPcmRecordingBindingFactory(new RtpPcmRecordingFeedFactory()));

    [TestMethod]
    public async Task OneLiveSubscriptionCanDeliverBothDirectionsBeforeStartReturns()
    {
        var source = new Source(); var sessions = new Sessions();
        source.OnSubscribe = () => { Assert.IsTrue(source.Send()); Assert.IsTrue(source.Receive()); };
        var recording = await Factory(sessions).StartAsync(source, Request());
        Assert.AreEqual(1, source.Subscriptions); Assert.AreEqual(2, sessions.Session.Blocks.Count);
        Assert.AreEqual(PcmRecordingState.Capturing, recording.State); Assert.IsFalse(recording.InputCompletion.IsCompleted);
        var result = await recording.StopAsync(TimeSpan.FromMilliseconds(40)); await recording.DisposeAsync();
        Assert.AreEqual(320L, result.Wave.FrameCount); Assert.AreEqual(1, source.Sub!.Disposals);
        Assert.AreEqual(1, sessions.Session.Stops); Assert.AreEqual(1, sessions.Session.Disposals);
        Assert.AreEqual(PcmRecordingState.Completed, recording.State); Assert.IsTrue(recording.InputCompletion.IsCompleted);
    }

    [TestMethod]
    public async Task AbortDrainsInputButNeverStopsOrPublishesTheBorrowedSource()
    {
        var source = new Source(); var sessions = new Sessions(); var recording = await Factory(sessions).StartAsync(source, Request());
        source.Send(); await recording.DisposeAsync(); await recording.DisposeAsync();
        Assert.AreEqual(0, source.Closes); Assert.AreEqual(0, sessions.Session.Stops); Assert.AreEqual(1, sessions.Session.Disposals);
        Assert.AreEqual(PcmRecordingState.Aborted, recording.State); Assert.IsTrue(recording.InputCompletion.IsCompleted);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(20)));
    }

    [TestMethod]
    public async Task InvalidStopLeavesCaptureActiveAndFirstValidStopWins()
    {
        var source = new Source(); var sessions = new Sessions(); sessions.Session.StopGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var recording = await Factory(sessions).StartAsync(source, Request());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => recording.StopAsync(TimeSpan.Zero));
        Assert.AreEqual(PcmRecordingState.Capturing, recording.State); Assert.IsTrue(source.Send());
        var first = recording.StopAsync(TimeSpan.FromMilliseconds(40));
        var again = recording.StopAsync(TimeSpan.FromMilliseconds(80), new CancellationToken(true)); Assert.AreSame(first, again);
        await sessions.Session.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(PcmRecordingState.Stopping, recording.State); Assert.AreEqual(TimeSpan.FromMilliseconds(40), sessions.Session.Duration);
        sessions.Session.StopGate.TrySetResult(); await first; await recording.DisposeAsync(); Assert.AreEqual(1, sessions.Session.Stops);
    }

    [TestMethod]
    public async Task DisposeDuringStopWaitsForThatStopAndDoesNotAbortItsMix()
    {
        var source = new Source(); var sessions = new Sessions(); sessions.Session.StopGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var recording = await Factory(sessions).StartAsync(source, Request()); var stopping = recording.StopAsync(TimeSpan.FromMilliseconds(20));
        await sessions.Session.StopEntered.Task.WaitAsync(TimeSpan.FromSeconds(10)); var disposing = recording.DisposeAsync().AsTask();
        Assert.IsFalse(disposing.IsCompleted); Assert.AreEqual(0, sessions.Session.Disposals);
        sessions.Session.StopGate.TrySetResult(); await stopping; await disposing;
        Assert.AreEqual(PcmRecordingState.Completed, recording.State); Assert.AreEqual(1, sessions.Session.Disposals);
    }

    [TestMethod]
    public async Task StopCannotFinalizeUntilTheCurrentPcmCallbackHasReturned()
    {
        var source = new Source(); var sessions = new Sessions(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        sessions.Session.AppendAction = () => { entered.Set(); release.Wait(); };
        var recording = await Factory(sessions).StartAsync(source, Request()); var sending = Task.Run(() => source.Send()); Task<PcmRecordingResult>? stop = null;
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10))); stop = recording.StopAsync(TimeSpan.FromMilliseconds(20));
            Assert.IsFalse(stop.IsCompleted); Assert.IsFalse(recording.InputCompletion.IsCompleted); Assert.AreEqual(0, sessions.Session.Stops);
        }
        finally { release.Set(); }
        Assert.IsTrue(await sending); await stop!; await recording.DisposeAsync(); Assert.AreEqual(1, sessions.Session.Blocks.Count);
    }

    [TestMethod]
    public async Task NativeSourceFailureSynchronouslyRejectsTheEntireRecording()
    {
        var source = new Source(); var sessions = new Sessions(); var recording = await Factory(sessions).StartAsync(source, Request());
        source.Send(); source.Fail(PcmObservationFailure.ReceivedMetadataUnavailable);
        Assert.AreEqual(PcmRecordingState.Failed, recording.State); Assert.AreEqual(PcmRecordingFailure.TimelineConflict, recording.Failure);
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.AreEqual(PcmRecordingFailure.TimelineConflict, error.Failure);
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.DisposeAsync().AsTask()); Assert.AreEqual(1, sessions.Session.Disposals);
    }

    [TestMethod]
    public async Task NormalSourceEndDrainsBindingsButDoesNotChooseAnImplicitRecordingDuration()
    {
        var source = new Source(); var sessions = new Sessions(); var recording = await Factory(sessions).StartAsync(source, Request());
        source.Send(); source.Close(); await recording.InputCompletion;
        Assert.AreEqual(0, sessions.Session.Stops); Assert.AreEqual(PcmRecordingState.Capturing, recording.State);
        await recording.StopAsync(TimeSpan.FromMilliseconds(20)); await recording.DisposeAsync(); Assert.AreEqual(1, sessions.Session.Stops);
    }

    [TestMethod]
    public async Task InvalidAnchorsAndPreCanceledStartDoNotCreateARecording()
    {
        var source = new Source(); var sessions = new Sessions(); var request = Request();
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => Factory(sessions).StartAsync(source, request with { Sent = request.Sent with { RtpClockRate = 0 } }));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => Factory(sessions).StartAsync(source, request with { Received = request.Received with { RecordingOffset = TimeSpan.FromHours(8) } }));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Factory(sessions).StartAsync(source, request, new CancellationToken(true)));
        Assert.AreEqual(0, sessions.Starts); Assert.AreEqual(0, source.Subscriptions);
    }

    [TestMethod]
    public async Task FailedSecondBindingRollsBackTheFirstBindingAndOwnedSession()
    {
        var source = new Source(); var sessions = new Sessions();
        await Assert.ThrowsExactlyAsync<IOException>(() => Factory(sessions, new BrokenReceive()).StartAsync(source, Request()));
        Assert.AreEqual(0, source.Subscriptions); Assert.AreEqual(1, sessions.Session.Disposals); Assert.AreEqual(0, sessions.Session.Stops);
    }

    [TestMethod]
    public async Task FailedLiveSubscriptionNeverPublishesAndReleasesOwnedResources()
    {
        var source = new Source { SubscribeError = new IOException("private-source-detail") }; var sessions = new Sessions();
        await Assert.ThrowsExactlyAsync<IOException>(() => Factory(sessions).StartAsync(source, Request()));
        Assert.AreEqual(1, sessions.Session.Disposals); Assert.AreEqual(0, sessions.Session.Stops);
    }

    [TestMethod]
    public async Task CancelDuringLiveAttachmentRollsBackTheSubscriptionAndRecording()
    {
        using var cancellation = new CancellationTokenSource(); var source = new Source { OnSubscribe = cancellation.Cancel }; var sessions = new Sessions();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Factory(sessions).StartAsync(source, Request(), cancellation.Token));
        Assert.AreEqual(1, source.Sub!.Disposals); Assert.AreEqual(1, sessions.Session.Disposals); Assert.AreEqual(0, sessions.Session.Stops);
    }

    [TestMethod]
    public async Task SourceEndingInsideSubscribeCannotReturnAnApparentlyActiveRecording()
    {
        var source = new Source(); source.OnSubscribe = source.Close; var sessions = new Sessions();
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => Factory(sessions).StartAsync(source, Request()));
        Assert.AreEqual(1, sessions.Session.Disposals); Assert.AreEqual(0, sessions.Session.Stops);
    }

    [TestMethod]
    public async Task CanceledStopIsSharedAndDisposeStillReleasesTheOwnedSession()
    {
        var source = new Source(); var sessions = new Sessions(); var recording = await Factory(sessions).StartAsync(source, Request());
        var stop = recording.StopAsync(TimeSpan.FromMilliseconds(20), new CancellationToken(true));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => stop);
        Assert.AreSame(stop, recording.StopAsync(TimeSpan.FromMilliseconds(40)));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => recording.DisposeAsync().AsTask());
        Assert.AreEqual(1, sessions.Session.Disposals); Assert.AreEqual(PcmRecordingState.Aborted, recording.State);
    }

    [TestMethod]
    public async Task SourceDisposalFailureRejectsPublicationEvenWhenItsCompletionArrivesNormally()
    {
        var source = new Source(); var sessions = new Sessions(); var recording = await Factory(sessions).StartAsync(source, Request());
        source.Sub!.DisposeError = new IOException("private-dispose-detail");
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.AreEqual(PcmRecordingFailure.TimelineConflict, error.Failure);
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.DisposeAsync().AsTask()); Assert.AreEqual(1, sessions.Session.Disposals);
    }

    [TestMethod]
    public async Task SubSampleDurationAndOversizedWaveAreRejectedBeforeAnyInputIsDetached()
    {
        var source = new Source(); var sessions = new Sessions(); var request = Request();
        var recording = await Factory(sessions).StartAsync(source, request with { Recording = request.Recording with { SampleRate = 48000 } });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => recording.StopAsync(TimeSpan.FromTicks(1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => recording.StopAsync(TimeSpan.FromHours(8)));
        Assert.AreEqual(PcmRecordingState.Capturing, recording.State); Assert.AreEqual(0, source.Sub!.Disposals);
        Assert.IsTrue(source.Send()); await recording.DisposeAsync(); Assert.AreEqual(0, sessions.Session.Stops);
    }

    private sealed class BrokenReceive : IReceivedPcmRecordingBindingFactory
    { public Task<IReceivedPcmRecordingBinding> AttachAsync(IConversationPcmFailureSource source, IPcmRecordingSession session, RtpPcmTimelineAnchor anchor) => throw new IOException("private-binding-detail"); }
    private sealed class Sessions : IPcmRecordingSessionFactory
    {
        internal readonly Session Session = new(); internal int Starts;
        public Task<IPcmRecordingSession> StartAsync(PcmRecordingRequest request, CancellationToken token = default)
        { Starts++; return Task.FromResult<IPcmRecordingSession>(Session); }
    }
    private sealed class Session : IPcmRecordingSession, IPcmRecordingInputGuard
    {
        internal readonly ConcurrentQueue<ConversationAudioSide> Blocks = new(); internal int Stops, Disposals; internal TimeSpan Duration;
        internal TaskCompletionSource? StopGate; internal readonly TaskCompletionSource StopEntered = new(TaskCreationOptions.RunContinuationsAsynchronously); internal Action? AppendAction;
        public PcmRecordingState State { get; private set; } = PcmRecordingState.Capturing;
        public PcmRecordingFailure? Failure { get; private set; }
        public string SegmentDirectory => "unused";
        public Task CaptureCompletion => Task.CompletedTask;
        public bool TryAppend(ConversationAudioSide side, TimeSpan start, int rate, ReadOnlySpan<short> samples)
        { AppendAction?.Invoke(); if (State != PcmRecordingState.Capturing) return false; Blocks.Enqueue(side); return true; }
        public void RejectInput(PcmRecordingFailure failure) { if (State == PcmRecordingState.Capturing) { Failure ??= failure; State = PcmRecordingState.Failed; } }
        public async Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken token = default)
        {
            Stops++; Duration = duration; StopEntered.TrySetResult();
            if (Failure is { } failure) throw new PcmRecordingException(failure);
            State = PcmRecordingState.Stopping;
            if (token.IsCancellationRequested) { State = PcmRecordingState.Aborted; token.ThrowIfCancellationRequested(); }
            if (StopGate is not null) await StopGate.Task;
            State = PcmRecordingState.Completed; return new(new(Guid.NewGuid(), Guid.NewGuid(), duration.Ticks * 8000 / TimeSpan.TicksPerSecond, 8000, 2, 0), []);
        }
        public ValueTask DisposeAsync() { Disposals++; if (State is PcmRecordingState.Capturing or PcmRecordingState.Stopping) State = PcmRecordingState.Aborted; return ValueTask.CompletedTask; }
    }
    private sealed class Source : IConversationPcmFailureSource
    {
        private IConversationPcmObserver? _observer; internal Sub? Sub; internal int Subscriptions, Closes; internal Action? OnSubscribe; internal Exception? SubscribeError;
        public long FailureCount => 0;
        public PcmObservationFailure? LastFailure => null;
        public IConversationPcmSubscription Subscribe(IConversationPcmObserver observer)
        { Subscriptions++; if (SubscribeError is { } error) throw error; _observer = observer; Sub = new(); OnSubscribe?.Invoke(); return Sub; }
        internal bool Send() => Emit(true);
        internal bool Receive() => Emit(false);
        private bool Emit(bool sent)
        {
            if (!Sub!.Enter()) return false;
            try
            {
                var result = sent ? ((ISentRtpPcmObserver)_observer!).TryObserveSent(new(17, 1000, 8000, 160), 8000, 1, new short[160])
                    : ((IReceivedRtpPcmObserver)_observer!).TryObserveReceived(new(91, 100, 5000, 8000), 8000, 1, new short[160]);
                if (!result) Fail(PcmObservationFailure.ObserverRejected); return result;
            }
            finally { Sub.Exit(); }
        }
        internal void Fail(PcmObservationFailure failure) { ((IConversationPcmFailureObserver)_observer!).OnPcmObservationFailed(failure); Sub!.Retire(failure); }
        internal void Close() { Closes++; Sub!.Retire(null); }
    }
    private sealed class Sub : IConversationPcmSubscription
    {
        private readonly object _gate = new(); private int _inflight; private bool _closed; private PcmObservationFailure? _failure;
        private readonly TaskCompletionSource<PcmObservationFailure?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Disposals; internal Exception? DisposeError;
        public Task<PcmObservationFailure?> Completion => _completion.Task;
        internal bool Enter() { lock (_gate) { if (_closed) return false; _inflight++; return true; } }
        internal void Exit() { lock (_gate) { _inflight--; if (_closed && _inflight == 0) _completion.TrySetResult(_failure); } }
        internal void Retire(PcmObservationFailure? failure) { lock (_gate) { _closed = true; _failure ??= failure; if (_inflight == 0) _completion.TrySetResult(_failure); } }
        public ValueTask DisposeAsync() { Disposals++; Retire(null); if (DisposeError is { } error) throw error; return new(Completion); }
    }
}
