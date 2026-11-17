using Dapper;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class RecordingConversionJournalTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wcp-conversion-journal-" + Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    [TestMethod] public async Task BeginReplaysOnlyCompletePairsAndRejectsIndependentTargets() => await RecordingConversionJournalAcceptance.BeginAsync(await Create());
    [TestMethod] public async Task CompletionFailureAndRetryCommitBothRowsAndSurviveRecreation() => await RecordingConversionJournalAcceptance.LifecycleAsync(await Create());
    [TestMethod] public async Task LateConflictsRollBackTheTargetAndConcurrentCompletionsKeepOnePair() => await RecordingConversionJournalAcceptance.ConflictsAsync(await Create());

    [TestMethod]
    [DataRow("format")][DataRow("target")][DataRow("time")][DataRow("target-version")][DataRow("job-version")]
    [DataRow("running")][DataRow("failed")][DataRow("completed")][DataRow("attempts")]
    [DataRow("write-target-version")][DataRow("write-job-version")][DataRow("status")]
    public async Task InconsistentPairsAreRejectedBeforeAnyDatabaseAccess(string invalid)
    {
        var context = new DatabaseRepositoryContext(new RejectTarget(), new(), new DatabaseMigrationEngineFactory(), new DatabaseMigrationLock());
        var journal = new DapperRecordingConversionJournal(context); var time = RepositoryAcceptance.Time;
        var source = Guid.NewGuid(); var target = Guid.NewGuid(); var state = invalid switch
        {
            "running" => RecordingConversionState.Running, "failed" => RecordingConversionState.Failed,
            "completed" or "status" => RecordingConversionState.Completed, _ => RecordingConversionState.Pending
        };
        var attempts = state == RecordingConversionState.Pending ? invalid == "attempts" ? 1 : 0 : 1;
        var job = new RecordingConversionJob(Guid.NewGuid(), source, invalid == "target" ? Guid.NewGuid() : target, new(8000, 2, 128000),
            state, attempts, state == RecordingConversionState.Pending ? null : Guid.NewGuid(), state == RecordingConversionState.Failed ? RecordingConversionFailure.Interrupted : null);
        var available = state == RecordingConversionState.Completed && invalid != "status";
        var availability = available ? RecordingAvailability.Available : state == RecordingConversionState.Failed ? RecordingAvailability.Failed : RecordingAvailability.Pending;
        var artifact = new RecordingArtifact(target, Guid.NewGuid(), Guid.NewGuid(), invalid == "format" ? RecordingArtifactFormat.Wave : RecordingArtifactFormat.Mp3,
            invalid == "format" ? "target.wav" : "target.mp3", availability, available ? 500 : null, available ? new string('a', 64) : null);
        var write = invalid.StartsWith("write-", StringComparison.Ordinal);
        var targetWrite = new RecordingArtifactWrite(artifact, invalid == "target-version" || write && invalid != "write-target-version" ? 1 : null, time);
        var jobWrite = new RecordingConversionWrite(job, invalid == "job-version" || write && invalid != "write-job-version" ? 1 : null, invalid == "time" ? time.AddTicks(1) : time);
        await Assert.ThrowsAsync<ArgumentException>(() => write ? journal.WriteAsync(targetWrite, jobWrite) : journal.BeginAsync(targetWrite, jobWrite));
    }
    private async Task<DatabaseTarget> Create()
    {
        var target = DatabaseTarget.Sqlite(Path.Combine(_root, "journal.db")); await new DatabaseProvisioner(new()).CreateAsync(target);
        await new DatabaseMigrator(new(), new FluentMigratorEngine(target.Type)).MigrateAsync(target); return target;
    }
    private sealed class RejectTarget : IDatabaseTargetSource
    { public DatabaseTarget GetTarget() => throw new AssertFailedException("Invalid pairs must not resolve a database target."); }
}

/// <summary>Atomic pair acceptance shared by SQLite and all disposable native providers.</summary>
public static class RecordingConversionJournalAcceptance
{
    private static DateTimeOffset Time => RepositoryAcceptance.Time.AddSeconds(2);
    public static async Task RunAsync(DatabaseTarget target)
    { await BeginAsync(target); await LifecycleAsync(target); await ConflictsAsync(target); }

