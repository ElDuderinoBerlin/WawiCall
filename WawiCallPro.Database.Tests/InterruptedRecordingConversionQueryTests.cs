using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class InterruptedRecordingConversionQueryTests
{
    [TestMethod]
    public async Task LocalTerminalRunningAttemptsUseAStableExclusiveCursor()
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-interrupted-query-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var target = DatabaseTarget.Sqlite(Path.Combine(root, "test.db"));
            var creation = await new DatabaseProvisioner(new()).CreateAsync(target);
            await new DatabaseMigrator(new(), new FluentMigratorEngine(target.Type)).MigrateAsync(target, creation: creation);
            await InterruptedRecordingConversionQueryAcceptance.RunAsync(target);
        }
        finally { Directory.Delete(root, true); }
    }
}

public static class InterruptedRecordingConversionQueryAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var jobs = new DapperRecordingConversionRepository(context);
        var calls = new DapperCallRepository(context); var owner = Guid.NewGuid(); var expected = new List<StoredRecordingConversion>();
        foreach (var kind in new[] { "ended", "failed-call", "second", "live", "foreign", "pending", "failed-job" })
        {
            var seed = await RecordingConversionAcceptance.SeedAsync(context, true, kind == "foreign" ? Guid.NewGuid() : owner);
            var storedCall = (await calls.GetAsync(seed.Recording.Recording.CallId))!;
            var call = storedCall.History.Call;
            var origin = call.Origin ?? throw new AssertFailedException("Seeded call origin is required.");
            var changed = DateTimeOffset.UtcNow;
            if (kind != "live")
                await calls.WriteAsync(new(call with { Revision = call.Revision + 1, State = kind == "failed-call" ? CallState.Failed : CallState.Ended,
                    StateChangedAtUtc = changed, EndedAtUtc = changed, EndReason = kind == "failed-call" ? CallEndReason.Error : CallEndReason.Completed }, storedCall.Version, [], [], origin));
            var job = new RecordingConversionJob(Guid.NewGuid(), seed.Wave.Id, seed.Mp3.Id, new(8000, 2, 128000), RecordingConversionState.Pending);
            await jobs.WriteAsync(new(job, null, changed));
            if (kind == "pending") continue;
            var running = new RecordingConversionJob(job.Id, job.SourceArtifactId, job.TargetArtifactId, job.Format, RecordingConversionState.Running, 1, Guid.NewGuid());
            var saved = await jobs.WriteAsync(new(running, 1, changed));
            if (kind == "failed-job") await jobs.WriteAsync(new(new(job.Id, job.SourceArtifactId, job.TargetArtifactId, job.Format,
                RecordingConversionState.Failed, 1, running.AttemptId, RecordingConversionFailure.Interrupted), 2, changed));
            if (kind is "ended" or "failed-call" or "second") expected.Add(saved);
        }
        expected = expected.OrderBy(item => DatabaseId.Encode(item.Job.Id), StringComparer.Ordinal).ToList();
        var query = new DapperInterruptedRecordingConversionQuery(new(target, new()));
        var first = await query.GetPageAsync(owner, limit: 1); Assert.IsTrue(first.HasMore); Assert.AreEqual(expected[0], first.Items.Single());
        var previous = first.Items.Single(); var old = previous.Job;
        await jobs.WriteAsync(new(new(old.Id, old.SourceArtifactId, old.TargetArtifactId, old.Format, RecordingConversionState.Failed,
            old.Attempts, old.AttemptId, RecordingConversionFailure.Interrupted), previous.Version, DateTimeOffset.UtcNow));
        var second = await query.GetPageAsync(owner, old.Id, 1); Assert.IsTrue(second.HasMore); Assert.AreEqual(expected[1], second.Items.Single());
        var third = await query.GetPageAsync(owner, second.Items.Single().Job.Id, 1); Assert.IsFalse(third.HasMore); Assert.AreEqual(expected[2], third.Items.Single());
        Assert.IsTrue((await query.GetPageAsync(Guid.NewGuid())).Items.IsEmpty);
        await Assert.ThrowsAsync<OperationCanceledException>(() => query.GetPageAsync(owner, cancellationToken: new CancellationToken(true)));
        Assert.Throws<ArgumentException>(() => query.GetPageAsync(Guid.Empty));
        Assert.Throws<ArgumentException>(() => query.GetPageAsync(owner, Guid.Empty));
        Assert.Throws<ArgumentException>(() => query.GetPageAsync(owner, limit: 0));
    }
}
