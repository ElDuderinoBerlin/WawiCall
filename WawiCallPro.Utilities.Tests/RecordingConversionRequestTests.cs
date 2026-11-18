using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingConversionRequestTests
{
    [TestMethod]
    public async Task RequestsCreateOnePairAndTheWorkerRunsItOnlyOnce()
    { using var database = await WaveArtifactFixture.Create(); await RecordingConversionRequestAcceptance.RunAsync(database.Target); }

    [TestMethod]
    [DataRow("live")][DataRow("foreign")][DataRow("identity")][DataRow("missing")][DataRow("changed")]
    [DataRow("locked")][DataRow("busy")][DataRow("receipt")][DataRow("destination")][DataRow("format")]
    public async Task InvalidOrUnprovenSourcesLeaveFilesAndTheCatalogUntouched(string guard)
    {
        using var file = await Ready(); var local = guard == "live"
            ? (await new DapperCallRepository(new(file.Target, new())).GetAsync(file.CompletedRecording.Recording.CallId))!.History.Call.Origin!.WorkstationId
            : await RecordingArtifactAuditAcceptance.EndAsync(file);
        var original = File.ReadAllBytes(file.Wave); var destination = Path.ChangeExtension(file.Wave, ".mp3");
        if (guard == "missing") File.Delete(file.Wave);
        if (guard == "changed") { var bytes = original.ToArray(); bytes[^1] ^= 1; File.WriteAllBytes(file.Wave, bytes); }
        if (guard == "destination") File.WriteAllBytes(destination, [7, 8, 9]);
        using var locked = guard == "locked" ? File.Open(file.Wave, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        await using var held = guard == "busy" ? file.Outbox.AcquireInspection(file.Id) : null;
        if (guard == "receipt") { await using var delivery = await file.Outbox.StageAsync(file.CompletedRecording, file.CompletedCapture); }
        var before = (await file.Artifacts.GetForRecordingAsync(file.Id)).Single(); var queue = new RequestSignal();
        var service = RecordingConversionRequestAcceptance.Service(file, guard == "identity" ? null : guard == "foreign" ? Guid.NewGuid() : local, queue);
        Task Request() => service.EnqueueAsync(file.Id, new(guard == "format" ? 16000 : 8000, 2, 128000));
        if (guard is "missing" or "changed" or "destination") await Assert.ThrowsExactlyAsync<Mp3ConversionException>(Request);
        else if (guard is "busy" or "receipt") await Assert.ThrowsExactlyAsync<RecordingOutboxException>(Request);
        else if (guard == "locked") await Assert.ThrowsAsync<IOException>(Request);
        else await Assert.ThrowsExactlyAsync<RepositoryConflictException>(Request);
        Assert.IsNull(await new DapperRecordingConversionRepository(new(file.Target, new())).GetForSourceAsync(file.Id));
        Assert.AreEqual(before, (await file.Artifacts.GetForRecordingAsync(file.Id)).Single()); Assert.AreEqual(0, queue.Calls);
        if (guard == "destination") CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, File.ReadAllBytes(destination));
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task UncertainCreationCanBeRetriedWithoutAdoptingOrDuplicatingAPair(bool committed)
    {
        using var file = await Ready(); var local = await RecordingArtifactAuditAcceptance.EndAsync(file); var queue = new RequestSignal();
        var inner = new DurableRecordingConversionJournal(new(new(file.Target, new())), file.ConversionOutbox());
        var uncertain = new UncertainBegin(inner, committed);
        var service = RecordingConversionRequestAcceptance.Service(file, local, queue, uncertain);
        await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => service.EnqueueAsync(file.Id, new(8000, 2, 128000)));
        Assert.AreEqual(0, queue.Calls);
        var existing = await new DapperRecordingConversionRepository(new(file.Target, new())).GetForSourceAsync(file.Id);
        var next = await RecordingConversionRequestAcceptance.Service(file, local, queue).EnqueueAsync(file.Id, new(8000, 2, 128000));
        if (committed) Assert.AreEqual(existing, next.Conversion); else Assert.IsNull(existing);
        Assert.AreEqual(2, (await file.Artifacts.GetForRecordingAsync(file.Id)).Count); Assert.AreEqual(1, queue.Calls);
    }

    [TestMethod]
    public async Task ExistingTargetWithoutAJobIsNotSilentlyAdopted()
    {
        using var file = await Ready(); var local = await RecordingArtifactAuditAcceptance.EndAsync(file);
        var target = new RecordingArtifact(Guid.NewGuid(), file.Id, file.CompletedRecording.Recording.CallId, RecordingArtifactFormat.Mp3, "retained.mp3", RecordingAvailability.Pending);
        var before = await file.Artifacts.WriteAsync(new(target, null, DateTimeOffset.UtcNow));
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => RecordingConversionRequestAcceptance.Service(file, local, new RequestSignal()).EnqueueAsync(file.Id, new(8000, 2, 128000)));
        Assert.AreEqual(before, await file.Artifacts.GetAsync(target.Id));
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task ProtectedCompletionPreventsARepeatedRequestFromSignallingTheWorker(bool after)
    {
        using var abandoned = await AbandonedMp3.Create(after: after); var ready = abandoned.Ready; var queue = new RequestSignal();
        var before = await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id);
        await Assert.ThrowsExactlyAsync<RecordingConversionOutboxException>(() => RecordingConversionRequestAcceptance.Service(ready.File, ready.Workstation, queue).EnqueueAsync(ready.File.Id, new(8000, 2, 128000)));
        Assert.AreEqual(before, await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id)); Assert.AreEqual(0, queue.Calls); Assert.IsTrue(File.Exists(abandoned.Entry));
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task TerminalJobsAreReturnedWithoutRequeueingOrSignalling(bool failed)
    {
        using var ready = await Mp3AttemptFixture.Create();
        await using (var session = await ready.Factory(new AttemptConverter { Failure = failed ? "error" : null }, ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id))
            await session.RunAsync();
        var before = await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id); var queue = new RequestSignal();
        var result = await RecordingConversionRequestAcceptance.Service(ready.File, ready.Workstation, queue).EnqueueAsync(ready.File.Id, new(8000, 2, 128000));
        Assert.AreEqual(before, result.Conversion); Assert.AreEqual(0, queue.Calls);
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task RepeatedPendingRequestsRevalidateSourceAndUnconfirmedDestination(bool destination)
    {
        using var file = await Ready(); var local = await RecordingArtifactAuditAcceptance.EndAsync(file); var queue = new RequestSignal();
        var service = RecordingConversionRequestAcceptance.Service(file, local, queue); var pair = await service.EnqueueAsync(file.Id, new(8000, 2, 128000));
        if (destination) File.WriteAllBytes(Path.ChangeExtension(file.Wave, ".mp3"), [5, 6]); else File.Delete(file.Wave);
        await Assert.ThrowsExactlyAsync<Mp3ConversionException>(() => service.EnqueueAsync(file.Id, new(8000, 2, 128000)));
        Assert.AreEqual(pair.Conversion, await new DapperRecordingConversionRepository(new(file.Target, new())).GetForSourceAsync(file.Id)); Assert.AreEqual(1, queue.Calls);
    }

    [TestMethod]
    public async Task CompetingRequestCannotCreateASecondPairWhileTheFirstOwnsTheRecording()
    {
        using var file = await Ready(); var local = await RecordingArtifactAuditAcceptance.EndAsync(file); var queue = new RequestSignal();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var journal = new PausedBegin(new DurableRecordingConversionJournal(new(new(file.Target, new())), file.ConversionOutbox()), entered, release);
        var first = RecordingConversionRequestAcceptance.Service(file, local, queue, journal).EnqueueAsync(file.Id, new(8000, 2, 128000));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try { await Assert.ThrowsExactlyAsync<RecordingOutboxException>(() => RecordingConversionRequestAcceptance.Service(file, local, queue).EnqueueAsync(file.Id, new(8000, 2, 128000))); }
        finally { release.TrySetResult(); }
        var pair = await first; Assert.AreEqual(pair, await RecordingConversionRequestAcceptance.Service(file, local, queue).EnqueueAsync(file.Id, new(8000, 2, 128000)));
        Assert.AreEqual(2, (await file.Artifacts.GetForRecordingAsync(file.Id)).Count);
    }

    [TestMethod]
    public async Task CancelledRequestsDoNotCreatePairsOrSignalWork()
    {
        using var file = await Ready(); var local = await RecordingArtifactAuditAcceptance.EndAsync(file); var queue = new RequestSignal();
        await Assert.ThrowsAsync<OperationCanceledException>(() => RecordingConversionRequestAcceptance.Service(file, local, queue).EnqueueAsync(file.Id, new(8000, 2, 128000), new CancellationToken(true)));
        Assert.AreEqual(1, (await file.Artifacts.GetForRecordingAsync(file.Id)).Count); Assert.AreEqual(0, queue.Calls);
    }

    private static async Task<WaveArtifactFixture> Ready()
    { var file = await WaveArtifactFixture.Create(); await file.Journal().WriteAsync(file.CompletedRecording, file.CompletedCapture); return file; }
    internal sealed class RequestSignal(Action? signal = null) : IRecordingConversionQueue
    { internal int Calls; public void Request() { Calls++; signal?.Invoke(); } }
    private sealed class UncertainBegin(IRecordingConversionJournal inner, bool committed) : IRecordingConversionJournal
    {
        public async Task<StoredRecordingConversionJournal> BeginAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken token = default)
        { if (committed) await inner.BeginAsync(target, conversion, token); throw new DatabaseOperationException(DatabaseErrorCode.CommitUncertain); }
        public Task<StoredRecordingConversionJournal> WriteAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken token = default) => inner.WriteAsync(target, conversion, token);
    }
    private sealed class PausedBegin(IRecordingConversionJournal inner, TaskCompletionSource entered, TaskCompletionSource release) : IRecordingConversionJournal
    {
        public async Task<StoredRecordingConversionJournal> BeginAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken token = default)
        { entered.SetResult(); await release.Task.WaitAsync(token); return await inner.BeginAsync(target, conversion, token); }
        public Task<StoredRecordingConversionJournal> WriteAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken token = default) => throw new AssertFailedException();
    }
}

