using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class InterruptedMp3RecoveryTests
{
    [TestMethod]
    public async Task AbandonedAttemptIsFailedOnceAndCanBeExplicitlyRequeuedWithoutChangingFiles()
    { using var database = await WaveArtifactFixture.Create(); await InterruptedMp3RecoveryAcceptance.RunAsync(database.Target); }

    [TestMethod]
    [DataRow("identity")][DataRow("foreign")][DataRow("live")][DataRow("busy")][DataRow("wave-receipt")]
    [DataRow("output")][DataRow("directory")][DataRow("corrupt-receipt")]
    public async Task UnprovenOwnerOrOutputNeverChangesTheRunningAttempt(string guard)
    {
        using var ready = await Mp3AttemptFixture.Create(ended: guard != "live"); await InterruptedMp3RecoveryAcceptance.Claim(ready);
        if (guard == "output") File.WriteAllBytes(ready.Output, [31, 37]);
        if (guard == "directory") Directory.CreateDirectory(ready.Output);
        if (guard == "wave-receipt") { await using var staged = await ready.File.Outbox.StageAsync(ready.File.CompletedRecording, ready.File.CompletedCapture); }
        if (guard == "corrupt-receipt")
        {
            var root = Path.Combine(ready.File.Paths.RootDirectory, "mp3-pending"); Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, ready.Pair.Conversion.Job.Id.ToString("N") + ".pending"), "synthetic-invalid");
        }
        await using var held = guard == "busy" ? ready.File.Outbox.AcquireInspection(ready.File.Id) : null;
        var before = await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id); var log = new InterruptedLog();
        var recovery = InterruptedMp3RecoveryAcceptance.Recovery(ready, guard == "identity" ? null : guard == "foreign" ? Guid.NewGuid() : ready.Workstation, log: log);
        var result = await recovery.RecoverAsync(); Assert.AreEqual(0, result.Recovered);
        Assert.AreEqual(before, await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id));
        Assert.AreEqual(ready.Pair.Target, await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id));
        if (guard == "output") CollectionAssert.AreEqual(new byte[] { 31, 37 }, File.ReadAllBytes(ready.Output));
        if (guard is not ("foreign" or "live")) CollectionAssert.Contains(log.Events, OperationEvent.RecordingConversionRecoveryIncomplete);
    }

    [TestMethod]
    public async Task ActiveOwnerKeepsItsAttemptEvenBeforeEncodingStarts()
    {
        using var ready = await Mp3AttemptFixture.Create();
        await using var owner = await ready.Factory(new AttemptConverter(), ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id);
        Assert.AreEqual(new InterruptedMp3RecoveryResult(0, 1, 0), await InterruptedMp3RecoveryAcceptance.Recovery(ready, ready.Workstation).RecoverAsync());
        Assert.AreEqual(RecordingConversionState.Running, (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!.Job.State);
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task ProtectedPreparedAndPublishedSuccessAlwaysWinsOverInterruption(bool published)
    {
        using var pending = await PreparedMp3.Create(published: published); var ready = pending.Ready;
        Assert.AreEqual(new InterruptedMp3RecoveryResult(0, 1, 0), await InterruptedMp3RecoveryAcceptance.Recovery(ready, ready.Workstation).RecoverAsync());
        Assert.AreEqual(RecordingConversionState.Running, (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!.Job.State);
        Assert.AreEqual(new RecordingConversionRecoveryResult(1, 0, 0), await pending.Recovery().RecoverAsync());
        Assert.AreEqual(new InterruptedMp3RecoveryResult(0, 0, 0), await InterruptedMp3RecoveryAcceptance.Recovery(ready, ready.Workstation).RecoverAsync());
        Assert.AreEqual(RecordingConversionState.Completed, (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!.Job.State);
    }

    [TestMethod]
    public async Task CancellationLeavesRunningMetadataAndReleasesOwnedLeases()
    {
        using var ready = await Mp3AttemptFixture.Create(); await InterruptedMp3RecoveryAcceptance.Claim(ready);
        await Assert.ThrowsAsync<OperationCanceledException>(() => InterruptedMp3RecoveryAcceptance.Recovery(ready, ready.Workstation).RecoverAsync(new CancellationToken(true)));
        Assert.AreEqual(RecordingConversionState.Running, (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!.Job.State);
        await using var lease = ready.File.Outbox.AcquireInspection(ready.File.Id);
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task AChangedAttemptOrLateProtectedReceiptWinsAfterCandidateSelection(bool protectedReceipt)
    {
        using var ready = await Mp3AttemptFixture.Create(); await InterruptedMp3RecoveryAcceptance.Claim(ready);
        var current = (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!; var old = current.Job; var artifact = ready.Pair.Target.Artifact;
        var failed = new RecordingConversionJob(old.Id, old.SourceArtifactId, old.TargetArtifactId, old.Format, RecordingConversionState.Failed,
            old.Attempts, old.AttemptId, RecordingConversionFailure.EncodingFailed);
        var changed = DateTimeOffset.UtcNow;
        var query = new LateQuery(new DapperInterruptedRecordingConversionQuery(new(ready.File.Target, new())), async () =>
        {
            if (protectedReceipt)
            {
                await using var receipt = await ready.File.ConversionOutbox().StageAsync(new(new(artifact.Id, artifact.RecordingId, artifact.CallId, artifact.Format,
                    artifact.RelativePath, RecordingAvailability.Failed), 1, changed), new(failed, current.Version, changed));
            }
            else await ready.Jobs.WriteAsync(new(failed, current.Version, changed));
        });
        var recovery = new InterruptedMp3Recovery(query, ready.Jobs, ready.File.Artifacts, new DapperCallRepository(new(ready.File.Target, new())),
            ready.File.Outbox, ready.File.ConversionOutbox(), new DurableRecordingConversionJournal(ready.Journal, ready.File.ConversionOutbox()),
            new LocalRecordingFileProbe(ready.File.Paths), new RecoveryIdentity(ready.Workstation), TimeProvider.System, NullLogger<InterruptedMp3Recovery>.Instance);
        Assert.AreEqual(new InterruptedMp3RecoveryResult(0, 1, 0), await recovery.RecoverAsync());
        if (protectedReceipt) Assert.AreEqual(new RecordingConversionRecoveryResult(1, 0, 0), await new AbandonedMp3(ready, ready.File.ConversionOutbox(), new AttemptConverter()).Recovery().RecoverAsync());
        Assert.AreEqual(RecordingConversionFailure.EncodingFailed, (await ready.Jobs.GetAsync(old.Id))!.Job.Failure);
        Assert.IsFalse(File.Exists(ready.Output));
    }

    private sealed class LateQuery(IInterruptedRecordingConversionQuery inner, Func<Task> afterSelection) : IInterruptedRecordingConversionQuery
    {
        public async Task<Page<StoredRecordingConversion>> GetPageAsync(Guid workstationId, Guid? afterJobId = null, int limit = 100, CancellationToken token = default)
        { var page = await inner.GetPageAsync(workstationId, afterJobId, limit, token); await afterSelection(); return page; }
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task UncertainJournalResponseNeverCreatesAnotherAttempt(bool after)
    {
        using var ready = await Mp3AttemptFixture.Create(); await InterruptedMp3RecoveryAcceptance.Claim(ready);
        var journal = new UncertainJournal(new DurableRecordingConversionJournal(ready.Journal, ready.File.ConversionOutbox()), after);
        var result = await InterruptedMp3RecoveryAcceptance.Recovery(ready, ready.Workstation, journal: journal).RecoverAsync();
        Assert.AreEqual(1, result.Failed); Assert.AreEqual(0, result.Recovered);
        var retry = await InterruptedMp3RecoveryAcceptance.Recovery(ready, ready.Workstation).RecoverAsync();
        Assert.AreEqual(after ? 0 : 1, retry.Recovered);
        Assert.AreEqual(1, (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!.Job.Attempts);
        Assert.IsFalse(File.Exists(ready.Output));
    }
    private sealed class UncertainJournal(IRecordingConversionJournal inner, bool after) : IRecordingConversionJournal
    {
        public Task<StoredRecordingConversionJournal> BeginAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken token = default) => throw new AssertFailedException();
        public async Task<StoredRecordingConversionJournal> WriteAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken token = default)
        { if (after) await inner.WriteAsync(target, conversion, token); throw new DatabaseOperationException(DatabaseErrorCode.CommitUncertain); }
    }
}

public static class InterruptedMp3RecoveryAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        using var ready = await Mp3AttemptFixture.Create(target); await Claim(ready);
        var original = File.ReadAllBytes(ready.File.Wave); var temporary = Path.Combine(ready.File.Paths.RecordingsDirectory, ".wawi-mp3-" + Guid.NewGuid().ToString("N") + ".tmp");
        byte[] partial = [1, 5, 9]; File.WriteAllBytes(temporary, partial);
        var before = (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!; var log = new InterruptedLog();
        Assert.AreEqual(new InterruptedMp3RecoveryResult(1, 0, 0), await Recovery(ready, ready.Workstation, log: log).RecoverAsync());
        var job = (await ready.Jobs.GetAsync(before.Job.Id))!; var artifact = (await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id))!;
        Assert.AreEqual(RecordingConversionFailure.Interrupted, job.Job.Failure); Assert.AreEqual(3L, job.Version);
        Assert.AreEqual(before.Job.AttemptId, job.Job.AttemptId); Assert.AreEqual(before.Job.Attempts, job.Job.Attempts);
        Assert.AreEqual(RecordingAvailability.Failed, artifact.Artifact.Availability); Assert.AreEqual(2L, artifact.Version); Assert.IsNull(artifact.Artifact.Sha256);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(ready.File.Wave)); CollectionAssert.AreEqual(partial, File.ReadAllBytes(temporary));
        Assert.AreEqual(0, ready.File.ConversionOutbox().GetPending().Count); Assert.IsFalse(File.Exists(ready.Output));
        CollectionAssert.Contains(log.Events, OperationEvent.RecordingConversionInterrupted);
        Assert.AreEqual(new InterruptedMp3RecoveryResult(0, 0, 0), await Recovery(ready, ready.Workstation).RecoverAsync());
        var queued = new RecordingConversionJob(job.Job.Id, job.Job.SourceArtifactId, job.Job.TargetArtifactId, job.Job.Format, RecordingConversionState.Pending, job.Job.Attempts);
        var changed = DateTimeOffset.UtcNow;
        await new DurableRecordingConversionJournal(ready.Journal, ready.File.ConversionOutbox()).WriteAsync(new(ready.Pair.Target.Artifact, artifact.Version, changed), new(queued, job.Version, changed));
        var converter = new AttemptConverter();
        await using var owner = await ready.Factory(converter, ready.Workstation).StartAsync(job.Job.Id);
        var finished = await owner.RunAsync(); Assert.AreEqual(RecordingConversionState.Completed, finished.Conversion.Job.State);
        Assert.AreEqual(2, finished.Conversion.Job.Attempts); Assert.AreNotEqual(before.Job.AttemptId, finished.Conversion.Job.AttemptId);
        Assert.AreEqual(1, converter.Calls); CollectionAssert.AreEqual(partial, File.ReadAllBytes(temporary));
    }
    internal static async Task Claim(Mp3AttemptFixture ready)
    {
        var old = ready.Pair.Conversion.Job;
        await ready.Jobs.WriteAsync(new(new(old.Id, old.SourceArtifactId, old.TargetArtifactId, old.Format, RecordingConversionState.Running, 1, Guid.NewGuid()), 1, DateTimeOffset.UtcNow));
    }
    internal static InterruptedMp3Recovery Recovery(Mp3AttemptFixture ready, Guid? local, IRecordingConversionJournal? journal = null, ILogger<InterruptedMp3Recovery>? log = null) =>
        new(new DapperInterruptedRecordingConversionQuery(new(ready.File.Target, new())), ready.Jobs, ready.File.Artifacts, new DapperCallRepository(new(ready.File.Target, new())),
            ready.File.Outbox, ready.File.ConversionOutbox(), journal ?? new DurableRecordingConversionJournal(ready.Journal, ready.File.ConversionOutbox()),
            new LocalRecordingFileProbe(ready.File.Paths), new RecoveryIdentity(local), TimeProvider.System, log ?? NullLogger<InterruptedMp3Recovery>.Instance);
}
internal sealed class InterruptedLog : ILogger<InterruptedMp3Recovery>
{
    internal List<OperationEvent> Events { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    { Assert.IsNull(exception); Assert.IsTrue(OperationEvents.TryGetEvent(state, out var operation)); Events.Add(operation); }
}
