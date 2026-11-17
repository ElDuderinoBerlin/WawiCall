using System.Data.Common;
using Dapper;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class RecordingArtifactTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wcp-artifacts-" + Guid.NewGuid().ToString("N"));
    [TestCleanup] public void Cleanup() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    [TestMethod] public async Task CanonicalArtifactsKeepOwnershipFingerprintsVersionsAndConstraints() => await RecordingArtifactAcceptance.RunAsync(await Create());
    [TestMethod] public async Task PopulatedVersionTenUpgradePreservesRecordingsAndDoesNotInventArtifacts() => await RecordingArtifactAcceptance.UpgradeAsync(DatabaseTarget.Sqlite(Path.Combine(_root, "upgrade.db")));
    [TestMethod]
    public async Task EveryArtifactColumnHasExactlyOneGermanDescription()
    {
        var target = await Create(); await using var db = new DatabaseFactory().CreateConnection(target); await db.OpenAsync();
        var columns = (await db.QueryAsync<string>("SELECT name FROM pragma_table_info('recordingartifacts')")).ToArray();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Docs", "Datenbank-Tabellen-und-Spalten.md"))) directory = directory.Parent;
        Assert.IsNotNull(directory);
        var lines = await File.ReadAllLinesAsync(Path.Combine(directory.FullName, "Docs", "Datenbank-Tabellen-und-Spalten.md"));
        var described = lines.SkipWhile(line => !line.StartsWith("## recordingartifacts ", StringComparison.Ordinal)).Skip(1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)).Select(line => System.Text.RegularExpressions.Regex.Match(line, @"^\|\s*`([^`]+)`\s*\|"))
            .Where(match => match.Success).Select(match => match.Groups[1].Value).ToArray();
        CollectionAssert.AreEquivalent(columns, described); Assert.AreEqual(described.Length, described.Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    [DataRow("id")][DataRow("recording")][DataRow("call")][DataRow("format")][DataRow("availability")]
    [DataRow("path")][DataRow("extension")][DataRow("length")][DataRow("digest")][DataRow("uppercase")]
    [DataRow("short")][DataRow("unverified-length")][DataRow("unverified-digest")][DataRow("missing")][DataRow("utc")][DataRow("version")]
    public void InvalidArtifactMetadataIsRejectedWithoutDatabaseAccess(string invalid)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var verified = invalid is "length" or "digest" or "uppercase" or "short";
            var availability = verified ? RecordingAvailability.Available : invalid == "missing" ? RecordingAvailability.Missing : RecordingAvailability.Pending;
            if (invalid == "availability") availability = (RecordingAvailability)4;
            long? length = verified || invalid == "unverified-length" ? 1 : null;
            string? digest = verified || invalid == "unverified-digest" ? new string('a', 64) : null;
            if (invalid == "length") length = 0;
            if (invalid == "digest") digest = null;
            if (invalid == "uppercase") digest = new string('A', 64);
            if (invalid == "short") digest = new string('a', 63);
            var artifact = new RecordingArtifact(invalid == "id" ? Guid.Empty : Guid.NewGuid(), invalid == "recording" ? Guid.Empty : Guid.NewGuid(),
                invalid == "call" ? Guid.Empty : Guid.NewGuid(), invalid == "format" ? (RecordingArtifactFormat)3 : RecordingArtifactFormat.Wave,
                invalid == "path" ? "../outside.wav" : invalid == "extension" ? "file.mp3" : "file.wav", availability, length, digest);
            _ = new RecordingArtifactWrite(artifact, invalid == "version" ? 0 : null,
                invalid == "utc" ? RepositoryAcceptance.Time.ToOffset(TimeSpan.FromHours(1)) : RepositoryAcceptance.Time);
        });
    }
    private async Task<DatabaseTarget> Create()
    {
        var target = DatabaseTarget.Sqlite(Path.Combine(_root, "artifacts.db")); await new DatabaseProvisioner(new()).CreateAsync(target);
        await new DatabaseMigrator(new(), new FluentMigratorEngine(target.Type)).MigrateAsync(target); return target;
    }
}

