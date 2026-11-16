namespace WawiCallPro.Database.Contracts;

public sealed record StoredRecordingJournal(StoredRecording Recording, StoredRecordingCapture Capture);

/// <summary>Atomic metadata writes, not a transaction with the filesystem. Never creates or removes audio files.</summary>
public interface IRecordingJournal
{
    /// <summary>Strict insert of a new recording and its capture. Reusing an ID cannot claim an existing capture.</summary>
    Task<StoredRecordingJournal> BeginAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default);
    /// <summary>Updates both versions in one transaction. Exact replay of the last committed pair is idempotent.</summary>
    Task<StoredRecordingJournal> WriteAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default);
}
