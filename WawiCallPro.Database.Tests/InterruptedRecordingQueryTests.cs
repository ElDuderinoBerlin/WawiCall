using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class InterruptedRecordingQueryTests
{
    [TestMethod]
    public async Task CandidateFiltersAndExclusiveCursorSurviveCompletedEarlierPages()
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-interrupted-query-" + Guid.NewGuid().ToString("N"));
        try
        {
            var target = DatabaseTarget.Sqlite(Path.Combine(root, "query.db"));
            await new DatabaseProvisioner(new()).CreateAsync(target);
            await new DatabaseMigrator(new(), new FluentMigratorEngine(target.Type)).MigrateAsync(target);
            await InterruptedRecordingQueryAcceptance.RunAsync(target);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>The same assertions run against SQLite and each isolated native database engine.</summary>
public static class InterruptedRecordingQueryAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var time = RepositoryAcceptance.Time;
        var workstation = Guid.NewGuid(); var ended = await Call(context, workstation, CallState.Ended);
        var failed = await Call(context, workstation, CallState.Failed);
        var live = await Call(context, workstation, CallState.Connected);
        var foreign = await Call(context, Guid.NewGuid(), CallState.Ended);
        var journal = new DapperRecordingJournal(context);
        var eligible = new List<StoredRecordingJournal>();
        foreach (var call in new[] { ended, ended, failed }) eligible.Add(await Begin(context, call));
        var stopping = eligible[1];
        eligible[1] = await journal.WriteAsync(new(stopping.Recording.Recording, 1, time.AddSeconds(1)),
            new(new(stopping.Capture.Capture.RecordingId, stopping.Capture.Capture.CallId, time, null, PcmRecordingState.Stopping, 8000, 2), 1, time.AddSeconds(1)));
        await Begin(context, live); await Begin(context, foreign);
        foreach (var availability in new[] { RecordingAvailability.Available, RecordingAvailability.Missing, RecordingAvailability.Failed })
        {
            var item = await Begin(context, ended);
            await new DapperRecordingRepository(context).WriteAsync(new(item.Recording.Recording with { Availability = availability }, 1, time.AddSeconds(1)));
        }
        var legacy = new Recording(Guid.NewGuid(), ended, "legacy.wav", "wav", null, RecordingAvailability.Pending);
        await new DapperRecordingRepository(context).WriteAsync(new(legacy, null, time));
        foreach (var altered in new[] { "format", "duration", "terminal" })
        {
            var item = await Begin(context, ended);
            if (altered == "terminal")
            {
                var aborted = new RecordingCapture(item.Capture.Capture.RecordingId, ended, time, null, PcmRecordingState.Aborted, 8000, 2, failure: RecordingCaptureFailure.Cancelled);
                await new DapperRecordingCaptureRepository(context).WriteAsync(new(aborted, 1, time.AddSeconds(1)));
            }
            else await new DapperRecordingRepository(context).WriteAsync(new(item.Recording.Recording with
                { Format = altered == "format" ? "mp3" : "wav", Duration = altered == "duration" ? TimeSpan.FromSeconds(1) : null }, 1, time.AddSeconds(1)));
        }
        var query = new DapperInterruptedRecordingQuery(context);
        var ordered = eligible.OrderBy(item => DatabaseId.Encode(item.Recording.Recording.Id), StringComparer.Ordinal).ToArray();
        var first = await query.GetPageAsync(workstation, limit: 1); Assert.IsTrue(first.HasMore); Assert.AreEqual(ordered[0], first.Items.Single().Journal);
        var selected = first.Items[0]; Assert.AreEqual(time.AddSeconds(10), selected.CallChangedAtUtc);
        var old = selected.Journal.Capture.Capture;
        var abort = new RecordingCapture(old.RecordingId, old.CallId, old.StartedAtUtc, null, PcmRecordingState.Aborted, old.SampleRate, old.Channels, failure: RecordingCaptureFailure.Interrupted);
        await journal.WriteAsync(new(selected.Journal.Recording.Recording with { Availability = RecordingAvailability.Failed }, selected.Journal.Recording.Version, time.AddSeconds(11)),
            new(abort, selected.Journal.Capture.Version, time.AddSeconds(11)));
        var second = await new DapperInterruptedRecordingQuery(new(target, new())).GetPageAsync(workstation, old.RecordingId, 1);
        Assert.IsTrue(second.HasMore); Assert.AreEqual(ordered[1], second.Items.Single().Journal);
        var third = await query.GetPageAsync(workstation, second.Items[0].Journal.Recording.Recording.Id, 1);
        Assert.IsFalse(third.HasMore); Assert.AreEqual(ordered[2], third.Items.Single().Journal);
        Assert.AreEqual(2, (await query.GetPageAsync(workstation)).Items.Length);
        Assert.IsTrue((await query.GetPageAsync(Guid.NewGuid())).Items.IsEmpty);
        Assert.IsTrue((await query.GetPageAsync(workstation, third.Items[0].Journal.Recording.Recording.Id)).Items.IsEmpty);
        await Assert.ThrowsAsync<OperationCanceledException>(() => query.GetPageAsync(workstation, cancellationToken: new CancellationToken(true)));
        foreach (var invalid in new[] { 0, 501 }) Assert.Throws<ArgumentException>(() => query.GetPageAsync(workstation, limit: invalid));
        Assert.Throws<ArgumentException>(() => query.GetPageAsync(Guid.Empty));
        Assert.Throws<ArgumentException>(() => query.GetPageAsync(workstation, Guid.Empty));
    }

    private static async Task<Guid> Call(DatabaseRepositoryContext context, Guid workstation, CallState state)
    {
        var time = RepositoryAcceptance.Time;
        var snapshot = new CallSnapshot { Id = Guid.NewGuid(), State = CallState.Connected, Revision = 0, Direction = CallDirection.Incoming,
            LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown), StartedAtUtc = time, AnsweredAtUtc = time, StateChangedAtUtc = time };
        var repository = new DapperCallRepository(context);
        var origin = new CallOrigin(workstation, "RECOVERY-TEST", "TEST\\Recovery");
        var saved = await repository.WriteAsync(new(snapshot, null, [], [], origin));
        if (state != CallState.Connected) await repository.WriteAsync(new(saved.History.Call with { Revision = 1, State = state, StateChangedAtUtc = time.AddSeconds(10),
            EndedAtUtc = time.AddSeconds(10), EndReason = state == CallState.Ended ? CallEndReason.Completed : CallEndReason.Interrupted }, saved.Version, [], [], origin));
        return snapshot.Id;
    }

    private static Task<StoredRecordingJournal> Begin(DatabaseRepositoryContext context, Guid call)
    {
        var id = Guid.NewGuid(); var time = RepositoryAcceptance.Time;
        return new DapperRecordingJournal(context).BeginAsync(new(new(id, call, id.ToString("N") + ".wav", "wav", null, RecordingAvailability.Pending), null, time),
            new(new(id, call, time, null, PcmRecordingState.Capturing, 8000, 2), null, time));
    }
}
