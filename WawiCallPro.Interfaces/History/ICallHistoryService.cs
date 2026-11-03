using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Interfaces.History;

/// <summary>Application history access, not a raw database connection. Failures throw; null means not found.</summary>
public interface ICallHistoryService
{
    Task<Page<CallSnapshot>> GetCallsAsync(CallHistoryQuery query, CancellationToken cancellationToken = default);
    Task<CallHistory?> GetCallAsync(Guid callId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a note by its stable ID and returns the persisted UTC timestamps.
    /// Unknown call IDs and note IDs belonging to a different call are rejected.
    /// </summary>
    Task<CallNote> SaveNoteAsync(Guid callId, Guid noteId, string text, CancellationToken cancellationToken = default);
}
