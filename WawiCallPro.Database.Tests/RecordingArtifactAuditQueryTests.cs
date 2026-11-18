using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Tests;

[TestClass]
public sealed class RecordingArtifactAuditQueryTests
{
    [TestMethod]
    public async Task AuditPagesIncludeOnlyConfirmedLocalTerminalCallWaves()
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-artifact-query-" + Guid.NewGuid().ToString("N"));
        try
        {
            var target = DatabaseTarget.Sqlite(Path.Combine(root, "audit.db")); await new DatabaseProvisioner(new()).CreateAsync(target);
            await new DatabaseMigrator(new(), new FluentMigratorEngine(target.Type)).MigrateAsync(target);
            await RecordingArtifactAuditQueryAcceptance.RunAsync(target);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

public static class RecordingArtifactAuditQueryAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        var context = new DatabaseRepositoryContext(target, new()); var workstation = Guid.NewGuid();
        var eligible = new List<StoredRecordingArtifact>();
        foreach (var state in new[] { CallState.Ended, CallState.Failed, CallState.Ended })
            eligible.Add(await Add(context, workstation, state, RecordingAvailability.Available));
        var repository = new DapperRecordingArtifactRepository(context);
        var first = eligible[0]; var item = first.Artifact;
        eligible[0] = await repository.WriteAsync(new(new(item.Id, item.RecordingId, item.CallId, item.Format, item.RelativePath, RecordingAvailability.Missing, item.ByteLength, item.Sha256), first.Version, first.UpdatedAtUtc));
        await Add(context, workstation, CallState.Connected, RecordingAvailability.Available);
        await Add(context, Guid.NewGuid(), CallState.Ended, RecordingAvailability.Available);
        await Add(context, workstation, CallState.Ended, RecordingAvailability.Pending);
        await Add(context, workstation, CallState.Ended, RecordingAvailability.Failed);
        eligible.Add(await Add(context, workstation, CallState.Ended, RecordingAvailability.Available, RecordingArtifactFormat.Mp3));
        await Add(context, workstation, CallState.Connected, RecordingAvailability.Available, RecordingArtifactFormat.Mp3);
        await Add(context, workstation, CallState.Ended, RecordingAvailability.Pending, RecordingArtifactFormat.Mp3);
        await Add(context, Guid.NewGuid(), CallState.Ended, RecordingAvailability.Available, RecordingArtifactFormat.Mp3);
        var query = new DapperRecordingArtifactAuditQuery(context);
        var sorted = eligible.OrderBy(value => DatabaseId.Encode(value.Artifact.Id), StringComparer.Ordinal).ToArray();
        Guid? after = null;
        for (var index = 0; index < sorted.Length; index++)
        {
            var page = await query.GetPageAsync(workstation, after, 1);
            Assert.AreEqual(index < sorted.Length - 1, page.HasMore); Assert.AreEqual(sorted[index], page.Items.Single()); after = page.Items[0].Artifact.Id;
        }
        Assert.IsTrue((await query.GetPageAsync(workstation, after, 1)).Items.IsEmpty);
        Assert.IsTrue((await query.GetPageAsync(Guid.NewGuid())).Items.IsEmpty);
        Assert.AreEqual(4, (await new DapperRecordingArtifactAuditQuery(new(target, new())).GetPageAsync(workstation)).Items.Length);
        await Assert.ThrowsAsync<OperationCanceledException>(() => query.GetPageAsync(workstation, cancellationToken: new CancellationToken(true)));
        foreach (var limit in new[] { 0, 501 }) Assert.Throws<ArgumentException>(() => query.GetPageAsync(workstation, limit: limit));
        Assert.Throws<ArgumentException>(() => query.GetPageAsync(Guid.Empty)); Assert.Throws<ArgumentException>(() => query.GetPageAsync(workstation, Guid.Empty));
    }
    private static async Task<StoredRecordingArtifact> Add(DatabaseRepositoryContext context, Guid workstation, CallState state,
        RecordingAvailability availability, RecordingArtifactFormat format = RecordingArtifactFormat.Wave)
    {
        var time = RepositoryAcceptance.Time; var origin = new CallOrigin(workstation, "AUDIT-QUERY", "TEST\\Audit");
        var call = new CallSnapshot { Id = Guid.NewGuid(), Revision = 0, State = CallState.Connected, Direction = CallDirection.Incoming,
            StartedAtUtc = time, AnsweredAtUtc = time, StateChangedAtUtc = time, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown) };
        var calls = new DapperCallRepository(context); var saved = await calls.WriteAsync(new(call, null, [], [], origin));
        if (state != CallState.Connected) await calls.WriteAsync(new(saved.History.Call with { State = state, Revision = 1,
            StateChangedAtUtc = time.AddSeconds(1), EndedAtUtc = time.AddSeconds(1), EndReason = state == CallState.Ended ? CallEndReason.Completed : CallEndReason.Interrupted }, saved.Version, [], [], origin));
        var recording = new Recording(Guid.NewGuid(), call.Id, "legacy.wav", "wav", null, RecordingAvailability.Pending);
        await new DapperRecordingRepository(context).WriteAsync(new(recording, null, time));
        var repository = new DapperRecordingArtifactRepository(context); var id = Guid.NewGuid(); var path = id.ToString("N") + (format == RecordingArtifactFormat.Wave ? ".wav" : ".mp3");
        var artifact = new RecordingArtifact(id, recording.Id, call.Id, format, path, RecordingAvailability.Pending);
        var initial = await repository.WriteAsync(new(artifact, null, time));
        if (availability == RecordingAvailability.Pending) return initial;
        return await repository.WriteAsync(new(new(id, recording.Id, call.Id, format, path, availability,
            availability == RecordingAvailability.Available ? 128 : null, availability == RecordingAvailability.Available ? new string('a', 64) : null), initial.Version, time));
    }
}
