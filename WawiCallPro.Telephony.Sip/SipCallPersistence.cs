using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Telephony.Sip;

public interface ISipCallPersistence
{
    Task<CallSnapshot> SaveAsync(CallSnapshot snapshot, CancellationToken cancellationToken = default);
    Task RecoverAsync(CancellationToken cancellationToken = default);
}

/// <summary>Uses the existing versioned repository; notes and recordings remain independent.</summary>
public sealed class SipCallPersistence(ICallRepository calls, ICallOriginProvider origin, TimeProvider clock) : ISipCallPersistence
{
    public async Task<CallSnapshot> SaveAsync(CallSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var existing = await calls.GetAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
        var captured = existing?.History.Call.Origin ?? snapshot.Origin ?? await origin.CaptureAsync(cancellationToken).ConfigureAwait(false);
        var observed = snapshot with { Origin = captured };
        var events = snapshot.Revision == 0 ? System.Collections.Immutable.ImmutableArray<CallEvent>.Empty
            : [new CallEvent(Guid.NewGuid(), snapshot.Id, snapshot.Revision, snapshot.StateChangedAtUtc, "Sip." + snapshot.State, snapshot.State)];
        return (await calls.WriteAsync(new(observed, existing?.Version, existing?.History.Parties ?? [], events, captured,
            existing?.History.Lookup), cancellationToken).ConfigureAwait(false)).History.Call;
    }

    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        var captured = await origin.CaptureAsync(cancellationToken).ConfigureAwait(false);
        var offset = 0;
        while (true)
        {
            var page = await calls.GetPageAsync(new(new PageRequest(offset, 100)) { WorkstationId = captured.WorkstationId }, cancellationToken).ConfigureAwait(false);
            foreach (var call in page.Items.Where(call => call.State is not (CallState.Ended or CallState.Failed)))
            {
                var now = clock.GetUtcNow();
                if (now < call.StateChangedAtUtc) now = call.StateChangedAtUtc;
                await SaveAsync(call with { Revision = call.Revision + 1, State = CallState.Failed, StateChangedAtUtc = now,
                    EndedAtUtc = now, EndReason = CallEndReason.Interrupted }, cancellationToken).ConfigureAwait(false);
            }
            if (!page.HasMore) return;
            offset += page.Items.Length;
        }
    }
}
