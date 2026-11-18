using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingArtifactAuditTests
{
    [TestMethod]
    public async Task ConfirmedFilesAreMarkedMissingAndOnlyTheOriginalBytesCanRestoreAvailability()
    {
        using var database = await WaveArtifactFixture.Create(); await RecordingArtifactAuditAcceptance.RunAsync(database.Target);
    }
    [TestMethod]
    [DataRow("changed")][DataRow("header")][DataRow("length")]
    public async Task ChangedFilesArePreservedAndNeverAdoptedAsNewContent(string change)
    {
        using var fixture = await Ready(); var workstation = await RecordingArtifactAuditAcceptance.EndAsync(fixture);
        var before = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single();
        var bytes = File.ReadAllBytes(fixture.Wave);
        if (change == "length") bytes = bytes[..^1]; else if (change == "header") bytes[0] ^= 1; else bytes[^1] ^= 1;
        File.WriteAllBytes(fixture.Wave, bytes);
        var log = new AuditLog(); var audit = RecordingArtifactAuditAcceptance.Service(fixture, workstation, log: log);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(1, 0, 0, 0, 0), await audit.ReconcileAsync());
        var after = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single();
        Assert.AreEqual(before.Artifact.Sha256, after.Artifact.Sha256); Assert.AreEqual(before.Artifact.ByteLength, after.Artifact.ByteLength);
        Assert.AreEqual(RecordingAvailability.Missing, after.Artifact.Availability); CollectionAssert.AreEqual(bytes, File.ReadAllBytes(fixture.Wave));
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 1, 0, 0), await audit.ReconcileAsync()); Assert.AreEqual(after, await fixture.Artifacts.GetAsync(after.Artifact.Id));
        Assert.IsTrue(log.Events.Contains(OperationEvent.RecordingArtifactUnavailable));
    }
    [TestMethod]
    [DataRow("live")][DataRow("foreign")][DataRow("identity")][DataRow("receipt")][DataRow("corrupt-receipt")][DataRow("invalid-directory")]
    public async Task OwnershipAndCompletionGuardsPreventFileAndMetadataChanges(string guard)
    {
        using var fixture = await Ready(); var workstation = guard == "live" ? (await new DapperCallRepository(new(fixture.Target, new())).GetAsync(fixture.CompletedRecording.Recording.CallId))!.History.Call.Origin!.WorkstationId : await RecordingArtifactAuditAcceptance.EndAsync(fixture);
        if (guard.Contains("receipt", StringComparison.Ordinal))
        {
            await using (await fixture.Outbox.StageAsync(fixture.CompletedRecording, fixture.CompletedCapture)) { }
            if (guard == "corrupt-receipt") File.WriteAllText(Path.Combine(fixture.Paths.RootDirectory, "recording-pending", fixture.Id.ToString("N") + ".pending"), "private-content");
        }
        if (guard == "invalid-directory") File.WriteAllText(Path.Combine(fixture.Paths.RootDirectory, "recording-pending", "unknown.pending"), "private-content");
        File.Delete(fixture.Wave); var before = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single();
        var identity = guard == "identity" ? null : (Guid?)(guard == "foreign" ? Guid.NewGuid() : workstation);
        var log = new AuditLog(); var result = await RecordingArtifactAuditAcceptance.Service(fixture, identity, log: log).ReconcileAsync();
        Assert.AreEqual(0, result.MarkedMissing); Assert.AreEqual(before, await fixture.Artifacts.GetAsync(before.Artifact.Id)); Assert.IsFalse(File.Exists(fixture.Wave));
        Assert.AreEqual(guard.Contains("receipt", StringComparison.Ordinal) ? 1 : 0, result.Deferred);
        Assert.AreEqual(guard is "identity" or "invalid-directory" ? 1 : 0, result.Failed);
        if (result.Failed + result.Deferred > 0) Assert.IsTrue(log.Events.Contains(OperationEvent.RecordingArtifactAuditIncomplete));
    }
    [TestMethod]
    [DataRow("access")][DataRow("locked")][DataRow("directory")]
    public async Task AccessFailuresAndNonFilesAreNotReportedAsMissing(string problem)
    {
        using var fixture = await Ready(); var workstation = await RecordingArtifactAuditAcceptance.EndAsync(fixture);
        var before = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single(); FileStream? locked = null;
        if (problem == "locked") locked = File.Open(fixture.Wave, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        if (problem == "directory") { File.Delete(fixture.Wave); Directory.CreateDirectory(fixture.Wave); }
        try
        {
            var log = new AuditLog(); var result = await RecordingArtifactAuditAcceptance.Service(fixture, workstation,
                inspector: problem == "access" ? new DeniedInspector() : null, log: log).ReconcileAsync();
            Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 0, 0, 1), result);
            Assert.AreEqual(before, await fixture.Artifacts.GetAsync(before.Artifact.Id)); Assert.IsTrue(log.Events.Contains(OperationEvent.RecordingArtifactAuditIncomplete));
        }
        finally { locked?.Dispose(); }
    }
    [TestMethod]
    public async Task InspectionLeaseBlocksConcurrentCompletionAndIsReleasedOnCancellation()
    {
        using var fixture = await Ready(); var workstation = await RecordingArtifactAuditAcceptance.EndAsync(fixture);
        await using (var lease = fixture.Outbox.AcquireInspection(fixture.Id))
        {
            Assert.IsFalse(lease.HasPending);
            await Assert.ThrowsExactlyAsync<RecordingOutboxException>(() => fixture.Outbox.StageAsync(fixture.CompletedRecording, fixture.CompletedCapture));
            Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 0, 0, 1), await RecordingArtifactAuditAcceptance.Service(fixture, workstation).ReconcileAsync());
        }
        await Assert.ThrowsAsync<OperationCanceledException>(() => RecordingArtifactAuditAcceptance.Service(fixture, workstation).ReconcileAsync(new CancellationToken(true)));
        await using var available = fixture.Outbox.AcquireInspection(fixture.Id); Assert.IsFalse(available.HasPending);
    }
    [TestMethod]
    public async Task StaleWriterCannotOverwriteANewerCatalogVersionAndItsFileReadLockIsHeld()
    {
        using var fixture = await Ready(); var workstation = await RecordingArtifactAuditAcceptance.EndAsync(fixture);
        var before = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single();
        var missing = new RecordingArtifact(before.Artifact.Id, fixture.Id, before.Artifact.CallId, before.Artifact.Format, before.Artifact.RelativePath,
            RecordingAvailability.Missing, before.Artifact.ByteLength, before.Artifact.Sha256);
        await fixture.Artifacts.WriteAsync(new(missing, before.Version, fixture.Changed.AddMinutes(1)));
        var mutating = new BeforeAuditWrite(fixture.Artifacts, async () =>
        {
            Assert.ThrowsExactly<IOException>(() => { using var ignored = File.Open(fixture.Wave, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });
            await fixture.Artifacts.WriteAsync(new(missing, 3, fixture.Changed.AddMinutes(2)));
        });
        var result = await RecordingArtifactAuditAcceptance.Service(fixture, workstation, repository: mutating).ReconcileAsync();
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 0, 1, 0), result);
        var current = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single(); Assert.AreEqual(4L, current.Version); Assert.AreEqual(RecordingAvailability.Missing, current.Artifact.Availability);
    }
    [TestMethod]
    public async Task AnUnconfirmedStatusCommitIsIdempotentOnTheNextAudit()
    {
        using var fixture = await Ready(); var workstation = await RecordingArtifactAuditAcceptance.EndAsync(fixture); File.Delete(fixture.Wave);
        var result = await RecordingArtifactAuditAcceptance.Service(fixture, workstation, repository: new LostAuditAck(fixture.Artifacts)).ReconcileAsync();
        Assert.AreEqual(1, result.Failed); var saved = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single(); Assert.AreEqual(3L, saved.Version);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 1, 0, 0), await RecordingArtifactAuditAcceptance.Service(fixture, workstation).ReconcileAsync());
        Assert.AreEqual(saved, await fixture.Artifacts.GetAsync(saved.Artifact.Id));
    }
    private static async Task<WaveArtifactFixture> Ready()
    {
        var fixture = await WaveArtifactFixture.Create(); await fixture.Journal().WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture); return fixture;
    }
    [TestMethod]
    public async Task AReceiptPublishedAfterEnumerationStillTakesPrecedenceUnderTheInspectionLease()
    {
        using var fixture = await Ready(); var workstation = await RecordingArtifactAuditAcceptance.EndAsync(fixture);
        var query = new LateReceiptQuery(new DapperRecordingArtifactAuditQuery(new(fixture.Target, new())), async () =>
        { await using (await fixture.Outbox.StageAsync(fixture.CompletedRecording, fixture.CompletedCapture)) { } File.Delete(fixture.Wave); });
        var audit = new RecordingArtifactReconciliation(query, fixture.Artifacts, fixture.Outbox,
            new RecordingArtifactFileInspector(fixture.Paths, new LocalRecordingFileProbe(fixture.Paths)), new KnownIdentity(workstation), TimeProvider.System,
            NullLogger<RecordingArtifactReconciliation>.Instance);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 0, 1, 0), await audit.ReconcileAsync());
        Assert.AreEqual(RecordingAvailability.Available, (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single().Artifact.Availability);
        Assert.AreEqual(1, fixture.Outbox.GetPending().Count);
    }
    [TestMethod]
    public async Task AuditingMoreThanOnePageChangesEachMissingArtifactExactlyOnce()
    {
        using var fixture = await Ready(); var workstation = await RecordingArtifactAuditAcceptance.EndAsync(fixture);
        var context = new DatabaseRepositoryContext(fixture.Target, new()); var call = fixture.CompletedRecording.Recording.CallId;
        var ids = new List<Guid>();
        for (var index = 0; index < 102; index++)
        {
            var id = Guid.NewGuid(); ids.Add(id); var path = id.ToString("N") + ".wav";
            await new DapperRecordingRepository(context).WriteAsync(new(new(id, call, path, "wav", null, RecordingAvailability.Pending), null, fixture.Changed));
            await fixture.Artifacts.WriteAsync(new(new(id, id, call, RecordingArtifactFormat.Wave, path, RecordingAvailability.Pending), null, fixture.Changed));
            await fixture.Artifacts.WriteAsync(new(new(id, id, call, RecordingArtifactFormat.Wave, path, RecordingAvailability.Available, 128, new string('a', 64)), 1, fixture.Changed));
        }
        var audit = RecordingArtifactAuditAcceptance.Service(fixture, workstation);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(102, 0, 1, 0, 0), await audit.ReconcileAsync());
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 103, 0, 0), await audit.ReconcileAsync());
        foreach (var id in ids) { var saved = await fixture.Artifacts.GetAsync(id); Assert.AreEqual(3L, saved!.Version); Assert.AreEqual(RecordingAvailability.Missing, saved.Artifact.Availability); }
    }
    private sealed class LateReceiptQuery(IRecordingArtifactAuditQuery inner, Func<Task> before) : IRecordingArtifactAuditQuery
    {
        public async Task<WawiCallPro.Interfaces.Common.Page<StoredRecordingArtifact>> GetPageAsync(Guid workstationId, Guid? afterArtifactId = null, int limit = 100, CancellationToken cancellationToken = default)
        { await before(); return await inner.GetPageAsync(workstationId, afterArtifactId, limit, cancellationToken); }
    }
    private sealed class KnownIdentity(Guid id) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult<Guid?>(id); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new InvalidOperationException(); }
    private sealed class DeniedInspector : IRecordingArtifactFileInspector
    { public Task<IRecordingArtifactObservation> InspectAsync(RecordingArtifact artifact, CancellationToken token = default) => throw new UnauthorizedAccessException("private-content"); }
    private sealed class BeforeAuditWrite(IRecordingArtifactRepository inner, Func<Task> before) : IRecordingArtifactRepository
    {
        public Task<StoredRecordingArtifact?> GetAsync(Guid id, CancellationToken token = default) => inner.GetAsync(id, token);
        public Task<IReadOnlyList<StoredRecordingArtifact>> GetForRecordingAsync(Guid id, CancellationToken token = default) => inner.GetForRecordingAsync(id, token);
        public async Task<StoredRecordingArtifact> WriteAsync(RecordingArtifactWrite write, CancellationToken token = default) { await before(); return await inner.WriteAsync(write, token); }
    }
    private sealed class LostAuditAck(IRecordingArtifactRepository inner) : IRecordingArtifactRepository
    {
        public Task<StoredRecordingArtifact?> GetAsync(Guid id, CancellationToken token = default) => inner.GetAsync(id, token);
        public Task<IReadOnlyList<StoredRecordingArtifact>> GetForRecordingAsync(Guid id, CancellationToken token = default) => inner.GetForRecordingAsync(id, token);
        public async Task<StoredRecordingArtifact> WriteAsync(RecordingArtifactWrite write, CancellationToken token = default)
        { await inner.WriteAsync(write, token); throw new DatabaseOperationException(DatabaseErrorCode.CommitUncertain); }
    }
    private sealed class AuditLog : ILogger<RecordingArtifactReconciliation>
    {
        internal readonly List<OperationEvent> Events = [];
        public bool IsEnabled(LogLevel level) => true; public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        { Assert.IsNull(error); Assert.IsTrue(OperationEvents.TryGetEvent(state, out var value)); Assert.AreEqual(LogLevel.Warning, level); Events.Add(value); Assert.IsFalse(formatter(state, error).Contains("private-content", StringComparison.Ordinal)); }
    }
}

