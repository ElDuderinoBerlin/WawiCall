using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Database;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingMp3ArtifactAuditTests
{
    internal static async Task VerifyRoundtripAsync(DatabaseTarget target)
    {
        using var ready = await Mp3AttemptFixture.Create(target);
        await using (var session = await ready.Factory(new AttemptConverter(), ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id))
            await session.RunAsync();
        var before = (await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id))!;
        var job = await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id);
        var original = File.ReadAllBytes(ready.Output);
        var audit = RecordingArtifactAuditAcceptance.Service(ready.File, ready.Workstation);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 2, 0, 0), await audit.ReconcileAsync());
        File.Delete(ready.Output);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(1, 0, 1, 0, 0), await audit.ReconcileAsync());
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 2, 0, 0), await audit.ReconcileAsync());
        var changed = original.ToArray(); changed[^1] ^= 1; File.WriteAllBytes(ready.Output, changed);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 2, 0, 0), await audit.ReconcileAsync());
        CollectionAssert.AreEqual(changed, File.ReadAllBytes(ready.Output));
        File.WriteAllBytes(ready.Output, original);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 1, 1, 0, 0), await RecordingArtifactAuditAcceptance.Service(ready.File, ready.Workstation).ReconcileAsync());
        var restored = (await ready.File.Artifacts.GetAsync(before.Artifact.Id))!;
        Assert.AreEqual(before.Artifact, restored.Artifact); Assert.AreEqual(before.Version + 2, restored.Version);
        Assert.AreEqual(job, await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id));
    }

    [TestMethod]
    [DataRow("changed")][DataRow("length")][DataRow("locked")][DataRow("busy")]
    public async Task ChangesAndLocksPreserveTheOriginalFingerprintAndConversion(string problem)
    {
        using var ready = await Mp3AttemptFixture.Create();
        await using (var session = await ready.Factory(new AttemptConverter(), ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id))
            await session.RunAsync();
        var before = (await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id))!;
        var job = await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id);
        var bytes = File.ReadAllBytes(ready.Output);
        if (problem == "changed") { bytes[^1] ^= 1; File.WriteAllBytes(ready.Output, bytes); }
        if (problem == "length") { bytes = bytes[..^1]; File.WriteAllBytes(ready.Output, bytes); }
        using var locked = problem == "locked" ? File.Open(ready.Output, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        await using var busy = problem == "busy" ? ready.File.Outbox.AcquireInspection(ready.File.Id) : null;
        var result = await RecordingArtifactAuditAcceptance.Service(ready.File, ready.Workstation).ReconcileAsync();
        var after = (await ready.File.Artifacts.GetAsync(before.Artifact.Id))!;
        Assert.AreEqual(problem is "changed" or "length" ? 1 : 0, result.MarkedMissing);
        Assert.AreEqual(problem == "locked" ? 1 : problem == "busy" ? 2 : 0, result.Failed);
        Assert.AreEqual(before.Artifact.Sha256, after.Artifact.Sha256); Assert.AreEqual(before.Artifact.ByteLength, after.Artifact.ByteLength);
        Assert.AreEqual(job, await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id));
    }

    [TestMethod]
    public async Task LegacyConstructionWithoutConversionProtectionDefersMp3InsteadOfChangingIt()
    {
        using var ready = await Mp3AttemptFixture.Create();
        await using (var session = await ready.Factory(new AttemptConverter(), ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id))
            await session.RunAsync();
        var before = await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id); File.Delete(ready.Output);
        var audit = new RecordingArtifactReconciliation(new WawiCallPro.Database.Repositories.DapperRecordingArtifactAuditQuery(new(ready.File.Target, new())),
            ready.File.Artifacts, ready.File.Outbox, new RecordingArtifactFileInspector(ready.File.Paths, new LocalRecordingFileProbe(ready.File.Paths)),
            new Identity(ready.Workstation), TimeProvider.System, Microsoft.Extensions.Logging.Abstractions.NullLogger<RecordingArtifactReconciliation>.Instance);
        Assert.AreEqual(new RecordingArtifactReconciliationResult(0, 0, 1, 1, 0), await audit.ReconcileAsync());
        Assert.AreEqual(before, await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id));
    }

    private sealed class Identity(Guid id) : IWorkstationIdentity
    {
        public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult<Guid?>(id);
        public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new AssertFailedException();
    }

    [TestMethod]
    public async Task ProtectedCompletionAfterDatabaseCommitIsDeferredWithoutBreakingAcknowledgement()
    {
        using var pending = await AbandonedMp3.Create(after: true); var ready = pending.Ready;
        var before = await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id);
        File.Delete(ready.Output);
        var result = await RecordingArtifactAuditAcceptance.Service(ready.File, ready.Workstation).ReconcileAsync();
        Assert.AreEqual(1, result.Deferred); Assert.AreEqual(0, result.MarkedMissing);
        Assert.AreEqual(before, await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id)); Assert.IsTrue(File.Exists(pending.Entry));
    }
}