/// <summary>Shared assertions for SQLite and disposable SQL Server, PostgreSQL, MySQL and MariaDB instances.</summary>
public static class RecordingArtifactAcceptance
{
    private static DateTimeOffset Time => RepositoryAcceptance.Time;
    private static string Digest => new('a', 64);
    public static async Task RunAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var repository = new DapperRecordingArtifactRepository(context);
        var initial = await Recording(context); var file = initial.Recording.Recording;
        var wave = Artifact(file); var mp3 = Artifact(file, RecordingArtifactFormat.Mp3);
        Assert.IsNull(await repository.GetAsync(wave.Id)); Assert.AreEqual(0, (await repository.GetForRecordingAsync(file.Id)).Count);
        var create = new RecordingArtifactWrite(wave, null, Time); var saved = await repository.WriteAsync(create);
        Assert.AreEqual(1L, saved.Version); Assert.AreEqual(saved, await repository.WriteAsync(create));
        Assert.AreEqual(saved, await new DapperRecordingArtifactRepository(new(target, new())).GetAsync(wave.Id));
        await repository.WriteAsync(new(mp3, null, Time));
        CollectionAssert.AreEqual(new[] { RecordingArtifactFormat.Wave, RecordingArtifactFormat.Mp3 }, (await repository.GetForRecordingAsync(file.Id)).Select(item => item.Artifact.Format).ToArray());
        await Conflict(() => repository.WriteAsync(new(Artifact(file), null, Time)), RepositoryConflictKind.AlreadyExists);
        var missing = new RecordingArtifact(Guid.NewGuid(), Guid.NewGuid(), file.CallId, RecordingArtifactFormat.Wave, "missing.wav", RecordingAvailability.Pending);
        await Conflict(() => repository.WriteAsync(new(missing, null, Time)), RepositoryConflictKind.MissingRecord);
        var wrongCall = new RecordingArtifact(wave.Id, file.Id, Guid.NewGuid(), wave.Format, wave.RelativePath, wave.Availability);
        await Conflict(() => repository.WriteAsync(new(wrongCall, 1, Time)), RepositoryConflictKind.OwnershipMismatch);
        var available = State(wave, RecordingAvailability.Available);
        await Conflict(() => repository.WriteAsync(new(State(wave, RecordingAvailability.Missing), 1, Time.AddSeconds(1))), RepositoryConflictKind.SnapshotMismatch);
        var completed = await repository.WriteAsync(new(available, 1, Time.AddSeconds(1)));
        Assert.AreEqual(completed, await repository.WriteAsync(new(available, 1, Time.AddSeconds(1))));
        await Conflict(() => repository.WriteAsync(new(State(wave, RecordingAvailability.Missing), 1, Time.AddSeconds(2))), RepositoryConflictKind.VersionMismatch);
        await Conflict(() => repository.WriteAsync(new(State(wave, RecordingAvailability.Missing, digest: new string('b', 64)), 2, Time.AddSeconds(2))), RepositoryConflictKind.SnapshotMismatch);
        await Conflict(() => repository.WriteAsync(new(State(wave, RecordingAvailability.Missing, length: 129), 2, Time.AddSeconds(2))), RepositoryConflictKind.SnapshotMismatch);
        await Conflict(() => repository.WriteAsync(new(wave, 2, Time.AddSeconds(2))), RepositoryConflictKind.SnapshotMismatch);
        await Conflict(() => repository.WriteAsync(new(State(wave, RecordingAvailability.Missing), 2, Time)), RepositoryConflictKind.SnapshotMismatch);
        var moved = new RecordingArtifact(wave.Id, file.Id, file.CallId, wave.Format, "other.wav", RecordingAvailability.Available, 128, Digest);
        await Conflict(() => repository.WriteAsync(new(moved, 2, Time.AddSeconds(2))), RepositoryConflictKind.OwnershipMismatch);
        var other = await Recording(context);
        var reassigned = new RecordingArtifact(wave.Id, other.Recording.Recording.Id, other.Recording.Recording.CallId, wave.Format, wave.RelativePath, RecordingAvailability.Available, 128, Digest);
        await Conflict(() => repository.WriteAsync(new(reassigned, 2, Time.AddSeconds(2))), RepositoryConflictKind.OwnershipMismatch);
        var absent = await repository.WriteAsync(new(State(wave, RecordingAvailability.Missing), 2, Time.AddSeconds(2)));
        Assert.AreEqual(3L, absent.Version); Assert.AreEqual(completed.CreatedAtUtc, absent.CreatedAtUtc);
        var restored = await repository.WriteAsync(new(available, 3, Time.AddSeconds(3))); Assert.AreEqual(4L, restored.Version);
        await repository.WriteAsync(new(State(mp3, RecordingAvailability.Failed), 1, Time.AddSeconds(1)));
        await Conflict(() => repository.WriteAsync(new(State(mp3, RecordingAvailability.Available), 2, Time.AddSeconds(2))), RepositoryConflictKind.SnapshotMismatch);
        await repository.WriteAsync(new(mp3, 2, Time.AddSeconds(2)));
        await repository.WriteAsync(new(State(mp3, RecordingAvailability.Available), 3, Time.AddSeconds(3)));
        Assert.AreEqual(initial.Recording, await new DapperRecordingRepository(context).GetAsync(file.Id));
        Assert.AreEqual(initial.Capture, await new DapperRecordingCaptureRepository(context).GetAsync(file.Id));
        Assert.AreEqual(0, (await repository.GetForRecordingAsync(Guid.NewGuid())).Count);
        await Conflict(() => repository.WriteAsync(new(State(Artifact(other.Recording.Recording), RecordingAvailability.Available), null, Time)), RepositoryConflictKind.SnapshotMismatch);
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.WriteAsync(new(Artifact(other.Recording.Recording), null, Time), new CancellationToken(true)));
        Assert.AreEqual(0, (await repository.GetForRecordingAsync(other.Recording.Recording.Id)).Count);
        await Constraints(target, restored.Artifact);
        var racing = Artifact(other.Recording.Recording); await repository.WriteAsync(new(racing, null, Time));
        await repository.WriteAsync(new(State(racing, RecordingAvailability.Available), 1, Time.AddSeconds(1)));
        async Task<bool> Finish(int seconds)
        {
            try { await new DapperRecordingArtifactRepository(new(target, new())).WriteAsync(new(State(racing, RecordingAvailability.Missing), 2, Time.AddSeconds(seconds))); return true; }
            catch (RepositoryConflictException error) when (error.Kind == RepositoryConflictKind.VersionMismatch) { return false; }
        }
        Assert.AreEqual(1, (await Task.WhenAll(Finish(4), Finish(5))).Count(success => success));
        await using var db = new DatabaseFactory().CreateConnection(target); await db.OpenAsync();
        await db.ExecuteAsync($"UPDATE {DatabaseSql.Table(target.Type, "recordingartifacts")} SET writeversion=@maximum WHERE id=@id", new { maximum = long.MaxValue, id = DatabaseId.Encode(wave.Id) });
        await Conflict(() => repository.WriteAsync(new(State(wave, RecordingAvailability.Missing), long.MaxValue, Time.AddSeconds(9))), RepositoryConflictKind.VersionExhausted);
        Assert.AreEqual(long.MaxValue, (await repository.GetAsync(wave.Id))!.Version);
        Assert.AreEqual(RecordingAvailability.Available, (await repository.GetAsync(wave.Id))!.Artifact.Availability);
        Assert.AreEqual(nameof(RecordingArtifact), wave.ToString());
    }

    public static async Task UpgradeAsync(DatabaseTarget target)
    {
        var factory = new DatabaseFactory(); var created = await new DatabaseProvisioner(factory).CreateAsync(target);
        var old = new VersionTenEngine(target.Type); await new DatabaseMigrator(factory, old).MigrateAsync(target, creation: created);
        var context = new DatabaseRepositoryContext(new FixedTarget(target), factory, new VersionTenFactory(old), new DatabaseMigrationLock());
        var initial = await Recording(context);
        var legacy = new WawiCallPro.Interfaces.History.Recording(Guid.NewGuid(), initial.Recording.Recording.CallId, "legacy.mp3", "mp3", TimeSpan.FromSeconds(3), RecordingAvailability.Available);
        var oldLegacy = await new DapperRecordingRepository(context).WriteAsync(new(legacy, null, Time));
        var current = new FluentMigratorEngine(target.Type);
        var status = await new DatabaseMigrator(factory, current).MigrateAsync(target); Assert.AreEqual(12L, status.AppliedVersions.Last());
        var upgraded = new DatabaseRepositoryContext(target, factory);
        Assert.AreEqual(initial.Recording, await new DapperRecordingRepository(upgraded).GetAsync(initial.Recording.Recording.Id));
        Assert.AreEqual(initial.Capture, await new DapperRecordingCaptureRepository(upgraded).GetAsync(initial.Recording.Recording.Id));
        Assert.AreEqual(oldLegacy, await new DapperRecordingRepository(upgraded).GetAsync(legacy.Id));
        var artifacts = new DapperRecordingArtifactRepository(upgraded);
        Assert.AreEqual(0, (await artifacts.GetForRecordingAsync(legacy.Id)).Count);
        Assert.AreEqual(0, (await artifacts.GetForRecordingAsync(initial.Recording.Recording.Id)).Count);
        CollectionAssert.AreEqual(status.AppliedVersions.ToArray(), (await new DatabaseMigrator(factory, current).MigrateAsync(target)).AppliedVersions.ToArray());
        await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => new DapperRecordingRepository(context).GetAsync(legacy.Id));
    }

    private static async Task Constraints(DatabaseTarget target, RecordingArtifact item)
    {
        await using var db = new DatabaseFactory().CreateConnection(target); await db.OpenAsync(); var table = DatabaseSql.Table(target.Type, "recordingartifacts");
        foreach (var (column, value) in new (string, object?)[]
        {
            ("format", 3), ("availability", 5), ("bytelength", 0L), ("bytelength", -1L), ("bytelength", null),
            ("sha256", null), ("sha256", new string('A', 64)), ("sha256", new string('a', 63)), ("writeversion", 0L),
            ("createdatutc", -1L), ("updatedatutc", Time.AddTicks(-1).Ticks), ("relativepath", "../outside.wav"),
            ("relativepath", "C:/outside.wav"), ("relativepath", "nested\\outside.wav"), ("relativepath", "wrong.mp3"),
            ("relativepath", new string('x', 1025) + ".wav"), ("recordingid", DatabaseId.Encode(Guid.NewGuid()))
        })
            await Assert.ThrowsAsync<DbException>(() => db.ExecuteAsync($"UPDATE {table} SET {Quote(target.Type, column)}=@value WHERE id=@id", new { value, id = DatabaseId.Encode(item.Id) }));
        await Assert.ThrowsAsync<DbException>(() => db.ExecuteAsync($"INSERT INTO {table} (id,recordingid,format,relativepath,availability,bytelength,sha256,writeversion,createdatutc,updatedatutc) " +
            $"SELECT @duplicate,recordingid,format,relativepath,availability,bytelength,sha256,writeversion,createdatutc,updatedatutc FROM {table} WHERE id=@id", new { duplicate = DatabaseId.Encode(Guid.NewGuid()), id = DatabaseId.Encode(item.Id) }));
        await Assert.ThrowsAsync<DbException>(() => db.ExecuteAsync($"DELETE FROM {DatabaseSql.Table(target.Type, "recordings")} WHERE id=@id", new { id = DatabaseId.Encode(item.RecordingId) }));
    }
    private static Task<StoredRecordingJournal> Recording(DatabaseRepositoryContext context) => Begin(context);
    private static async Task<StoredRecordingJournal> Begin(DatabaseRepositoryContext context)
    {
        var call = new CallSnapshot { Id = Guid.NewGuid(), State = CallState.Connected, Direction = CallDirection.Incoming, Revision = 0,
            StartedAtUtc = Time, AnsweredAtUtc = Time, StateChangedAtUtc = Time, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown) };
        await new DapperCallRepository(context).WriteAsync(new(call, null, [], [], new(Guid.NewGuid(), "ARTIFACT-TEST", "TEST\\Artifact")));
        var recording = new WawiCallPro.Interfaces.History.Recording(Guid.NewGuid(), call.Id, "source.wav", "wav", null, RecordingAvailability.Pending);
        return await new DapperRecordingJournal(context).BeginAsync(new(recording, null, Time), new(new(recording.Id, call.Id, Time, null, PcmRecordingState.Capturing, 8000, 2), null, Time));
    }
    private static RecordingArtifact Artifact(WawiCallPro.Interfaces.History.Recording file, RecordingArtifactFormat format = RecordingArtifactFormat.Wave) =>
        new(Guid.NewGuid(), file.Id, file.CallId, format, "Aufnahme Ä 电话 ' 001." + (format == RecordingArtifactFormat.Wave ? "wav" : "mp3"), RecordingAvailability.Pending);
    private static RecordingArtifact State(RecordingArtifact item, RecordingAvailability availability, long length = 128, string? digest = null) =>
        new(item.Id, item.RecordingId, item.CallId, item.Format, item.RelativePath, availability,
            availability is RecordingAvailability.Available or RecordingAvailability.Missing ? length : null,
            availability is RecordingAvailability.Available or RecordingAvailability.Missing ? digest ?? Digest : null);
    private static string Quote(DatabaseType type, string column) => type switch { DatabaseType.SqlServer => "[" + column + "]", DatabaseType.MySql => "`" + column + "`", _ => "\"" + column + "\"" };
    private static async Task Conflict(Func<Task> action, RepositoryConflictKind kind) => Assert.AreEqual(kind, (await Assert.ThrowsExactlyAsync<RepositoryConflictException>(action)).Kind);
    private sealed class FixedTarget(DatabaseTarget target) : IDatabaseTargetSource { public DatabaseTarget GetTarget() => target; }
    private sealed class VersionTenEngine(DatabaseType type) : IDatabaseMigrationEngine
    {
        private readonly FluentMigratorEngine _inner = new(type);
        public DatabaseMigrationCatalog Catalog { get; } = new(Enumerable.Range(1, 10).Select(value => (long)value));
        public void MigrateUp(DatabaseTarget target, long version) => _inner.MigrateUp(target, version);
    }
    private sealed class VersionTenFactory(IDatabaseMigrationEngine engine) : IDatabaseMigrationEngineFactory
    { public IDatabaseMigrationEngine GetEngine(DatabaseType type) => engine; }
}