public static class RecordingArtifactAuditAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        using var fixture = await WaveArtifactFixture.Create(target); await fixture.Journal().WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture);
        var workstation = await EndAsync(fixture); var before = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single();
        var parent = await fixture.Parent.GetAsync(fixture.Id); var capture = await fixture.Captures.GetAsync(fixture.Id);
        var mp3 = new RecordingArtifact(Guid.NewGuid(), fixture.Id, before.Artifact.CallId, RecordingArtifactFormat.Mp3, "unrelated.mp3", RecordingAvailability.Pending);
        var preserved = await fixture.Artifacts.WriteAsync(new(mp3, null, fixture.Changed));
        var service = Service(fixture, workstation); Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 1, 0, 0), await service.ReconcileAsync());
        var temporary = Path.Combine(fixture.Paths.RootDirectory, "saved.wav"); File.Move(fixture.Wave, temporary);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(1, 0, 0, 0, 0), await service.ReconcileAsync());
        var missing = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single(item => item.Artifact.Format == RecordingArtifactFormat.Wave);
        Assert.AreEqual(3L, missing.Version); Assert.AreEqual(before.Artifact.Sha256, missing.Artifact.Sha256); Assert.AreEqual(before.Artifact.ByteLength, missing.Artifact.ByteLength);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 1, 0, 0), await service.ReconcileAsync());
        File.Move(temporary, fixture.Wave);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 1, 0, 0, 0), await Service(fixture, workstation).ReconcileAsync());
        var restored = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single(item => item.Artifact.Format == RecordingArtifactFormat.Wave);
        Assert.AreEqual(4L, restored.Version); Assert.AreEqual(before.Artifact, restored.Artifact);
        Assert.AreEqual(parent, await fixture.Parent.GetAsync(fixture.Id)); Assert.AreEqual(capture, await fixture.Captures.GetAsync(fixture.Id));
        Assert.AreEqual(preserved, await fixture.Artifacts.GetAsync(mp3.Id));
        await RecordingMp3ArtifactAuditTests.VerifyRoundtripAsync(target);
    }
    internal static RecordingArtifactReconciliation Service(WaveArtifactFixture fixture, Guid? workstation,
        IRecordingArtifactFileInspector? inspector = null, IRecordingArtifactRepository? repository = null, ILogger<RecordingArtifactReconciliation>? log = null) =>
        new(new DapperRecordingArtifactAuditQuery(new(fixture.Target, new())), repository ?? fixture.Artifacts, fixture.Outbox,
            inspector ?? new RecordingArtifactFileInspector(fixture.Paths, new LocalRecordingFileProbe(fixture.Paths)), new Identity(workstation), TimeProvider.System,
            log ?? NullLogger<RecordingArtifactReconciliation>.Instance, fixture.ConversionOutbox(), new DapperRecordingConversionRepository(new(fixture.Target, new())));
    internal static async Task<Guid> EndAsync(WaveArtifactFixture fixture)
    {
        var calls = new DapperCallRepository(new(fixture.Target, new())); var stored = (await calls.GetAsync(fixture.CompletedRecording.Recording.CallId))!;
        var now = DateTimeOffset.UtcNow; var call = stored.History.Call;
        await calls.WriteAsync(new(call with { Revision = call.Revision + 1, State = CallState.Ended, StateChangedAtUtc = now, EndedAtUtc = now, EndReason = CallEndReason.Completed },
            stored.Version, stored.History.Parties, [], call.Origin!)); return call.Origin!.WorkstationId;
    }
    private sealed class Identity(Guid? id) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult(id); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new InvalidOperationException("Audit must not create an identity."); }
}
