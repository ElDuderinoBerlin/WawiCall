using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingConversionRecoveryTests
{
    [TestMethod] public async Task FreshServicesDeliverProtectedSuccessAndFailureWithoutEncodingAgain()
    { using var database = await WaveArtifactFixture.Create(); await RecordingConversionRecoveryAcceptance.RunAsync(database.Target); }

    [TestMethod]
    [DataRow("changed")][DataRow("missing")][DataRow("locked")][DataRow("corrupt")][DataRow("oversize")]
    [DataRow("product")][DataRow("database")][DataRow("identity")][DataRow("foreign")][DataRow("version")][DataRow("busy")][DataRow("wave-receipt")]
    public async Task UnprovenDeliveryPreservesTheReceiptFilesAndMetadata(string guard)
    {
        using var pending = await AbandonedMp3.Create(); var ready = pending.Ready; var file = ready.File;
        var original = File.ReadAllBytes(ready.Output); var encrypted = File.ReadAllBytes(pending.Entry);
        var beforeTarget = await file.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id); var beforeJob = await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id);
        if (guard == "changed") { var bytes = original.ToArray(); bytes[^1] ^= 1; File.WriteAllBytes(ready.Output, bytes); }
        if (guard == "missing") File.Delete(ready.Output);
        if (guard == "corrupt") File.WriteAllText(pending.Entry, "private-invalid-json");
        if (guard == "oversize") File.WriteAllBytes(pending.Entry, new byte[65537]);
        if (guard == "version")
            beforeTarget = await file.Artifacts.WriteAsync(new(ready.Pair.Target.Artifact, 1, DateTimeOffset.UtcNow));
        if (guard == "wave-receipt") { await using var staged = await file.Outbox.StageAsync(file.CompletedRecording, file.CompletedCapture); }
        using var locked = guard == "locked" ? File.Open(ready.Output, FileMode.Open, FileAccess.Write, FileShare.None) : null;
        await using var held = guard == "busy" ? file.Outbox.AcquireInspection(file.Id) : null;
        var box = guard == "product" ? file.ConversionOutbox("FinOffice") : pending.Box;
        if (guard == "database")
        {
            var secrets = new WindowsUserSecretProtector();
            var store = new ConfigurationStore(new ConfigurationLocation(Path.Combine(file.Paths.RootDirectory, "changed-config")), new ApplicationOptionsValidator(secrets), null);
            await store.SaveAsync(new() { Database = new() { SqliteFileName = "different.db" } });
            var configuration = new ApplicationConfigurationService(store); await configuration.StartAsync(CancellationToken.None);
            box = new(file.Paths, secrets, configuration, new ApplicationDescriptor("WawiCallPro", "Pro"), new LocalRecordingFileProbe(file.Paths));
        }
        var identity = new RecoveryIdentity(guard == "identity" ? null : guard == "foreign" ? Guid.NewGuid() : ready.Workstation);
        var log = new RecoveryLog(); var result = await pending.Recovery(box, identity, log).RecoverAsync();
        Assert.AreEqual(0, result.Delivered); Assert.AreEqual(1, result.Deferred + result.Failed); Assert.IsTrue(File.Exists(pending.Entry));
        Assert.AreEqual(beforeTarget, await file.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id)); Assert.AreEqual(beforeJob, await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id));
        Assert.AreEqual(1, pending.Converter.Calls); CollectionAssert.Contains(log.Events, OperationEvent.RecordingConversionRecoveryIncomplete);
        if (guard is "changed" or "missing" or "corrupt" or "oversize")
        {
            File.WriteAllBytes(ready.Output, original); File.WriteAllBytes(pending.Entry, encrypted);
            Assert.AreEqual(new RecordingConversionRecoveryResult(1, 0, 0), await pending.Recovery().RecoverAsync());
        }
    }

    [TestMethod]
    public async Task ProtectedPayloadIsOpaqueIdempotentExclusiveAndCancellationSafe()
    {
        using var pending = await AbandonedMp3.Create(); var text = File.ReadAllText(pending.Entry);
        Assert.DoesNotContain("captured.mp3", text);
        await using (var delivery = await pending.Box.OpenAsync(pending.Ready.Pair.Conversion.Job.Id))
        {
            Assert.DoesNotContain(delivery.Conversion.Job.AttemptId!.Value.ToString("N"), text);
            await Assert.ThrowsExactlyAsync<RecordingConversionOutboxException>(() => pending.Box.OpenAsync(delivery.Conversion.Job.Id));
            await Assert.ThrowsAsync<OperationCanceledException>(() => pending.Box.StageAsync(delivery.Target, delivery.Conversion, new CancellationToken(true)));
        }
        Assert.IsTrue(pending.Box.HasPending(pending.Ready.Pair.Conversion.Job.Id));
        await using (var delivery = await pending.Box.OpenAsync(pending.Ready.Pair.Conversion.Job.Id))
        {
            var target = delivery.Target; var job = delivery.Conversion;
            await delivery.DisposeAsync();
            await using var replay = await pending.Box.StageAsync(target, job);
            Assert.AreEqual(target, replay.Target); Assert.AreEqual(job, replay.Conversion);
        }
        Assert.AreEqual(1, pending.Box.GetPending().Count);
    }
    [TestMethod]
    public async Task InvalidEntryNamesBlockEnumerationAndDoNotPartiallyDeliver()
    {
        using var pending = await AbandonedMp3.Create();
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(pending.Entry)!, "NOT-A-GUID.pending"), "invalid");
        Assert.AreEqual(new RecordingConversionRecoveryResult(0, 0, 1), await pending.Recovery().RecoverAsync());
        Assert.IsTrue(File.Exists(pending.Entry)); Assert.AreEqual(RecordingConversionState.Running, (await pending.Ready.Jobs.GetAsync(pending.Ready.Pair.Conversion.Job.Id))!.Job.State);
    }
    [TestMethod]
    public async Task PendingFailureReceiptBlocksRequeueUntilItIsDelivered()
    {
        using var pending = await AbandonedMp3.Create(after: true, failure: true); var ready = pending.Ready;
        var durable = new DurableRecordingConversionJournal(ready.Journal, pending.Box);
        var job = ready.Pair.Conversion.Job; var queued = new RecordingConversionJob(job.Id, job.SourceArtifactId, job.TargetArtifactId, job.Format, RecordingConversionState.Pending, 1);
        var changed = DateTimeOffset.UtcNow;
        await Assert.ThrowsExactlyAsync<RecordingConversionOutboxException>(() => durable.WriteAsync(new(ready.Pair.Target.Artifact, 2, changed), new(queued, 3, changed)));
        Assert.IsTrue(File.Exists(pending.Entry)); Assert.AreEqual(new RecordingConversionRecoveryResult(1, 0, 0), await pending.Recovery().RecoverAsync());
        var saved = await durable.WriteAsync(new(ready.Pair.Target.Artifact, 2, changed), new(queued, 3, changed));
        Assert.AreEqual(RecordingConversionState.Pending, saved.Conversion.Job.State); Assert.AreEqual(4L, saved.Conversion.Version);
    }
    [TestMethod]
    public async Task OrdinaryLiveConfirmationAcknowledgesItsProtectedReceipt()
    {
        using var ready = await Mp3AttemptFixture.Create(); var box = ready.File.ConversionOutbox();
        await using var owner = await ready.Factory(new AttemptConverter(), ready.Workstation, new DurableRecordingConversionJournal(ready.Journal, box)).StartAsync(ready.Pair.Conversion.Job.Id);
        Assert.AreEqual(RecordingConversionState.Completed, (await owner.RunAsync()).Conversion.Job.State);
        Assert.AreEqual(0, box.GetPending().Count);
    }
    [TestMethod]
    public async Task RecoveryCancellationRetainsAllReceipts()
    {
        using var pending = await AbandonedMp3.Create();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending.Recovery().RecoverAsync(new CancellationToken(true)));
        Assert.IsTrue(File.Exists(pending.Entry));
    }
    [TestMethod]
    public async Task WavCiphertextCannotBeReusedAsAnMp3Completion()
    {
        using var pending = await AbandonedMp3.Create(); var file = pending.Ready.File; byte[] ciphertext;
        await using (var wave = await file.Outbox.StageAsync(file.CompletedRecording, file.CompletedCapture))
        {
            ciphertext = File.ReadAllBytes(Path.Combine(file.Paths.RootDirectory, "recording-pending", file.Id.ToString("N") + ".pending"));
            wave.Acknowledge();
        }
        File.WriteAllBytes(pending.Entry, ciphertext);
        Assert.AreEqual(new RecordingConversionRecoveryResult(0, 0, 1), await pending.Recovery().RecoverAsync());
        CollectionAssert.AreEqual(ciphertext, File.ReadAllBytes(pending.Entry));
        Assert.AreEqual(RecordingConversionState.Running, (await pending.Ready.Jobs.GetAsync(pending.Ready.Pair.Conversion.Job.Id))!.Job.State);
    }
    [TestMethod]
    public async Task ProtectedCompletionPreventsAClaimEvenAfterAnIndependentRequeue()
    {
        using var pending = await AbandonedMp3.Create(after: true, failure: true); var ready = pending.Ready;
        var old = ready.Pair.Conversion.Job;
        var queued = new RecordingConversionJob(old.Id, old.SourceArtifactId, old.TargetArtifactId, old.Format, RecordingConversionState.Pending, 1);
        var time = DateTimeOffset.UtcNow;
        var changed = await ready.Journal.WriteAsync(new(ready.Pair.Target.Artifact, 2, time), new(queued, 3, time));
        var converter = new AttemptConverter();
        await Assert.ThrowsExactlyAsync<RecordingConversionOutboxException>(() => ready.Factory(converter, ready.Workstation).StartAsync(old.Id));
        Assert.AreEqual(changed.Conversion, await ready.Jobs.GetAsync(old.Id)); Assert.AreEqual(0, converter.Calls); Assert.IsTrue(File.Exists(pending.Entry));
    }
    [TestMethod]
    public async Task ReservationAndCompletionPublicationShareTheSameExclusiveJobLease()
    {
        using var ready = await Mp3AttemptFixture.Create(); var box = ready.File.ConversionOutbox(); var old = ready.Pair.Target.Artifact;
        var result = AttemptConverter.Publish(new(ready.File.Wave, ready.Output, ready.Pair.Conversion.Job.Format));
        var target = new RecordingArtifact(old.Id, old.RecordingId, old.CallId, old.Format, old.RelativePath, RecordingAvailability.Available, result.FileSize, result.Sha256);
        var job = ready.Pair.Conversion.Job;
        var completed = new RecordingConversionJob(job.Id, job.SourceArtifactId, job.TargetArtifactId, job.Format, RecordingConversionState.Completed, 1, Guid.NewGuid());
        await using var reservation = box.AcquireEmpty(job.Id);
        var time = DateTimeOffset.UtcNow;
        await Assert.ThrowsExactlyAsync<RecordingConversionOutboxException>(() => box.StageAsync(new(target, 1, time), new(completed, 2, time)));
        Assert.AreEqual(0, box.GetPending().Count);
    }
}

public static class RecordingConversionRecoveryAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        foreach (var after in new[] { false, true }) foreach (var failure in new[] { false, true })
        {
            using var pending = await AbandonedMp3.Create(target, after, failure); var ready = pending.Ready;
            Assert.IsTrue(File.Exists(pending.Entry));
            Assert.AreEqual(new RecordingConversionRecoveryResult(1, 0, 0), await pending.Recovery().RecoverAsync());
            Assert.IsFalse(File.Exists(pending.Entry));
            var job = (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!; var artifact = (await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id))!;
            Assert.AreEqual(failure ? RecordingConversionState.Failed : RecordingConversionState.Completed, job.Job.State);
            Assert.AreEqual(failure ? RecordingAvailability.Failed : RecordingAvailability.Available, artifact.Artifact.Availability);
            Assert.AreEqual(3L, job.Version); Assert.AreEqual(2L, artifact.Version); Assert.AreEqual(1, pending.Converter.Calls);
            Assert.AreEqual(new RecordingConversionRecoveryResult(0, 0, 0), await pending.Recovery().RecoverAsync());
            Assert.AreEqual(job, await ready.Jobs.GetAsync(job.Job.Id)); Assert.AreEqual(artifact, await ready.File.Artifacts.GetAsync(artifact.Artifact.Id));
            Assert.AreEqual(2L, (await ready.File.Parent.GetAsync(ready.File.Id))!.Version); Assert.AreEqual(2L, (await ready.File.Captures.GetAsync(ready.File.Id))!.Version);
        }
    }
}

