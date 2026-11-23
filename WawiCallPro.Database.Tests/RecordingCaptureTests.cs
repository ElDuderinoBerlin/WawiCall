using Dapper;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class RecordingCaptureTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wcp-capture-" + Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    [TestMethod] public async Task LifecycleConflictsRecoveryAndConstraintsSurviveRepositoryRecreation() => await RecordingCaptureAcceptance.RunAsync(await CreateAsync());
    [TestMethod] public async Task UpgradeFromNinePreservesLegacyRecordingsWithoutInventingCaptureTimes() => await RecordingCaptureAcceptance.UpgradeAsync(DatabaseTarget.Sqlite(Path.Combine(_root, "upgrade.db")));

    [TestMethod]
    [DataRow("end-before-start")]
    [DataRow("end-before-completion")]
    [DataRow("completed-no-frames")]
    [DataRow("completed-no-end")]
    [DataRow("active-frames")]
    [DataRow("failed-no-code")]
    [DataRow("rate")]
    [DataRow("channels")]
    [DataRow("size")]
    [DataRow("duration")]
    [DataRow("state")]
    [DataRow("failure")]
    [DataRow("utc")]
    public void InvalidCaptureMetadataIsRejectedBeforeDatabaseAccess(string invalid)
    {
        var time = RepositoryAcceptance.Time; var id = Guid.NewGuid(); var call = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() =>
        {
            var item = invalid switch
            {
                "end-before-start" => new RecordingCapture(id, call, time, time.AddTicks(-1), PcmRecordingState.Completed, 8000, 2, 160),
                "completed-no-frames" => new(id, call, time, time, PcmRecordingState.Completed, 8000, 2),
                "completed-no-end" => new(id, call, time, null, PcmRecordingState.Completed, 8000, 2, 160),
                "active-frames" => new(id, call, time, null, PcmRecordingState.Capturing, 8000, 2, 160),
                "failed-no-code" => new(id, call, time, null, PcmRecordingState.Failed, 8000, 2),
                "rate" => new(id, call, time, null, PcmRecordingState.Capturing, 192000, 2),
                "channels" => new(id, call, time, null, PcmRecordingState.Capturing, 8000, 3),
                "size" => new(id, call, time, time, PcmRecordingState.Completed, 48000, 2, 600000000),
                "duration" => new(id, call, time, time, PcmRecordingState.Completed, 8000, 1, 8000L * 28800 + 1),
                "state" => new(id, call, time, null, (PcmRecordingState)5, 8000, 2),
                "failure" => new(id, call, time, null, PcmRecordingState.Failed, 8000, 2, failure: (RecordingCaptureFailure)10),
                "utc" => new(id, call, time.ToOffset(TimeSpan.FromHours(1)), null, PcmRecordingState.Capturing, 8000, 2),
                _ => new(id, call, time, time.AddSeconds(1), PcmRecordingState.Completed, 8000, 2, 160)
            };
            _ = new RecordingCaptureWrite(item, null, time);
        });
    }
    private async Task<DatabaseTarget> CreateAsync()
    {
        var target = DatabaseTarget.Sqlite(Path.Combine(_root, "capture.db")); await new DatabaseProvisioner(new()).CreateAsync(target);
        await new DatabaseMigrator(new(), new FluentMigratorEngine(target.Type)).MigrateAsync(target); return target;
    }
}

