using WawiCallPro.Interfaces.Common;

namespace WawiCallPro.Database.Contracts;

public sealed record InterruptedRecordingCandidate(StoredRecordingJournal Journal, DateTimeOffset CallChangedAtUtc);

/// <summary>Reads only unfinished pending WAV captures of terminal calls owned by the selected workstation.
/// The exclusive ID cursor remains stable when earlier candidates are completed during recovery.</summary>
public interface IInterruptedRecordingQuery
{
    Task<Page<InterruptedRecordingCandidate>> GetPageAsync(Guid workstationId, Guid? afterRecordingId = null,
        int limit = 100, CancellationToken cancellationToken = default);
}
