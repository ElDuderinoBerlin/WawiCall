using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

internal sealed class DuplexPcmRecording : IDuplexPcmRecording, IRecordingInputDrain
{
    private readonly IPcmRecordingSession _session;
    private readonly IPcmRecordingInputGuard _guard;
    private readonly IConversationPcmSubscription _source;
    private readonly DuplexPcmRecordingSource _forwarder;
    private readonly ISentPcmRecordingBinding _sent;
    private readonly IReceivedPcmRecordingBinding _received;
    private readonly int _sampleRate, _channels;
    private readonly object _gate = new();
    private Task<PcmRecordingResult>? _stop;
    private Task? _dispose;
    private int _closing;
    private Task? _detach;
    public async Task<long> DrainInputAsync()
    {
        await DrainOnceAsync().ConfigureAwait(false);
        return (_session as IPcmRecordingInputExtent)?.InputFrameCount
            ?? throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
    }
    public PcmRecordingState State => Volatile.Read(ref _closing) != 0 && _session.State == PcmRecordingState.Capturing ? PcmRecordingState.Stopping : _session.State;
    public PcmRecordingFailure? Failure => _session.Failure;
    public Task InputCompletion { get; }

    internal DuplexPcmRecording(IPcmRecordingSession session, IPcmRecordingInputGuard guard,
        IConversationPcmSubscription source, DuplexPcmRecordingSource forwarder,
        ISentPcmRecordingBinding sent, IReceivedPcmRecordingBinding received, PcmRecordingRequest request)
    {
        _session = session; _guard = guard; _source = source; _forwarder = forwarder; _sent = sent; _received = received;
        _sampleRate = request.SampleRate; _channels = request.Layout == ConversationWaveLayout.SeparatedStereo ? 2 : 1;
        InputCompletion = CompleteInputAsync();
    }

    private async Task CompleteInputAsync()
    {
        PcmObservationFailure? failure;
        try { failure = await _source.Completion.ConfigureAwait(false); }
        catch (Exception) { failure = PcmObservationFailure.ObserverFailed; Reject(); }
        if (failure is { } reason) _forwarder.OnPcmObservationFailed(reason);
        _forwarder.Complete(failure);
        await Task.WhenAll(_sent.Completion, _received.Completion).ConfigureAwait(false);
    }

    public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_stop is not null) return _stop;
            if (_dispose is not null) return Task.FromException<PcmRecordingResult>(new ObjectDisposedException(nameof(IDuplexPcmRecording)));
            if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(8))
                throw new ArgumentOutOfRangeException(nameof(duration));
            var frames = (duration.Ticks * _sampleRate + TimeSpan.TicksPerSecond / 2) / TimeSpan.TicksPerSecond;
            if (frames == 0 || frames * _channels * 2 > int.MaxValue - 44)
                throw new ArgumentOutOfRangeException(nameof(duration), "Die Aufnahmedauer liegt außerhalb der unterstützten WAV-Größe.");
            Volatile.Write(ref _closing, 1);
            return _stop = StopCoreAsync(duration, cancellationToken);
        }
    }

    private async Task<PcmRecordingResult> StopCoreAsync(TimeSpan duration, CancellationToken token)
    {
        try
        {
            await DrainOnceAsync().ConfigureAwait(false);
            return await _session.StopAsync(duration, token).ConfigureAwait(false);
        }
        finally { await _session.DisposeAsync().ConfigureAwait(false); }
    }

    private Task DrainOnceAsync() { lock (_gate) return _detach ??= DetachCoreAsync(); }
    private async Task DetachCoreAsync()
    {
        try
        {
            try { await _source.DisposeAsync().ConfigureAwait(false); }
            catch (Exception) { Reject(); }
            await InputCompletion.ConfigureAwait(false);
        }
        finally
        {
            try { await _sent.DisposeAsync().ConfigureAwait(false); }
            finally { await _received.DisposeAsync().ConfigureAwait(false); }
        }
    }

    private void Reject() { try { _guard.RejectInput(PcmRecordingFailure.TimelineConflict); } catch (Exception) { } }
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_dispose is not null) return new(_dispose);
            Volatile.Write(ref _closing, 1);
            return new(_dispose = _stop is { } stopping ? AwaitStopAsync(stopping) : AbortAsync());
        }
    }
    private static async Task AwaitStopAsync(Task<PcmRecordingResult> stopping) => await stopping.ConfigureAwait(false);
    private async Task AbortAsync()
    {
        try { await DrainOnceAsync().ConfigureAwait(false); }
        finally { await _session.DisposeAsync().ConfigureAwait(false); }
    }
}
