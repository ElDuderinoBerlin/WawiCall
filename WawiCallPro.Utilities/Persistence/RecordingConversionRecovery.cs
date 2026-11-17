using Microsoft.Extensions.Logging;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

public sealed record RecordingConversionRecoveryResult(int Delivered, int Deferred, int Failed);
public interface IRecordingConversionRecovery
{
    Task<RecordingConversionRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default);
}

/// <summary>Delivers only protected terminal pairs for the current database and local ended calls; never encodes.</summary>
public sealed class RecordingConversionRecovery(IRecordingConversionOutbox pending, DapperRecordingConversionJournal journal,
    IRecordingConversionRepository jobs, IRecordingArtifactRepository artifacts, ICallRepository calls,
    IRecordingJournalOutbox recordingLeases, IWorkstationIdentity workstation, ILogger<RecordingConversionRecovery> log) : IRecordingConversionRecovery
{
    public async Task<RecordingConversionRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var delivered = 0; var deferred = 0; var failed = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); var entries = pending.GetPending();
            if (entries.Count == 0) return new(0, 0, 0);
            var local = await workstation.GetAsync(cancellationToken).ConfigureAwait(false);
            if (local is null) deferred = entries.Count;
            else foreach (var id in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var current = await jobs.GetAsync(id, cancellationToken).ConfigureAwait(false) ?? throw new RecordingConversionOutboxException();
                    var target = await artifacts.GetAsync(current.Job.TargetArtifactId, cancellationToken).ConfigureAwait(false) ?? throw new RecordingConversionOutboxException();
                    var call = await calls.GetAsync(target.Artifact.CallId, cancellationToken).ConfigureAwait(false);
                    if (call?.History.Call.Origin?.WorkstationId != local || call.History.Call.State is not (CallState.Ended or CallState.Failed)) { deferred++; continue; }
                    IRecordingArtifactAuditLease lease;
                    try { lease = recordingLeases.AcquireInspection(target.Artifact.RecordingId); }
                    catch (RecordingOutboxException) { deferred++; continue; }
                    await using var owned = lease;
                    if (lease.HasPending) { deferred++; continue; }
                    await using var delivery = await pending.OpenAsync(id, cancellationToken).ConfigureAwait(false);
                    if (delivery.Target.Artifact.Id != target.Artifact.Id || delivery.Target.Artifact.RecordingId != target.Artifact.RecordingId ||
                        delivery.Target.Artifact.CallId != target.Artifact.CallId || delivery.Conversion.Job.SourceArtifactId != current.Job.SourceArtifactId)
                        throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
                    await journal.WriteAsync(delivery.Target, delivery.Conversion, cancellationToken).ConfigureAwait(false);
                    delivery.Acknowledge(); delivered++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (RepositoryConflictException) { deferred++; }
                catch (Exception) { failed++; }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { failed++; }
        if (deferred > 0 || failed > 0) OperationEvents.Write(log, OperationEvent.RecordingConversionRecoveryIncomplete);
        return new(delivered, deferred, failed);
    }
}