/// <summary>Shared SQLite and disposable native-engine assertions; no audio files or customer data.</summary>
public static class RecordingCaptureAcceptance
{
    private static DateTimeOffset Time => RepositoryAcceptance.Time;
    public static async Task RunAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var captures = new DapperRecordingCaptureRepository(context);
        var call = await CallAsync(context); var recording = await RecordingAsync(context, call.History.Call.Id);
        await Conflict(() => captures.WriteAsync(new(new(Guid.NewGuid(), call.History.Call.Id, Time, null, PcmRecordingState.Capturing, 44100, 2), null, Time)), RepositoryConflictKind.MissingRecord);
        Assert.IsNull(await captures.GetAsync(recording.Id));
        var active = Capture(recording, PcmRecordingState.Capturing); var started = new RecordingCaptureWrite(active, null, Time);
        var saved = await captures.WriteAsync(started); Assert.AreEqual(1L, saved.Version); Assert.AreEqual(saved, await captures.WriteAsync(started));
        var stopping = new RecordingCaptureWrite(Capture(recording, PcmRecordingState.Stopping), 1, Time.AddTicks(1));
        await captures.WriteAsync(stopping);
        await Conflict(() => captures.WriteAsync(new(active, 2, Time.AddTicks(2))), RepositoryConflictKind.SnapshotMismatch);
        await Conflict(() => captures.WriteAsync(new(new(recording.Id, recording.CallId, Time.AddTicks(1), null, PcmRecordingState.Stopping, 44100, 2), 2, Time.AddTicks(2))), RepositoryConflictKind.SnapshotMismatch);
        var done = Capture(recording, PcmRecordingState.Completed); var completed = new RecordingCaptureWrite(done, 2, Time.AddSeconds(6));
        saved = await captures.WriteAsync(completed); Assert.AreEqual(3L, saved.Version); Assert.AreEqual(saved, await captures.WriteAsync(completed));
        var reloaded = await new DapperRecordingCaptureRepository(new(target, new())).GetAsync(recording.Id);
        Assert.AreEqual(saved, reloaded); Assert.AreEqual(TimeSpan.FromMilliseconds(100), reloaded!.Capture.Duration);
        Assert.AreEqual(recording, (await new DapperRecordingRepository(context).GetAsync(recording.Id))!.Recording);
        await Conflict(() => captures.WriteAsync(new(active, 3, Time.AddSeconds(7))), RepositoryConflictKind.SnapshotMismatch);
        await Conflict(() => captures.WriteAsync(new(active, 1, Time.AddSeconds(7))), RepositoryConflictKind.VersionMismatch);
        await Conflict(() => captures.WriteAsync(new(new(recording.Id, Guid.NewGuid(), Time, null, PcmRecordingState.Capturing, 44100, 2), 3, Time)), RepositoryConflictKind.OwnershipMismatch);

        await CheckConstraintsAsync(target, recording.Id);
        var live = await RecordingAsync(context, call.History.Call.Id); await captures.WriteAsync(new(Capture(live, PcmRecordingState.Capturing), null, Time));
        await Conflict(() => captures.WriteAsync(new(new(live.Id, live.CallId, Time, null, PcmRecordingState.Capturing, 16000, 2), 1, Time)), RepositoryConflictKind.SnapshotMismatch);
        await Conflict(() => captures.WriteAsync(new(new(live.Id, live.CallId, Time, null, PcmRecordingState.Capturing, 44100, 1), 1, Time)), RepositoryConflictKind.SnapshotMismatch);
        await Conflict(() => captures.RecoverEndedCallAsync(call.History.Call.Id, Time.AddSeconds(10)), RepositoryConflictKind.SnapshotMismatch);
        var otherCall = await CallAsync(context); var other = await RecordingAsync(context, otherCall.History.Call.Id);
        await captures.WriteAsync(new(Capture(other, PcmRecordingState.Capturing), null, Time));
        await EndAsync(context, call);
        Assert.AreEqual(1, await captures.RecoverEndedCallAsync(call.History.Call.Id, Time.AddSeconds(20)));
        Assert.AreEqual(0, await captures.RecoverEndedCallAsync(call.History.Call.Id, Time.AddSeconds(21)));
        var recovered = (await captures.GetAsync(live.Id))!.Capture;
        Assert.AreEqual(PcmRecordingState.Aborted, recovered.State); Assert.AreEqual(RecordingCaptureFailure.Interrupted, recovered.Failure);
        Assert.IsNull(recovered.EndedAtUtc); Assert.IsNull(recovered.Duration); Assert.AreEqual(saved, await captures.GetAsync(recording.Id));
        Assert.AreEqual(PcmRecordingState.Capturing, (await captures.GetAsync(other.Id))!.Capture.State);

