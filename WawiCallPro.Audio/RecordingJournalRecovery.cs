using Microsoft.Extensions.Logging;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
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
    IRecordingCompletionPublisher completions,
    IRecordingRepository recordings,
    IRecordingCaptureRepository captures) : IRecordingJournalRecovery
{
    private readonly SemaphoreSlim _pass = new(1);

    public async Task<RecordingJournalRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default)
    {
        // Host startup and SIP registration share the same delivery pass.
        await _pass.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await RecoverPendingAsync(cancellationToken).ConfigureAwait(false); }
        finally { _pass.Release(); }
    }

    private async Task<RecordingJournalRecoveryResult> RecoverPendingAsync(CancellationToken cancellationToken)
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
                if (delivery.NeedsWavePublication)
                {
                    await VerifyPreparedOwnerAsync(delivery, cancellationToken).ConfigureAwait(false);
                    await delivery.PublishPreparedWaveAsync(cancellationToken).ConfigureAwait(false);
                }
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

    private async Task VerifyPreparedOwnerAsync(IRecordingJournalDelivery delivery, CancellationToken token)
    {
        var file = delivery.Recording.Recording;
        var completed = delivery.Capture.Capture;
        var recording = await recordings.GetAsync(file.Id, token).ConfigureAwait(false);
        var capture = await captures.GetAsync(file.Id, token).ConfigureAwait(false);
        if (recording is null || capture is null ||
            recording.Version != delivery.Recording.ExpectedVersion || capture.Version != delivery.Capture.ExpectedVersion ||
            recording.Recording != file with { Availability = RecordingAvailability.Pending, Duration = null } ||
            capture.Capture.CallId != file.CallId || capture.Capture.StartedAtUtc != completed.StartedAtUtc ||
            capture.Capture.SampleRate != completed.SampleRate || capture.Capture.Channels != completed.Channels ||
            capture.Capture.State is not (PcmRecordingState.Capturing or PcmRecordingState.Stopping) ||
            recording.UpdatedAtUtc > delivery.Recording.ChangedAtUtc || capture.UpdatedAtUtc > delivery.Capture.ChangedAtUtc)
            throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
    }
}
