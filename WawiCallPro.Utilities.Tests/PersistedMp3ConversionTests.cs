using System.Collections.Immutable;
using System.Security.Cryptography;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class PersistedMp3ConversionTests
{
    [TestMethod] public async Task OwnedOutputAndExactMetadataRetrySurviveUncertainConfirmation()
    { using var source = await WaveArtifactFixture.Create(); await PersistedMp3Acceptance.RunAsync(source.Target); }

    [TestMethod]
    [DataRow("missing")][DataRow("changed")][DataRow("locked")][DataRow("existing")]
    [DataRow("foreign")][DataRow("identity")][DataRow("live")][DataRow("receipt")][DataRow("unsupported")]
    public async Task GuardsLeavePendingMetadataAndFilesUntouched(string guard)
    {
        using var ready = await Mp3AttemptFixture.Create(ended: guard != "live"); var fixture = ready.File;
        var converter = new AttemptConverter { Supported = guard != "unsupported" };
        if (guard == "missing") File.Delete(fixture.Wave);
        if (guard == "changed") { var bytes = File.ReadAllBytes(fixture.Wave); bytes[^1] ^= 1; File.WriteAllBytes(fixture.Wave, bytes); }
        if (guard == "existing") File.WriteAllBytes(ready.Output, [1, 2, 3]);
        if (guard == "receipt") { await using var receipt = await fixture.Outbox.StageAsync(fixture.CompletedRecording, fixture.CompletedCapture); }
        using var locked = guard == "locked" ? File.Open(fixture.Wave, FileMode.Open, FileAccess.Write, FileShare.None) : null;
        var factory = ready.Factory(converter, local: guard == "foreign" ? Guid.NewGuid() : guard == "identity" ? null : ready.Workstation);
        async Task Start() { await using var attempt = await factory.StartAsync(ready.Pair.Conversion.Job.Id); }
        if (guard is "foreign" or "identity" or "live" or "changed") await Assert.ThrowsExactlyAsync<RepositoryConflictException>(Start);
        else if (guard == "receipt") await Assert.ThrowsExactlyAsync<RecordingOutboxException>(Start);
        else await Assert.ThrowsExactlyAsync<Mp3ConversionException>(Start);
        Assert.AreEqual(ready.Pair.Target, await fixture.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id));
        Assert.AreEqual(ready.Pair.Conversion, await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id)); Assert.AreEqual(0, converter.Calls);
    }

    [TestMethod]
    [DataRow("error")][DataRow("digest")][DataRow("no-digest")][DataRow("length")]
    [DataRow("format")][DataRow("duration")][DataRow("removed")][DataRow("foreign-cancellation")][DataRow("changed")]
    public async Task UnverifiedOutputAndEncoderErrorsNeverBecomeAvailable(string failure)
    {
        using var ready = await Mp3AttemptFixture.Create(); var converter = new AttemptConverter { Failure = failure };
        await using var attempt = await ready.Factory(converter, ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id);
        var result = await attempt.RunAsync();
        Assert.AreEqual(RecordingAvailability.Failed, result.Target.Artifact.Availability); Assert.IsNull(result.Target.Artifact.Sha256);
        Assert.AreEqual(RecordingConversionState.Failed, result.Conversion.Job.State);
        Assert.AreEqual(failure == "removed" ? RecordingConversionFailure.DestinationUnavailable : RecordingConversionFailure.EncodingFailed, result.Conversion.Job.Failure);
        Assert.AreEqual(1, converter.Calls); Assert.IsTrue(File.Exists(ready.File.Wave));
        Assert.AreEqual(ready.File.Digest, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ready.File.Wave))).ToLowerInvariant());
        if (failure is not ("error" or "removed" or "foreign-cancellation")) Assert.IsTrue(File.Exists(ready.Output));
    }

    [TestMethod]
    public async Task DisposeWithoutRunConfirmsCancellationAndReleasesOwnedLocks()
    {
        using var ready = await Mp3AttemptFixture.Create(); var converter = new AttemptConverter();
        var attempt = await ready.Factory(converter, ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => attempt.ConfirmAsync());
        await attempt.DisposeAsync(); await attempt.DisposeAsync(); Assert.AreEqual(0, converter.Calls);
        var job = (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!;
        Assert.AreEqual(RecordingConversionFailure.Cancelled, job.Job.Failure); Assert.AreEqual(3L, job.Version);
        Assert.AreEqual(RecordingAvailability.Failed, (await ready.File.Artifacts.GetAsync(ready.Pair.Target.Artifact.Id))!.Artifact.Availability);
        Assert.IsFalse(File.Exists(ready.Output)); using var writable = File.Open(ready.File.Wave, FileMode.Open, FileAccess.Write, FileShare.None);
    }
    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public async Task DisposeWaitsForEncodingAndObservesCancellationOrCommittedOutput(bool publishDespiteCancellation)
    {
        using var ready = await Mp3AttemptFixture.Create(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var converter = new AttemptConverter { Handler = async (request, token) =>
        {
            entered.SetResult();
            if (publishDespiteCancellation) await release.Task; else await Task.Delay(Timeout.Infinite, token);
            return AttemptConverter.Publish(request);
        }};
        var attempt = await ready.Factory(converter, ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id);
        var running = attempt.RunAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var dispose = attempt.DisposeAsync().AsTask();
        if (publishDespiteCancellation) { Assert.IsFalse(dispose.IsCompleted); release.SetResult(); Assert.AreEqual(RecordingConversionState.Completed, (await running).Conversion.Job.State); }
        else await Assert.ThrowsAsync<OperationCanceledException>(() => running);
        await dispose.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(publishDespiteCancellation, File.Exists(ready.Output)); using var writable = File.Open(ready.File.Wave, FileMode.Open, FileAccess.Write, FileShare.None);
    }
    [TestMethod]
    public async Task CallerCancellationIsRecordedBeforeItPropagates()
    {
        using var ready = await Mp3AttemptFixture.Create(); using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await using var attempt = await ready.Factory(new AttemptConverter(), ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id);
        await Assert.ThrowsAsync<OperationCanceledException>(() => attempt.RunAsync(canceled.Token));
        Assert.AreEqual(RecordingConversionFailure.Cancelled, (await attempt.ConfirmAsync()).Conversion.Job.Failure);
        Assert.IsFalse(File.Exists(ready.Output));
    }
    [TestMethod]
    public async Task PersistentVersionConflictKeepsOutputAndReleasesLocksWithoutTakingOverMetadata()
    {
        using var ready = await Mp3AttemptFixture.Create(); var converter = new AttemptConverter();
        var owner = await ready.Factory(converter, ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id);
        var old = ready.Pair.Target.Artifact;
        var changed = await ready.File.Artifacts.WriteAsync(new(new(old.Id, old.RecordingId, old.CallId, old.Format, old.RelativePath, RecordingAvailability.Failed), 1, DateTimeOffset.UtcNow));
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => owner.RunAsync());
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => owner.ConfirmAsync());
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => owner.DisposeAsync().AsTask());
        Assert.AreEqual(changed, await ready.File.Artifacts.GetAsync(old.Id)); Assert.IsTrue(File.Exists(ready.Output)); Assert.AreEqual(1, converter.Calls);
        Assert.AreEqual(RecordingConversionState.Running, (await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id))!.Job.State);
        using var output = File.Open(ready.Output, FileMode.Open, FileAccess.Write, FileShare.None);
        using var source = File.Open(ready.File.Wave, FileMode.Open, FileAccess.Write, FileShare.None);
    }
    [TestMethod]
    public async Task FileProbeErrorsAreRedactedAndReleaseTheRecordingLeaseBeforeClaiming()
    {
        using var ready = await Mp3AttemptFixture.Create(); var file = ready.File; var converter = new AttemptConverter();
        var factory = new PersistedMp3ConversionFactory(ready.Jobs, file.Artifacts, file.Captures, new DapperCallRepository(new(file.Target, new())),
            new ProbeIdentity(ready.Workstation), file.Paths, file.Outbox, new FailingProbe(), converter, ready.Journal, TimeProvider.System, file.ConversionOutbox());
        var error = await Assert.ThrowsExactlyAsync<RecordingOutboxException>(() => factory.StartAsync(ready.Pair.Conversion.Job.Id));
        Assert.IsNull(error.InnerException); Assert.DoesNotContain("private-probe-path", error.ToString());
        Assert.AreEqual(ready.Pair.Conversion, await ready.Jobs.GetAsync(ready.Pair.Conversion.Job.Id)); Assert.AreEqual(0, converter.Calls);
        await using var released = file.Outbox.AcquireInspection(file.Id);
    }
    private sealed class FailingProbe : IRecordingFileProbe
    { public bool IsAbsent(string path) => throw new IOException("private-probe-path"); }
    private sealed class ProbeIdentity(Guid id) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult<Guid?>(id); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new InvalidOperationException(); }
}

