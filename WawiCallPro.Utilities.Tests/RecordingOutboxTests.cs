using System.Text;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingOutboxTests
{
    [TestMethod]
    public async Task ProtectedEntrySurvivesARecreatedStoreAndKeepsTheWaveReadLockedUntilAcknowledged()
    {
        using var fixture = await Fixture.Create();
        await using (var staged = await fixture.Outbox.StageAsync(fixture.Recording, fixture.Capture))
        {
            Assert.AreEqual(1, fixture.Outbox.GetPending().Count);
            var encrypted = File.ReadAllText(fixture.Entry);
            Assert.IsFalse(encrypted.Contains("captured.wav", StringComparison.Ordinal));
            Assert.IsFalse(encrypted.Contains(fixture.Id.ToString(), StringComparison.Ordinal));
            Assert.ThrowsExactly<IOException>(() => File.Open(fixture.Wave, FileMode.Open, FileAccess.Write, FileShare.ReadWrite));
            await Assert.ThrowsExactlyAsync<RecordingOutboxException>(() => fixture.NewStore().OpenAsync(fixture.Id));
        }
        await using var loaded = await fixture.NewStore().OpenAsync(fixture.Id);
        Assert.AreEqual(fixture.Recording, loaded.Recording); Assert.AreEqual(fixture.Capture, loaded.Capture);
        Assert.AreEqual(new FileInfo(fixture.Wave).Length, loaded.Wave!.Length);
        loaded.Acknowledge(); Assert.AreEqual(0, fixture.Outbox.GetPending().Count); Assert.IsTrue(File.Exists(fixture.Wave));
    }

    [TestMethod]
    public async Task ExactRestagingRetainsTheEncryptedEntryAndRejectsAChangedRequest()
    {
        using var fixture = await Fixture.Create(); await using (await fixture.Outbox.StageAsync(fixture.Recording, fixture.Capture)) { }
        var original = File.ReadAllBytes(fixture.Entry);
        await using (await fixture.NewStore().StageAsync(fixture.Recording, fixture.Capture)) { }
        CollectionAssert.AreEqual(original, File.ReadAllBytes(fixture.Entry));
        await Assert.ThrowsExactlyAsync<RecordingOutboxException>(() => fixture.Outbox.StageAsync(
            new(fixture.Recording.Recording, 1, fixture.Recording.ChangedAtUtc.AddTicks(1)), new(fixture.Capture.Capture, 1, fixture.Capture.ChangedAtUtc.AddTicks(1))));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(fixture.Entry));
    }

    [TestMethod]
    [DataRow("changed-wave")]
    [DataRow("missing-wave")]
    [DataRow("damaged-entry")]
    [DataRow("wrong-edition")]
    [DataRow("wrong-database")]
    public async Task UnverifiableDataIsRetainedAndNeverLoadedForDelivery(string problem)
    {
        using var fixture = await Fixture.Create(); await using (await fixture.Outbox.StageAsync(fixture.Recording, fixture.Capture)) { }
        if (problem == "changed-wave") { var data = File.ReadAllBytes(fixture.Wave); data[^1] ^= 1; File.WriteAllBytes(fixture.Wave, data); }
        if (problem == "missing-wave") File.Delete(fixture.Wave);
        if (problem == "damaged-entry") File.WriteAllText(fixture.Entry, "private-content");
        var store = fixture.NewStore(problem == "wrong-edition" ? "FinOffice" : "Pro");
        if (problem == "wrong-database")
        {
            await fixture.Store.SaveAsync(new() { Database = new() { SqliteFileName = "another.db" } }, await fixture.Store.LoadAsync());
            var config = new ApplicationConfigurationService(fixture.Store); await config.StartAsync(CancellationToken.None);
            store = fixture.NewStore(configuration: config);
        }
        var error = await Assert.ThrowsExactlyAsync<RecordingOutboxException>(() => store.OpenAsync(fixture.Id));
        Assert.IsNull(error.InnerException); Assert.IsFalse(error.ToString().Contains("private-content", StringComparison.Ordinal));
        Assert.IsTrue(File.Exists(fixture.Entry));
    }

    [TestMethod]
    public async Task FailedCaptureDeliveryNeedsNoWaveAndDoesNotInventAnEnd()
    {
        using var fixture = await Fixture.Create(); File.Delete(fixture.Wave);
        var failed = new RecordingCapture(fixture.Id, fixture.Capture.Capture.CallId, fixture.Capture.Capture.StartedAtUtc, null,
            PcmRecordingState.Failed, 8000, 2, failure: RecordingCaptureFailure.FileFailure);
        var recording = new RecordingWrite(fixture.Recording.Recording with { Availability = RecordingAvailability.Failed, Duration = null }, 1, fixture.Recording.ChangedAtUtc);
        var capture = new RecordingCaptureWrite(failed, 1, fixture.Capture.ChangedAtUtc);
        await using (await fixture.Outbox.StageAsync(recording, capture)) { }
        await using var loaded = await fixture.NewStore().OpenAsync(fixture.Id);
        Assert.IsNull(loaded.Wave); Assert.IsNull(loaded.Capture.Capture.EndedAtUtc); loaded.Acknowledge();
    }

    [TestMethod]
    public async Task ReadOnlyAcknowledgementFailureAndCancellationDoNotDiscardAnEntry()
    {
        using var fixture = await Fixture.Create(); await using (await fixture.Outbox.StageAsync(fixture.Recording, fixture.Capture)) { }
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Outbox.OpenAsync(fixture.Id, new CancellationToken(true)));
        await using (var loaded = await fixture.Outbox.OpenAsync(fixture.Id))
        {
            File.SetAttributes(fixture.Entry, FileAttributes.ReadOnly);
            try { Assert.ThrowsExactly<RecordingOutboxException>(loaded.Acknowledge); Assert.IsTrue(File.Exists(fixture.Entry)); }
            finally { File.SetAttributes(fixture.Entry, FileAttributes.Normal); }
        }
        await using var retry = await fixture.NewStore().OpenAsync(fixture.Id); retry.Acknowledge();
    }

    [TestMethod]
    public async Task InvalidNamesAndOversizedEntriesAreNotSilentlySkipped()
    {
        using var fixture = await Fixture.Create(); await using (await fixture.Outbox.StageAsync(fixture.Recording, fixture.Capture)) { }
        var invalid = Path.Combine(Path.GetDirectoryName(fixture.Entry)!, "unknown.pending"); File.WriteAllText(invalid, "private-content");
        Assert.ThrowsExactly<RecordingOutboxException>(() => fixture.Outbox.GetPending()); File.Delete(invalid);
        File.WriteAllBytes(fixture.Entry, new byte[65537]);
        await Assert.ThrowsExactlyAsync<RecordingOutboxException>(() => fixture.Outbox.OpenAsync(fixture.Id)); Assert.IsTrue(File.Exists(fixture.Entry));
    }

    [TestMethod]
    public async Task APrecanceledStageCreatesNeitherDirectoryNorEntry()
    {
        using var fixture = await Fixture.Create();
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Outbox.StageAsync(fixture.Recording, fixture.Capture, new CancellationToken(true)));
        Assert.AreEqual(0, fixture.Outbox.GetPending().Count); Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(fixture.Entry)));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "wcp-recording-outbox-" + Guid.NewGuid().ToString("N"));
        private readonly WindowsUserSecretProtector _protector = new();
        private ApplicationConfigurationService _configuration = null!;
        internal ConfigurationStore Store { get; }
        internal ApplicationPaths Paths { get; }
        internal Guid Id { get; } = Guid.NewGuid();
        internal string Wave => Path.Combine(Paths.RecordingsDirectory, "captured.wav");
        internal string Entry => Path.Combine(_root, "recording-pending", Id.ToString("N") + ".pending");
        internal RecordingWrite Recording { get; }
        internal RecordingCaptureWrite Capture { get; }
        internal ProtectedRecordingJournalOutbox Outbox { get; private set; } = null!;
        private Fixture()
        {
            var location = new ConfigurationLocation(_root); Paths = new(location); Store = new(location, new ApplicationOptionsValidator(_protector), null);
            var start = DateTimeOffset.UtcNow.AddMinutes(-1); var call = Guid.NewGuid();
            var capture = new RecordingCapture(Id, call, start, start.AddMilliseconds(20), PcmRecordingState.Completed, 8000, 2, 160);
            Recording = new(new(Id, call, "captured.wav", "wav", capture.Duration, RecordingAvailability.Available), 1, start.AddSeconds(1));
            Capture = new(capture, 1, Recording.ChangedAtUtc);
        }
        internal static async Task<Fixture> Create()
        {
            var result = new Fixture(); await result.Store.SaveAsync(new()); result._configuration = new(result.Store);
            await result._configuration.StartAsync(CancellationToken.None); result.Outbox = result.NewStore();
            Directory.CreateDirectory(result.Paths.RecordingsDirectory);
            using var writer = new BinaryWriter(File.Create(result.Wave), Encoding.ASCII, leaveOpen: false);
            writer.Write(0x46464952U); writer.Write(36 + 640); writer.Write(0x45564157U); writer.Write(0x20746d66U); writer.Write(16);
            writer.Write((ushort)1); writer.Write((ushort)2); writer.Write(8000); writer.Write(32000); writer.Write((ushort)4); writer.Write((ushort)16);
            writer.Write(0x61746164U); writer.Write(640); writer.Write(new byte[640]);
            return result;
        }
        internal ProtectedRecordingJournalOutbox NewStore(string line = "Pro", ApplicationConfigurationService? configuration = null) =>
            new(Paths, _protector, configuration ?? _configuration, new ApplicationDescriptor("WawiCallPro", line));
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
}
