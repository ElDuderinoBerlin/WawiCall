using WawiCallPro.Audio;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class PersistedRecordingTests
{
    [TestMethod]
    public async Task MetadataPrecedesCaptureAndOnlyFinalFramesMakeTheRecordingAvailable()
    {
        using var fixture = new Fixture(); fixture.Native.OnStart = () => Assert.AreEqual(PcmRecordingState.Capturing, fixture.Journal.Saved!.Capture.Capture.State);
        var recording = await fixture.Start(); Assert.AreEqual(RecordingAvailability.Pending, fixture.Journal.Saved!.Recording.Recording.Availability);
        var stop = recording.StopAsync(TimeSpan.FromMilliseconds(20)); Assert.AreSame(stop, recording.StopAsync(TimeSpan.Zero)); var result = await stop;
        Assert.AreEqual(160L, result.Wave.FrameCount); var saved = fixture.Journal.Saved;
        Assert.AreEqual(PcmRecordingState.Completed, saved!.Capture.Capture.State); Assert.AreEqual(fixture.Epoch.AddMilliseconds(20), saved.Capture.Capture.EndedAtUtc);
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), saved.Recording.Recording.Duration); Assert.AreEqual(RecordingAvailability.Available, saved.Recording.Recording.Availability);
        Assert.AreEqual("sample.wav", saved.Recording.Recording.RelativePath); await recording.DisposeAsync(); Assert.AreEqual(1, fixture.Native.Recording.Stops);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedMetadataConfirmationCanBeRetriedWithoutMixingAgain(bool commitUncertain)
    {
        using var fixture = new Fixture(); var recording = await fixture.Start(); fixture.Journal.FailWrite = true; fixture.Journal.CommitThenFail = commitUncertain;
        var error = await Assert.ThrowsExactlyAsync<RecordingPersistenceException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.IsTrue(error.FinalizedWavePreserved); Assert.IsNull(error.InnerException); Assert.IsFalse(error.ToString().Contains("private-data", StringComparison.Ordinal));
        var status = (IRecordingPersistenceStatus)recording; Assert.IsTrue(status.HasPendingPersistence);
        fixture.Journal.FailWrite = false; await status.RetryPersistenceAsync(); Assert.IsFalse(status.HasPendingPersistence);
        Assert.AreEqual(2L, fixture.Journal.Saved!.Recording.Version); Assert.AreEqual(2L, fixture.Journal.Saved.Capture.Version);
        Assert.AreEqual(1, fixture.Native.Recording.Stops); await status.RetryPersistenceAsync(); Assert.AreEqual(2, fixture.Journal.Writes);
        await Assert.ThrowsExactlyAsync<RecordingPersistenceException>(() => recording.DisposeAsync().AsTask());
    }

    [TestMethod]
    public async Task AbortHasNoInventedEndAndItsPersistenceCanBeRetriedAfterDisposal()
    {
        using var fixture = new Fixture(); var recording = await fixture.Start(); fixture.Journal.FailWrite = true;
        await Assert.ThrowsExactlyAsync<RecordingPersistenceException>(() => recording.DisposeAsync().AsTask());
        Assert.AreEqual(PcmRecordingState.Aborted, recording.State); Assert.AreEqual(0, fixture.Native.Recording.Stops);
        fixture.Journal.FailWrite = false; await ((IRecordingPersistenceStatus)recording).RetryPersistenceAsync();
        var capture = fixture.Journal.Saved!.Capture.Capture; Assert.AreEqual(RecordingCaptureFailure.Cancelled, capture.Failure);
        Assert.IsNull(capture.EndedAtUtc); Assert.IsNull(capture.Duration); Assert.AreEqual(RecordingAvailability.Failed, fixture.Journal.Saved.Recording.Recording.Availability);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MediaFailureOrStopCancellationIsPersistedAndRemainsObservable(bool cancel)
    {
        using var fixture = new Fixture(); var recording = await fixture.Start();
        fixture.Native.Recording.StopError = cancel ? new OperationCanceledException() : new PcmRecordingException(PcmRecordingFailure.BufferOverflow);
        if (cancel) await Assert.ThrowsAsync<OperationCanceledException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(20)));
        else await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.AreEqual(cancel ? RecordingCaptureFailure.Cancelled : RecordingCaptureFailure.BufferOverflow, fixture.Journal.Saved!.Capture.Capture.Failure);
        Assert.IsNull(fixture.Journal.Saved.Capture.Capture.EndedAtUtc);
        if (cancel) await Assert.ThrowsAsync<OperationCanceledException>(() => recording.DisposeAsync().AsTask());
        else await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.DisposeAsync().AsTask());
    }

    [TestMethod]
    public async Task DatabaseFailurePreventsAnyCaptureOrFileCreation()
    {
        using var fixture = new Fixture(); fixture.Journal.BeginError = new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
        await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => fixture.Start()); Assert.AreEqual(0, fixture.Native.Starts);
        Assert.IsFalse(Directory.Exists(fixture.Paths.RecordingsDirectory));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedOrCanceledStartupFinalizesMetadataWithoutClaimingAMediaEnd(bool cancel)
    {
        using var fixture = new Fixture(); using var stopping = new CancellationTokenSource();
        if (cancel) fixture.Native.OnStart = stopping.Cancel;
        else fixture.Native.StartError = new PcmRecordingException(PcmRecordingFailure.FileFailure);
        if (cancel) await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Start(stopping.Token));
        else await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => fixture.Start());
        var capture = fixture.Journal.Saved!.Capture.Capture;
        Assert.AreEqual(cancel ? PcmRecordingState.Aborted : PcmRecordingState.Failed, capture.State);
        Assert.AreEqual(cancel ? RecordingCaptureFailure.Cancelled : RecordingCaptureFailure.FileFailure, capture.Failure); Assert.IsNull(capture.EndedAtUtc);
        Assert.AreEqual(cancel ? 1 : 0, fixture.Native.Recording.Disposals);
    }

    [TestMethod]
    [DataRow("missing-epoch")]
    [DataRow("future-epoch")]
    [DataRow("offset-epoch")]
    [DataRow("outside-root")]
    [DataRow("bad-anchor")]
    public async Task InvalidPersistentRequestsAreRejectedBeforeTheJournal(string invalid)
    {
        using var fixture = new Fixture(); var request = fixture.Request;
        request = invalid switch
        {
            "missing-epoch" => request with { RecordingEpochUtc = null },
            "future-epoch" => request with { RecordingEpochUtc = fixture.Clock.Now.AddSeconds(1) },
            "offset-epoch" => request with { RecordingEpochUtc = fixture.Epoch.ToOffset(TimeSpan.FromHours(1)) },
            "outside-root" => request with { Recording = request.Recording with { WavePath = Path.Combine(fixture.Root, "outside.wav") } },
            _ => request with { Sent = request.Sent with { RtpClockRate = 0 } }
        };
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Factory.StartAsync(new Source(), request)); Assert.AreEqual(0, fixture.Journal.Begins); Assert.AreEqual(0, fixture.Native.Starts);
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(1L)]
    [DataRow(90000000000L)]
    [DataRow(1200000000L)]
    [DataRow(324000000000L)]
    public async Task InvalidDurationsLeaveCaptureActiveAndDoNotWriteAFalseCompletion(long ticks)
    {
        using var fixture = new Fixture(); var recording = await fixture.Start();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => recording.StopAsync(TimeSpan.FromTicks(ticks)));
        Assert.AreEqual(0, fixture.Native.Recording.Stops); Assert.AreEqual(0, fixture.Journal.Writes); Assert.AreEqual(PcmRecordingState.Capturing, recording.State);
        await recording.DisposeAsync();
    }

    [TestMethod]
    public async Task ConcurrentDisposeWaitsForStopAndCanceledRetryDoesNotInterruptTheMetadataWrite()
    {
        using var fixture = new Fixture(); var recording = await fixture.Start(); fixture.Journal.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var stop = recording.StopAsync(TimeSpan.FromMilliseconds(20)); var close = recording.DisposeAsync().AsTask();
        Assert.AreSame(close, recording.DisposeAsync().AsTask()); Assert.IsFalse(close.IsCompleted);
        using var cancel = new CancellationTokenSource(); var retry = ((IRecordingPersistenceStatus)recording).RetryPersistenceAsync(cancel.Token); cancel.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => retry); fixture.Journal.Release.SetResult(); await stop; await close;
        Assert.AreEqual(1, fixture.Native.Recording.Stops); Assert.AreEqual(1, fixture.Journal.Writes);
    }

    [TestMethod]
    public async Task AConflictingMixerResultCannotMakeTheRecordingAvailable()
    {
        using var fixture = new Fixture(); var recording = await fixture.Start(); fixture.Native.Recording.WrongIdentity = true;
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(20)));
        Assert.AreEqual(RecordingAvailability.Failed, fixture.Journal.Saved!.Recording.Recording.Availability);
        Assert.AreEqual(RecordingCaptureFailure.MixingFailed, fixture.Journal.Saved.Capture.Capture.Failure);
        await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => recording.DisposeAsync().AsTask());
    }

    [TestMethod]
    public async Task PrecanceledStartDoesNotReachTheJournal()
    {
        using var fixture = new Fixture(); await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Start(new CancellationToken(true)));
        Assert.AreEqual(0, fixture.Journal.Begins); Assert.AreEqual(0, fixture.Native.Starts);
    }

    [TestMethod]
    public async Task FailedCleanupDuringCanceledStartupIsNotSilentlyReportedAsSuccessfulCancellation()
    {
        using var fixture = new Fixture(); using var stopping = new CancellationTokenSource();
        fixture.Native.OnStart = stopping.Cancel; fixture.Native.Recording.DisposeError = new IOException("private-data");
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => fixture.Start(stopping.Token));
        Assert.IsNull(error.InnerException); Assert.IsFalse(error.ToString().Contains("private-data", StringComparison.Ordinal));
        Assert.AreEqual(PcmRecordingState.Failed, fixture.Journal.Saved!.Capture.Capture.State);
        Assert.AreEqual(RecordingCaptureFailure.StartupFailed, fixture.Journal.Saved.Capture.Capture.Failure);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "wcp-persisted-" + Guid.NewGuid().ToString("N"));
        internal readonly DateTimeOffset Epoch = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
        internal readonly NativeFactory Native = new(); internal readonly Journal Journal = new(); internal readonly Clock Clock = new();
        internal ApplicationPaths Paths { get; }
        internal DuplexPcmRecordingRequest Request { get; }
        internal PersistedDuplexPcmRecordingFactory Factory { get; }
        internal Fixture()
        {
            Paths = new(new ConfigurationLocation(Root)); Clock.Now = Epoch.AddMinutes(1);
            Request = new(new(Guid.NewGuid(), Guid.NewGuid(), Path.Combine(Paths.RecordingsDirectory, "sample.wav"), 8000), new(1, 0, 8000, TimeSpan.Zero), new(2, 0, 0, 8000, TimeSpan.Zero)) { RecordingEpochUtc = Epoch };
            Native.Request = Request.Recording; Factory = new(Native, Journal, Paths, Clock);
        }
        internal Task<IDuplexPcmRecording> Start(CancellationToken token = default) => Factory.StartAsync(new Source(), Request, token);
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private sealed class Clock : TimeProvider { internal DateTimeOffset Now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Source : IConversationPcmFailureSource
    {
        public long FailureCount => 0; public PcmObservationFailure? LastFailure => null;
        public IConversationPcmSubscription Subscribe(IConversationPcmObserver observer) => throw new NotSupportedException();
    }
    private sealed class NativeFactory : IDuplexPcmRecordingFactory
    {
        internal PcmRecordingRequest Request = null!; internal readonly NativeRecording Recording = new(); internal Action? OnStart; internal Exception? StartError; internal int Starts;
        public Task<IDuplexPcmRecording> StartAsync(IConversationPcmFailureSource source, DuplexPcmRecordingRequest request, CancellationToken cancellationToken = default)
        { Starts++; OnStart?.Invoke(); if (StartError is not null) return Task.FromException<IDuplexPcmRecording>(StartError); Recording.Request = Request; return Task.FromResult<IDuplexPcmRecording>(Recording); }
    }
    private sealed class NativeRecording : IDuplexPcmRecording
    {
        internal PcmRecordingRequest Request = null!; internal int Stops, Disposals; internal Exception? StopError, DisposeError; internal bool WrongIdentity;
        private Task<PcmRecordingResult>? _stop; private Task? _dispose;
        public PcmRecordingState State { get; private set; } = PcmRecordingState.Capturing;
        public PcmRecordingFailure? Failure => StopError is PcmRecordingException pcm ? pcm.Failure : null;
        public Task InputCompletion => Task.CompletedTask;
        public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken cancellationToken = default) => _stop ??= StopCore(duration);
        private Task<PcmRecordingResult> StopCore(TimeSpan duration)
        {
            Stops++;
            if (StopError is not null) { State = StopError is OperationCanceledException ? PcmRecordingState.Aborted : PcmRecordingState.Failed; return Task.FromException<PcmRecordingResult>(StopError); }
            State = PcmRecordingState.Completed; return Task.FromResult(new PcmRecordingResult(new(Request.CallId, WrongIdentity ? Guid.NewGuid() : Request.RecordingId,
                (duration.Ticks * Request.SampleRate + TimeSpan.TicksPerSecond / 2) / TimeSpan.TicksPerSecond, Request.SampleRate, 2, 0), []));
        }
        public ValueTask DisposeAsync() => new(_dispose ??= DisposeCore());
        private async Task DisposeCore() { Disposals++; if (_stop is not null) await _stop; else State = PcmRecordingState.Aborted; if (DisposeError is not null) throw DisposeError; }
    }
    private sealed class Journal : IRecordingJournal
    {
        internal StoredRecordingJournal? Saved; internal int Begins, Writes; internal bool FailWrite, CommitThenFail; internal Exception? BeginError; internal TaskCompletionSource? Release;
        public Task<StoredRecordingJournal> BeginAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default)
        { Begins++; if (BeginError is not null) return Task.FromException<StoredRecordingJournal>(BeginError); return Task.FromResult(Saved = State(recording, capture)); }
        public async Task<StoredRecordingJournal> WriteAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default)
        {
            Writes++; if (Release is not null) await Release.Task;
            if (FailWrite) { if (CommitThenFail) Saved = State(recording, capture); throw new IOException("private-data"); }
            if (Saved is { } saved && saved.Recording.Version - 1 == recording.ExpectedVersion && saved.Capture.Version - 1 == capture.ExpectedVersion) return saved;
            return Saved = State(recording, capture);
        }
        private static StoredRecordingJournal State(RecordingWrite recording, RecordingCaptureWrite capture) => new(
            new(recording.Recording, (recording.ExpectedVersion ?? 0) + 1, recording.ChangedAtUtc, recording.ChangedAtUtc), new(capture.Capture, (capture.ExpectedVersion ?? 0) + 1, capture.ChangedAtUtc));
    }
}
