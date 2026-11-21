using System.Security.Cryptography;
using System.Text;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingWaveArtifactTests
{
    [TestMethod]
    public async Task ConfirmedAndUnconfirmedArtifactsAreRetriedWithoutAnotherFileOrVersion()
    {
        using var database = await WaveArtifactFixture.Create(); await RecordingWaveArtifactAcceptance.RunAsync(database.Target);
    }

    [TestMethod]
    [DataRow("header")][DataRow("rate")][DataRow("frames")][DataRow("changed-wave")][DataRow("missing-wave")]
    [DataRow("catalog-owner")][DataRow("catalog-path")][DataRow("catalog-hash")][DataRow("catalog-failed")]
    public async Task InvalidFilesOrConflictingCatalogEntriesRetainTheProtectedDelivery(string problem)
    {
        using var fixture = await WaveArtifactFixture.Create(); var repository = fixture.Artifacts;
        StoredRecordingArtifact? before = null;
        if (problem.StartsWith("catalog-", StringComparison.Ordinal))
        {
            var item = fixture.PendingArtifact(problem == "catalog-owner" ? Guid.NewGuid() : fixture.Id, problem == "catalog-path" ? "different.wav" : "captured.wav");
            before = await repository.WriteAsync(new(item, null, fixture.Changed));
            if (problem is "catalog-hash" or "catalog-failed")
            {
                var complete = problem == "catalog-hash";
                var digest = fixture.Digest; digest = (digest[0] == 'a' ? "b" : "a") + digest[1..];
                var conflicting = new RecordingArtifact(item.Id, item.RecordingId, item.CallId, item.Format, item.RelativePath,
                    complete ? RecordingAvailability.Available : RecordingAvailability.Failed, complete ? new FileInfo(fixture.Wave).Length : null, complete ? digest : null);
                before = await repository.WriteAsync(new(conflicting, 1, fixture.Changed));
            }
        }
        if (problem is "changed-wave" or "missing-wave")
        {
            await using (await fixture.Outbox.StageAsync(fixture.CompletedRecording, fixture.CompletedCapture)) { }
            if (problem == "missing-wave") File.Delete(fixture.Wave);
            else { var bytes = File.ReadAllBytes(fixture.Wave); bytes[^1] ^= 1; File.WriteAllBytes(fixture.Wave, bytes); }
        }
        if (problem == "header") File.WriteAllBytes(fixture.Wave, new byte[684]);
        if (problem == "rate") fixture.WriteWave(16000);
        if (problem == "frames") fixture.WriteWave(8000, 320);
        if (problem == "header") await Assert.ThrowsExactlyAsync<Mp3ConversionException>(() => fixture.Journal().WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture));
        else if (problem.StartsWith("catalog-", StringComparison.Ordinal)) await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => fixture.Journal().WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture));
        else await Assert.ThrowsExactlyAsync<RecordingOutboxException>(() => fixture.Journal().WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture));
        Assert.AreEqual(1, fixture.Outbox.GetPending().Count);
        var items = await repository.GetForRecordingAsync(fixture.Id);
        if (before is null) Assert.AreEqual(0, items.Count); else Assert.AreEqual(before, items.Single());
        var parent = await fixture.Parent.GetAsync(fixture.Id); var capture = await fixture.Captures.GetAsync(fixture.Id);
        Assert.AreEqual(problem.StartsWith("catalog-", StringComparison.Ordinal) ? 2L : 1L, parent!.Version);
        Assert.AreEqual(parent.Version, capture!.Version);
    }

    [TestMethod]
    [DataRow(PcmRecordingState.Failed)]
    [DataRow(PcmRecordingState.Aborted)]
    public async Task FailedAndCanceledCapturesPublishNoArtifactAndPreCancellationWritesNothing(PcmRecordingState state)
    {
        using var fixture = await WaveArtifactFixture.Create();
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Journal().WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture, new CancellationToken(true)));
        Assert.AreEqual(0, fixture.Outbox.GetPending().Count); Assert.AreEqual(0, (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Count);
        File.Delete(fixture.Wave);
        var old = fixture.CompletedCapture.Capture;
        var aborted = new RecordingCapture(old.RecordingId, old.CallId, old.StartedAtUtc, null, state, 8000, 2,
            failure: state == PcmRecordingState.Aborted ? RecordingCaptureFailure.Cancelled : RecordingCaptureFailure.FileFailure);
        await fixture.Journal().WriteAsync(new(fixture.CompletedRecording.Recording with { Availability = RecordingAvailability.Failed, Duration = null }, 1, fixture.Changed), new(aborted, 1, fixture.Changed));
        Assert.AreEqual(0, (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Count); Assert.AreEqual(0, fixture.Outbox.GetPending().Count);
    }

    [TestMethod]
    public async Task AnIdenticalMissingArtifactIsRestoredWithoutChangingItsFingerprintOrMp3()
    {
        using var fixture = await WaveArtifactFixture.Create(); await fixture.Journal().WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture);
        var existing = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single(); var old = existing.Artifact;
        var missing = new RecordingArtifact(old.Id, old.RecordingId, old.CallId, old.Format, old.RelativePath, RecordingAvailability.Missing, old.ByteLength, old.Sha256);
        await fixture.Artifacts.WriteAsync(new(missing, existing.Version, fixture.Changed.AddMinutes(1)));
        await fixture.Journal().WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture);
        var restored = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single();
        Assert.AreEqual(old, restored.Artifact); Assert.AreEqual(4L, restored.Version); Assert.AreEqual(fixture.Changed.AddMinutes(1), restored.UpdatedAtUtc);
        await fixture.Journal().WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture); Assert.AreEqual(restored, (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single());
    }

    [TestMethod]
    public async Task TheArtifactHashMustMatchTheProtectedReceiptEvenWithAnOpenReadStream()
    {
        using var fixture = await WaveArtifactFixture.Create();
        await using var delivery = await fixture.Outbox.StageAsync(fixture.CompletedRecording, fixture.CompletedCapture);
        await Assert.ThrowsExactlyAsync<RecordingOutboxException>(() => new RecordingWaveArtifactPublisher(fixture.Artifacts).PublishAsync(new WrongDigest(delivery)));
        Assert.AreEqual(0, (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Count); Assert.AreEqual(1, fixture.Outbox.GetPending().Count);
    }
    private sealed class WrongDigest(IRecordingJournalDelivery inner) : IRecordingJournalDelivery
    {
        public RecordingWrite Recording => inner.Recording; public RecordingCaptureWrite Capture => inner.Capture; public Stream? Wave => inner.Wave;
        public string? WaveSha256 { get; } = new string('0', 64); public void Acknowledge() => throw new InvalidOperationException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>Invoked for SQLite and each isolated native engine. WAVs contain only synthetic zero samples.</summary>
public static class RecordingWaveArtifactAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        foreach (var fault in new[] { "none", "before", "after" })
        {
            using var fixture = await WaveArtifactFixture.Create(target);
            var mp3 = new RecordingArtifact(Guid.NewGuid(), fixture.Id, fixture.CompletedRecording.Recording.CallId, RecordingArtifactFormat.Mp3, "pending.mp3", RecordingAvailability.Pending);
            var other = await fixture.Artifacts.WriteAsync(new(mp3, null, fixture.Changed));
            var failing = new FailArtifactConfirmation(fixture.Artifacts, fault, fixture.Wave);
            var journal = fixture.Journal(failing);
            if (fault == "none") await journal.WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture);
            else
            {
                await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => journal.WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture));
                Assert.AreEqual(1, fixture.Outbox.GetPending().Count);
                var unconfirmed = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single(item => item.Artifact.Format == RecordingArtifactFormat.Wave);
                Assert.AreEqual(fault == "after" ? RecordingAvailability.Available : RecordingAvailability.Pending, unconfirmed.Artifact.Availability);
            }
            await fixture.Journal().WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture);
            var saved = (await fixture.Artifacts.GetForRecordingAsync(fixture.Id)).Single(item => item.Artifact.Format == RecordingArtifactFormat.Wave);
            Assert.AreEqual(fixture.Id, saved.Artifact.Id); Assert.AreEqual(2L, saved.Version); Assert.AreEqual(RecordingAvailability.Available, saved.Artifact.Availability);
            Assert.AreEqual(fixture.Digest, saved.Artifact.Sha256); Assert.AreEqual(new FileInfo(fixture.Wave).Length, saved.Artifact.ByteLength);
            Assert.AreEqual(fixture.Digest, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture.Wave))).ToLowerInvariant());
            Assert.AreEqual(2L, (await fixture.Parent.GetAsync(fixture.Id))!.Version); Assert.AreEqual(2L, (await fixture.Captures.GetAsync(fixture.Id))!.Version);
            Assert.AreEqual(other, await fixture.Artifacts.GetAsync(mp3.Id)); Assert.AreEqual(0, fixture.Outbox.GetPending().Count);
            await fixture.Journal().WriteAsync(fixture.CompletedRecording, fixture.CompletedCapture);
            Assert.AreEqual(saved, await fixture.Artifacts.GetAsync(saved.Artifact.Id));
        }
    }
    private sealed class FailArtifactConfirmation(IRecordingArtifactRepository inner, string fault, string path) : IRecordingArtifactRepository
    {
        private bool _failed;
        public Task<StoredRecordingArtifact?> GetAsync(Guid id, CancellationToken token = default) => inner.GetAsync(id, token);
        public Task<IReadOnlyList<StoredRecordingArtifact>> GetForRecordingAsync(Guid id, CancellationToken token = default) => inner.GetForRecordingAsync(id, token);
        public async Task<StoredRecordingArtifact> WriteAsync(RecordingArtifactWrite write, CancellationToken token = default)
        {
            Assert.ThrowsExactly<IOException>(() => { using var ignored = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });
            var fail = !_failed && write.Artifact.Availability == RecordingAvailability.Available && fault != "none";
            if (fail) _failed = true;
            if (fail && fault == "before") throw new DatabaseOperationException(DatabaseErrorCode.CommitUncertain);
            var saved = await inner.WriteAsync(write, token);
            if (fail) throw new DatabaseOperationException(DatabaseErrorCode.CommitUncertain);
            return saved;
        }
    }
}