    public static async Task BeginAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var artifacts = new DapperRecordingArtifactRepository(context);
        var jobs = new DapperRecordingConversionRepository(context); var journal = new DapperRecordingConversionJournal(context);
        var seed = await RecordingConversionAcceptance.SeedAsync(context, false); var job = Pending(seed);
        var artifactWrite = new RecordingArtifactWrite(seed.Mp3, null, Time); var jobWrite = new RecordingConversionWrite(job, null, Time);
        var saved = await journal.BeginAsync(artifactWrite, jobWrite);
        Assert.AreEqual(1L, saved.Target.Version); Assert.AreEqual(1L, saved.Conversion.Version);
        Assert.AreEqual(saved, await new DapperRecordingConversionJournal(new(target, new())).BeginAsync(artifactWrite, jobWrite));
        Assert.AreEqual(saved.Target, await artifacts.GetAsync(seed.Mp3.Id)); Assert.AreEqual(saved.Conversion, await jobs.GetForSourceAsync(seed.Wave.Id));
        Assert.AreEqual(2, (await artifacts.GetForRecordingAsync(seed.Mp3.RecordingId)).Count);
        await Conflict(() => journal.BeginAsync(artifactWrite, new(Pending(seed), null, Time)), RepositoryConflictKind.AlreadyExists);
        var orphan = await RecordingConversionAcceptance.SeedAsync(context, true);
        var original = await artifacts.GetAsync(orphan.Mp3.Id);
        await Conflict(() => journal.BeginAsync(new(orphan.Mp3, null, Time), new(Pending(orphan), null, Time)), RepositoryConflictKind.AlreadyExists);
        Assert.AreEqual(original, await artifacts.GetAsync(orphan.Mp3.Id)); Assert.IsNull(await jobs.GetForSourceAsync(orphan.Wave.Id));

        var absent = await RecordingConversionAcceptance.SeedAsync(context, false);
        var missing = new RecordingArtifact(absent.Wave.Id, absent.Wave.RecordingId, absent.Wave.CallId, RecordingArtifactFormat.Wave,
            absent.Wave.RelativePath, RecordingAvailability.Missing, 684, new string('c', 64));
        await artifacts.WriteAsync(new(missing, 2, Time));
        await Conflict(() => journal.BeginAsync(new(absent.Mp3, null, Time), new(Pending(absent), null, Time)), RepositoryConflictKind.SnapshotMismatch);
        Assert.IsNull(await artifacts.GetAsync(absent.Mp3.Id)); Assert.IsNull(await jobs.GetForSourceAsync(absent.Wave.Id));
        Assert.AreEqual(missing, (await artifacts.GetAsync(absent.Wave.Id))!.Artifact);
        var canceled = await RecordingConversionAcceptance.SeedAsync(context, false);
        await Assert.ThrowsAsync<OperationCanceledException>(() => journal.BeginAsync(new(canceled.Mp3, null, Time), new(Pending(canceled), null, Time), new CancellationToken(true)));
        Assert.IsNull(await artifacts.GetAsync(canceled.Mp3.Id)); Assert.IsNull(await jobs.GetForSourceAsync(canceled.Wave.Id));

