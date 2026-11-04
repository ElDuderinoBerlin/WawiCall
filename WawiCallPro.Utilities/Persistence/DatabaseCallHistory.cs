using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Utilities.Persistence;

public sealed class DatabaseCallHistory(ICallRepository calls, ICallNoteRepository notes, TimeProvider clock) : ICallHistoryService
{
    public Task<Page<CallSnapshot>> GetCallsAsync(CallHistoryQuery query, CancellationToken cancellationToken = default) => calls.GetPageAsync(query, cancellationToken);
    public async Task<CallHistory?> GetCallAsync(Guid callId, CancellationToken cancellationToken = default) =>
        (await calls.GetAsync(callId, cancellationToken).ConfigureAwait(false))?.History;
    public async Task<CallNote> SaveNoteAsync(Guid callId, Guid noteId, string text, CancellationToken cancellationToken = default)
    {
        var existing = await notes.GetAsync(noteId, cancellationToken).ConfigureAwait(false);
        return (await notes.WriteAsync(new(callId, noteId, text, existing?.Version, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false)).Note;
    }
}

public sealed class DatabaseCallJournal(ICallRepository calls, ICallOriginProvider origin) : ICallJournal
{
    public async Task<CallSnapshot> BeginAsync(CallSnapshot call, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        var captured = await origin.CaptureAsync(cancellationToken).ConfigureAwait(false);
        return (await calls.WriteAsync(new(call, null, [], [], captured), cancellationToken).ConfigureAwait(false)).History.Call;
    }
}
