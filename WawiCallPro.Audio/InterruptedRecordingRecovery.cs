using Microsoft.Extensions.Logging;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Audio;

/// <summary>Runs after completion replay under the SIP account lease; existing artifacts and receipts always take precedence.</summary>
public sealed class InterruptedRecordingRecovery(IInterruptedRecordingQuery candidates, IRecordingJournal journal,
    IRecordingJournalOutbox pending, IRecordingFileProbe files, IWorkstationIdentity workstation,
    TimeProvider clock, ILogger<InterruptedRecordingRecovery> log) : IInterruptedRecordingRecovery
{
    public async Task<InterruptedRecordingRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var interrupted = 0; var deferred = 0; var failed = 0;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var local = await workstation.GetAsync(cancellationToken).ConfigureAwait(false);
            if (local is null) { Warn("WorkstationIdentityMissing"); return new(0, 0, 1); }
            // A malformed/unreadable directory prevents recovery; it must not hide a completion receipt.
            var protectedIds = pending.GetPending().ToHashSet();
            Guid? after = null;
            while (true)
            {
                var page = await candidates.GetPageAsync(local.Value, after, 100, cancellationToken).ConfigureAwait(false);
                foreach (var candidate in page.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var saved = candidate.Journal; var file = saved.Recording.Recording; var old = saved.Capture.Capture;
                    try
                    {
                        if (protectedIds.Contains(file.Id) || !files.IsAbsent(file.RelativePath))
                        { deferred++; Warn("RecordingArtifactRequiresReview"); continue; }
                        var changed = new[] { clock.GetUtcNow(), candidate.CallChangedAtUtc, saved.Recording.UpdatedAtUtc, saved.Capture.UpdatedAtUtc }.Max();
                        var aborted = new RecordingCapture(old.RecordingId, old.CallId, old.StartedAtUtc, null,
                            PcmRecordingState.Aborted, old.SampleRate, old.Channels, failure: RecordingCaptureFailure.Interrupted);
                        await journal.WriteAsync(new(file with { Availability = RecordingAvailability.Failed, Duration = null }, saved.Recording.Version, changed),
                            new(aborted, saved.Capture.Version, changed), cancellationToken).ConfigureAwait(false);
                        interrupted++;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (RepositoryConflictException) { deferred++; Warn("MetadataConflict"); }
                    catch (Exception) { failed++; Warn("InterruptedRecordingNotConfirmed"); }
                }
                if (!page.HasMore) break;
                if (page.Items.IsEmpty || page.Items[^1].Journal.Recording.Recording.Id == after) throw new RecordingOutboxException();
                after = page.Items[^1].Journal.Recording.Recording.Id;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { failed++; Warn("InterruptedRecordingScanUnavailable"); }
        return new(interrupted, deferred, failed);
    }

    private void Warn(string code) => log.LogWarning("Recording recovery: {Code}", code);
}