/// <summary>Uses a synthetic converter for transactional/file ownership checks on all five actual engines.</summary>
public static class PersistedMp3Acceptance
{
    public static async Task RunAsync(DatabaseTarget target)
    {
        foreach (var fault in new[] { "none", "before", "after" })
        {
            using var ready = await Mp3AttemptFixture.Create(target); var converter = new AttemptConverter();
            var journal = new UncertainJournal(ready.Journal, fault);
            var owner = await ready.Factory(converter, ready.Workstation, journal).StartAsync(ready.Pair.Conversion.Job.Id);
            Assert.ThrowsExactly<IOException>(() => { using var invalid = File.Open(ready.File.Wave, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });
            await Assert.ThrowsExactlyAsync<RepositoryConflictException>(async () => { await using var duplicate = await ready.Factory(converter, ready.Workstation).StartAsync(ready.Pair.Conversion.Job.Id); });
            var run = owner.RunAsync(); Assert.AreSame(run, owner.RunAsync());
            if (fault == "none") await run; else await Assert.ThrowsExactlyAsync<DatabaseOperationException>(() => run);
            var confirmed = await owner.ConfirmAsync(); Assert.AreEqual(1, converter.Calls);
            Assert.AreEqual(RecordingConversionState.Completed, confirmed.Conversion.Job.State); Assert.AreEqual(3L, confirmed.Conversion.Version);
            Assert.AreEqual(RecordingAvailability.Available, confirmed.Target.Artifact.Availability); Assert.AreEqual(2L, confirmed.Target.Version);
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ready.Output))).ToLowerInvariant(), confirmed.Target.Artifact.Sha256);
            Assert.ThrowsExactly<IOException>(() => File.Delete(ready.Output));
            Assert.AreEqual(confirmed, await owner.ConfirmAsync()); await owner.DisposeAsync();
            using (var writable = File.Open(ready.Output, FileMode.Open, FileAccess.Write, FileShare.None)) { }
            Assert.AreEqual(ready.File.Digest, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ready.File.Wave))).ToLowerInvariant());
            Assert.AreEqual(2L, (await ready.File.Parent.GetAsync(ready.File.Id))!.Version); Assert.AreEqual(2L, (await ready.File.Captures.GetAsync(ready.File.Id))!.Version);
        }
    }
    private sealed class UncertainJournal(IRecordingConversionJournal inner, string fault) : IRecordingConversionJournal
    {
        private bool _failed;
        public Task<StoredRecordingConversionJournal> BeginAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken token = default) => inner.BeginAsync(target, conversion, token);
        public async Task<StoredRecordingConversionJournal> WriteAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken token = default)
        {
            var fail = !_failed && fault != "none"; _failed = true;
            if (fail && fault == "before") throw new DatabaseOperationException(DatabaseErrorCode.CommitUncertain);
            var result = await inner.WriteAsync(target, conversion, token);
            if (fail) throw new DatabaseOperationException(DatabaseErrorCode.CommitUncertain);
            return result;
        }
    }
}

