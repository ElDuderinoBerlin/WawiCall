using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.Audio;

internal sealed class RecordingJournalWriter(IRecordingJournal journal, TimeProvider clock, StoredRecordingJournal initial)
{
    private readonly SemaphoreSlim _gate = new(1);
    private StoredRecordingJournal _saved = initial;
    private PendingWrite? _pending;
    internal bool HasPending => Volatile.Read(ref _pending) is not null;

    internal async Task SaveAsync(RecordingCapture capture)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_pending is not null) throw new RecordingPersistenceException(_pending.Capture.Capture.State == PcmRecordingState.Completed);
            var changed = clock.GetUtcNow();
            if (changed < _saved.Capture.UpdatedAtUtc) changed = _saved.Capture.UpdatedAtUtc;
            if (capture.EndedAtUtc is { } end && changed < end) changed = end;
            var file = _saved.Recording.Recording with { Duration = capture.Duration,
                Availability = capture.State == PcmRecordingState.Completed ? RecordingAvailability.Available : RecordingAvailability.Failed };
            Volatile.Write(ref _pending, new(new(file, _saved.Recording.Version, changed), new(capture, _saved.Capture.Version, changed)));
            await SavePendingAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    internal async Task RetryAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try { await SavePendingAsync(token).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    private async Task SavePendingAsync(CancellationToken token)
    {
        if (_pending is not { } pending) return;
        try
        {
            _saved = await journal.WriteAsync(pending.Recording, pending.Capture, token).ConfigureAwait(false);
            Volatile.Write(ref _pending, null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { throw new RecordingPersistenceException(pending.Capture.Capture.State == PcmRecordingState.Completed); }
    }
    private sealed record PendingWrite(RecordingWrite Recording, RecordingCaptureWrite Capture);
}
