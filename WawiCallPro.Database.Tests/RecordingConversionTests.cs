using System.Data.Common;
using Dapper;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class RecordingConversionTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wcp-conversions-" + Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    [TestMethod] public async Task RestartRetriesConcurrentClaimsAndConstraintsKeepOneCanonicalJob() => await RecordingConversionAcceptance.RunAsync(await Create());
    [TestMethod] public async Task PopulatedElevenUpgradeKeepsCaptureArtifactsAndCreatesNoJobs() => await RecordingConversionAcceptance.UpgradeAsync(DatabaseTarget.Sqlite(Path.Combine(_root, "upgrade.db")));

    [TestMethod]
    public async Task EveryConversionColumnHasExactlyOneGermanDescription()
    {
        var target = await Create(); await using var db = new DatabaseFactory().CreateConnection(target); await db.OpenAsync();
        var columns = (await db.QueryAsync<string>("SELECT name FROM pragma_table_info('recordingconversions')")).ToArray();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Docs", "Datenbank-Tabellen-und-Spalten.md"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        var lines = await File.ReadAllLinesAsync(Path.Combine(directory.FullName, "Docs", "Datenbank-Tabellen-und-Spalten.md"));
        var described = lines.SkipWhile(line => !line.StartsWith("## recordingconversions ", StringComparison.Ordinal)).Skip(1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)).Select(line => System.Text.RegularExpressions.Regex.Match(line, @"^\|\s*`([^`]+)`\s*\|"))
            .Where(match => match.Success).Select(match => match.Groups[1].Value).ToArray();
        CollectionAssert.AreEquivalent(columns, described); Assert.AreEqual(described.Length, described.Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    [DataRow("id")][DataRow("source")][DataRow("target")][DataRow("same")][DataRow("rate")][DataRow("channels")]
    [DataRow("bitrate")][DataRow("state")][DataRow("attempts")][DataRow("attemptid")][DataRow("running")]
    [DataRow("pending")][DataRow("failed")][DataRow("failure")][DataRow("utc")][DataRow("version")]
    public void InvalidJobMetadataIsRejectedBeforeOpeningADatabase(string invalid)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var source = Guid.NewGuid(); var target = invalid == "same" ? source : Guid.NewGuid();
            var state = invalid == "state" ? (RecordingConversionState)4 : invalid == "failed" ? RecordingConversionState.Failed :
                invalid == "running" ? RecordingConversionState.Running : RecordingConversionState.Pending;
            var job = new RecordingConversionJob(invalid == "id" ? Guid.Empty : Guid.NewGuid(), invalid == "source" ? Guid.Empty : source,
                invalid == "target" ? Guid.Empty : target, new(invalid == "rate" ? 11025 : 8000, invalid == "channels" ? 3 : 2, invalid == "bitrate" ? 128001 : 128000),
                state, invalid == "attempts" ? -1 : 0, invalid == "attemptid" ? Guid.Empty : invalid == "pending" ? Guid.NewGuid() : null,
                invalid == "failure" ? (RecordingConversionFailure)10 : null);
            _ = new RecordingConversionWrite(job, invalid == "version" ? 0 : null,
                invalid == "utc" ? RepositoryAcceptance.Time.ToOffset(TimeSpan.FromHours(1)) : RepositoryAcceptance.Time);
        });
    }
    private async Task<DatabaseTarget> Create()
    {
        var target = DatabaseTarget.Sqlite(Path.Combine(_root, "conversions.db")); await new DatabaseProvisioner(new()).CreateAsync(target);
        await new DatabaseMigrator(new(), new FluentMigratorEngine(target.Type)).MigrateAsync(target); return target;
    }
}