internal sealed class Mp3AttemptFixture(WaveArtifactFixture file, Guid workstation, StoredRecordingConversionJournal pair) : IDisposable
{
    internal WaveArtifactFixture File => file;
    internal Guid Workstation => workstation;
    internal StoredRecordingConversionJournal Pair => pair;
    internal string Output => file.Paths.ResolveRecordingPath(pair.Target.Artifact.RelativePath);
    internal DapperRecordingConversionRepository Jobs { get; } = new(new(file.Target, new()));
    internal DapperRecordingConversionJournal Journal { get; } = new(new(file.Target, new()));
    internal static async Task<Mp3AttemptFixture> Create(DatabaseTarget? target = null, bool ended = true)
    {
        var file = await WaveArtifactFixture.Create(target);
        try
        {
            await file.Journal().WriteAsync(file.CompletedRecording, file.CompletedCapture);
            var local = ended ? await RecordingArtifactAuditAcceptance.EndAsync(file) :
                (await new DapperCallRepository(new(file.Target, new())).GetAsync(file.CompletedRecording.Recording.CallId))!.History.Call.Origin!.WorkstationId;
            var output = new RecordingArtifact(Guid.NewGuid(), file.Id, file.CompletedRecording.Recording.CallId, RecordingArtifactFormat.Mp3, "captured.mp3", RecordingAvailability.Pending);
            var job = new RecordingConversionJob(Guid.NewGuid(), file.Id, output.Id, new(8000, 2, 128000), RecordingConversionState.Pending);
            var time = DateTimeOffset.UtcNow; var pair = await new DapperRecordingConversionJournal(new(file.Target, new())).BeginAsync(new(output, null, time), new(job, null, time));
            return new(file, local, pair);
        }
        catch { file.Dispose(); throw; }
    }
    internal PersistedMp3ConversionFactory Factory(IMp3Converter converter, Guid? local, IRecordingConversionJournal? journal = null) =>
        new(Jobs, file.Artifacts, file.Captures, new DapperCallRepository(new(file.Target, new())), new Identity(local), file.Paths,
            file.Outbox, new LocalRecordingFileProbe(file.Paths), converter, journal ?? Journal, TimeProvider.System, file.ConversionOutbox());
    public void Dispose() => file.Dispose();
    private sealed class Identity(Guid? local) : IWorkstationIdentity
    { public Task<Guid?> GetAsync(CancellationToken token = default) => Task.FromResult(local); public Task<Guid> InitializeAsync(CancellationToken token = default) => throw new AssertFailedException("Must not create an identity."); }
}

