using Microsoft.Extensions.Logging;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

public sealed record RecordingArtifactReconciliationResult(int MarkedMissing, int Restored, int Unchanged, int Deferred, int Failed);
/// <summary>Audits registered local WAVs without adopting files. Per-entry failures retain metadata and are reported in the result.</summary>
public interface IRecordingArtifactReconciliation
{
    Task<RecordingArtifactReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reconciles only local terminal-call WAVs already confirmed in the catalog. No discovery or adoption of other files.</summary>
public sealed class RecordingArtifactReconciliation(IRecordingArtifactAuditQuery candidates, IRecordingArtifactRepository artifacts,
    IRecordingJournalOutbox pending, IRecordingArtifactFileInspector files, IWorkstationIdentity workstation,
    TimeProvider clock, ILogger<RecordingArtifactReconciliation> log) : IRecordingArtifactReconciliation
{
    public async Task<RecordingArtifactReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var missing = 0; var restored = 0; var unchanged = 0; var deferred = 0; var failed = 0; var unavailable = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var local = await workstation.GetAsync(cancellationToken).ConfigureAwait(false);
            if (local is null) throw new RecordingOutboxException();
            var protectedIds = pending.GetPending().ToHashSet(); Guid? after = null;
            while (true)
            {
                var page = await candidates.GetPageAsync(local.Value, after, 100, cancellationToken).ConfigureAwait(false);
                foreach (var saved in page.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested(); var item = saved.Artifact;
                    try
                    {
                        if (protectedIds.Contains(item.RecordingId)) { deferred++; continue; }
                        await using var lease = pending.AcquireInspection(item.RecordingId);
                        if (lease.HasPending) { deferred++; continue; }
                        await using var observed = await files.InspectAsync(item, cancellationToken).ConfigureAwait(false);
                        var availability = observed.State switch
                        {
                            RecordingArtifactFileState.Matches => RecordingAvailability.Available,
                            RecordingArtifactFileState.Missing or RecordingArtifactFileState.Changed => RecordingAvailability.Missing,
                            _ => throw new RecordingOutboxException()
                        };
                        unavailable |= availability == RecordingAvailability.Missing;
                        if (item.Availability == availability) { unchanged++; continue; }
                        var changed = clock.GetUtcNow(); if (changed < saved.UpdatedAtUtc) changed = saved.UpdatedAtUtc;
                        var next = new RecordingArtifact(item.Id, item.RecordingId, item.CallId, item.Format, item.RelativePath, availability, item.ByteLength, item.Sha256);
                        await artifacts.WriteAsync(new(next, saved.Version, changed), cancellationToken).ConfigureAwait(false);
                        if (availability == RecordingAvailability.Missing) missing++; else restored++;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (RepositoryConflictException) { deferred++; }
                    catch (Exception) { failed++; }
                }
                if (!page.HasMore) break;
                if (page.Items.IsEmpty || page.Items[^1].Artifact.Id == after) throw new RecordingOutboxException();
                after = page.Items[^1].Artifact.Id;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { failed++; }
        if (unavailable) OperationEvents.Write(log, OperationEvent.RecordingArtifactUnavailable);
        if (failed > 0 || deferred > 0) OperationEvents.Write(log, OperationEvent.RecordingArtifactAuditIncomplete);
        return new(missing, restored, unchanged, deferred, failed);
    }
}
