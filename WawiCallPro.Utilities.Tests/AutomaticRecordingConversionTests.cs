using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class AutomaticRecordingConversionTests
{
    [TestMethod]
    public async Task OptInCreatesOnlyNewJobsAndWorkerCompletesThemOnce()
    { using var database = await WaveArtifactFixture.Create(); await AutomaticRecordingConversionAcceptance.RunAsync(database.Target); }

    [TestMethod]
    [DataRow("default")][DataRow("older")][DataRow("identity")][DataRow("foreign")][DataRow("live")][DataRow("database")]
    public async Task PolicyAndOwnershipExcludeUnapprovedSources(string guard)
    {
        using var file = await WaveArtifactFixture.Create(); await file.Journal().WriteAsync(file.CompletedRecording, file.CompletedCapture);
        var local = guard == "live" ? (await new DapperCallRepository(new(file.Target, new())).GetAsync(file.CompletedRecording.Recording.CallId))!.History.Call.Origin!.WorkstationId
            : await RecordingArtifactAuditAcceptance.EndAsync(file);
        var (store, active) = await AutomaticRecordingConversionAcceptance.Settings(file);
        var snapshot = await store.LoadAsync();
        await store.SaveAsync(snapshot.Options with { AutomaticMp3 = new() { Enabled = guard != "default", SinceUtc = guard == "older" ? DateTimeOffset.UtcNow.AddDays(1) : DateTimeOffset.UnixEpoch },
            Database = guard == "database" ? snapshot.Options.Database with { SqliteFileName = "another.db" } : snapshot.Options.Database }, snapshot);
        var service = AutomaticRecordingConversionAcceptance.Service(file, store, active, guard == "identity" ? null : guard == "foreign" ? Guid.NewGuid() : local);
        if (guard == "database") await Assert.ThrowsExactlyAsync<RecordingOutboxException>(() => service.PrepareAsync());
        else Assert.AreEqual(0, await service.PrepareAsync());
        Assert.IsNull(await new DapperRecordingConversionRepository(new(file.Target, new())).GetForSourceAsync(file.Id));
    }

    [TestMethod]
    public async Task FailedExistingJobIsNeverAutomaticallyRequeued()
    {
        using var ready = await Mp3AttemptFixture.Create();
        await using (var session = await ready.Factory(new AttemptConverter { Failure = "error" }, ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id)) await session.RunAsync();
        var before = await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id);
        var (store, active) = await AutomaticRecordingConversionAcceptance.Settings(ready.File); var current = await store.LoadAsync();
        await store.SaveAsync(current.Options with { AutomaticMp3 = new() { Enabled = true, SinceUtc = DateTimeOffset.UnixEpoch } }, current);
        Assert.AreEqual(0, await AutomaticRecordingConversionAcceptance.Service(ready.File, store, active, ready.Workstation).PrepareAsync());
        Assert.AreEqual(before, await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id));
    }

    [TestMethod]
    [DataRow("missing")][DataRow("changed")][DataRow("destination")]
    public async Task UnprovenFilesRemainUntouchedAndAreReported(string fault)
    {
        using var file = await WaveArtifactFixture.Create(); await file.Journal().WriteAsync(file.CompletedRecording, file.CompletedCapture);
        var local = await RecordingArtifactAuditAcceptance.EndAsync(file); var (store, active) = await AutomaticRecordingConversionAcceptance.Settings(file);
        var snapshot = await store.LoadAsync(); await store.SaveAsync(snapshot.Options with { AutomaticMp3 = new() { Enabled = true, SinceUtc = DateTimeOffset.UnixEpoch } }, snapshot);
        if (fault == "missing") File.Delete(file.Wave);
        if (fault == "changed") { var bytes = File.ReadAllBytes(file.Wave); bytes[^1] ^= 1; File.WriteAllBytes(file.Wave, bytes); }
        if (fault == "destination") File.WriteAllBytes(Path.ChangeExtension(file.Wave, ".mp3"), [1, 2, 3]);
        Assert.AreEqual(1, await AutomaticRecordingConversionAcceptance.Service(file, store, active, local).PrepareAsync());
        Assert.IsNull(await new DapperRecordingConversionRepository(new(file.Target, new())).GetForSourceAsync(file.Id));
        if (fault == "destination") CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.ChangeExtension(file.Wave, ".mp3")));
    }

    [TestMethod]
    public async Task SharedSignalsWakeTheOwnedWorkerWithoutAContainerCycle()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var signals = new RecordingConversionSignals();
        await using var worker = new RecordingConversionQueue(new SignalledBatch(entered), Microsoft.Extensions.Logging.Abstractions.NullLogger<RecordingConversionQueue>.Instance, signals);
        await worker.StartAsync(default); signals.Request(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); await worker.StopAsync(default);
    }
    private sealed class SignalledBatch(TaskCompletionSource entered) : IRecordingConversionBatch
    { public Task<RecordingConversionBatchResult> RunAsync(CancellationToken token = default) { entered.TrySetResult(); return Task.FromResult(new RecordingConversionBatchResult(0, 0, 0)); } }

    [TestMethod]
    public async Task CompletionSignalsOnlyAfterAcknowledgementAndReleaseOfTheRecordingLease()
    {
        using var file = await WaveArtifactFixture.Create(); var signalled = 0;
        var signal = new RecordingConversionRequestTests.RequestSignal(() =>
        {
            Assert.AreEqual(0, file.Outbox.GetPending().Count);
            var lease = file.Outbox.AcquireInspection(file.Id); lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            using var writable = File.Open(file.Wave, FileMode.Open, FileAccess.Write, FileShare.None); signalled++;
        });
        var journal = new DurableRecordingJournal(new(new(file.Target, new())), file.Outbox, new RecordingWaveArtifactPublisher(file.Artifacts), signal);
        await journal.WriteAsync(file.CompletedRecording, file.CompletedCapture); Assert.AreEqual(1, signalled);
    }

    [TestMethod]
    public void AutomaticOptInRequiresUtcAndAnExplicitBoundary()
    {
        var validator = new ApplicationOptionsValidator(new WindowsUserSecretProtector());
        validator.Validate(new());
        foreach (var policy in new[] { new AutomaticMp3Settings { Enabled = true }, new AutomaticMp3Settings { Enabled = true, SinceUtc = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(2)) } })
            Assert.ThrowsExactly<ConfigurationException>(() => validator.Validate(new() { AutomaticMp3 = policy }));
    }

    [TestMethod]
    public async Task OldSettingsRemainDisabledAndPortableImportDoesNotGrantFileProcessing()
    {
        using var file = await WaveArtifactFixture.Create(); var (store, _) = await AutomaticRecordingConversionAcceptance.Settings(file);
        var path = Path.Combine(file.Paths.RootDirectory, "preferences.json");
        var snapshot = await store.LoadAsync(); await store.SaveAsync(snapshot.Options with { AutomaticMp3 = new() { Enabled = true, SinceUtc = DateTimeOffset.UnixEpoch } }, snapshot);
        await store.ExportPortableAsync(path); Assert.DoesNotContain("automaticMp3", File.ReadAllText(path));
        snapshot = await store.LoadAsync(); await store.SaveAsync(snapshot.Options with { AutomaticMp3 = new() }, snapshot);
        var imported = await store.ImportPortableAsync(path, await store.LoadAsync()); Assert.IsFalse(imported.Options.AutomaticMp3.Enabled);
        var settingsFile = new ConfigurationLocation(file.Paths.RootDirectory).FilePath;
        await File.WriteAllTextAsync(settingsFile, "{\"formatVersion\":10,\"settings\":{}}");
        var legacy = await store.LoadAsync(); Assert.AreEqual(10, legacy.FormatVersion); Assert.IsTrue(legacy.RequiresUpgrade); Assert.IsFalse(legacy.Options.AutomaticMp3.Enabled);
    }
}

public static class AutomaticRecordingConversionAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        using var file = await WaveArtifactFixture.Create(target); await file.Journal().WriteAsync(file.CompletedRecording, file.CompletedCapture);
        var local = await RecordingArtifactAuditAcceptance.EndAsync(file); var (store, active) = await Settings(file);
        var automatic = Service(file, store, active, local);
        Assert.AreEqual(0, await automatic.PrepareAsync());
        var snapshot = await store.LoadAsync(); await store.SaveAsync(snapshot.Options with { AutomaticMp3 = new() { Enabled = true, SinceUtc = DateTimeOffset.UnixEpoch } }, snapshot);
        Assert.AreEqual(0, await automatic.PrepareAsync());
        var jobs = new DapperRecordingConversionRepository(new(file.Target, new())); var created = (await jobs.GetForSourceAsync(file.Id))!;
        var targetArtifact = (await file.Artifacts.GetAsync(created.Job.TargetArtifactId))!;
        using var ready = new Mp3AttemptFixture(file, local, new(targetArtifact, created)); var converter = new AttemptConverter();
        var batch = new RecordingConversionBatch(jobs, new Identity(local), ready.Factory(converter, local), automatic);
        Assert.AreEqual(new RecordingConversionBatchResult(1, 0, 0), await batch.RunAsync());
        Assert.AreEqual(new RecordingConversionBatchResult(0, 0, 0), await batch.RunAsync()); Assert.AreEqual(1, converter.Calls);
        snapshot = await store.LoadAsync(); await store.SaveAsync(snapshot.Options with { AutomaticMp3 = new() }, snapshot);
        Assert.AreEqual(0, await automatic.PrepareAsync()); Assert.AreEqual(RecordingConversionState.Completed, (await jobs.GetAsync(created.Job.Id))!.Job.State);
    }
    internal static async Task<(ConfigurationStore, ApplicationConfigurationService)> Settings(WaveArtifactFixture file)
    {
        var store = new ConfigurationStore(new(file.Paths.RootDirectory), new ApplicationOptionsValidator(new WindowsUserSecretProtector()));
        var active = new ApplicationConfigurationService(store); await active.StartAsync(default); return (store, active);
    }
    internal static AutomaticRecordingConversions Service(WaveArtifactFixture file, ConfigurationStore store, ApplicationConfigurationService active, Guid? local) =>
        new(store, active, new Identity(local), new DapperRecordingArtifactAuditQuery(new(file.Target, new())), new DapperRecordingConversionRepository(new(file.Target, new())), file.Captures,
            RecordingConversionRequestAcceptance.Service(file, local, new RecordingConversionRequestTests.RequestSignal()));
    private sealed class Identity(Guid? id) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult(id); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new AssertFailedException(); }
}
