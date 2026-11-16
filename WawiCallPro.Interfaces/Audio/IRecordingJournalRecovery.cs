namespace WawiCallPro.Interfaces.Audio;

public sealed record RecordingJournalRecoveryResult(int Delivered, int Deferred, int Failed);

/// <summary>Replays protected terminal metadata after call recovery. Never resumes recording or reconstructs unknown audio.</summary>
public interface IRecordingJournalRecovery
{
    Task<RecordingJournalRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default);
}
