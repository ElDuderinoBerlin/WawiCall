using SIPSorcery.SIP.App;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class RecordingSipMediaSessionTests
{
    [TestMethod]
    public async Task ClosingAbortsBeforeClosingMediaAndSharesItsCompletion()
    {
        var native = new Media(); var factory = new Factory(); var owner = new RecordingSipMediaSession(native, factory);
        var recording = (Recording)await owner.StartRecordingAsync(Request()); recording.Release = new();
        var close = owner.DisposeAsync().AsTask();
        Assert.AreSame(close, owner.DisposeAsync().AsTask()); Assert.IsFalse(close.IsCompleted); Assert.AreEqual(0, native.Closed);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => owner.StartRecordingAsync(Request()));
        recording.Release.SetResult(); await close;
        Assert.AreEqual(PcmRecordingState.Aborted, recording.State); Assert.AreEqual(1, recording.Disposed); Assert.AreEqual(1, native.Closed);
    }

    [TestMethod]
    public async Task ClosingDuringStartupWaitsForAndAbortsTheLateRecording()
    {
        var native = new Media(); var factory = new Factory { Pending = new() }; var owner = new RecordingSipMediaSession(native, factory);
        var start = owner.StartRecordingAsync(Request()); Assert.AreEqual(1, factory.Starts);
        var close = owner.DisposeAsync().AsTask(); Assert.IsFalse(close.IsCompleted); Assert.AreEqual(0, native.Closed);
        var recording = new Recording(); factory.Pending.SetResult(recording);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => start); await close;
        Assert.AreEqual(PcmRecordingState.Aborted, recording.State); Assert.AreEqual(1, recording.Disposed); Assert.AreEqual(1, native.Closed);
    }

    [TestMethod]
    public async Task ParallelStartsCannotCreateTwoLiveRecordings()
    {
        var factory = new Factory { Pending = new() }; await using var owner = new RecordingSipMediaSession(new Media(), factory);
        var first = owner.StartRecordingAsync(Request()); var second = owner.StartRecordingAsync(Request());
        factory.Pending.SetResult(new Recording()); await first;
        var error = await Assert.ThrowsExactlyAsync<TelephonyException>(() => second);
        Assert.AreEqual("RecordingAlreadyActive", error.Code); Assert.AreEqual(1, factory.Starts);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ClosingWaitsForAnExplicitStopAndStillClosesMediaOnStopFailure(bool fail)
    {
        var native = new Media(); var factory = new Factory(); var owner = new RecordingSipMediaSession(native, factory);
        var recording = (Recording)await owner.StartRecordingAsync(Request()); var stop = recording.StopAsync(TimeSpan.FromSeconds(1));
        var close = owner.DisposeAsync().AsTask(); Assert.IsFalse(close.IsCompleted); Assert.AreEqual(0, native.Closed);
        if (fail)
        {
            recording.Stopped.SetException(new PcmRecordingException(PcmRecordingFailure.MixingFailed));
            await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => stop);
            await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => close);
            await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => owner.DisposeAsync().AsTask());
        }
        else { recording.Stopped.SetResult(Result()); await stop; await close; Assert.AreEqual(PcmRecordingState.Completed, recording.State); }
        Assert.AreEqual(1, native.Closed); Assert.AreEqual(1, recording.Disposed);
    }

    [TestMethod]
    public async Task AFailedStartLeavesTheMediaAvailableForAnExplicitRetry()
    {
        var factory = new Factory { Pending = new() }; await using var owner = new RecordingSipMediaSession(new Media(), factory);
        var failed = owner.StartRecordingAsync(Request()); factory.Pending.SetException(new PcmRecordingException(PcmRecordingFailure.FileFailure));
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => failed); factory.Pending = null;
        Assert.AreEqual(PcmRecordingState.Capturing, (await owner.StartRecordingAsync(Request())).State); Assert.AreEqual(2, factory.Starts);
    }

    [TestMethod]
    public async Task FinishedOrExplicitlyAbortedRecordingAllowsANewSegment()
    {
        var factory = new Factory(); await using var owner = new RecordingSipMediaSession(new Media(), factory);
        var first = (Recording)await owner.StartRecordingAsync(Request()); first.Stopped.SetResult(Result()); await first.StopAsync(TimeSpan.FromSeconds(1));
        var second = await owner.StartRecordingAsync(Request()); Assert.AreEqual(1, first.Disposed); Assert.AreNotSame(first, second);
        await second.DisposeAsync(); var third = await owner.StartRecordingAsync(Request()); Assert.AreNotSame(second, third); Assert.AreEqual(3, factory.Starts);
    }

    [TestMethod]
    public async Task CanceledWaitingStartDoesNotReplaceTheRunningRecording()
    {
        var factory = new Factory { Pending = new() }; await using var owner = new RecordingSipMediaSession(new Media(), factory);
        var first = owner.StartRecordingAsync(Request()); using var canceled = new CancellationTokenSource();
        var waiting = owner.StartRecordingAsync(Request(), canceled.Token); canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => waiting); factory.Pending.SetResult(new Recording()); await first;
        Assert.AreEqual(1, factory.Starts);
    }

    [TestMethod]
    public async Task PlainPcmWithoutSynchronousFailureBoundaryCannotBeRecorded()
    {
        var native = new Media { Source = new PlainSource() }; var factory = new Factory();
        await using var owner = new RecordingSipMediaSession(native, factory);
        var error = await Assert.ThrowsExactlyAsync<TelephonyException>(() => owner.StartRecordingAsync(Request()));
        Assert.AreEqual("RecordingUnavailable", error.Code); Assert.AreEqual(0, factory.Starts);
        Assert.AreSame(native.Session, owner.Session); Assert.AreSame(native.Source, owner.PcmSource);
        owner.ApplyPlaybackSettings(); Assert.AreEqual(1, native.Applied);
    }

    private static DuplexPcmRecordingRequest Request() => new(new(Guid.NewGuid(), Guid.NewGuid(), "unused.wav"), new(1, 0, 8000, TimeSpan.Zero), new(2, 0, 0, 8000, TimeSpan.Zero));
    private static PcmRecordingResult Result() => new(new(Guid.NewGuid(), Guid.NewGuid(), 8000, 8000, 2, 0), []);
    private sealed class Factory : IDuplexPcmRecordingFactory
    {
        internal int Starts;
        internal TaskCompletionSource<IDuplexPcmRecording>? Pending;
        public Task<IDuplexPcmRecording> StartAsync(IConversationPcmFailureSource source, DuplexPcmRecordingRequest request, CancellationToken cancellationToken = default)
        { Interlocked.Increment(ref Starts); return Pending?.Task ?? Task.FromResult<IDuplexPcmRecording>(new Recording()); }
    }
    private sealed class Recording : IDuplexPcmRecording
    {
        internal int Disposed;
        internal TaskCompletionSource? Release;
        internal TaskCompletionSource<PcmRecordingResult> Stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task<PcmRecordingResult>? _stop;
        private Task? _dispose;
        public PcmRecordingState State { get; private set; } = PcmRecordingState.Capturing;
        public PcmRecordingFailure? Failure => null;
        public Task InputCompletion => Task.CompletedTask;
        public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken cancellationToken = default) => _stop ??= StopCore();
        private async Task<PcmRecordingResult> StopCore()
        { State = PcmRecordingState.Stopping; var result = await Stopped.Task; State = PcmRecordingState.Completed; return result; }
        public ValueTask DisposeAsync() => new(_dispose ??= DisposeCore());
        private async Task DisposeCore()
        { Interlocked.Increment(ref Disposed); if (_stop is not null) await _stop; else { if (Release is not null) await Release.Task; State = PcmRecordingState.Aborted; } }
    }
    private class PlainSource : IConversationPcmSource
    {
        public long FailureCount => 0;
        public PcmObservationFailure? LastFailure => null;
        public IConversationPcmSubscription Subscribe(IConversationPcmObserver observer) => throw new NotSupportedException();
    }
    private sealed class Source : PlainSource, IConversationPcmFailureSource { }
    private sealed class Media : ISipPcmMediaSession
    {
        public IMediaSession Session => null!;
        internal IConversationPcmSource Source = new Source();
        public IConversationPcmSource PcmSource => Source;
        internal int Closed, Applied;
        public void ApplyPlaybackSettings() => Applied++;
        public ValueTask DisposeAsync() { Closed++; return ValueTask.CompletedTask; }
    }
}
