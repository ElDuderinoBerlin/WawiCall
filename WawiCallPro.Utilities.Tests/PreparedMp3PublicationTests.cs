using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class PreparedMp3PublicationTests
{
    [TestMethod]
    public async Task FreshServicesPublishThePreparedOriginalAndDeliverTheFrozenPair()
    { using var database = await WaveArtifactFixture.Create(); await PreparedMp3PublicationAcceptance.RunAsync(database.Target); }

    [TestMethod]
    [DataRow("changed")][DataRow("missing")][DataRow("locked")][DataRow("collision")][DataRow("directory")]
    public async Task UnprovenPreparedFilesRemainUnconfirmedAndAreNeverOverwritten(string guard)
    {
        using var pending = await PreparedMp3.Create(); var ready = pending.Ready;
        var original = File.ReadAllBytes(pending.Temporary); var receipt = File.ReadAllBytes(pending.Entry);
        var job = await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id); var artifact = await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id);
        if (guard == "changed") { var bytes = original.ToArray(); bytes[^1] ^= 1; File.WriteAllBytes(pending.Temporary, bytes); }
        if (guard == "missing") File.Delete(pending.Temporary);
        if (guard == "collision") File.WriteAllBytes(ready.Output, [19, 23]);
        if (guard == "directory") Directory.CreateDirectory(ready.Output);
        using (var locked = guard == "locked" ? File.Open(pending.Temporary, FileMode.Open, FileAccess.Write, FileShare.None) : null)
        {
            Assert.AreEqual(new RecordingConversionRecoveryResult(0, 0, 1), await pending.Recovery().RecoverAsync());
            CollectionAssert.AreEqual(receipt, File.ReadAllBytes(pending.Entry));
            Assert.AreEqual(job, await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id));
            Assert.AreEqual(artifact, await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id));
            if (guard == "collision") CollectionAssert.AreEqual(new byte[] { 19, 23 }, File.ReadAllBytes(ready.Output));
            if (guard != "missing") Assert.IsTrue(File.Exists(pending.Temporary));
        }
        if (guard == "collision") File.Delete(ready.Output);
        if (guard == "directory") Directory.Delete(ready.Output);
        if (guard is "changed" or "missing") File.WriteAllBytes(pending.Temporary, original);
        Assert.AreEqual(new RecordingConversionRecoveryResult(1, 0, 0), await pending.Recovery().RecoverAsync());
        Assert.AreEqual(1, pending.Converter.Calls);
    }

    [TestMethod]
    [DataRow("../.wawi-mp3-00000000000000000000000000000001.tmp")]
    [DataRow("other/.wawi-mp3-00000000000000000000000000000001.tmp")]
    [DataRow(".wawi-mp3-00000000000000000000000000000000.tmp")]
    [DataRow(".wawi-mp3-AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.tmp")]
    [DataRow("unknown.tmp")][DataRow("")][DataRow(null)]
    public async Task InvalidProtectedPreparedPathsAreRejectedWithoutTouchingFiles(string? path)
    {
        using var pending = await PreparedMp3.Create();
        pending.RewritePayload(payload => payload["PreparedRelativePath"] = path);
        Assert.AreEqual(new RecordingConversionRecoveryResult(0, 0, 1), await pending.Recovery().RecoverAsync());
        Assert.IsTrue(File.Exists(pending.Entry)); Assert.IsTrue(File.Exists(pending.Temporary)); Assert.IsFalse(File.Exists(pending.Ready.Output));
    }

    [TestMethod]
    public async Task LegacyPublishedReceiptWithoutPreparedPathIsStillDelivered()
    {
        using var pending = await PreparedMp3.Create(published: true);
        pending.RewritePayload(payload => { payload["Version"] = 1; payload.Remove("PreparedRelativePath"); });
        Assert.AreEqual(new RecordingConversionRecoveryResult(1, 0, 0), await pending.Recovery().RecoverAsync());
        Assert.AreEqual(RecordingConversionState.Completed, (await pending.Ready.Jobs.GetAsync(pending.Ready.Pair.Conversion.Job.Id))!.Job.State);
    }

    [TestMethod]
    public async Task UnknownSiblingFilesAreNeverAdoptedOrDeleted()
    {
        using var pending = await PreparedMp3.Create();
        var unknown = pending.Temporary + ".unknown"; File.Move(pending.Temporary, unknown);
        Assert.AreEqual(new RecordingConversionRecoveryResult(0, 0, 1), await pending.Recovery().RecoverAsync());
        Assert.IsTrue(File.Exists(unknown)); Assert.IsTrue(File.Exists(pending.Entry)); Assert.IsFalse(File.Exists(pending.Ready.Output));
    }

    [TestMethod]
    [DataRow("version")][DataRow("protected-path")][DataRow("attempt")]
    public async Task ChangedCatalogOrProtectedOwnershipPreventsTheRename(string mismatch)
    {
        using var pending = await PreparedMp3.Create(); var ready = pending.Ready;
        if (mismatch == "version") await ready.File.Artifacts.WriteAsync(new(ready.Pair.Target.Artifact, 1, DateTimeOffset.UtcNow));
        if (mismatch == "protected-path") pending.RewritePayload(payload => payload["Target"]!["Artifact"]!["RelativePath"] = "different.mp3");
        if (mismatch == "attempt") pending.RewritePayload(payload => payload["Conversion"]!["Job"]!["AttemptId"] = Guid.NewGuid());
        Assert.AreEqual(new RecordingConversionRecoveryResult(0, 0, 1), await pending.Recovery().RecoverAsync());
        Assert.IsTrue(File.Exists(pending.Temporary)); Assert.IsTrue(File.Exists(pending.Entry));
        Assert.IsFalse(File.Exists(ready.Output)); Assert.IsFalse(File.Exists(Path.Combine(ready.File.Paths.RecordingsDirectory, "different.mp3")));
    }

    [TestMethod]
    public async Task SuccessfulAdapterMustHonorTheDurablePublicationHandoff()
    {
        using var ready = await Mp3AttemptFixture.Create();
        var converter = new AttemptConverter { Handler = (request, _) => Task.FromResult(AttemptConverter.Publish(request with { Publication = null })) };
        await using var owner = await ready.Factory(converter, ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id);
        var result = await owner.RunAsync();
        Assert.AreEqual(RecordingConversionFailure.EncodingFailed, result.Conversion.Job.Failure);
        Assert.AreEqual(RecordingAvailability.Failed, result.Target.Artifact.Availability);
        Assert.IsTrue(File.Exists(ready.Output));
    }
}