/// <summary>The same synthetic metadata scenarios run on all five actual database engines.</summary>
public static class RecordingConversionAcceptance
{
    private static DateTimeOffset Time => RepositoryAcceptance.Time;
    private static DateTimeOffset Changed => Time.AddSeconds(2);
    private static string Digest => new('c', 64);
    public static async Task RunAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var jobs = new DapperRecordingConversionRepository(context);
        var seed = await SeedAsync(context); var pending = Job(seed);
        var unknown = new RecordingConversionJob(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), pending.Format, RecordingConversionState.Pending);
        await Conflict(() => jobs.WriteAsync(new(unknown, null, Changed)), RepositoryConflictKind.MissingRecord);
        await Conflict(() => jobs.WriteAsync(new(State(pending, RecordingConversionState.Running, 1, Guid.NewGuid()), null, Changed)), RepositoryConflictKind.SnapshotMismatch);
        Assert.IsNull(await jobs.GetAsync(pending.Id)); Assert.IsNull(await jobs.GetForSourceAsync(seed.Wave.Id));
        var create = new RecordingConversionWrite(pending, null, Changed);
        var saved = await jobs.WriteAsync(create); Assert.AreEqual(1L, saved.Version); Assert.AreEqual(saved, await jobs.WriteAsync(create));
        jobs = new(new(target, new())); Assert.AreEqual(saved, await jobs.GetForSourceAsync(seed.Wave.Id));
        await Conflict(() => jobs.WriteAsync(new(Job(seed), null, Changed)), RepositoryConflictKind.AlreadyExists);
        var first = State(pending, RecordingConversionState.Running, 1, Guid.NewGuid());
        var competing = State(pending, RecordingConversionState.Running, 1, Guid.NewGuid());
        async Task<bool> Claim(RecordingConversionJob job)
        {
            try { await new DapperRecordingConversionRepository(new(target, new())).WriteAsync(new(job, 1, Changed.AddTicks(1))); return true; }
            catch (RepositoryConflictException error) when (error.Kind == RepositoryConflictKind.VersionMismatch) { return false; }
        }
        Assert.AreEqual(1, (await Task.WhenAll(Claim(first), Claim(competing))).Count(won => won));
        var running = (await jobs.GetAsync(pending.Id))!; Assert.AreEqual(2L, running.Version);
        Assert.AreEqual(running, await jobs.WriteAsync(new(running.Job, 1, Changed.AddTicks(1))));
        Assert.AreEqual(running, await new DapperRecordingConversionRepository(new(target, new())).GetAsync(pending.Id));
        // A new repository leaves an owned running attempt untouched. Only an explicit CAS interruption permits retry.
        await Conflict(() => jobs.WriteAsync(new(State(pending, RecordingConversionState.Pending, 1), 2, Changed.AddTicks(2))), RepositoryConflictKind.SnapshotMismatch);
        await Conflict(() => jobs.WriteAsync(new(State(running.Job, RecordingConversionState.Failed, 1, Guid.NewGuid(), RecordingConversionFailure.Interrupted), 2, Changed.AddTicks(2))), RepositoryConflictKind.SnapshotMismatch);
        var interrupted = State(running.Job, RecordingConversionState.Failed, 1, running.Job.AttemptId, RecordingConversionFailure.Interrupted);
        var failed = await jobs.WriteAsync(new(interrupted, 2, Changed.AddTicks(2)));
        Assert.AreEqual(failed, await jobs.WriteAsync(new(interrupted, 2, Changed.AddTicks(2))));
        var retry = State(pending, RecordingConversionState.Pending, 1);
        await jobs.WriteAsync(new(retry, 3, Changed.AddTicks(3)));
        var second = State(pending, RecordingConversionState.Running, 2, Guid.NewGuid());
        await jobs.WriteAsync(new(second, 4, Changed.AddTicks(4)));
        await Conflict(() => jobs.WriteAsync(new(State(running.Job, RecordingConversionState.Completed, 1, running.Job.AttemptId), 2, Changed.AddTicks(5))), RepositoryConflictKind.VersionMismatch);
        var done = State(second, RecordingConversionState.Completed, 2, second.AttemptId);
        await Conflict(() => jobs.WriteAsync(new(done, 5, Changed.AddTicks(5))), RepositoryConflictKind.SnapshotMismatch);
        var artifacts = new DapperRecordingArtifactRepository(context);
        await artifacts.WriteAsync(new(Available(seed.Mp3), 1, Changed.AddTicks(5)));
        await Conflict(() => jobs.WriteAsync(new(done, 5, Changed.AddTicks(4))), RepositoryConflictKind.SnapshotMismatch);
        var completed = await jobs.WriteAsync(new(done, 5, Changed.AddTicks(6)));
        Assert.AreEqual(6L, completed.Version); Assert.AreEqual(2, completed.Job.Attempts); Assert.AreEqual(saved.CreatedAtUtc, completed.CreatedAtUtc);
        Assert.AreEqual(completed, await jobs.WriteAsync(new(done, 5, Changed.AddTicks(6))));
        await Conflict(() => jobs.WriteAsync(new(retry, 6, Changed.AddTicks(7))), RepositoryConflictKind.SnapshotMismatch);
        Assert.AreEqual(seed.Recording, await new DapperRecordingRepository(context).GetAsync(seed.Recording.Recording.Id));
        Assert.AreEqual(seed.Capture, await new DapperRecordingCaptureRepository(context).GetAsync(seed.Recording.Recording.Id));
        Assert.AreEqual(2, (await artifacts.GetForRecordingAsync(seed.Recording.Recording.Id)).Count);
        await Constraints(target, completed.Job);

        var other = await SeedAsync(context, seed.Workstation); var waiting = Job(other);
        await artifacts.WriteAsync(new(new(other.Wave.Id, other.Wave.RecordingId, other.Wave.CallId, other.Wave.Format, other.Wave.RelativePath, RecordingAvailability.Missing, 684, Digest), 2, Changed));
        await Conflict(() => jobs.WriteAsync(new(waiting, null, Changed)), RepositoryConflictKind.SnapshotMismatch);
        Assert.IsNull(await jobs.GetAsync(waiting.Id));
        await artifacts.WriteAsync(new(Available(other.Wave), 3, Changed));
        var wrongFormat = new RecordingConversionJob(waiting.Id, waiting.SourceArtifactId, waiting.TargetArtifactId, new(16000, 2, 128000), RecordingConversionState.Pending);
        await Conflict(() => jobs.WriteAsync(new(wrongFormat, null, Changed)), RepositoryConflictKind.SnapshotMismatch);
        var foreign = await SeedAsync(context);
        var crossOwner = new RecordingConversionJob(Guid.NewGuid(), other.Wave.Id, foreign.Mp3.Id, pending.Format, RecordingConversionState.Pending);
        await Conflict(() => jobs.WriteAsync(new(crossOwner, null, Changed)), RepositoryConflictKind.OwnershipMismatch);
        await Assert.ThrowsAsync<OperationCanceledException>(() => jobs.WriteAsync(new(waiting, null, Changed), new CancellationToken(true)));
        var waitingStored = await jobs.WriteAsync(new(waiting, null, Changed));
        var altered = new RecordingConversionJob(waiting.Id, waiting.SourceArtifactId, waiting.TargetArtifactId, new(8000, 2, 64000), RecordingConversionState.Running, 1, Guid.NewGuid());
        await Conflict(() => jobs.WriteAsync(new(altered, 1, Changed.AddTicks(1))), RepositoryConflictKind.OwnershipMismatch);
        await Conflict(() => jobs.WriteAsync(new(State(waiting, RecordingConversionState.Running, 2, Guid.NewGuid()), 1, Changed.AddTicks(1))), RepositoryConflictKind.SnapshotMismatch);
        await Conflict(() => jobs.WriteAsync(new(State(waiting, RecordingConversionState.Running, 1, Guid.NewGuid()), 1, Time)), RepositoryConflictKind.SnapshotMismatch);
        await jobs.WriteAsync(new(Job(foreign), null, Changed));
        var page = await jobs.GetPageAsync(seed.Workstation, limit: 1); Assert.AreEqual(1, page.Items.Length); Assert.IsTrue(page.HasMore);
        var next = await jobs.GetPageAsync(seed.Workstation, page.Items[0].Job.Id, 1); Assert.AreEqual(1, next.Items.Length); Assert.IsFalse(next.HasMore);
        CollectionAssert.AreEquivalent(new[] { pending.Id, waiting.Id }, page.Items.Concat(next.Items).Select(item => item.Job.Id).ToArray());
        Assert.AreEqual(0, (await jobs.GetPageAsync(Guid.NewGuid())).Items.Length);
        Assert.AreEqual(waitingStored, await new DapperRecordingConversionRepository(new(target, new())).GetAsync(waiting.Id));
        await using var db = new DatabaseFactory().CreateConnection(target); await db.OpenAsync();
        await db.ExecuteAsync($"UPDATE {DatabaseSql.Table(target.Type, "recordingconversions")} SET writeversion=@maximum WHERE id=@id", new { maximum = long.MaxValue, id = DatabaseId.Encode(waiting.Id) });
        await Conflict(() => jobs.WriteAsync(new(State(waiting, RecordingConversionState.Running, 1, Guid.NewGuid()), long.MaxValue, Changed.AddTicks(1))), RepositoryConflictKind.VersionExhausted);
        Assert.AreEqual(RecordingConversionState.Pending, (await jobs.GetAsync(waiting.Id))!.Job.State);
        Assert.AreEqual(nameof(RecordingConversionJob), pending.ToString());
    }

    public static async Task UpgradeAsync(DatabaseTarget target)
    {
        var factory = new DatabaseFactory(); var creation = await new DatabaseProvisioner(factory).CreateAsync(target);
        var old = new VersionElevenEngine(target.Type); await new DatabaseMigrator(factory, old).MigrateAsync(target, creation: creation);
        var oldContext = new DatabaseRepositoryContext(new FixedTarget(target), factory, new VersionElevenFactory(old), new DatabaseMigrationLock());
        var seed = await SeedAsync(oldContext); var oldArtifacts = await new DapperRecordingArtifactRepository(oldContext).GetForRecordingAsync(seed.Recording.Recording.Id);
        var migrator = new DatabaseMigrator(factory, new FluentMigratorEngine(target.Type));
        var status = await migrator.MigrateAsync(target); Assert.AreEqual(12L, status.AppliedVersions.Last());
        var context = new DatabaseRepositoryContext(target, factory);
        Assert.AreEqual(seed.Recording, await new DapperRecordingRepository(context).GetAsync(seed.Recording.Recording.Id));
        Assert.AreEqual(seed.Capture, await new DapperRecordingCaptureRepository(context).GetAsync(seed.Recording.Recording.Id));
        CollectionAssert.AreEqual(oldArtifacts.ToArray(), (await new DapperRecordingArtifactRepository(context).GetForRecordingAsync(seed.Recording.Recording.Id)).ToArray());
        Assert.AreEqual(0, (await new DapperRecordingConversionRepository(context).GetPageAsync(seed.Workstation)).Items.Length);
        CollectionAssert.AreEqual(status.AppliedVersions.ToArray(), (await migrator.MigrateAsync(target)).AppliedVersions.ToArray());
        await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DapperRecordingArtifactRepository(oldContext).GetAsync(seed.Wave.Id));
        await new DapperRecordingConversionRepository(context).WriteAsync(new(Job(seed), null, Changed));
    }

    private static async Task<Seed> SeedAsync(DatabaseRepositoryContext context, Guid? workstation = null)
    {
        var owner = workstation ?? Guid.NewGuid();
        var call = new CallSnapshot { Id = Guid.NewGuid(), State = CallState.Connected, Direction = CallDirection.Incoming, Revision = 0,
            StartedAtUtc = Time, AnsweredAtUtc = Time, StateChangedAtUtc = Time, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown) };
        await new DapperCallRepository(context).WriteAsync(new(call, null, [], [], new(owner, "CONVERSION-TEST", "TEST\\Conversion")));
        var recording = new Recording(Guid.NewGuid(), call.Id, "conversions/Quelle Ä 电话.wav", "wav", null, RecordingAvailability.Pending);
        var journal = new DapperRecordingJournal(context);
        await journal.BeginAsync(new(recording, null, Time), new(new(recording.Id, call.Id, Time, null, PcmRecordingState.Capturing, 8000, 2), null, Time));
        var capture = new RecordingCapture(recording.Id, call.Id, Time, Time.AddMilliseconds(20), PcmRecordingState.Completed, 8000, 2, 160);
        var saved = await journal.WriteAsync(new(recording with { Availability = RecordingAvailability.Available, Duration = capture.Duration }, 1, Time.AddSeconds(1)), new(capture, 1, Time.AddSeconds(1)));
        var wave = new RecordingArtifact(Guid.NewGuid(), recording.Id, call.Id, RecordingArtifactFormat.Wave, recording.RelativePath, RecordingAvailability.Pending);
        var mp3 = new RecordingArtifact(Guid.NewGuid(), recording.Id, call.Id, RecordingArtifactFormat.Mp3, "conversions/Ziel Ä 电话.mp3", RecordingAvailability.Pending);
        var artifacts = new DapperRecordingArtifactRepository(context);
        await artifacts.WriteAsync(new(wave, null, Time.AddSeconds(1))); await artifacts.WriteAsync(new(Available(wave), 1, Time.AddSeconds(1)));
        await artifacts.WriteAsync(new(mp3, null, Time.AddSeconds(1)));
        return new(saved.Recording, saved.Capture, wave, mp3, owner);
    }
    private static RecordingArtifact Available(RecordingArtifact file) => new(file.Id, file.RecordingId, file.CallId, file.Format, file.RelativePath, RecordingAvailability.Available, 684, Digest);
    private static RecordingConversionJob Job(Seed seed) => new(Guid.NewGuid(), seed.Wave.Id, seed.Mp3.Id, new(8000, 2, 128000), RecordingConversionState.Pending);
    private static RecordingConversionJob State(RecordingConversionJob item, RecordingConversionState state, int attempts, Guid? attempt = null, RecordingConversionFailure? failure = null) =>
        new(item.Id, item.SourceArtifactId, item.TargetArtifactId, item.Format, state, attempts, attempt, failure);
    private static async Task Conflict(Func<Task> action, RepositoryConflictKind kind) => Assert.AreEqual(kind, (await Assert.ThrowsExactlyAsync<RepositoryConflictException>(action)).Kind);
    private static async Task Constraints(DatabaseTarget target, RecordingConversionJob job)
    {
        await using var db = new DatabaseFactory().CreateConnection(target); await db.OpenAsync(); var table = DatabaseSql.Table(target.Type, "recordingconversions");
        foreach (var (column, value) in new (string, object?)[]
        {
            ("state", 4), ("attempts", 0), ("attempts", -1), ("attemptid", null), ("attemptid", new string('0', 32)),
            ("samplerate", 11025), ("channels", 3), ("bitrate", 128001), ("bitrate", 0), ("failure", 1),
            ("writeversion", 0L), ("createdatutc", -1L), ("updatedatutc", Time.Ticks), ("sourceartifactid", DatabaseId.Encode(Guid.NewGuid()))
        })
        {
            var quoted = target.Type switch { DatabaseType.SqlServer => "[" + column + "]", DatabaseType.MySql => "`" + column + "`", _ => "\"" + column + "\"" };
            await Assert.ThrowsAsync<DbException>(() => db.ExecuteAsync($"UPDATE {table} SET {quoted}=@value WHERE id=@id", new { value, id = DatabaseId.Encode(job.Id) }));
        }
        await Assert.ThrowsAsync<DbException>(() => db.ExecuteAsync($"INSERT INTO {table} (id,sourceartifactid,targetartifactid,samplerate,channels,bitrate,state,attempts,attemptid,failure,writeversion,createdatutc,updatedatutc) " +
            $"SELECT @duplicate,sourceartifactid,targetartifactid,samplerate,channels,bitrate,state,attempts,attemptid,failure,writeversion,createdatutc,updatedatutc FROM {table} WHERE id=@id", new { duplicate = DatabaseId.Encode(Guid.NewGuid()), id = DatabaseId.Encode(job.Id) }));
        await Assert.ThrowsAsync<DbException>(() => db.ExecuteAsync($"DELETE FROM {DatabaseSql.Table(target.Type, "recordingartifacts")} WHERE id=@id", new { id = DatabaseId.Encode(job.SourceArtifactId) }));
    }
    private sealed record Seed(StoredRecording Recording, StoredRecordingCapture Capture, RecordingArtifact Wave, RecordingArtifact Mp3, Guid Workstation);
    private sealed class FixedTarget(DatabaseTarget target) : IDatabaseTargetSource { public DatabaseTarget GetTarget() => target; }
    private sealed class VersionElevenEngine(DatabaseType type) : IDatabaseMigrationEngine
    {
        private readonly FluentMigratorEngine _inner = new(type);
        public DatabaseMigrationCatalog Catalog { get; } = new(Enumerable.Range(1, 11).Select(value => (long)value));
        public void MigrateUp(DatabaseTarget target, long version) => _inner.MigrateUp(target, version);
    }
    private sealed class VersionElevenFactory(IDatabaseMigrationEngine engine) : IDatabaseMigrationEngineFactory
    { public IDatabaseMigrationEngine GetEngine(DatabaseType type) => engine; }
}
