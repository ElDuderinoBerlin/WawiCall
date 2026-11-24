using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>One bounded preparation per explicit request; reuses the production journal, bindings and WAV pipeline.</summary>
public sealed class LivePcmRecordingFactory(IPersistedDuplexPcmRecordingFactory recordings, TimeProvider clock)
{
    public async Task<LivePcmRecording> StartAsync(IConversationPcmFailureSource source, PcmRecordingRequest request,
        CancellationToken cancellationToken = default)
    {
        var original = source is PlaybackRecordingSource playback ? playback.Sent : source;
        var preparation = new LiveRecordingPreparation(original, clock);
        IDuplexPcmRecording? recording = null;
        var lifetime = new CancellationTokenSource();
        using var startup = cancellationToken.Register(() => lifetime.Cancel());
        try
        {
            await preparation.Ready.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            var calibrated = preparation.CreateRequest(request);
            var input = source is PlaybackRecordingSource selected
                ? selected.WithSent(preparation) : (IConversationPcmFailureSource)preparation;
            recording = await recordings.StartAsync(input, calibrated, lifetime.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new LivePcmRecording(preparation, recording, request, lifetime);
        }
        catch
        {
            try { await preparation.DisposeAsync().ConfigureAwait(false); }
            finally
            {
                try { if (recording is not null) await recording.DisposeAsync().ConfigureAwait(false); }
                finally { startup.Dispose(); lifetime.Dispose(); }
            }
            throw;
        }
    }
}

/// <summary>Stop fixes the monotonic end time before draining, then includes bounded in-flight audio.
/// UTC is metadata only. Disposal without Stop explicitly aborts instead of inventing a duration.</summary>
public sealed class LivePcmRecording(LiveRecordingPreparation preparation, IDuplexPcmRecording inner,
    PcmRecordingRequest request, CancellationTokenSource lifetime) : IAsyncDisposable
{
    private readonly object _gate = new();
    private Task<PcmRecordingResult>? _stop;
    private Task? _dispose;
    public Guid RecordingId => request.RecordingId;
    public PcmRecordingState State => inner.State;
    public Task InputCompletion => inner.InputCompletion;
    public bool HasPendingPersistence => inner is IRecordingPersistenceStatus { HasPendingPersistence: true };
    public Task<PcmRecordingResult> StopAsync()
    {
        lock (_gate)
        {
            if (_stop is not null) return _stop;
            ObjectDisposedException.ThrowIf(_dispose is not null, this);
            return _stop = StopCoreAsync();
        }
    }
    private async Task<PcmRecordingResult> StopCoreAsync()
    {
        var stoppedAt = preparation.GetElapsed();
        try
        {
            await preparation.DisposeAsync().ConfigureAwait(false);
            if (inner is not IRecordingInputDrain drain) throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
            var inputFrames = await drain.DrainInputAsync().ConfigureAwait(false);
            var stopFrames = (long)((Int128)stoppedAt.Ticks * request.SampleRate / TimeSpan.TicksPerSecond);
            var frames = Math.Max(inputFrames, stopFrames);
            if (frames < 1 || frames > 8L * 3600 * request.SampleRate) throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
            var duration = TimeSpan.FromTicks((long)(((Int128)frames * TimeSpan.TicksPerSecond + request.SampleRate - 1) / request.SampleRate));
            if (duration - stoppedAt > LiveRecordingPreparation.MaximumInputLead)
                throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
            await preparation.WaitForInputEndAsync(duration).ConfigureAwait(false);
            return await inner.StopAsync(duration).ConfigureAwait(false);
        }
        finally { await inner.DisposeAsync().ConfigureAwait(false); }
    }
    public ValueTask DisposeAsync() { lock (_gate) return new(_dispose ??= DisposeCoreAsync()); }
    private async Task DisposeCoreAsync()
    {
        try
        {
            if (_stop is { } stopping) await stopping.ConfigureAwait(false);
            else
            {
                try { await preparation.DisposeAsync().ConfigureAwait(false); }
                finally { await inner.DisposeAsync().ConfigureAwait(false); }
            }
        }
        finally { lifetime.Dispose(); }
    }
}