internal sealed class AbandonedMp3(Mp3AttemptFixture ready, ProtectedRecordingConversionOutbox box, AttemptConverter converter) : IDisposable
{
    internal Mp3AttemptFixture Ready => ready;
    internal ProtectedRecordingConversionOutbox Box => box;
    internal AttemptConverter Converter => converter;
    internal string Entry => Path.Combine(ready.File.Paths.RootDirectory, "mp3-pending", ready.Pair.Conversion.Job.Id.ToString("N") + ".pending");
    internal static async Task<AbandonedMp3> Create(DatabaseTarget? target = null, bool after = false, bool failure = false)
    {
        var ready = await Mp3AttemptFixture.Create(target); var box = ready.File.ConversionOutbox(); var converter = new AttemptConverter { Failure = failure ? "error" : null };
        IPersistedMp3Conversion? owner = null;
        try
        {
            var result = new AbandonedMp3(ready, box, converter);
            var journal = new DurableRecordingConversionJournal(ready.Journal, new StopDelivery(box, after));
            owner = await ready.Factory(converter, ready.Workstation, journal).StartAsync(ready.Pair.Conversion.Job.Id);
            await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => owner.RunAsync());
            Assert.AreEqual(new RecordingConversionRecoveryResult(0, 1, 0), await result.Recovery().RecoverAsync());
            await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => owner.DisposeAsync().AsTask());
            owner = null;
            return result;
        }
        catch
        {
            if (owner is not null) { try { await owner.DisposeAsync(); } catch (Exception) { } }
            ready.Dispose(); throw;
        }
    }
    internal RecordingConversionRecovery Recovery(IRecordingConversionOutbox? pending = null, IWorkstationIdentity? identity = null, ILogger<RecordingConversionRecovery>? log = null) =>
        new(pending ?? ready.File.ConversionOutbox(), new DapperRecordingConversionJournal(new(ready.File.Target, new())),
            new DapperRecordingConversionRepository(new(ready.File.Target, new())), new DapperRecordingArtifactRepository(new(ready.File.Target, new())),
            new DapperCallRepository(new(ready.File.Target, new())), ready.File.Outbox, identity ?? new RecoveryIdentity(ready.Workstation), log ?? NullLogger<RecordingConversionRecovery>.Instance);
    public void Dispose() => ready.Dispose();
    private sealed class StopDelivery(IRecordingConversionOutbox inner, bool after) : IRecordingConversionOutbox
    {
        public IReadOnlyList<Guid> GetPending() => inner.GetPending(); public bool HasPending(Guid id) => inner.HasPending(id);
        public IAsyncDisposable AcquireEmpty(Guid id) => inner.AcquireEmpty(id);
        public Task<IRecordingConversionDelivery> OpenAsync(Guid id, CancellationToken token = default) => inner.OpenAsync(id, token);
        public async Task<IRecordingConversionDelivery> StageAsync(RecordingArtifactWrite target, RecordingConversionWrite job, CancellationToken token = default)
        {
            var delivery = await inner.StageAsync(target, job, token);
            if (after) return new NoAcknowledge(delivery);
            await delivery.DisposeAsync(); throw new DatabaseOperationException(DatabaseErrorCode.CommitUncertain);
        }
        private sealed class NoAcknowledge(IRecordingConversionDelivery inner) : IRecordingConversionDelivery
        {
            public RecordingArtifactWrite Target => inner.Target; public RecordingConversionWrite Conversion => inner.Conversion;
            public void Acknowledge() => throw new DatabaseOperationException(DatabaseErrorCode.CommitUncertain);
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
internal sealed class RecoveryIdentity(Guid? id) : IWorkstationIdentity
{ public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult(id); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new AssertFailedException("Recovery must not initialize an identity."); }
internal sealed class RecoveryLog : ILogger<RecordingConversionRecovery>
{
    internal List<OperationEvent> Events { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    { Assert.IsNull(exception); Assert.IsTrue(OperationEvents.TryGetEvent(state, out var operation)); Events.Add(operation); }
}
