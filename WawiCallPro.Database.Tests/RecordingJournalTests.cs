using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class RecordingJournalTests
{
    [TestMethod]
    public async Task RecordingAndCaptureCommitTogetherAndExactReplaysDoNotAdvanceVersions()
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-journal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var target = DatabaseTarget.Sqlite(Path.Combine(root, "journal.db")); var factory = new DatabaseFactory();
            await new DatabaseProvisioner(factory).CreateAsync(target); await new DatabaseMigrator(factory, new FluentMigratorEngine(target.Type)).MigrateAsync(target);
            await RecordingJournalAcceptance.RunAsync(target);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

public static class RecordingJournalAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var journal = new DapperRecordingJournal(context); var time = RepositoryAcceptance.Time;
        var call = new CallSnapshot { Id = Guid.NewGuid(), Revision = 0, State = CallState.Connected, Direction = CallDirection.Incoming,
            LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown), StartedAtUtc = time, AnsweredAtUtc = time, StateChangedAtUtc = time };
        await new DapperCallRepository(context).WriteAsync(new(call, null, [], [], new(Guid.NewGuid(), "JOURNAL-TEST", "TEST\\Journal")));
        var file = new Recording(Guid.NewGuid(), call.Id, "journal/missing.wav", "wav", null, RecordingAvailability.Pending);
        var active = new RecordingCapture(file.Id, file.CallId, time, null, PcmRecordingState.Capturing, 8000, 2);
        var started = await journal.BeginAsync(new(file, null, time), new(active, null, time));
        Assert.AreEqual(1L, started.Recording.Version); Assert.AreEqual(1L, started.Capture.Version);
        var duplicate = await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => journal.BeginAsync(new(file, null, time), new(active, null, time)));
        Assert.AreEqual(RepositoryConflictKind.AlreadyExists, duplicate.Kind);
        var end = time.AddMilliseconds(20); var changed = time.AddSeconds(1);
        var completed = new RecordingCapture(file.Id, file.CallId, time, end, PcmRecordingState.Completed, 8000, 2, 160);
        var available = file with { Availability = RecordingAvailability.Available, Duration = completed.Duration };
        var stale = await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => journal.WriteAsync(new(available, 1, changed), new(completed, 2, changed)));
        Assert.AreEqual(RepositoryConflictKind.VersionMismatch, stale.Kind);
        Assert.AreEqual(started.Recording, await new DapperRecordingRepository(new(target, new())).GetAsync(file.Id));
        Assert.AreEqual(started.Capture, await new DapperRecordingCaptureRepository(new(target, new())).GetAsync(file.Id));
        var saved = await journal.WriteAsync(new(available, 1, changed), new(completed, 1, changed));
        Assert.AreEqual(2L, saved.Recording.Version); Assert.AreEqual(2L, saved.Capture.Version);
        Assert.AreEqual(saved, await journal.WriteAsync(new(available, 1, changed), new(completed, 1, changed)));
        var mixedReplay = await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => journal.WriteAsync(new(available, 2, changed), new(completed, 1, changed)));
        Assert.AreEqual(RepositoryConflictKind.VersionMismatch, mixedReplay.Kind);
        var regression = await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => journal.WriteAsync(new(file, 2, changed), new(active, 2, changed)));
        Assert.AreEqual(RepositoryConflictKind.SnapshotMismatch, regression.Kind);
        Assert.AreEqual(saved.Recording, await new DapperRecordingRepository(new(target, new())).GetAsync(file.Id));
        Assert.AreEqual(saved.Capture, await new DapperRecordingCaptureRepository(new(target, new())).GetAsync(file.Id));
        var moved = available with { RelativePath = "journal/other.wav" };
        var ownership = await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => journal.WriteAsync(new(moved, 2, changed), new(completed, 2, changed)));
        Assert.AreEqual(RepositoryConflictKind.OwnershipMismatch, ownership.Kind);
        await Assert.ThrowsAsync<OperationCanceledException>(() => journal.WriteAsync(new(available, 1, changed), new(completed, 1, changed), new CancellationToken(true)));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => journal.WriteAsync(new(file, 2, changed), new(completed, 2, changed)));
        var missing = file with { Id = Guid.NewGuid(), CallId = Guid.NewGuid() };
        var missingCapture = new RecordingCapture(missing.Id, missing.CallId, time, null, PcmRecordingState.Capturing, 8000, 2);
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => journal.BeginAsync(new(missing, null, time), new(missingCapture, null, time)));
        Assert.IsNull(await new DapperRecordingRepository(context).GetAsync(missing.Id)); Assert.IsNull(await new DapperRecordingCaptureRepository(context).GetAsync(missing.Id));
    }
}
