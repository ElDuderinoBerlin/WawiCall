using System.Security.Cryptography;
using WawiCallPro.Core.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

public sealed class PersistedMp3ConversionFactory(IRecordingConversionRepository jobs, IRecordingArtifactRepository artifacts,
    IRecordingCaptureRepository captures, ICallRepository calls, IWorkstationIdentity workstation, ApplicationPaths paths,
    IRecordingJournalOutbox pending, IRecordingFileProbe probe, IMp3Converter converter, IRecordingConversionJournal journal,
    TimeProvider clock) : IPersistedMp3ConversionFactory
{
    public async Task<IPersistedMp3Conversion> StartAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _ = DatabaseId.Encode(jobId); cancellationToken.ThrowIfCancellationRequested();
        var saved = await jobs.GetAsync(jobId, cancellationToken).ConfigureAwait(false) ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
        var job = saved.Job;
        if (job.State != RecordingConversionState.Pending || job.Attempts == int.MaxValue) Conflict();
        var source = await artifacts.GetAsync(job.SourceArtifactId, cancellationToken).ConfigureAwait(false) ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
        var target = await artifacts.GetAsync(job.TargetArtifactId, cancellationToken).ConfigureAwait(false) ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
        if (source.Artifact.Format != RecordingArtifactFormat.Wave || target.Artifact.Format != RecordingArtifactFormat.Mp3 ||
            source.Artifact.RecordingId != target.Artifact.RecordingId || source.Artifact.CallId != target.Artifact.CallId ||
            source.Artifact.Availability != RecordingAvailability.Available || target.Artifact.Availability != RecordingAvailability.Pending) Conflict();
        var call = await calls.GetAsync(source.Artifact.CallId, cancellationToken).ConfigureAwait(false);
        var local = await workstation.GetAsync(cancellationToken).ConfigureAwait(false);
        if (local is null || call is null || call.History.Call.Origin?.WorkstationId != local || call.History.Call.State is not (CallState.Ended or CallState.Failed))
            throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
        var capture = await captures.GetAsync(source.Artifact.RecordingId, cancellationToken).ConfigureAwait(false);
        if (capture is null || capture.Capture.State != PcmRecordingState.Completed || capture.Capture.CallId != source.Artifact.CallId ||
            capture.Capture.SampleRate != job.Format.SampleRate || capture.Capture.Channels != job.Format.Channels) Conflict();
        var lease = pending.AcquireInspection(source.Artifact.RecordingId); FileStream? wave = null;
        try
        {
            if (lease.HasPending) throw new RecordingOutboxException();
            if (probe.IsAbsent(source.Artifact.RelativePath)) throw new Mp3ConversionException(Mp3ConversionFailure.SourceUnavailable);
            try { wave = new(paths.ResolveRecordingPath(source.Artifact.RelativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { throw new Mp3ConversionException(Mp3ConversionFailure.SourceUnavailable); }
            if (probe.IsAbsent(source.Artifact.RelativePath) || wave.Length is < 44 or > int.MaxValue || wave.Length != source.Artifact.ByteLength) Conflict();
            var header = PcmWaveHeader.Read(wave, cancellationToken);
            if (header.SampleRate != capture!.Capture.SampleRate || header.Channels != capture.Capture.Channels || header.Duration != capture.Capture.Duration) Conflict();
            var digest = Convert.ToHexString(await SHA256.HashDataAsync(wave, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
            wave.Position = 0;
            if (digest != source.Artifact.Sha256) Conflict();
            var output = paths.ResolveRecordingPath(target.Artifact.RelativePath);
            if (!probe.IsAbsent(target.Artifact.RelativePath) || !Directory.Exists(Path.GetDirectoryName(output)))
                throw new Mp3ConversionException(Mp3ConversionFailure.DestinationUnavailable);
            var formats = await converter.GetSupportedFormatsAsync(cancellationToken).ConfigureAwait(false);
            if (!formats.Contains(job.Format)) throw new Mp3ConversionException(formats.IsEmpty ? Mp3ConversionFailure.EncoderUnavailable : Mp3ConversionFailure.UnsupportedFormat);
            cancellationToken.ThrowIfCancellationRequested();
            var attempt = new RecordingConversionJob(job.Id, job.SourceArtifactId, job.TargetArtifactId, job.Format, RecordingConversionState.Running, job.Attempts + 1, Guid.NewGuid());
            var changed = clock.GetUtcNow(); if (changed < saved.UpdatedAtUtc) changed = saved.UpdatedAtUtc;
            var claimed = await jobs.WriteAsync(new(attempt, saved.Version, changed), cancellationToken).ConfigureAwait(false);
            return new PersistedMp3Conversion(claimed, target, header.Duration, new(paths.ResolveRecordingPath(source.Artifact.RelativePath), output, job.Format),
                wave, lease, converter, journal, paths, probe, clock);
        }
        catch (Exception error)
        {
            try { if (wave is not null) await wave.DisposeAsync().ConfigureAwait(false); }
            finally { await lease.DisposeAsync().ConfigureAwait(false); }
            if (error is RepositoryConflictException or WawiCallPro.Database.DatabaseOperationException or Mp3ConversionException or RecordingOutboxException ||
                error is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
            throw new RecordingOutboxException();
        }
    }
    private static void Conflict() => throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
}