/// <summary>Reopens retained files and services at both sides of the rename on every actual database provider.</summary>
public static class PreparedMp3PublicationAcceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        foreach (var published in new[] { false, true })
        {
            using var pending = await PreparedMp3.Create(target, published); var ready = pending.Ready;
            var payload = pending.ReadPayload(); var changed = payload["Target"]!["ChangedAtUtc"]!.GetValue<DateTimeOffset>();
            var receipt = File.ReadAllText(pending.Entry); Assert.DoesNotContain("captured.mp3", receipt);
            Assert.AreEqual(2, payload["Version"]!.GetValue<int>());
            Assert.AreEqual(published, File.Exists(ready.Output)); Assert.AreEqual(!published, File.Exists(pending.Temporary));
            Assert.AreEqual(new RecordingConversionRecoveryResult(1, 0, 0), await pending.Recovery().RecoverAsync());
            var artifact = (await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id))!;
            var job = (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!;
            Assert.AreEqual(RecordingAvailability.Available, artifact.Artifact.Availability); Assert.AreEqual(2L, artifact.Version);
            Assert.AreEqual(RecordingConversionState.Completed, job.Job.State); Assert.AreEqual(3L, job.Version);
            Assert.AreEqual(changed, artifact.UpdatedAtUtc); Assert.AreEqual(changed, job.UpdatedAtUtc);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ready.Output))).ToLowerInvariant(), artifact.Artifact.Sha256);
            Assert.IsFalse(File.Exists(pending.Temporary)); Assert.IsFalse(File.Exists(pending.Entry));
            Assert.AreEqual(new RecordingConversionRecoveryResult(0, 0, 0), await pending.Recovery().RecoverAsync());
            Assert.AreEqual(1, pending.Converter.Calls);
            Assert.AreEqual(ready.File.Digest, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ready.File.Wave))).ToLowerInvariant());
        }
    }
}