internal sealed class WaveArtifactFixture : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wcp-wave-artifact-" + Guid.NewGuid().ToString("N"));
    private readonly WindowsUserSecretProtector _secrets = new();
    private ApplicationConfigurationService _configuration = null!;
    internal DatabaseTarget Target { get; private set; } = null!;
    internal ApplicationPaths Paths { get; }
    internal string Wave => Path.Combine(Paths.RecordingsDirectory, "captured.wav");
    internal string Digest { get; private set; } = "";
    internal Guid Id { get; } = Guid.NewGuid();
    internal DateTimeOffset Changed { get; } = DateTimeOffset.UtcNow;
    internal RecordingWrite CompletedRecording { get; private set; } = null!;
    internal RecordingCaptureWrite CompletedCapture { get; private set; } = null!;
    internal DapperRecordingArtifactRepository Artifacts { get; private set; } = null!;
    internal DapperRecordingRepository Parent { get; private set; } = null!;
    internal DapperRecordingCaptureRepository Captures { get; private set; } = null!;
    internal ProtectedRecordingJournalOutbox Outbox { get; private set; } = null!;
    private WaveArtifactFixture() => Paths = new(new ConfigurationLocation(_root));
    internal static async Task<WaveArtifactFixture> Create(DatabaseTarget? supplied = null)
    {
        var result = new WaveArtifactFixture(); result.Target = supplied ?? DatabaseTarget.Sqlite(Path.Combine(result._root, "communications.db"));
        if (supplied is null) { await new DatabaseProvisioner(new()).CreateAsync(result.Target); await new DatabaseMigrator(new(), new FluentMigratorEngine(result.Target.Type)).MigrateAsync(result.Target); }
        using var connection = new DatabaseFactory().CreateConnection(result.Target);
        var provider = result.Target.Type switch { DatabaseType.Sqlite => CommunicationDatabaseProvider.Sqlite, DatabaseType.SqlServer => CommunicationDatabaseProvider.SqlServer, DatabaseType.MySql => CommunicationDatabaseProvider.MySql, _ => CommunicationDatabaseProvider.PostgreSql };
        var settings = provider == CommunicationDatabaseProvider.Sqlite ? new CommunicationDatabaseSettings { SqliteFileName = connection.DataSource } :
            new CommunicationDatabaseSettings { Provider = provider, ConnectionString = result._secrets.Protect(connection.ConnectionString, CommunicationDatabaseSettings.ConnectionStringPurpose) };
        var store = new ConfigurationStore(new(result._root), new ApplicationOptionsValidator(result._secrets), null); await store.SaveAsync(new() { Database = settings });
        result._configuration = new(store); await result._configuration.StartAsync(CancellationToken.None);
        var context = new DatabaseRepositoryContext(result.Target, new()); var start = result.Changed.AddMinutes(-1);
        result.Artifacts = new(context); result.Parent = new(context); result.Captures = new(context); result.Outbox = result.ReopenOutbox();
        var call = new CallSnapshot { Id = Guid.NewGuid(), Direction = CallDirection.Incoming, Revision = 0, State = CallState.Connected,
            StartedAtUtc = start, AnsweredAtUtc = start, StateChangedAtUtc = start, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown) };
        await new DapperCallRepository(context).WriteAsync(new(call, null, [], [], new(Guid.NewGuid(), "ARTIFACT-WAVE-TEST", "TEST\\Wave")));
        var recording = new Recording(result.Id, call.Id, "captured.wav", "wav", null, RecordingAvailability.Pending);
        await new DapperRecordingJournal(context).BeginAsync(new(recording, null, start), new(new(result.Id, call.Id, start, null, PcmRecordingState.Capturing, 8000, 2), null, start));
        var complete = new RecordingCapture(result.Id, call.Id, start, start.AddMilliseconds(20), PcmRecordingState.Completed, 8000, 2, 160);
        result.CompletedRecording = new(recording with { Availability = RecordingAvailability.Available, Duration = complete.Duration }, 1, result.Changed);
        result.CompletedCapture = new(complete, 1, result.Changed); result.WriteWave(); return result;
    }
    internal DurableRecordingJournal Journal(IRecordingArtifactRepository? repository = null) =>
        new(new(new(Target, new())), ReopenOutbox(), new RecordingWaveArtifactPublisher(repository ?? new DapperRecordingArtifactRepository(new(Target, new()))));
    private ProtectedRecordingJournalOutbox ReopenOutbox() => new(Paths, _secrets, _configuration, new("WawiCallPro", "Pro"));
    internal ProtectedRecordingConversionOutbox ConversionOutbox(string productLine = "Pro") =>
        new(Paths, _secrets, _configuration, new("WawiCallPro", productLine), new LocalRecordingFileProbe(Paths));
    internal RecordingArtifact PendingArtifact(Guid id, string path) => new(id, Id, CompletedRecording.Recording.CallId, RecordingArtifactFormat.Wave, path, RecordingAvailability.Pending);
    internal void WriteWave(int rate = 8000, int frames = 160)
    {
        Directory.CreateDirectory(Paths.RecordingsDirectory);
        using (var writer = new BinaryWriter(File.Create(Wave), Encoding.ASCII, false))
        {
            writer.Write(0x46464952U); writer.Write(36 + frames * 4); writer.Write(0x45564157U); writer.Write(0x20746d66U); writer.Write(16);
            writer.Write((ushort)1); writer.Write((ushort)2); writer.Write(rate); writer.Write(rate * 4); writer.Write((ushort)4); writer.Write((ushort)16);
            writer.Write(0x61746164U); writer.Write(frames * 4); writer.Write(new byte[frames * 4]);
        }
        Digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Wave))).ToLowerInvariant();
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