        var racing = await RecordingConversionAcceptance.SeedAsync(context, false);
        async Task<bool> Enqueue()
        {
            try { await new DapperRecordingConversionJournal(new(target, new())).BeginAsync(new(racing.Mp3, null, Time), new(Pending(racing), null, Time)); return true; }
            catch (RepositoryConflictException error) when (error.Kind == RepositoryConflictKind.AlreadyExists) { return false; }
        }
        Assert.AreEqual(1, (await Task.WhenAll(Enqueue(), Enqueue())).Count(success => success));
        Assert.AreEqual(2, (await artifacts.GetForRecordingAsync(racing.Mp3.RecordingId)).Count);
        Assert.AreEqual(1L, (await jobs.GetForSourceAsync(racing.Wave.Id))!.Version);
    }

    public static async Task LifecycleAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var seed = await RecordingConversionAcceptance.SeedAsync(context, false);
        var journal = new DapperRecordingConversionJournal(context); var jobs = new DapperRecordingConversionRepository(context);
        var job = Pending(seed); await journal.BeginAsync(new(seed.Mp3, null, Time), new(job, null, Time));
        var running = State(job, RecordingConversionState.Running, 1, Guid.NewGuid());
        await jobs.WriteAsync(new(running, 1, Time.AddTicks(1)));
        var failed = State(running, RecordingConversionState.Failed, 1, running.AttemptId, RecordingConversionFailure.Interrupted);
        var failedArtifact = Artifact(seed.Mp3, RecordingAvailability.Failed);
        var failedTarget = new RecordingArtifactWrite(failedArtifact, 1, Time.AddTicks(2)); var failedJob = new RecordingConversionWrite(failed, 2, Time.AddTicks(2));
        var failure = await journal.WriteAsync(failedTarget, failedJob);
        Assert.AreEqual(2L, failure.Target.Version); Assert.AreEqual(3L, failure.Conversion.Version);
        Assert.AreEqual(failure, await new DapperRecordingConversionJournal(new(target, new())).WriteAsync(failedTarget, failedJob));
        var queued = State(job, RecordingConversionState.Pending, 1, null);
        var requeued = await journal.WriteAsync(new(seed.Mp3, 2, Time.AddTicks(3)), new(queued, 3, Time.AddTicks(3)));
        Assert.AreEqual(3L, requeued.Target.Version); Assert.AreEqual(4L, requeued.Conversion.Version);
        running = State(job, RecordingConversionState.Running, 2, Guid.NewGuid());
        await jobs.WriteAsync(new(running, 4, Time.AddTicks(4)));
        var done = State(running, RecordingConversionState.Completed, 2, running.AttemptId);
        var mp3 = Artifact(seed.Mp3, RecordingAvailability.Available);
        var completeTarget = new RecordingArtifactWrite(mp3, 3, Time.AddTicks(5)); var completeJob = new RecordingConversionWrite(done, 5, Time.AddTicks(5));
        var completed = await journal.WriteAsync(completeTarget, completeJob);
        Assert.AreEqual(4L, completed.Target.Version); Assert.AreEqual(6L, completed.Conversion.Version);
        Assert.AreEqual(Time, completed.Target.CreatedAtUtc); Assert.AreEqual(Time, completed.Conversion.CreatedAtUtc);
        Assert.AreEqual(completed, await new DapperRecordingConversionJournal(new(target, new())).WriteAsync(completeTarget, completeJob));
        await Conflict(() => journal.WriteAsync(new(mp3, 4, completeTarget.ChangedAtUtc), completeJob), RepositoryConflictKind.VersionMismatch);
        await Conflict(() => journal.WriteAsync(completeTarget, new(done, 6, completeJob.ChangedAtUtc)), RepositoryConflictKind.VersionMismatch);
        Assert.AreEqual(completed.Target, await new DapperRecordingArtifactRepository(context).GetAsync(seed.Mp3.Id));
        Assert.AreEqual(completed.Conversion, await jobs.GetAsync(job.Id));
        Assert.AreEqual(seed.Recording, await new DapperRecordingRepository(context).GetAsync(seed.Mp3.RecordingId));
        Assert.AreEqual(seed.Capture, await new DapperRecordingCaptureRepository(context).GetAsync(seed.Mp3.RecordingId));
        Assert.AreEqual(2, (await new DapperRecordingArtifactRepository(context).GetForRecordingAsync(seed.Mp3.RecordingId)).Count);
    }

    public static async Task ConflictsAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var seed = await RecordingConversionAcceptance.SeedAsync(context, false);
        var journal = new DapperRecordingConversionJournal(context); var jobs = new DapperRecordingConversionRepository(context);
        var artifacts = new DapperRecordingArtifactRepository(context); var job = Pending(seed);
        var initial = await journal.BeginAsync(new(seed.Mp3, null, Time), new(job, null, Time));
        var running = State(job, RecordingConversionState.Running, 1, Guid.NewGuid()); var active = await jobs.WriteAsync(new(running, 1, Time.AddTicks(1)));
        var done = State(running, RecordingConversionState.Completed, 1, running.AttemptId);
        var mp3 = Artifact(seed.Mp3, RecordingAvailability.Available);
        var wrongAttempt = State(running, RecordingConversionState.Completed, 1, Guid.NewGuid());
        await Conflict(() => journal.WriteAsync(new(mp3, 1, Time.AddTicks(2)), new(wrongAttempt, 2, Time.AddTicks(2))), RepositoryConflictKind.SnapshotMismatch);
        Assert.AreEqual(initial.Target, await artifacts.GetAsync(seed.Mp3.Id)); Assert.AreEqual(active, await jobs.GetAsync(job.Id));
        var wrongFormat = new RecordingConversionJob(job.Id, job.SourceArtifactId, job.TargetArtifactId, new(16000, 2, 128000), done.State, 1, running.AttemptId);
        await Conflict(() => journal.WriteAsync(new(mp3, 1, Time.AddTicks(2)), new(wrongFormat, 2, Time.AddTicks(2))), RepositoryConflictKind.OwnershipMismatch);
        Assert.AreEqual(initial.Target, await artifacts.GetAsync(seed.Mp3.Id)); Assert.AreEqual(active, await jobs.GetAsync(job.Id));
        var moved = new RecordingArtifact(mp3.Id, mp3.RecordingId, mp3.CallId, mp3.Format, "changed.mp3", mp3.Availability, mp3.ByteLength, mp3.Sha256);
        await Conflict(() => journal.WriteAsync(new(moved, 1, Time.AddTicks(2)), new(done, 2, Time.AddTicks(2))), RepositoryConflictKind.OwnershipMismatch);
        await Assert.ThrowsAsync<OperationCanceledException>(() => journal.WriteAsync(new(mp3, 1, Time.AddTicks(2)), new(done, 2, Time.AddTicks(2)), new CancellationToken(true)));
        Assert.AreEqual(initial.Target, await artifacts.GetAsync(seed.Mp3.Id)); Assert.AreEqual(active, await jobs.GetAsync(job.Id));
        async Task<bool> Complete(char digest, int ticks)
        {
            var file = Artifact(seed.Mp3, RecordingAvailability.Available, digest);
            try { await new DapperRecordingConversionJournal(new(target, new())).WriteAsync(new(file, 1, Time.AddTicks(ticks)), new(done, 2, Time.AddTicks(ticks))); return true; }
            catch (RepositoryConflictException error) when (error.Kind == RepositoryConflictKind.VersionMismatch) { return false; }
        }
        Assert.AreEqual(1, (await Task.WhenAll(Complete('a', 3), Complete('b', 4))).Count(success => success));
        var targetFile = (await artifacts.GetAsync(seed.Mp3.Id))!; var targetJob = (await jobs.GetAsync(job.Id))!;
        Assert.AreEqual(2L, targetFile.Version); Assert.AreEqual(3L, targetJob.Version); Assert.AreEqual(targetFile.UpdatedAtUtc, targetJob.UpdatedAtUtc);
        Assert.AreEqual(new string(targetFile.UpdatedAtUtc == Time.AddTicks(3) ? 'a' : 'b', 64), targetFile.Artifact.Sha256);
        Assert.AreEqual(RecordingConversionState.Completed, targetJob.Job.State);

        var exhausted = await RecordingConversionAcceptance.SeedAsync(context, false); var exhaustedJob = Pending(exhausted);
        var original = await journal.BeginAsync(new(exhausted.Mp3, null, Time), new(exhaustedJob, null, Time));
        var owned = State(exhaustedJob, RecordingConversionState.Running, 1, Guid.NewGuid());
        await jobs.WriteAsync(new(owned, 1, Time.AddTicks(1)));
        await using (var db = new DatabaseFactory().CreateConnection(target))
        {
            await db.OpenAsync();
            await db.ExecuteAsync($"UPDATE {DatabaseSql.Table(target.Type, "recordingconversions")} SET writeversion=@maximum WHERE id=@id",
                new { maximum = long.MaxValue, id = DatabaseId.Encode(owned.Id) });
        }
        await Conflict(() => journal.WriteAsync(new(Artifact(exhausted.Mp3, RecordingAvailability.Available), 1, Time.AddTicks(2)),
            new(State(owned, RecordingConversionState.Completed, 1, owned.AttemptId), long.MaxValue, Time.AddTicks(2))), RepositoryConflictKind.VersionExhausted);
        Assert.AreEqual(original.Target, await artifacts.GetAsync(exhausted.Mp3.Id));
        var retained = (await jobs.GetAsync(owned.Id))!;
        Assert.AreEqual(long.MaxValue, retained.Version); Assert.AreEqual(owned, retained.Job);
    }
    private static RecordingConversionJob Pending(RecordingConversionAcceptance.Seed seed) => new(Guid.NewGuid(), seed.Wave.Id, seed.Mp3.Id, new(8000, 2, 128000), RecordingConversionState.Pending);
    private static RecordingConversionJob State(RecordingConversionJob item, RecordingConversionState state, int attempts, Guid? attempt, RecordingConversionFailure? failure = null) =>
        new(item.Id, item.SourceArtifactId, item.TargetArtifactId, item.Format, state, attempts, attempt, failure);
    private static RecordingArtifact Artifact(RecordingArtifact item, RecordingAvailability state, char digest = 'a') => new(item.Id, item.RecordingId, item.CallId, item.Format, item.RelativePath,
        state, state == RecordingAvailability.Available ? 500 : null, state == RecordingAvailability.Available ? new string(digest, 64) : null);
    private static async Task Conflict(Func<Task> action, RepositoryConflictKind kind) => Assert.AreEqual(kind, (await Assert.ThrowsExactlyAsync<RepositoryConflictException>(action)).Kind);
}
