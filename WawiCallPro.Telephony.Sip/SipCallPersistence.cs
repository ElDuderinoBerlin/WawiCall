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
        var events = snapshot.Revision == 0 ? System.Collections.Immutable.ImmutableArray<CallEvent>.Empty
            : [new CallEvent(Guid.NewGuid(), snapshot.Id, snapshot.Revision, snapshot.StateChangedAtUtc, "Sip." + snapshot.State, snapshot.State)];
        for (var attempt = 0; ; attempt++)
        {
            var existing = await calls.GetAsync(snapshot.Id, cancellationToken).ConfigureAwait(false);
            var captured = existing?.History.Call.Origin ?? snapshot.Origin ?? await origin.CaptureAsync(cancellationToken).ConfigureAwait(false);
            var observed = snapshot with { Origin = captured,
                Company = existing?.History.Parties.Any(party => party.IsSelected) == true ? existing.History.Call.Company : snapshot.Company };
            if (existing is not null)
            {
                var stored = existing.History.Call;
                if (stored.Direction != observed.Direction || stored.StartedAtUtc != observed.StartedAtUtc ||
                    (snapshot.Origin is not null && snapshot.Origin != stored.Origin))
                    throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
                // A lost commit acknowledgement must not append the same sequence with a new event ID.
                if (stored.Revision > observed.Revision) return stored;
                if (stored.Revision == observed.Revision)
                {
                    if (stored with { Company = observed.Company } != observed)
                        throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
                    return stored;
                }
            }
            try
            {
                return (await calls.WriteAsync(new(observed, existing?.Version, existing?.History.Parties ?? [], events, captured,
                    existing?.History.Lookup), cancellationToken).ConfigureAwait(false)).History.Call;
            }
            catch (RepositoryConflictException exception) when (exception.Kind == RepositoryConflictKind.VersionMismatch && attempt < 7) { }
        }
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
