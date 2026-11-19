using System.Security.Cryptography;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class RecordingExportTests
{
    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task ConfirmedOriginalIsExportedWithoutChangingMetadata(bool mp3)
    {
        using var file = await WaveArtifactFixture.Create(); await file.Journal().WriteAsync(file.CompletedRecording, file.CompletedCapture);
        var local = await RecordingArtifactAuditAcceptance.EndAsync(file);
        var artifact = (await file.Artifacts.GetForRecordingAsync(file.Id)).Single();
        if (mp3)
        {
            // Export verifies confirmed bytes; encoding/decoding is tested by the native MP3 suite.
            File.WriteAllBytes(file.Paths.ResolveRecordingPath("confirmed.mp3"), [1, 2, 3]);
            var pending = new RecordingArtifact(Guid.NewGuid(), file.Id, artifact.Artifact.CallId, RecordingArtifactFormat.Mp3, "confirmed.mp3", RecordingAvailability.Pending);
            var added = await file.Artifacts.WriteAsync(new(pending, null, file.Changed));
            artifact = await file.Artifacts.WriteAsync(new(new RecordingArtifact(pending.Id, pending.RecordingId, pending.CallId, pending.Format, pending.RelativePath,
                RecordingAvailability.Available, 3, Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3 })).ToLowerInvariant()), added.Version, file.Changed));
        }
        using var destination = new ExportFolder(); var target = Path.Combine(destination.Path, mp3 ? "export.mp3" : "export.wav");
        var parent = await file.Parent.GetAsync(file.Id); var capture = await file.Captures.GetAsync(file.Id);
        await Service(file, local).ExportAsync(artifact.Artifact.Id, target);
        CollectionAssert.AreEqual(File.ReadAllBytes(file.Paths.ResolveRecordingPath(artifact.Artifact.RelativePath)), File.ReadAllBytes(target));
        Assert.AreEqual(artifact, await file.Artifacts.GetAsync(artifact.Artifact.Id)); Assert.AreEqual(parent, await file.Parent.GetAsync(file.Id)); Assert.AreEqual(capture, await file.Captures.GetAsync(file.Id));
        Assert.HasCount(1, Directory.GetFiles(destination.Path));
    }

    [TestMethod]
    [DataRow("foreign")][DataRow("identity")][DataRow("live")][DataRow("changed")][DataRow("missing")]
    [DataRow("existing")][DataRow("internal")][DataRow("extension")][DataRow("locked")][DataRow("pending")][DataRow("relative")]
    public async Task InvalidSourcesAndTargetsNeverPublishOrOverwrite(string guard)
    {
        using var file = await WaveArtifactFixture.Create(); await file.Journal().WriteAsync(file.CompletedRecording, file.CompletedCapture);
        var local = guard == "live" ? (await new DapperCallRepository(new(file.Target, new())).GetAsync(file.CompletedRecording.Recording.CallId))!.History.Call.Origin!.WorkstationId
            : await RecordingArtifactAuditAcceptance.EndAsync(file);
        if (guard == "foreign") local = Guid.NewGuid();
        var artifact = (await file.Artifacts.GetForRecordingAsync(file.Id)).Single();
        using var destination = new ExportFolder(); var target = Path.Combine(destination.Path, "export.wav");
        if (guard == "changed") File.WriteAllBytes(file.Wave, [7, 8]);
        if (guard == "missing") File.Delete(file.Wave);
        if (guard == "existing") File.WriteAllBytes(target, [9]);
        if (guard == "internal") target = file.Paths.ResolveRecordingPath("export.wav");
        if (guard == "extension") target = Path.Combine(destination.Path, "export.mp3");
        if (guard == "relative") target = "relative-export.wav";
        if (guard == "pending") { await using var staged = await file.Outbox.StageAsync(file.CompletedRecording, file.CompletedCapture); }
        using var locked = guard == "locked" ? File.Open(file.Wave, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        try { await Service(file, guard == "identity" ? null : local).ExportAsync(artifact.Artifact.Id, target); Assert.Fail("Export should fail."); }
        catch (Exception error) when (error is RecordingOutboxException or IOException) { }
        if (guard == "existing") CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(target)); else Assert.IsFalse(File.Exists(target));
        Assert.AreEqual(artifact, await file.Artifacts.GetAsync(artifact.Artifact.Id));
        Assert.IsFalse(Directory.EnumerateFiles(destination.Path, "*.tmp").Any());
    }

    [TestMethod]
    [DataRow("cancel")][DataRow("revision")][DataRow("target")]
    public async Task LateChangesDiscardOnlyTheOwnedTemporaryFile(string change)
    {
        using var file = await WaveArtifactFixture.Create(); await file.Journal().WriteAsync(file.CompletedRecording, file.CompletedCapture);
        var local = await RecordingArtifactAuditAcceptance.EndAsync(file); var artifact = (await file.Artifacts.GetForRecordingAsync(file.Id)).Single();
        using var destination = new ExportFolder(); var target = Path.Combine(destination.Path, "export.wav"); using var cancellation = new CancellationTokenSource();
        var repository = new LateRead(file.Artifacts, async () =>
        {
            Assert.IsTrue(Directory.EnumerateFiles(destination.Path, "*.tmp").Any());
            Assert.ThrowsExactly<IOException>(() => { using var ignored = File.Open(file.Wave, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });
            if (change == "cancel") cancellation.Cancel();
            if (change == "target") File.WriteAllBytes(target, [9]);
            if (change == "revision")
            {
                var original = artifact.Artifact;
                await file.Artifacts.WriteAsync(new(new RecordingArtifact(original.Id, original.RecordingId, original.CallId, original.Format, original.RelativePath,
                    RecordingAvailability.Missing, original.ByteLength, original.Sha256), artifact.Version, DateTimeOffset.UtcNow));
            }
        });
        try { await Service(file, local, repository).ExportAsync(artifact.Artifact.Id, target, cancellation.Token); Assert.Fail("Late change should prevent publication."); }
        catch (Exception error) when (error is RecordingOutboxException or OperationCanceledException) { }
        if (change == "target") CollectionAssert.AreEqual(new byte[] { 9 }, File.ReadAllBytes(target)); else Assert.IsFalse(File.Exists(target));
        Assert.IsFalse(Directory.EnumerateFiles(destination.Path, "*.tmp").Any());
    }

    private static RecordingExport Service(WaveArtifactFixture file, Guid? local, IRecordingArtifactRepository? artifacts = null) => new(
        artifacts ?? file.Artifacts, new DapperCallRepository(new(file.Target, new())), new Identity(local), file.Outbox,
        file.ConversionOutbox(), new DapperRecordingConversionRepository(new(file.Target, new())),
        new RecordingArtifactFileInspector(file.Paths, new LocalRecordingFileProbe(file.Paths)), file.Paths);
    private sealed class Identity(Guid? id) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult(id); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new AssertFailedException(); }
    private sealed class LateRead(IRecordingArtifactRepository inner, Func<Task> action) : IRecordingArtifactRepository
    {
        private int _reads;
        public async Task<StoredRecordingArtifact?> GetAsync(Guid id, CancellationToken token = default)
        { if (++_reads == 2) await action(); return await inner.GetAsync(id, token); }
        public Task<IReadOnlyList<StoredRecordingArtifact>> GetForRecordingAsync(Guid id, CancellationToken token = default) => inner.GetForRecordingAsync(id, token);
        public Task<StoredRecordingArtifact> WriteAsync(RecordingArtifactWrite write, CancellationToken token = default) => inner.WriteAsync(write, token);
    }
    private sealed class ExportFolder : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wcp-export-" + Guid.NewGuid().ToString("N"));
        internal ExportFolder() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            var resolved = System.IO.Path.GetFullPath(Path);
            if (!resolved.StartsWith(System.IO.Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) || !System.IO.Path.GetFileName(resolved).StartsWith("wcp-export-", StringComparison.Ordinal)) throw new AssertFailedException();
            Directory.Delete(resolved, true);
        }
    }
}
