using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Utilities.Persistence;

public interface IRecordingConversionRequests
{
    /// <summary>Enqueues one verified local WAV or returns its existing matching job without requeueing it.</summary>
    Task<StoredRecordingConversionJournal> EnqueueAsync(Guid recordingId, Mp3EncodingFormat format, CancellationToken cancellationToken = default);
}

/// <summary>Creates the canonical target/job pair under the recording lease. Signals the worker only after releasing file/recording locks.</summary>
public sealed class RecordingConversionRequests(IRecordingArtifactRepository artifacts, IRecordingCaptureRepository captures,
    IRecordingConversionRepository jobs, IRecordingConversionJournal journal, ICallRepository calls,
    IWorkstationIdentity workstation, IRecordingJournalOutbox recordings, IRecordingConversionOutbox conversions,
    IRecordingArtifactFileInspector files, IRecordingFileProbe probe, IRecordingConversionQueue queue, TimeProvider clock) : IRecordingConversionRequests
{
    public async Task<StoredRecordingConversionJournal> EnqueueAsync(Guid recordingId, Mp3EncodingFormat format, CancellationToken cancellationToken = default)
    {
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
            var existing = await jobs.GetForSourceAsync(wave.Id, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                var target = catalog.SingleOrDefault(item => item.Artifact.Id == existing.Job.TargetArtifactId);
                if (existing.Job.Format != format || target is null || target.Artifact.Format != RecordingArtifactFormat.Mp3 || target.Artifact.CallId != wave.CallId)
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
                if (catalog.Any(item => item.Artifact.Format == RecordingArtifactFormat.Mp3))
                    throw new RepositoryConflictException(RepositoryConflictKind.AlreadyExists);
                var path = Path.ChangeExtension(wave.RelativePath, ".mp3");
                if (!probe.IsAbsent(path)) throw new Mp3ConversionException(Mp3ConversionFailure.DestinationUnavailable);
                await using var original = await files.InspectAsync(wave, cancellationToken).ConfigureAwait(false);
                if (original.State != RecordingArtifactFileState.Matches) throw new Mp3ConversionException(Mp3ConversionFailure.SourceUnavailable);
                var target = new RecordingArtifact(Guid.NewGuid(), recordingId, wave.CallId, RecordingArtifactFormat.Mp3, path, RecordingAvailability.Pending);
                var job = new RecordingConversionJob(Guid.NewGuid(), wave.Id, target.Id, format, RecordingConversionState.Pending);
                var changed = clock.GetUtcNow();
                if (changed < source.UpdatedAtUtc) changed = source.UpdatedAtUtc;
                if (changed < capture.UpdatedAtUtc) changed = capture.UpdatedAtUtc;
                result = await journal.BeginAsync(new(target, null, changed), new(job, null, changed), cancellationToken).ConfigureAwait(false);
            }
        }
        if (result.Conversion.Job.State == RecordingConversionState.Pending) queue.Request();
        return result;
    }
}