internal sealed class PreparedMp3(Mp3AttemptFixture ready, AttemptConverter converter) : IDisposable
{
    internal Mp3AttemptFixture Ready => ready;
    internal AttemptConverter Converter => converter;
    internal string Temporary { get; private set; } = "";
    internal string Entry => Path.Combine(ready.File.Paths.RootDirectory, "mp3-pending", ready.Pair.Conversion.Job.Id.ToString("N") + ".pending");
    internal RecordingConversionRecovery Recovery() => new AbandonedMp3(ready, ready.File.ConversionOutbox(), converter).Recovery();
    internal static async Task<PreparedMp3> Create(DatabaseTarget? target = null, bool published = false)
    {
        var ready = await Mp3AttemptFixture.Create(target); PreparedMp3? pending = null;
        var converter = new AttemptConverter { Handler = (request, _) =>
        {
            var original = request.Publication ?? throw new AssertFailedException();
            AttemptConverter.Publish(request with { Publication = new Handoff((path, file, result, token) =>
            {
                pending!.Temporary = path; original.Prepare(path, file, result, token);
                Assert.IsTrue(File.Exists(pending.Entry)); Assert.IsFalse(File.Exists(ready.Output));
                Assert.ThrowsExactly<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose());
                if (!published) throw new IOException("synthetic-interruption-before-rename");
            }) });
            throw new IOException("synthetic-interruption-after-rename");
        }};
        pending = new(ready, converter);
        try
        {
            var owner = await ready.Factory(converter, ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id);
            await Assert.ThrowsExactlyAsync<RecordingConversionOutboxException>(() => owner.RunAsync());
            await Assert.ThrowsExactlyAsync<RecordingConversionOutboxException>(() => owner.ConfirmAsync());
            Assert.AreEqual(new RecordingConversionRecoveryResult(0, 1, 0), await pending.Recovery().RecoverAsync());
            await Assert.ThrowsExactlyAsync<RecordingConversionOutboxException>(() => owner.DisposeAsync().AsTask());
            Assert.AreEqual(RecordingConversionState.Running, (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!.Job.State);
            return pending;
        }
        catch { ready.Dispose(); throw; }
    }
    internal JsonObject ReadPayload()
    {
        var protector = new WindowsUserSecretProtector();
        var encrypted = JsonSerializer.Deserialize<ProtectedSecret>(File.ReadAllText(Entry))!;
        using var plain = protector.Unprotect(encrypted, "recording-conversion-v1");
        return JsonNode.Parse(plain.Reveal())!.AsObject();
    }
    internal void RewritePayload(Action<JsonObject> change)
    {
        var payload = ReadPayload(); change(payload);
        var encrypted = new WindowsUserSecretProtector().Protect(payload.ToJsonString(), "recording-conversion-v1");
        File.WriteAllText(Entry, JsonSerializer.Serialize(encrypted));
    }
    public void Dispose() => ready.Dispose();
    private sealed class Handoff(Action<string, FileStream, Mp3ConversionResult, CancellationToken> action) : IMp3PublicationObserver
    { public void Prepare(string path, FileStream output, Mp3ConversionResult result, CancellationToken token) => action(path, output, result, token); }
}
