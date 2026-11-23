using CSCore;
using CSCore.Codecs.WAV;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Console.Tests;

public sealed partial class SentRtpPcmTests
{
    [TestMethod]
    [DataRow("normal")]
    [DataRow("staged")]
    [DataRow("published")]
    [DataRow("conflict")]
    [DataRow("tampered")]
    [DataRow("version")]
    [DataRow("foreign")]
    public async Task PreparedWaveIsRecoveredOnlyFromAProtectedMixAndLocalMatchingJournal(string state)
    {
        await using var fixture = new Fixture();
        await fixture.Connect();
        using var files = new PersistedFiles();
        var request = await files.Initialize(fixture);
        var outbox = await Pending(files);
        var interrupted = state != "normal";
        var media = files.Media(fixture,
            new DurableRecordingJournal(Backend(new SwitchTarget(files.Target)), interrupted ? new FailBeforePublishing(outbox) : outbox, Publisher(files)),
            outbox);
        try
        {
            var recording = await media.StartRecordingAsync(request);
            fixture.Capture(Samples(fixture.Format.ClockRate / 50, 0));
            await fixture.Packets(1);
            if (interrupted) await Assert.ThrowsExactlyAsync<RecordingPersistenceException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(40)));
            else await recording.StopAsync(TimeSpan.FromMilliseconds(40));
        }
        finally { try { await media.DisposeAsync(); } catch (RecordingPersistenceException) when (interrupted) { } }

        if (!interrupted)
        {
            Assert.IsTrue(File.Exists(files.Output));
            Assert.AreEqual(0, outbox.GetPending().Count);
            Assert.AreEqual(RecordingAvailability.Available, (await files.Artifacts()).Single().Artifact.Availability);
            return;
        }

        Assert.IsFalse(File.Exists(files.Output));
        Assert.AreEqual(1, outbox.GetPending().Count);
        var prepared = Directory.GetFiles(files.Paths.RecordingsDirectory, ".wawi-mix-*.tmp").Single();
        byte[] unrelated = [4, 2, 7];
        var orphan = Path.Combine(files.Paths.RecordingsDirectory, "unrelated.wav");
        File.WriteAllBytes(orphan, unrelated);
        if (state == "conflict") File.WriteAllBytes(files.Output, unrelated);
        if (state == "tampered")
        {
            var bytes = File.ReadAllBytes(prepared);
            bytes[^1] ^= 1;
            File.WriteAllBytes(prepared, bytes);
        }
        var expected = File.ReadAllBytes(prepared);
        if (state == "published") File.Move(prepared, files.Output);
        var calls = new DapperCallRepository(new(files.Target, new()));
        var origin = (await calls.GetAsync(request.Recording.CallId))!.History.Call.Origin!;
        var recovery = Recovery(files, await Pending(files, false), new Identity(state == "foreign" ? Guid.NewGuid() : origin.WorkstationId));
        if (state is "staged" or "published")
            Assert.AreEqual(new RecordingJournalRecoveryResult(0, 1, 0), await recovery.RecoverAsync());
        await new SipCallPersistence(calls, new Origin(origin), TimeProvider.System).RecoverAsync();
        if (state == "published")
        {
            var context = new DatabaseRepositoryContext(files.Target, new());
            var inventory = new RecordingDirectoryInventory(new DapperRecordingFileCatalogQuery(context), new Identity(origin.WorkstationId),
                new LocalRecordingDirectoryEntries(files.Paths), files.Paths, new DapperInterruptedRecordingQuery(context));
            var review = await inventory.ReadAsync();
            Assert.IsTrue(review.Review.Any(entry => entry.RelativePath == "captured.wav" && entry.AwaitingConfirmation));
        }
        if (state == "version")
        {
            var old = (await files.Recording())!;
            await new DapperRecordingRepository(new(files.Target, new())).WriteAsync(new(old.Recording, old.Version, old.UpdatedAtUtc.AddSeconds(1)));
        }
        var result = await recovery.RecoverAsync();
        if (state is "staged" or "published")
        {
            Assert.AreEqual(new RecordingJournalRecoveryResult(1, 0, 0), result);
            Assert.IsFalse(File.Exists(prepared));
            CollectionAssert.AreEqual(expected, File.ReadAllBytes(files.Output));
            Assert.AreEqual(2L, (await files.Capture())!.Version);
            Assert.AreEqual(2L, (await files.Artifacts()).Single().Version);
            Assert.AreEqual(new RecordingJournalRecoveryResult(0, 0, 0), await recovery.RecoverAsync());
        }
        else
        {
            Assert.AreEqual(0, result.Delivered);
            Assert.AreEqual(1, result.Deferred + result.Failed);
            CollectionAssert.AreEqual(expected, File.ReadAllBytes(prepared));
            if (state == "conflict") CollectionAssert.AreEqual(unrelated, File.ReadAllBytes(files.Output));
            else Assert.IsFalse(File.Exists(files.Output));
            Assert.AreEqual(PcmRecordingState.Capturing, (await files.Capture())!.Capture.State);
            Assert.AreEqual(0, (await files.Artifacts()).Count);
            Assert.AreEqual(1, outbox.GetPending().Count);
        }
        CollectionAssert.AreEqual(unrelated, File.ReadAllBytes(orphan));
    }

    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA, false)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU, false)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722, false)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA, true)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU, true)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722, true)]
    public async Task FreshRecoveryDeliversActualWaveMetadataAfterCallRecoveryWithoutRecordingAgain(SDPWellKnownMediaFormatsEnum codec, bool committed)
    {
        await using var fixture = new Fixture(codec); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        var outbox = await Pending(files); var targets = new SwitchTarget(files.Target);
        var backend = Backend(targets); IRecordingJournalOutbox publishing = committed ? new FailedAcknowledgement(outbox) : outbox;
        var media = files.Media(fixture, new DurableRecordingJournal(backend, publishing, Publisher(files)));
        try
        {
            var recording = await media.StartRecordingAsync(request); fixture.Capture(Samples(fixture.Format.ClockRate / 50, 0)); await fixture.Packets(1);
            if (!committed) targets.Current = DatabaseTarget.Sqlite(Path.Combine(files.Paths.RootDirectory, "not-created.db"));
            await Assert.ThrowsExactlyAsync<RecordingPersistenceException>(() => recording.StopAsync(TimeSpan.FromMilliseconds(40)));
        }
        finally { try { await media.DisposeAsync(); } catch (RecordingPersistenceException) { } }
        var before = File.ReadAllBytes(files.Output); var calls = new DapperCallRepository(new(files.Target, new()));
        var call = (await calls.GetAsync(request.Recording.CallId))!.History.Call; var origin = call.Origin!;
        var recovered = Recovery(files, await Pending(files, createSettings: false), new Identity(origin.WorkstationId));
        Assert.AreEqual(new RecordingJournalRecoveryResult(0, 1, 0), await recovered.RecoverAsync());
        var recoveredCalls = new InterruptedRecordingSipPersistence(
            new RecordingRecoverySipPersistence(new SipCallPersistence(calls, new Origin(origin), TimeProvider.System), recovered),
            Interrupted(files, outbox, origin.WorkstationId));
        var requested = false;
        var conversionsRequested = 0;
        var callRecovery = new ReconciledRecordingSipPersistence(recoveredCalls, new AuditRequest(() => { Assert.AreEqual(0, outbox.GetPending().Count); requested = true; }),
            new ConversionRequest(() => { Assert.IsTrue(requested); Assert.AreEqual(0, outbox.GetPending().Count); conversionsRequested++; }));
        await callRecovery.RecoverAsync();
        Assert.IsTrue(requested);
        Assert.AreEqual(1, conversionsRequested);
        await callRecovery.SaveAsync((await calls.GetAsync(call.Id))!.History.Call);
        Assert.AreEqual(2, conversionsRequested);
        Assert.AreEqual(CallState.Failed, (await calls.GetAsync(call.Id))!.History.Call.State);
        Assert.AreEqual(CallEndReason.Interrupted, (await calls.GetAsync(call.Id))!.History.Call.EndReason);
        Assert.AreEqual(PcmRecordingState.Completed, (await files.Capture())!.Capture.State);
        Assert.AreEqual(2L, (await files.Capture())!.Version); Assert.AreEqual(2L, (await files.Recording())!.Version);
        Assert.AreEqual(RecordingAvailability.Available, (await files.Recording())!.Recording.Availability);
        var artifact = (await files.Artifacts()).Single();
        Assert.AreEqual(request.Recording.RecordingId, artifact.Artifact.Id); Assert.AreEqual(2L, artifact.Version);
        Assert.AreEqual(RecordingArtifactFormat.Wave, artifact.Artifact.Format); Assert.AreEqual(RecordingAvailability.Available, artifact.Artifact.Availability);
        Assert.AreEqual(before.LongLength, artifact.Artifact.ByteLength);
        Assert.AreEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(before)).ToLowerInvariant(), artifact.Artifact.Sha256);
        Assert.AreEqual(0, outbox.GetPending().Count); CollectionAssert.AreEqual(before, File.ReadAllBytes(files.Output));
        Assert.AreEqual(1, fixture.Codec.Encodes); Assert.AreEqual(new RecordingJournalRecoveryResult(0, 0, 0), await recovered.RecoverAsync());
    }

    [TestMethod]
    [DataRow("foreign-workstation")]
    [DataRow("missing-identity")]
    [DataRow("changed-wave")]
    [DataRow("missing-wave")]
    [DataRow("invalid-header")]
    [DataRow("version-conflict")]
    public async Task RecoveryRetainsUnverifiableOrUnownedEntriesWithoutChangingDatabaseMetadata(string blocked)
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        var outbox = await Pending(files); var direct = new DapperRecordingJournal(new(files.Target, new()));
        var epoch = request.RecordingEpochUtc!.Value; var active = new RecordingCapture(request.Recording.RecordingId, request.Recording.CallId, epoch, null, PcmRecordingState.Capturing, 8000, 2);
        var pending = new Recording(request.Recording.RecordingId, request.Recording.CallId, "captured.wav", "wav", null, RecordingAvailability.Pending);
        var changed = DateTimeOffset.UtcNow; await direct.BeginAsync(new(pending, null, changed), new(active, null, changed));
        Directory.CreateDirectory(files.Paths.RecordingsDirectory);
        using (var writer = new WaveWriter(files.Output, new WaveFormat(blocked == "invalid-header" ? 16000 : 8000, 16, 2))) writer.Write(new byte[1280], 0, 1280);
        var complete = new RecordingCapture(pending.Id, pending.CallId, epoch, epoch.AddMilliseconds(40), PcmRecordingState.Completed, 8000, 2, 320);
        var available = pending with { Availability = RecordingAvailability.Available, Duration = complete.Duration };
        await using (await outbox.StageAsync(new(available, 1, changed), new(complete, 1, changed))) { }
        if (blocked == "changed-wave") { var bytes = File.ReadAllBytes(files.Output); bytes[^1] ^= 1; File.WriteAllBytes(files.Output, bytes); }
        if (blocked == "missing-wave") File.Delete(files.Output);
        var calls = new DapperCallRepository(new(files.Target, new())); var call = (await calls.GetAsync(pending.CallId))!;
        var origin = call.History.Call.Origin!;
        await new SipCallPersistence(calls, new Origin(origin), TimeProvider.System).RecoverAsync();
        if (blocked == "version-conflict") await new DapperRecordingRepository(new(files.Target, new())).WriteAsync(new(pending, 1, changed.AddTicks(1)));
        var identity = blocked == "missing-identity" ? null : (Guid?)(blocked == "foreign-workstation" ? Guid.NewGuid() : origin.WorkstationId);
        var log = new RecoveryLog(); var result = await Recovery(files, await Pending(files, false), new Identity(identity), log).RecoverAsync();
        Assert.AreEqual(0, result.Delivered); Assert.AreEqual(1, result.Deferred + result.Failed); Assert.AreEqual(1, outbox.GetPending().Count);
        Assert.AreEqual(RecordingAvailability.Pending, (await files.Recording())!.Recording.Availability);
        Assert.AreEqual(PcmRecordingState.Capturing, (await files.Capture())!.Capture.State);
        if (blocked is not "foreign-workstation") { Assert.IsTrue(log.Warnings > 0); Assert.IsFalse(log.Text.Contains(files.Output, StringComparison.Ordinal)); }
    }

    [TestMethod]
    public async Task AbortedCaptureReceiptNeedsNoWaveAndIsDeliveredAfterCallRecovery()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        var outbox = await Pending(files); var targets = new SwitchTarget(files.Target); var media = files.Media(fixture, new DurableRecordingJournal(Backend(targets), outbox, Publisher(files)));
        var recording = await media.StartRecordingAsync(request); targets.Current = DatabaseTarget.Sqlite(Path.Combine(files.Paths.RootDirectory, "not-created.db"));
        await Assert.ThrowsExactlyAsync<RecordingPersistenceException>(() => media.DisposeAsync().AsTask()); Assert.AreEqual(1, outbox.GetPending().Count);
        var calls = new DapperCallRepository(new(files.Target, new())); var origin = (await calls.GetAsync(request.Recording.CallId))!.History.Call.Origin!;
        await new RecordingRecoverySipPersistence(new SipCallPersistence(calls, new Origin(origin), TimeProvider.System), Recovery(files, await Pending(files, false), new Identity(origin.WorkstationId))).RecoverAsync();
        var capture = (await files.Capture())!.Capture; Assert.AreEqual(PcmRecordingState.Aborted, capture.State);
        Assert.IsNull(capture.EndedAtUtc); Assert.IsNull(capture.Duration); Assert.IsFalse(File.Exists(files.Output)); Assert.AreEqual(0, outbox.GetPending().Count);
    }

    [TestMethod]
    public async Task CanceledRecoveryLeavesTheProtectedEntryAvailableForTheNextAttempt()
    {
        await using var fixture = new Fixture(); await fixture.Connect(); using var files = new PersistedFiles(); var request = await files.Initialize(fixture);
        var outbox = await Pending(files); var epoch = request.RecordingEpochUtc!.Value;
        var failed = new RecordingCapture(request.Recording.RecordingId, request.Recording.CallId, epoch, null, PcmRecordingState.Aborted, 8000, 2, failure: RecordingCaptureFailure.Cancelled);
        var changed = DateTimeOffset.UtcNow;
        await using (await outbox.StageAsync(new(new(failed.RecordingId, failed.CallId, "captured.wav", "wav", null, RecordingAvailability.Failed), 1, changed), new(failed, 1, changed))) { }
        var recovery = Recovery(files, outbox, new Identity(Guid.NewGuid()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => recovery.RecoverAsync(new CancellationToken(true))); Assert.AreEqual(1, outbox.GetPending().Count);
    }

    private static async Task<ProtectedRecordingJournalOutbox> Pending(PersistedFiles files, bool createSettings = true)
    {
        var protector = new WindowsUserSecretProtector(); var settings = new ConfigurationStore(new ConfigurationLocation(files.Paths.RootDirectory), new ApplicationOptionsValidator(protector), null);
        if (createSettings) await settings.SaveAsync(new());
        var configuration = new ApplicationConfigurationService(settings); await configuration.StartAsync(CancellationToken.None);
        return new(files.Paths, protector, configuration, new ApplicationDescriptor("WawiCallPro", "Pro"));
    }
    private static DapperRecordingJournal Backend(SwitchTarget target) => new(new DatabaseRepositoryContext(target, new(), new DatabaseMigrationEngineFactory(), new DatabaseMigrationLock()));
    private static RecordingJournalRecovery Recovery(PersistedFiles files, IRecordingJournalOutbox pending, IWorkstationIdentity identity, ILogger<RecordingJournalRecovery>? log = null) =>
        new(pending, new DapperRecordingJournal(new(files.Target, new())), new DapperCallRepository(new(files.Target, new())), identity,
            log ?? NullLogger<RecordingJournalRecovery>.Instance, Publisher(files),
            new DapperRecordingRepository(new(files.Target, new())), new DapperRecordingCaptureRepository(new(files.Target, new())));
    private static RecordingWaveArtifactPublisher Publisher(PersistedFiles files) => new(new DapperRecordingArtifactRepository(new(files.Target, new())));
    private sealed class AuditRequest(Action request) : IRecordingArtifactAuditScheduler { public void Request() => request(); }
    private sealed class ConversionRequest(Action request) : IRecordingConversionQueue { public void Request() => request(); }
    private sealed class SwitchTarget(DatabaseTarget target) : IDatabaseTargetSource
    { internal DatabaseTarget Current = target; public DatabaseTarget GetTarget() => Current; }
    private sealed class Identity(Guid? id) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(id); public Task<Guid> InitializeAsync(CancellationToken cancellationToken = default) => Task.FromResult(id ?? throw new InvalidOperationException()); }
    private sealed class Origin(CallOrigin origin) : ICallOriginProvider
    { public Task<CallOrigin> CaptureAsync(CancellationToken cancellationToken = default) => Task.FromResult(origin); }
    private sealed class RecoveryLog : ILogger<RecordingJournalRecovery>
    {
        internal int Warnings; internal string Text = "";
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (level >= LogLevel.Warning) Warnings++; Text += formatter(state, exception); Assert.IsNull(exception); }
    }
    private sealed class FailBeforePublishing(IRecordingJournalOutbox inner) : IRecordingJournalOutbox
    {
        public IReadOnlyList<Guid> GetPending() => inner.GetPending();
        public IRecordingArtifactAuditLease AcquireInspection(Guid id) => inner.AcquireInspection(id);
        public Task<IRecordingJournalDelivery> OpenAsync(Guid id, CancellationToken token = default) => inner.OpenAsync(id, token);
        public Task<IRecordingJournalDelivery> StageAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken token = default) =>
            throw new RecordingOutboxException();
    }

    private sealed class FailedAcknowledgement(IRecordingJournalOutbox inner) : IRecordingJournalOutbox
    {
        public IReadOnlyList<Guid> GetPending() => inner.GetPending();
        public IRecordingArtifactAuditLease AcquireInspection(Guid recordingId) => inner.AcquireInspection(recordingId);
        public Task<IRecordingJournalDelivery> OpenAsync(Guid id, CancellationToken token = default) => inner.OpenAsync(id, token);
        public async Task<IRecordingJournalDelivery> StageAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken token = default) => new Delivery(await inner.StageAsync(recording, capture, token));
        private sealed class Delivery(IRecordingJournalDelivery inner) : IRecordingJournalDelivery
        {
            public RecordingWrite Recording => inner.Recording; public RecordingCaptureWrite Capture => inner.Capture; public Stream? Wave => inner.Wave;
            public string? WaveSha256 => inner.WaveSha256;
            public void Acknowledge() => throw new RecordingOutboxException(); public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
