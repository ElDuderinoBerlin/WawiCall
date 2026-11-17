using System.Security.Cryptography;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

internal sealed class PersistedMp3Conversion(StoredRecordingConversion claimed, StoredRecordingArtifact target, TimeSpan duration,
    Mp3ConversionRequest request, FileStream wave, IRecordingArtifactAuditLease lease, IMp3Converter converter,
    IRecordingConversionJournal journal, ApplicationPaths paths, IRecordingFileProbe probe, TimeProvider clock) : IPersistedMp3Conversion
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private Task<StoredRecordingConversionJournal>? _run, _confirmationTask;
    private Task? _disposal;
    private RecordingArtifactWrite? _artifactWrite;
    private RecordingConversionWrite? _jobWrite;
    private FileStream? _output;
    private bool _disposed;

    public Task<StoredRecordingConversionJournal> RunAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) { ObjectDisposedException.ThrowIf(_disposed, this); return _run ??= RunCoreAsync(cancellationToken); }
    }
    public Task<StoredRecordingConversionJournal> ConfirmAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) { ObjectDisposedException.ThrowIf(_disposed, this); return ConfirmCore(cancellationToken); }
    }
    private Task<StoredRecordingConversionJournal> ConfirmCore(CancellationToken token)
    {
        if (_artifactWrite is null || _jobWrite is null) throw new InvalidOperationException("Das Konvertierungsergebnis liegt noch nicht vor.");
        if (_confirmationTask is null || _confirmationTask.IsFaulted || _confirmationTask.IsCanceled)
            _confirmationTask = journal.WriteAsync(_artifactWrite, _jobWrite, token);
        return _confirmationTask;
    }
    private async Task<StoredRecordingConversionJournal> RunCoreAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
        Mp3ConversionResult? result = null; RecordingConversionFailure? failure = null; var canceled = false;
        try
        {
            result = await converter.ConvertAsync(request, linked.Token).ConfigureAwait(false);
            await VerifyAsync(result).ConfigureAwait(false); // Publication has committed: finish confirmation even if cancellation now arrives.
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { failure = RecordingConversionFailure.Cancelled; canceled = true; }
        catch (Mp3ConversionException error)
        {
            failure = error.Failure switch
            {
                Mp3ConversionFailure.EncoderUnavailable => RecordingConversionFailure.EncoderUnavailable,
                Mp3ConversionFailure.UnsupportedFormat => RecordingConversionFailure.UnsupportedFormat,
                Mp3ConversionFailure.InvalidWave => RecordingConversionFailure.InvalidWave,
                Mp3ConversionFailure.SourceUnavailable => RecordingConversionFailure.SourceUnavailable,
                Mp3ConversionFailure.DestinationUnavailable => RecordingConversionFailure.DestinationUnavailable,
                Mp3ConversionFailure.CleanupFailed => RecordingConversionFailure.CleanupFailed,
                _ => RecordingConversionFailure.EncodingFailed
            };
        }
        catch (Exception) { failure = RecordingConversionFailure.EncodingFailed; }
        Prepare(result, failure);
        Task<StoredRecordingConversionJournal> confirmation;
        lock (_gate) confirmation = ConfirmCore(CancellationToken.None);
        var saved = await confirmation.ConfigureAwait(false);
        if (canceled) throw new OperationCanceledException(linked.Token);
        return saved;
    }
    private async Task VerifyAsync(Mp3ConversionResult result)
    {
        if (result.Format != request.Format || result.SourceDuration != duration || result.FileSize is <= 0 or > int.MaxValue ||
            result.Sha256 is not { Length: 64 } hash || hash.Any(ch => !(ch is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new Mp3ConversionException(Mp3ConversionFailure.EncodingFailed);
        if (probe.IsAbsent(target.Artifact.RelativePath)) throw new Mp3ConversionException(Mp3ConversionFailure.DestinationUnavailable);
        _output = new(paths.ResolveRecordingPath(target.Artifact.RelativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (probe.IsAbsent(target.Artifact.RelativePath) || _output.Length != result.FileSize) throw new Mp3ConversionException(Mp3ConversionFailure.EncodingFailed);
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(_output).ConfigureAwait(false)).ToLowerInvariant();
        _output.Position = 0;
        if (digest != hash) throw new Mp3ConversionException(Mp3ConversionFailure.EncodingFailed);
    }
    private void Prepare(Mp3ConversionResult? result, RecordingConversionFailure? failure)
    {
        var old = target.Artifact; var job = claimed.Job; var success = failure is null && result is not null;
        var artifact = new RecordingArtifact(old.Id, old.RecordingId, old.CallId, old.Format, old.RelativePath,
            success ? RecordingAvailability.Available : RecordingAvailability.Failed, success ? result!.FileSize : null, success ? result!.Sha256 : null);
        var completed = new RecordingConversionJob(job.Id, job.SourceArtifactId, job.TargetArtifactId, job.Format,
            success ? RecordingConversionState.Completed : RecordingConversionState.Failed, job.Attempts, job.AttemptId, success ? null : failure ?? RecordingConversionFailure.Cancelled);
        var changed = clock.GetUtcNow(); if (changed < claimed.UpdatedAtUtc) changed = claimed.UpdatedAtUtc; if (changed < target.UpdatedAtUtc) changed = target.UpdatedAtUtc;
        lock (_gate) { _artifactWrite = new(artifact, target.Version, changed); _jobWrite = new(completed, claimed.Version, changed); }
    }
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposal is not null) return new(_disposal);
            _disposed = true;
            return new(_disposal = Task.Run(DisposeCoreAsync));
        }
    }
    private async Task DisposeCoreAsync()
    {
        try
        {
            _stop.Cancel();
            if (_run is null) Prepare(null, RecordingConversionFailure.Cancelled);
            else
            {
                try { await _run.ConfigureAwait(false); }
                catch (Exception) when (_artifactWrite is not null) { } // Observe the shared task; the latest confirmation below determines success.
            }
            Task<StoredRecordingConversionJournal> confirmation;
            lock (_gate) confirmation = _confirmationTask ?? ConfirmCore(CancellationToken.None);
            await confirmation.ConfigureAwait(false);
        }
        finally { await ReleaseAsync().ConfigureAwait(false); }
    }
    private async Task ReleaseAsync()
    {
        await using var ownedLease = lease;
        await using var ownedWave = wave;
        await using var ownedOutput = _output;
        _stop.Dispose();
    }
}
