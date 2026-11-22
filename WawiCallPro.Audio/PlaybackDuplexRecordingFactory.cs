using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Combines RTP-timed local PCM with QPC-timed playback capture in the existing PCM recording/mixer.</summary>
public sealed class PlaybackDuplexRecordingFactory(IPcmRecordingSessionFactory sessions,
    ISentPcmRecordingBindingFactory sentBindings, IPlaybackPcmCaptureFactory capture, TimeProvider clock)
{
    public async Task<IDuplexPcmRecording> StartAsync(PlaybackRecordingSource source, DuplexPcmRecordingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Recording); ArgumentNullException.ThrowIfNull(request.Sent);
        if (request.RecordingEpochUtc is not { } epoch || epoch.Offset != TimeSpan.Zero || epoch > clock.GetUtcNow()
            || request.Sent.RtpClockRate is < 1 or > 192000 || request.Sent.RecordingOffset < TimeSpan.FromSeconds(-2)
            || request.Sent.RecordingOffset >= TimeSpan.FromHours(8))
            throw new ArgumentException("Eine gültige gemeinsame Aufnahmeepoche und ein gültiger Sendeanker sind erforderlich.");
        cancellationToken.ThrowIfCancellationRequested();
        var session = await sessions.StartAsync(request.Recording, cancellationToken).ConfigureAwait(false);
        ISentPcmRecordingBinding? sent = null;
        IPlaybackPcmCapture? playback = null;
        try
        {
            if (session is not IPcmRecordingInputGuard guard) throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
            var observer = new PlaybackObserver(session, guard);
            sent = sentBindings.Attach(source.Sent, session, request.Sent);
            playback = await capture.StartAsync(source.DeviceId, epoch, observer, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (session.State != PcmRecordingState.Capturing || playback.Completion.IsCompleted || sent.Completion.IsCompleted)
                throw new PcmRecordingException(session.Failure ?? PcmRecordingFailure.TimelineConflict);
            return new Recording(session, sent, playback, guard, request.Recording);
        }
        catch
        {
            try { if (playback is not null) await playback.DisposeAsync().ConfigureAwait(false); }
            finally
            {
                try { if (sent is not null) await sent.DisposeAsync().ConfigureAwait(false); }
                finally { await session.DisposeAsync().ConfigureAwait(false); }
            }
            throw;
        }
    }

    private sealed class PlaybackObserver(IPcmRecordingSession session, IPcmRecordingInputGuard guard) : IPlaybackPcmObserver
    {
        public bool TryObserve(TimeSpan position, int sampleRate, ReadOnlySpan<short> samples) =>
            session.TryAppend(ConversationAudioSide.Received, position, sampleRate, samples);
        public void OnFailure() => guard.RejectInput(PcmRecordingFailure.TimelineConflict);
    }

    private sealed class Recording : IDuplexPcmRecording, IRecordingInputDrain
    {
        private readonly IPcmRecordingSession _session;
        private readonly ISentPcmRecordingBinding _sent;
        private readonly IPlaybackPcmCapture _playback;
        private readonly IPcmRecordingInputGuard _guard;
        private readonly PcmRecordingRequest _request;
        private readonly object _gate = new();
        private Task<PcmRecordingResult>? _stop;
        private Task? _dispose;
        private Task? _detach;
        public async Task<long> DrainInputAsync()
        {
            await DrainOnceAsync().ConfigureAwait(false);
            return (_session as IPcmRecordingInputExtent)?.InputFrameCount
                ?? throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
        }
        public PcmRecordingState State => _session.State;
        public PcmRecordingFailure? Failure => _session.Failure;
        public Task InputCompletion { get; }
        internal Recording(IPcmRecordingSession session, ISentPcmRecordingBinding sent, IPlaybackPcmCapture playback,
            IPcmRecordingInputGuard guard, PcmRecordingRequest request)
        { _session = session; _sent = sent; _playback = playback; _guard = guard; _request = request; InputCompletion = ObserveAsync(); }

        private async Task ObserveAsync()
        {
            try
            {
                await Task.WhenAll(_playback.Completion, _sent.Completion).ConfigureAwait(false);
                if (await _sent.Completion.ConfigureAwait(false) is not null) _guard.RejectInput(PcmRecordingFailure.TimelineConflict);
            }
            catch { _guard.RejectInput(PcmRecordingFailure.TimelineConflict); }
        }

        public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_stop is not null) return _stop;
                ObjectDisposedException.ThrowIf(_dispose is not null, this);
                var frames = duration.Ticks * (decimal)_request.SampleRate / TimeSpan.TicksPerSecond;
                if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(8) || frames < 0.5m
                    || decimal.Round(frames, 0, MidpointRounding.AwayFromZero) * (_request.Layout == ConversationWaveLayout.SeparatedStereo ? 4 : 2) > int.MaxValue - 44)
                    throw new ArgumentOutOfRangeException(nameof(duration));
                return _stop = StopCoreAsync(duration, cancellationToken);
            }
        }

        private Task DrainOnceAsync() { lock (_gate) return _detach ??= DetachCoreAsync(); }
        private async Task DetachCoreAsync()
        {
            try { await _playback.DisposeAsync().ConfigureAwait(false); }
            finally { await _sent.DisposeAsync().ConfigureAwait(false); }
            await InputCompletion.ConfigureAwait(false);
        }
        private async Task<PcmRecordingResult> StopCoreAsync(TimeSpan duration, CancellationToken token)
        {
            try { await DrainOnceAsync().ConfigureAwait(false); return await _session.StopAsync(duration, token).ConfigureAwait(false); }
            finally { await _session.DisposeAsync().ConfigureAwait(false); }
        }
        public ValueTask DisposeAsync()
        {
            lock (_gate) return new(_dispose ??= _stop is { } stop ? AwaitStopAsync(stop) : AbortAsync());
        }
        private static async Task AwaitStopAsync(Task stop) => await stop.ConfigureAwait(false);
        private async Task AbortAsync()
        {
            try { await DrainOnceAsync().ConfigureAwait(false); }
            finally { await _session.DisposeAsync().ConfigureAwait(false); }
        }
    }
}