public static class RecordingConversionRequestAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        using var file = await WaveArtifactFixture.Create(target); await file.Journal().WriteAsync(file.CompletedRecording, file.CompletedCapture);
        var local = await RecordingArtifactAuditAcceptance.EndAsync(file); var wave = File.ReadAllBytes(file.Wave);
        var queue = new RecordingConversionRequestTests.RequestSignal(() =>
        { using var unlocked = File.Open(file.Wave, FileMode.Open, FileAccess.Write, FileShare.None); var lease = file.Outbox.AcquireInspection(file.Id); lease.DisposeAsync().AsTask().GetAwaiter().GetResult(); });
        var service = Service(file, local, queue); var format = new Mp3EncodingFormat(8000, 2, 128000);
        var pair = await service.EnqueueAsync(file.Id, format); Assert.AreEqual(pair, await service.EnqueueAsync(file.Id, format));
        Assert.AreEqual(2, queue.Calls); Assert.AreEqual(2, (await file.Artifacts.GetForRecordingAsync(file.Id)).Count);
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => service.EnqueueAsync(file.Id, new(8000, 2, 96000)));
        using var ready = new Mp3AttemptFixture(file, local, pair); var converter = new AttemptConverter();
        var batch = new RecordingConversionBatch(ready.Jobs, new Identity(local), ready.Factory(converter, local));
        Assert.AreEqual(new RecordingConversionBatchResult(1, 0, 0), await batch.RunAsync());
        var completed = await service.EnqueueAsync(file.Id, format); Assert.AreEqual(RecordingConversionState.Completed, completed.Conversion.Job.State);
        Assert.AreEqual(2, queue.Calls); Assert.AreEqual(new RecordingConversionBatchResult(0, 0, 0), await batch.RunAsync()); Assert.AreEqual(1, converter.Calls);
        CollectionAssert.AreEqual(wave, File.ReadAllBytes(file.Wave));
    }
    internal static RecordingConversionRequests Service(WaveArtifactFixture file, Guid? local, IRecordingConversionQueue queue, IRecordingConversionJournal? journal = null) =>
        new(file.Artifacts, file.Captures, new DapperRecordingConversionRepository(new(file.Target, new())),
            journal ?? new DurableRecordingConversionJournal(new(new(file.Target, new())), file.ConversionOutbox()), new DapperCallRepository(new(file.Target, new())),
            new Identity(local), file.Outbox, file.ConversionOutbox(), new RecordingArtifactFileInspector(file.Paths, new LocalRecordingFileProbe(file.Paths)), new LocalRecordingFileProbe(file.Paths), queue, TimeProvider.System);
    private sealed class Identity(Guid? id) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult(id); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new AssertFailedException(); }
}