        var concurrent = await RecordingAsync(context, otherCall.History.Call.Id); await captures.WriteAsync(new(Capture(concurrent, PcmRecordingState.Capturing), null, Time));
        async Task<bool> Finish(long frames)
        {
            try { await new DapperRecordingCaptureRepository(new(target, new())).WriteAsync(new(new(concurrent.Id, concurrent.CallId, Time, Time.AddSeconds(5), PcmRecordingState.Completed, 44100, 2, frames), 1, Time.AddSeconds(6))); return true; }
            catch (RepositoryConflictException error) when (error.Kind == RepositoryConflictKind.VersionMismatch) { return false; }
        }
        Assert.AreEqual(1, (await Task.WhenAll(Finish(4410), Finish(8820))).Count(won => won));
        var canceled = await RecordingAsync(context, otherCall.History.Call.Id);
        await Assert.ThrowsAsync<OperationCanceledException>(() => captures.WriteAsync(new(Capture(canceled, PcmRecordingState.Capturing), null, Time), new CancellationToken(true)));
        Assert.IsNull(await captures.GetAsync(canceled.Id));
        foreach (var state in new[] { PcmRecordingState.Failed, PcmRecordingState.Aborted })
        {
            var failed = await RecordingAsync(context, otherCall.History.Call.Id);
            await captures.WriteAsync(new(Capture(failed, PcmRecordingState.Capturing), null, Time));
            var failure = state == PcmRecordingState.Failed ? RecordingCaptureFailure.FileFailure : RecordingCaptureFailure.Cancelled;
            var terminal = new RecordingCapture(failed.Id, failed.CallId, Time, null, state, 44100, 2, failure: failure);
            await captures.WriteAsync(new(terminal, 1, Time.AddSeconds(3)));
            Assert.AreEqual(terminal, (await new DapperRecordingCaptureRepository(new(target, new())).GetAsync(failed.Id))!.Capture);
            await Conflict(() => captures.WriteAsync(new(Capture(failed, PcmRecordingState.Capturing), 2, Time.AddSeconds(4))), RepositoryConflictKind.SnapshotMismatch);
            Assert.AreEqual(nameof(RecordingCapture), terminal.ToString());
        }
        await RecoveryRollbackAsync(context, captures);
    }

    public static async Task UpgradeAsync(DatabaseTarget target)
    {
        var factory = new DatabaseFactory(); var creation = await new DatabaseProvisioner(factory).CreateAsync(target);
        var old = new VersionNineEngine(target.Type); await new DatabaseMigrator(factory, old).MigrateAsync(target, creation: creation);
        var context = new DatabaseRepositoryContext(new Target(target), factory, new VersionNineFactory(old), new DatabaseMigrationLock());
        var call = await CallAsync(context); var recording = await RecordingAsync(context, call.History.Call.Id);
        var status = await new DatabaseMigrator(factory, new FluentMigratorEngine(target.Type)).MigrateAsync(target);
        Assert.AreEqual(15L, status.AppliedVersions.Last());
        Assert.AreEqual(recording, (await new DapperRecordingRepository(new(target, factory)).GetAsync(recording.Id))!.Recording);
        Assert.IsNull(await new DapperRecordingCaptureRepository(new(target, factory)).GetAsync(recording.Id));
        var repeated = await new DatabaseMigrator(factory, new FluentMigratorEngine(target.Type)).MigrateAsync(target);
        CollectionAssert.AreEqual(status.AppliedVersions.ToArray(), repeated.AppliedVersions.ToArray());
    }

    public static DatabaseTarget UpgradeTarget(DatabaseTarget source)
    {
        var name = "capture_upgrade_" + Guid.NewGuid().ToString("N");
        return source.Type switch
        {
            DatabaseType.SqlServer => DatabaseTarget.Server(source.Type, new SqlConnectionStringBuilder(source.ConnectionString) { InitialCatalog = name, Pooling = false }.ConnectionString),
            DatabaseType.MySql => DatabaseTarget.Server(source.Type, new MySqlConnectionStringBuilder(source.ConnectionString) { Database = name, Pooling = false }.ConnectionString),
            DatabaseType.PostgreSql => DatabaseTarget.Server(source.Type, new NpgsqlConnectionStringBuilder(source.ConnectionString) { Database = name, Pooling = false }.ConnectionString),
            _ => DatabaseTarget.Sqlite(Path.Combine(Path.GetDirectoryName(source.SqlitePath!)!, name + ".db"))
        };
    }

    private static async Task RecoveryRollbackAsync(DatabaseRepositoryContext context, DapperRecordingCaptureRepository repository)
    {
        var call = await CallAsync(context); var first = await RecordingAsync(context, call.History.Call.Id, new Guid("00000000-0000-0000-0000-000000000001"));
        var second = await RecordingAsync(context, call.History.Call.Id, new Guid("00000000-0000-0000-0000-000000000002"));
        await repository.WriteAsync(new(Capture(first, PcmRecordingState.Capturing), null, Time));
        await Conflict(() => repository.WriteAsync(new(Capture(second, PcmRecordingState.Stopping), null, Time)), RepositoryConflictKind.SnapshotMismatch);
        await repository.WriteAsync(new(Capture(second, PcmRecordingState.Capturing), null, Time));
        await repository.WriteAsync(new(Capture(second, PcmRecordingState.Stopping), 1, Time.AddSeconds(40)));
        await EndAsync(context, call);
        await Conflict(() => repository.RecoverEndedCallAsync(call.History.Call.Id, Time.AddSeconds(30)), RepositoryConflictKind.SnapshotMismatch);
        Assert.AreEqual(1L, (await repository.GetAsync(first.Id))!.Version);
        Assert.AreEqual(PcmRecordingState.Capturing, (await repository.GetAsync(first.Id))!.Capture.State);
        Assert.AreEqual(2, await repository.RecoverEndedCallAsync(call.History.Call.Id, Time.AddSeconds(50)));
    }
    private static async Task CheckConstraintsAsync(DatabaseTarget target, Guid id)
    {
        await using var db = new DatabaseFactory().CreateConnection(target); await db.OpenAsync();
        foreach (var (column, value) in new (string, long)[] { ("state", 5), ("channels", 3), ("samplerate", 12000), ("framecount", -1), ("framecount", 600000000), ("failure", 10), ("startedatutc", long.MaxValue), ("endedatutc", 0), ("writeversion", 0) })
            await Assert.ThrowsAsync<System.Data.Common.DbException>(() => db.ExecuteAsync($"UPDATE {DatabaseSql.Table(target.Type, "recordingcaptures")} SET {column}=@value WHERE id=@id", new { value, id = DatabaseId.Encode(id) }));
    }
    private static RecordingCapture Capture(Recording recording, PcmRecordingState state) => new(recording.Id, recording.CallId, Time,
        state == PcmRecordingState.Completed ? Time.AddSeconds(5) : null, state, 44100, 2, state == PcmRecordingState.Completed ? 4410 : null);
    private static async Task<StoredCall> CallAsync(DatabaseRepositoryContext context)
    {
        var call = new CallSnapshot { Id = Guid.NewGuid(), Revision = 0, State = CallState.Connected, Direction = CallDirection.Incoming,
            LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown), StartedAtUtc = Time, AnsweredAtUtc = Time, StateChangedAtUtc = Time };
        return await new DapperCallRepository(context).WriteAsync(new(call, null, [], [], new CallOrigin(Guid.NewGuid(), "CAPTURE-TEST", "TEST\\Capture")));
    }
    private static async Task EndAsync(DatabaseRepositoryContext context, StoredCall call)
    {
        var snapshot = call.History.Call with { Revision = 1, State = CallState.Ended, StateChangedAtUtc = Time.AddSeconds(10), EndedAtUtc = Time.AddSeconds(10), EndReason = CallEndReason.Completed };
        await new DapperCallRepository(context).WriteAsync(new(snapshot, call.Version, [], [new(Guid.NewGuid(), snapshot.Id, 1, snapshot.StateChangedAtUtc, "call.ended", CallState.Ended)], snapshot.Origin!));
    }
    private static async Task<Recording> RecordingAsync(DatabaseRepositoryContext context, Guid callId, Guid? id = null)
    {
        var recording = new Recording(id ?? Guid.NewGuid(), callId, "fixture/missing.wav", "wav", null, RecordingAvailability.Pending);
        await new DapperRecordingRepository(context).WriteAsync(new(recording, null, Time)); return recording;
    }
    private static async Task Conflict(Func<Task> action, RepositoryConflictKind expected)
    { var error = await Assert.ThrowsExactlyAsync<RepositoryConflictException>(action); Assert.AreEqual(expected, error.Kind); }
    private sealed class Target(DatabaseTarget target) : IDatabaseTargetSource { public DatabaseTarget GetTarget() => target; }
    private sealed class VersionNineEngine(DatabaseType type) : IDatabaseMigrationEngine
    {
        private readonly FluentMigratorEngine _inner = new(type);
        public DatabaseMigrationCatalog Catalog { get; } = new(Enumerable.Range(1, 9).Select(value => (long)value));
        public void MigrateUp(DatabaseTarget target, long version) => _inner.MigrateUp(target, version);
    }
    private sealed class VersionNineFactory(IDatabaseMigrationEngine engine) : IDatabaseMigrationEngineFactory
    { public IDatabaseMigrationEngine GetEngine(DatabaseType type) => engine; }
}
