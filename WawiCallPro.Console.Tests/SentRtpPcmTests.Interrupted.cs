using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Console.Tests;

public sealed partial class SentRtpPcmTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StartupClosesAnUnfinishedCaptureWithoutWaveAndPreservesUnknownMediaTimes(bool stopping)
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        var saved = await BeginUnfinished(files, request);
        if (stopping)
        {
            // Persist a later timestamp to simulate the wall clock having moved backwards at restart.
            var old = saved.Capture.Capture; var now = DateTimeOffset.UtcNow.AddMinutes(1);
            saved = await files.Journal.WriteAsync(new(saved.Recording.Recording, 1, now),
                new(new(old.RecordingId, old.CallId, old.StartedAtUtc, null, PcmRecordingState.Stopping, old.SampleRate, old.Channels), 1, now));
        }
        var pending = await Pending(files); var calls = new DapperCallRepository(new(files.Target, new()));
        var origin = (await calls.GetAsync(request.Recording.CallId))!.History.Call.Origin!;
        var log = new InterruptedLog(); var recovery = Interrupted(files, pending, origin.WorkstationId, log: log);
        Assert.AreEqual(new InterruptedRecordingRecoveryResult(0, 0, 0), await recovery.RecoverAsync()); // Still a live call.
        var startup = new InterruptedRecordingSipPersistence(new(new(calls, new Origin(origin), TimeProvider.System), Recovery(files, pending, new Identity(origin.WorkstationId))), recovery);
        await startup.RecoverAsync();
        var capture = (await files.Capture())!; var recording = (await files.Recording())!;
        Assert.AreEqual(PcmRecordingState.Aborted, capture.Capture.State); Assert.AreEqual(RecordingCaptureFailure.Interrupted, capture.Capture.Failure);
        Assert.IsNull(capture.Capture.EndedAtUtc); Assert.IsNull(capture.Capture.FrameCount); Assert.IsNull(capture.Capture.Duration);
        Assert.AreEqual(saved.Capture.Capture.StartedAtUtc, capture.Capture.StartedAtUtc);
        Assert.AreEqual(RecordingAvailability.Failed, recording.Recording.Availability); Assert.IsNull(recording.Recording.Duration);
        Assert.AreEqual(saved.Capture.Version + 1, capture.Version); Assert.AreEqual(saved.Recording.Version + 1, recording.Version);
        Assert.AreEqual(recording.UpdatedAtUtc, capture.UpdatedAtUtc); Assert.IsTrue(capture.UpdatedAtUtc >= (await calls.GetAsync(request.Recording.CallId))!.History.Call.StateChangedAtUtc);
        Assert.IsTrue(capture.UpdatedAtUtc >= saved.Capture.UpdatedAtUtc); Assert.IsTrue(recording.UpdatedAtUtc >= saved.Recording.UpdatedAtUtc);
        Assert.IsFalse(File.Exists(files.Output)); Assert.AreEqual(0, pending.GetPending().Count);
        Assert.AreEqual(new InterruptedRecordingRecoveryResult(0, 0, 0), await recovery.RecoverAsync()); Assert.AreEqual(0, log.Warnings);
    }

    [TestMethod]
    [DataRow("file")]
    [DataRow("directory")]
    [DataRow("receipt")]
    [DataRow("corrupt-receipt")]
    public async Task UnverifiedArtifactsAndPendingCompletionsPreventInterruption(string blocker)
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        await BeginUnfinished(files, request); var pending = await Pending(files); var origin = await EndForInterruption(files, request);
        byte[]? bytes = null;
        if (blocker == "file") { Directory.CreateDirectory(files.Paths.RecordingsDirectory); bytes = [9, 8, 7]; File.WriteAllBytes(files.Output, bytes); }
        if (blocker == "directory") Directory.CreateDirectory(files.Output);
        if (blocker.Contains("receipt", StringComparison.Ordinal))
        {
            var old = (await files.Capture())!.Capture; var changed = DateTimeOffset.UtcNow;
            var aborted = new RecordingCapture(old.RecordingId, old.CallId, old.StartedAtUtc, null, PcmRecordingState.Aborted, old.SampleRate, old.Channels, failure: RecordingCaptureFailure.Cancelled);
            await using (await pending.StageAsync(new((await files.Recording())!.Recording with { Availability = RecordingAvailability.Failed }, 1, changed), new(aborted, 1, changed))) { }
            if (blocker == "corrupt-receipt") File.WriteAllText(Path.Combine(files.Paths.RootDirectory, "recording-pending", old.RecordingId.ToString("N") + ".pending"), "private-content");
        }
        var before = await files.Capture(); var log = new InterruptedLog();
        var result = await Interrupted(files, pending, origin.WorkstationId, log: log).RecoverAsync();
        Assert.AreEqual(new InterruptedRecordingRecoveryResult(0, 1, 0), result); Assert.AreEqual(before, await files.Capture());
        Assert.AreEqual(RecordingAvailability.Pending, (await files.Recording())!.Recording.Availability); Assert.IsTrue(log.Warnings > 0);
        Assert.AreEqual(blocker.Contains("receipt", StringComparison.Ordinal) ? 1 : 0, pending.GetPending().Count);
        if (bytes is not null) CollectionAssert.AreEqual(bytes, File.ReadAllBytes(files.Output));
        if (blocker == "directory") Assert.IsTrue(Directory.Exists(files.Output));
    }

    [TestMethod]
    [DataRow("identity")]
    [DataRow("foreign")]
    [DataRow("outbox")]
    [DataRow("access")]
    public async Task UnavailableOwnershipOrFileEvidenceNeverChangesTheCapture(string blocked)
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        await BeginUnfinished(files, request); var pending = await Pending(files); var origin = await EndForInterruption(files, request);
        if (blocked == "outbox") { var root = Path.Combine(files.Paths.RootDirectory, "recording-pending"); Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "unknown.pending"), "private-content"); }
        var local = blocked == "identity" ? null : (Guid?)(blocked == "foreign" ? Guid.NewGuid() : origin.WorkstationId);
        var log = new InterruptedLog(); var before = await files.Capture();
        var result = await Interrupted(files, pending, local, blocked == "access" ? new DeniedWaveProbe() : null, log: log).RecoverAsync();
        Assert.AreEqual(0, result.Interrupted); Assert.AreEqual(before, await files.Capture());
        Assert.AreEqual(RecordingAvailability.Pending, (await files.Recording())!.Recording.Availability);
        Assert.AreEqual(blocked == "foreign" ? 0 : 1, result.Failed); Assert.IsFalse(File.Exists(files.Output));
        await Assert.ThrowsAsync<OperationCanceledException>(() => Interrupted(files, pending, local).RecoverAsync(new CancellationToken(true)));
    }

    [TestMethod]
    public async Task UnconfirmedInterruptionIsDeliveredByFreshCompletionRecoveryWithoutInventingAnEnd()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        await BeginUnfinished(files, request); var pending = await Pending(files); var origin = await EndForInterruption(files, request);
        var targets = new SwitchTarget(DatabaseTarget.Sqlite(Path.Combine(files.Paths.RootDirectory, "not-created.db")));
        var failing = new DurableRecordingJournal(Backend(targets), pending);
        var result = await Interrupted(files, pending, origin.WorkstationId, journal: failing).RecoverAsync();
        Assert.AreEqual(new InterruptedRecordingRecoveryResult(0, 0, 1), result); Assert.AreEqual(1, pending.GetPending().Count);
        Assert.AreEqual(PcmRecordingState.Capturing, (await files.Capture())!.Capture.State);
        Assert.AreEqual(new RecordingJournalRecoveryResult(1, 0, 0), await Recovery(files, await Pending(files, false), new Identity(origin.WorkstationId)).RecoverAsync());
        var saved = (await files.Capture())!; Assert.AreEqual(PcmRecordingState.Aborted, saved.Capture.State); Assert.IsNull(saved.Capture.EndedAtUtc);
        Assert.AreEqual(2L, saved.Version); Assert.AreEqual(2L, (await files.Recording())!.Version); Assert.AreEqual(0, pending.GetPending().Count);
    }

    [TestMethod]
    public async Task ConcurrentMetadataChangeRollsBackInterruptionAndPreservesItsProtectedRequest()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        await BeginUnfinished(files, request); var pending = await Pending(files); var origin = await EndForInterruption(files, request);
        var durable = new DurableRecordingJournal(new(new(files.Target, new())), pending);
        var mutating = new BeforeInterruptWrite(durable, async () =>
        {
            var saved = (await files.Capture())!; var old = saved.Capture;
            await new DapperRecordingCaptureRepository(new(files.Target, new())).WriteAsync(new(new(old.RecordingId, old.CallId, old.StartedAtUtc, null,
                PcmRecordingState.Stopping, old.SampleRate, old.Channels), saved.Version, DateTimeOffset.UtcNow));
        });
        var result = await Interrupted(files, pending, origin.WorkstationId, journal: mutating).RecoverAsync();
        Assert.AreEqual(new InterruptedRecordingRecoveryResult(0, 1, 0), result);
        Assert.AreEqual(PcmRecordingState.Stopping, (await files.Capture())!.Capture.State); Assert.AreEqual(2L, (await files.Capture())!.Version);
        Assert.AreEqual(RecordingAvailability.Pending, (await files.Recording())!.Recording.Availability); Assert.AreEqual(1L, (await files.Recording())!.Version);
        Assert.AreEqual(1, pending.GetPending().Count);
    }

    [TestMethod]
    public async Task RemovingRecoveredCandidatesDoesNotSkipTheSecondPageOrChangeAnExistingArtifact()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        var ids = new List<Guid>();
        for (var index = 0; index < 103; index++)
        {
            var id = Guid.NewGuid(); ids.Add(id);
            await BeginUnfinished(files, request with { Recording = request.Recording with { RecordingId = id, WavePath = Path.Combine(files.Paths.RecordingsDirectory, id.ToString("N") + ".wav") } });
        }
        Directory.CreateDirectory(files.Paths.RecordingsDirectory); var preserved = Path.Combine(files.Paths.RecordingsDirectory, ids[0].ToString("N") + ".wav"); File.WriteAllBytes(preserved, [7]);
        var pending = await Pending(files); var origin = await EndForInterruption(files, request);
        var recovery = Interrupted(files, pending, origin.WorkstationId);
        Assert.AreEqual(new InterruptedRecordingRecoveryResult(102, 1, 0), await recovery.RecoverAsync());
        Assert.AreEqual(new InterruptedRecordingRecoveryResult(0, 1, 0), await recovery.RecoverAsync());
        var remaining = await new DapperInterruptedRecordingQuery(new(files.Target, new())).GetPageAsync(origin.WorkstationId);
        Assert.AreEqual(ids[0], remaining.Items.Single().Journal.Recording.Recording.Id); Assert.IsFalse(remaining.HasMore);
        CollectionAssert.AreEqual(new byte[] { 7 }, File.ReadAllBytes(preserved)); Assert.AreEqual(0, pending.GetPending().Count);
    }

    private static Task<StoredRecordingJournal> BeginUnfinished(PersistedFiles files, DuplexPcmRecordingRequest request)
    {
        var old = request.Recording; var time = request.RecordingEpochUtc!.Value; var relative = Path.GetRelativePath(files.Paths.RecordingsDirectory, old.WavePath).Replace('\\', '/');
        return files.Journal.BeginAsync(new(new(old.RecordingId, old.CallId, relative, "wav", null, RecordingAvailability.Pending), null, time),
            new(new(old.RecordingId, old.CallId, time, null, PcmRecordingState.Capturing, old.SampleRate, 2), null, time));
    }
    private static async Task<CallOrigin> EndForInterruption(PersistedFiles files, DuplexPcmRecordingRequest request)
    {
        var calls = new DapperCallRepository(new(files.Target, new())); var origin = (await calls.GetAsync(request.Recording.CallId))!.History.Call.Origin!;
        await new SipCallPersistence(calls, new Origin(origin), TimeProvider.System).RecoverAsync(); return origin;
    }
    private static InterruptedRecordingRecovery Interrupted(PersistedFiles files, IRecordingJournalOutbox pending, Guid? workstation,
        IRecordingFileProbe? probe = null, IRecordingJournal? journal = null, InterruptedLog? log = null) =>
        new(new DapperInterruptedRecordingQuery(new(files.Target, new())), journal ?? new DurableRecordingJournal(new(new(files.Target, new())), pending),
            pending, probe ?? new LocalRecordingFileProbe(files.Paths), new Identity(workstation), TimeProvider.System, log ?? (ILogger<InterruptedRecordingRecovery>)NullLogger<InterruptedRecordingRecovery>.Instance);
    private sealed class DeniedWaveProbe : IRecordingFileProbe
    { public bool IsAbsent(string relativePath) => throw new UnauthorizedAccessException("private-content"); }
    private sealed class InterruptedLog : ILogger<InterruptedRecordingRecovery>
    {
        internal int Warnings;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (level >= LogLevel.Warning) Warnings++; Assert.IsNull(exception); Assert.IsFalse(formatter(state, exception).Contains("private-content", StringComparison.Ordinal)); }
    }
    private sealed class BeforeInterruptWrite(IRecordingJournal inner, Func<Task> before) : IRecordingJournal
    {
        public Task<StoredRecordingJournal> BeginAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken token = default) => inner.BeginAsync(recording, capture, token);
        public async Task<StoredRecordingJournal> WriteAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken token = default)
        { await before(); return await inner.WriteAsync(recording, capture, token); }
    }
}
