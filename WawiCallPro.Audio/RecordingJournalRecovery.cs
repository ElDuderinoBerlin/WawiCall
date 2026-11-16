using Microsoft.Extensions.Logging;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Audio;

/// <summary>Only the selected database, local workstation and terminal calls may receive a pending completion.</summary>
public sealed class RecordingJournalRecovery(
    IRecordingJournalOutbox pending,
    DapperRecordingJournal journal,
    ICallRepository calls,
    IWorkstationIdentity workstation,
    ILogger<RecordingJournalRecovery> log,
    IRecordingCompletionPublisher completions) : IRecordingJournalRecovery
{
    public async Task<RecordingJournalRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> entries; Guid? local;
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); entries = pending.GetPending();
            if (entries.Count == 0) return new(0, 0, 0);
            local = await workstation.GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { log.LogWarning("Recording recovery: {Code}", "PendingJournalUnavailable"); return new(0, 0, 1); }
        if (local is null) { log.LogWarning("Recording recovery: {Code}", "WorkstationIdentityMissing"); return new(0, entries.Count, 0); }
        var delivered = 0; var deferred = 0; var failed = 0;
        foreach (var id in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var delivery = await pending.OpenAsync(id, cancellationToken).ConfigureAwait(false);
                var call = await calls.GetAsync(delivery.Recording.Recording.CallId, cancellationToken).ConfigureAwait(false);
                if (call?.History.Call.Origin?.WorkstationId != local || call.History.Call.State is not (CallState.Ended or CallState.Failed))
                { deferred++; continue; }
                completions.Validate(delivery, cancellationToken);
                await journal.WriteAsync(delivery.Recording, delivery.Capture, cancellationToken).ConfigureAwait(false);
                await completions.PublishAsync(delivery, cancellationToken).ConfigureAwait(false);
                delivery.Acknowledge(); delivered++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (RepositoryConflictException) { deferred++; log.LogWarning("Recording recovery: {Code}", "MetadataConflict"); }
            catch (Exception) { failed++; log.LogWarning("Recording recovery: {Code}", "PendingCompletionNotConfirmed"); }
        }
        return new(delivered, deferred, failed);
    }
}
