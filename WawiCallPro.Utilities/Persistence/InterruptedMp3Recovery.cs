using Microsoft.Extensions.Logging;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

public sealed record InterruptedMp3RecoveryResult(int Recovered, int Deferred, int Failed, bool HasMore = false);
public interface IInterruptedMp3Recovery
{
    Task<InterruptedMp3RecoveryResult> RecoverAsync(CancellationToken cancellationToken = default);
}

/// <summary>Fails abandoned attempts only after acquiring their recording lease. Never reads, adopts,
/// deletes or re-encodes unknown output; protected terminal pairs always have priority.</summary>
public sealed class InterruptedMp3Recovery(IInterruptedRecordingConversionQuery candidates, IRecordingConversionRepository jobs,
    IRecordingArtifactRepository artifacts, ICallRepository calls, IRecordingJournalOutbox recordingLeases,
    IRecordingConversionOutbox pending, IRecordingConversionJournal journal, IRecordingFileProbe probe,
    IWorkstationIdentity workstation, TimeProvider clock, ILogger<InterruptedMp3Recovery> log) : IInterruptedMp3Recovery
{
    private const int MaximumPerPass = 4096;
    public async Task<InterruptedMp3RecoveryResult> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var recovered = 0; var deferred = 0; var failed = 0; var hasMore = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var local = await workstation.GetAsync(cancellationToken).ConfigureAwait(false);
            if (local is null)
            {
                OperationEvents.Write(log, OperationEvent.RecordingConversionRecoveryIncomplete);
                return new(0, 1, 0);
            }
            _ = pending.GetPending(); // Malformed protected storage must not look empty.
            Guid? after = null; var visited = 0;
            while (true)
            {
                var page = await candidates.GetPageAsync(local.Value, after, Math.Min(100, MaximumPerPass - visited), cancellationToken).ConfigureAwait(false);
                foreach (var saved in page.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested(); visited++;
                    try
                    {
                        var source = await artifacts.GetAsync(saved.Job.SourceArtifactId, cancellationToken).ConfigureAwait(false) ?? throw new RecordingConversionOutboxException();
                        IRecordingArtifactAuditLease lease;
                        try { lease = recordingLeases.AcquireInspection(source.Artifact.RecordingId); }
                        catch (RecordingOutboxException) { deferred++; continue; }
                        await using var owned = lease;
                        if (lease.HasPending || pending.HasPending(saved.Job.Id)) { deferred++; continue; }
                        var current = await jobs.GetAsync(saved.Job.Id, cancellationToken).ConfigureAwait(false);
                        if (current != saved || saved.Job.State != RecordingConversionState.Running) { deferred++; continue; }
                        var target = await artifacts.GetAsync(saved.Job.TargetArtifactId, cancellationToken).ConfigureAwait(false) ?? throw new RecordingConversionOutboxException();
                        var call = await calls.GetAsync(source.Artifact.CallId, cancellationToken).ConfigureAwait(false);
                        if (call?.History.Call.Origin?.WorkstationId != local || call.History.Call.State is not (CallState.Ended or CallState.Failed) ||
                            source.Artifact.Format != RecordingArtifactFormat.Wave || target.Artifact.Format == RecordingArtifactFormat.Wave ||
                            source.Artifact.RecordingId != target.Artifact.RecordingId || source.Artifact.CallId != target.Artifact.CallId ||
                            target.Artifact.Availability != RecordingAvailability.Pending || !probe.IsAbsent(target.Artifact.RelativePath))
                        { deferred++; continue; }
                        var changed = new[] { clock.GetUtcNow(), saved.UpdatedAtUtc, source.UpdatedAtUtc, target.UpdatedAtUtc, call.History.Call.StateChangedAtUtc }.Max();
                        var old = target.Artifact; var job = saved.Job;
                        var unavailable = new RecordingArtifact(old.Id, old.RecordingId, old.CallId, old.Format, old.RelativePath, RecordingAvailability.Failed);
                        var interrupted = new RecordingConversionJob(job.Id, job.SourceArtifactId, job.TargetArtifactId, job.Format,
                            RecordingConversionState.Failed, job.Attempts, job.AttemptId, RecordingConversionFailure.Interrupted);
                        // Durable staging rechecks the job lease and never replaces a competing protected completion.
                        await journal.WriteAsync(new(unavailable, target.Version, changed), new(interrupted, saved.Version, changed), cancellationToken).ConfigureAwait(false);
                        recovered++;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (RepositoryConflictException) { deferred++; }
                    catch (Exception) { failed++; }
                }
                if (!page.HasMore) break;
                if (page.Items.IsEmpty || page.Items[^1].Job.Id == after) throw new RecordingConversionOutboxException();
                if (visited >= MaximumPerPass) { hasMore = true; break; }
                after = page.Items[^1].Job.Id;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { failed++; }
        if (recovered > 0) OperationEvents.Write(log, OperationEvent.RecordingConversionInterrupted);
        if (deferred > 0 || failed > 0 || hasMore) OperationEvents.Write(log, OperationEvent.RecordingConversionRecoveryIncomplete);
        return new(recovered, deferred, failed, hasMore);
    }
}
