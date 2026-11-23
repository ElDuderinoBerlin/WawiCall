using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Utilities.Persistence;

public interface IRecordingConversionRequests
{
    /// <summary>Enqueues one verified local WAV or returns its existing matching job without requeueing it.</summary>
    Task<StoredRecordingConversionJournal> EnqueueAsync(Guid recordingId, Mp3EncodingFormat format, CancellationToken cancellationToken = default);
    Task<StoredRecordingConversionJournal> EnqueueAsync(Guid recordingId, Mp3EncodingFormat format, RecordingArtifactFormat targetFormat,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>Creates the canonical target/job pair under the recording lease. Signals the worker only after releasing file/recording locks.</summary>
public sealed class RecordingConversionRequests(IRecordingArtifactRepository artifacts, IRecordingCaptureRepository captures,
    IRecordingConversionRepository jobs, IRecordingConversionJournal journal, ICallRepository calls,
    IWorkstationIdentity workstation, IRecordingJournalOutbox recordings, IRecordingConversionOutbox conversions,
    IRecordingArtifactFileInspector files, IRecordingFileProbe probe, IRecordingConversionQueue queue, TimeProvider clock) : IRecordingConversionRequests
{
    public Task<StoredRecordingConversionJournal> EnqueueAsync(Guid recordingId, Mp3EncodingFormat format, CancellationToken cancellationToken = default) =>
        EnqueueAsync(recordingId, format, RecordingArtifactFormat.Mp3, cancellationToken);

    public async Task<StoredRecordingConversionJournal> EnqueueAsync(Guid recordingId, Mp3EncodingFormat format,
        RecordingArtifactFormat targetFormat, CancellationToken cancellationToken = default)
    {
        var extension = targetFormat.Extension();
        if (targetFormat == RecordingArtifactFormat.Wave) throw new ArgumentOutOfRangeException(nameof(targetFormat));
        _ = DatabaseId.Encode(recordingId); ArgumentNullException.ThrowIfNull(format); cancellationToken.ThrowIfCancellationRequested();
        StoredRecordingConversionJournal result;
        await using (var lease = recordings.AcquireInspection(recordingId))
        {
            if (lease.HasPending) throw new RecordingOutboxException();
            var catalog = await artifacts.GetForRecordingAsync(recordingId, cancellationToken).ConfigureAwait(false);
            var source = catalog.SingleOrDefault(item => item.Artifact.Format == RecordingArtifactFormat.Wave)
                ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
            var wave = source.Artifact;
            var call = await calls.GetAsync(wave.CallId, cancellationToken).ConfigureAwait(false);
            var local = await workstation.GetAsync(cancellationToken).ConfigureAwait(false);
            if (local is null || call?.History.Call.Origin?.WorkstationId != local || call.History.Call.State is not (CallState.Ended or CallState.Failed))
                throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
            var capture = await captures.GetAsync(recordingId, cancellationToken).ConfigureAwait(false);
            if (wave.Availability != RecordingAvailability.Available || capture is null || capture.Capture.State != PcmRecordingState.Completed ||
                capture.Capture.CallId != wave.CallId || capture.Capture.SampleRate != format.SampleRate || capture.Capture.Channels != format.Channels)
                throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
            var target = catalog.SingleOrDefault(item => item.Artifact.Format == targetFormat);
            var existing = targetFormat == RecordingArtifactFormat.Mp3
                ? await jobs.GetForSourceAsync(wave.Id, cancellationToken).ConfigureAwait(false)
                : target is null ? null : await jobs.GetForTargetAsync(target.Artifact.Id, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                if (existing.Job.Format != format || target is null || target.Artifact.Id != existing.Job.TargetArtifactId || target.Artifact.CallId != wave.CallId)
                    throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
                if (conversions.HasPending(existing.Job.Id)) throw new RecordingConversionOutboxException();
                if (existing.Job.State == RecordingConversionState.Pending)
                {
                    if (!probe.IsAbsent(target.Artifact.RelativePath)) throw new Mp3ConversionException(Mp3ConversionFailure.DestinationUnavailable);
                    await using var original = await files.InspectAsync(wave, cancellationToken).ConfigureAwait(false);
                    if (original.State != RecordingArtifactFileState.Matches) throw new Mp3ConversionException(Mp3ConversionFailure.SourceUnavailable);
                }
                result = new(target, existing);
            }
            else
            {
                if (target is not null)
                    throw new RepositoryConflictException(RepositoryConflictKind.AlreadyExists);
                var path = Path.ChangeExtension(wave.RelativePath, extension);
                if (!probe.IsAbsent(path)) throw new Mp3ConversionException(Mp3ConversionFailure.DestinationUnavailable);
                await using var original = await files.InspectAsync(wave, cancellationToken).ConfigureAwait(false);
                if (original.State != RecordingArtifactFileState.Matches) throw new Mp3ConversionException(Mp3ConversionFailure.SourceUnavailable);
                var requested = new RecordingArtifact(Guid.NewGuid(), recordingId, wave.CallId, targetFormat, path, RecordingAvailability.Pending);
                var job = new RecordingConversionJob(Guid.NewGuid(), wave.Id, requested.Id, format, RecordingConversionState.Pending);
                var changed = clock.GetUtcNow();
                if (changed < source.UpdatedAtUtc) changed = source.UpdatedAtUtc;
                if (changed < capture.UpdatedAtUtc) changed = capture.UpdatedAtUtc;
                result = await journal.BeginAsync(new(requested, null, changed), new(job, null, changed), cancellationToken).ConfigureAwait(false);
            }
        }
        if (result.Conversion.Job.State == RecordingConversionState.Pending) queue.Request();
        return result;
    }
}
