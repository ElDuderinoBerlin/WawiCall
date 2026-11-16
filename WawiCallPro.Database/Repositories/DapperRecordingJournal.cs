using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperRecordingJournal(DatabaseRepositoryContext context) : IRecordingJournal
{
    public Task<StoredRecordingJournal> BeginAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default) =>
        SaveAsync(recording, capture, true, cancellationToken);
    public Task<StoredRecordingJournal> WriteAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default) =>
        SaveAsync(recording, capture, false, cancellationToken);

    private Task<StoredRecordingJournal> SaveAsync(RecordingWrite recording, RecordingCaptureWrite capture, bool begin, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(recording); ArgumentNullException.ThrowIfNull(capture);
        var file = recording.Recording; var item = capture.Capture;
        var availability = item.State switch { PcmRecordingState.Completed => RecordingAvailability.Available,
            PcmRecordingState.Failed or PcmRecordingState.Aborted => RecordingAvailability.Failed, _ => RecordingAvailability.Pending };
        if (file.Id != item.RecordingId || file.CallId != item.CallId || recording.ChangedAtUtc != capture.ChangedAtUtc
            || !string.Equals(file.Format, "wav", StringComparison.Ordinal) || file.Duration != item.Duration || file.Availability != availability
            || begin && (recording.ExpectedVersion is not null || capture.ExpectedVersion is not null || item.State != PcmRecordingState.Capturing)
            || !begin && (recording.ExpectedVersion is null || capture.ExpectedVersion is null))
            throw new ArgumentException("Aufnahme- und Verlaufsdaten müssen denselben WAV-Vorgang und Status beschreiben.");
        return context.RunAsync(true, async db =>
        {
            var id = DatabaseId.Encode(file.Id);
            RepositoryRow? captureRow = null;
            var previous = await db.OneAsync($"SELECT * FROM {db.T("recordings")} WHERE id=@id", new { id }).ConfigureAwait(false);
            if (!begin && previous is not null)
            {
                var stored = DapperRecordingRepository.Read(previous);
                if (stored.Recording.CallId != file.CallId || stored.Recording.RelativePath != file.RelativePath || stored.Recording.Format != file.Format)
                    throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
                captureRow = await db.OneAsync($"SELECT c.*, r.callid FROM {db.T("recordingcaptures")} c JOIN {db.T("recordings")} r ON c.id=r.id WHERE c.id=@id", new { id }).ConfigureAwait(false);
                if (captureRow is not null)
                {
                    var old = DapperRecordingCaptureRepository.Read(captureRow);
                    if (stored.Recording == file && old.Capture == item && stored.UpdatedAtUtc == recording.ChangedAtUtc && old.UpdatedAtUtc == capture.ChangedAtUtc
                        && stored.Version - 1 == recording.ExpectedVersion && old.Version - 1 == capture.ExpectedVersion)
                        return new StoredRecordingJournal(stored, old);
                }
            }
            var saved = await DapperRecordingRepository.WriteCoreAsync(recording, db).ConfigureAwait(false);
            RepositorySession.Expected(captureRow, capture.ExpectedVersion);
            var savedCapture = await DapperRecordingCaptureRepository.WriteCoreAsync(capture, db).ConfigureAwait(false);
            return new StoredRecordingJournal(saved, savedCapture);
        }, token);
    }
}