internal sealed class AttemptConverter : IMp3Converter
{
    internal string? Failure { get; init; }
    internal bool Supported { get; init; } = true;
    internal int Calls;
    internal Func<Mp3ConversionRequest, CancellationToken, Task<Mp3ConversionResult>>? Handler { get; init; }
    public Task<ImmutableArray<Mp3EncodingFormat>> GetSupportedFormatsAsync(CancellationToken token = default) => Task.FromResult<ImmutableArray<Mp3EncodingFormat>>(Supported ? [new(8000, 2, 128000)] : []);
    public Task<Mp3ConversionResult> ConvertAsync(Mp3ConversionRequest request, CancellationToken token = default)
    {
        Interlocked.Increment(ref Calls);
        if (Handler is not null) return Handler(request, token);
        return Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            if (Failure == "error") throw new IOException("private-native-detail");
            if (Failure == "foreign-cancellation") throw new OperationCanceledException("private-native-detail");
            var result = Publish(request);
            if (Failure == "removed") File.Delete(request.Mp3Path);
            if (Failure == "changed") { var bytes = File.ReadAllBytes(request.Mp3Path); bytes[^1] ^= 1; File.WriteAllBytes(request.Mp3Path, bytes); }
            return Failure switch
            {
                "digest" => result with { Sha256 = new string('a', 64) }, "no-digest" => result with { Sha256 = null },
                "length" => result with { FileSize = result.FileSize + 1 }, "format" => result with { Format = new(44100, 1, 128000) },
                "duration" => result with { SourceDuration = TimeSpan.FromSeconds(1) }, _ => result
            };
        }, token);
    }
    internal static Mp3ConversionResult Publish(Mp3ConversionRequest request)
    {
        byte[] bytes = [1, 3, 5, 7, 9, 11]; using (var file = new FileStream(request.Mp3Path, FileMode.CreateNew, FileAccess.Write)) file.Write(bytes);
        return new(TimeSpan.FromMilliseconds(20), bytes.Length, request.Format) { Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };
    }
}
