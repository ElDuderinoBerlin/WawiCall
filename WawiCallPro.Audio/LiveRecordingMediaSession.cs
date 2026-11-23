using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Native resources belong to the existing media owner. Live capture is finalized before that owner closes.</summary>
public sealed class LiveRecordingMediaSession(ISipPcmMediaSession inner, IDuplexPcmRecordingFactory recordings,
    LivePcmRecordingFactory live, ApplicationPaths paths) : ISipLiveRecordingMediaSession
{
    private readonly RecordingSipMediaSession _legacy = new(inner, recordings);
    private readonly SemaphoreSlim _commands = new(1);
    private readonly object _gate = new();
    private readonly CancellationTokenSource _closing = new();
    private LivePcmRecording? _recording;
    private IDuplexPcmRecording? _legacyRecording;
    private Task? _dispose;
    private Task _watch = Task.CompletedTask;
    private CancellationTokenSource? _startup;
    private LiveRecordingSnapshot _status = new(Guid.Empty, null, LiveRecordingState.Idle);
    public SIPSorcery.SIP.App.IMediaSession Session => inner.Session;
    public IConversationPcmSource PcmSource => inner.PcmSource;
    public void ApplyPlaybackSettings() => inner.ApplyPlaybackSettings();
    public LiveRecordingSnapshot RecordingStatus => Volatile.Read(ref _status);
    public event EventHandler<LiveRecordingChangedEventArgs>? RecordingChanged;
    private void SetStatus(LiveRecordingState state, LiveRecordingFailure? failure = null)
    {
        var value = _status with { State = state, Failure = failure }; Volatile.Write(ref _status, value);
        foreach (var handler in RecordingChanged?.GetInvocationList() ?? [])
            try { ((EventHandler<LiveRecordingChangedEventArgs>)handler)(this, new(value)); } catch { }
    }
    public async Task StartLiveAsync(Guid callId, CancellationToken cancellationToken)
    {
        if (callId == Guid.Empty) throw new ArgumentException("Ein laufendes Gespräch ist erforderlich.", nameof(callId));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closing.Token);
        await _commands.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            lock (_gate) _startup = linked;
            linked.Token.ThrowIfCancellationRequested();
            if (_recording is { State: PcmRecordingState.Capturing or PcmRecordingState.Stopping }) throw new TelephonyException("RecordingAlreadyActive");
            if (_legacyRecording is { State: PcmRecordingState.Capturing or PcmRecordingState.Stopping }) throw new TelephonyException("RecordingAlreadyActive");
            if (_recording is { } previous)
            {
                try { await previous.DisposeAsync().ConfigureAwait(false); }
                catch (PcmRecordingException) when (!previous.HasPendingPersistence) { }
                _recording = null;
            }
            var id = Guid.NewGuid(); _status = new(callId, id, LiveRecordingState.Starting); SetStatus(LiveRecordingState.Starting);
            try
            {
                if (inner.PcmSource is not IConversationPcmFailureSource source) throw new TelephonyException("RecordingUnavailable");
                _recording = await live.StartAsync(source, new(callId, id, paths.ResolveRecordingPath(id.ToString("N") + ".wav")), linked.Token).ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();
                SetStatus(LiveRecordingState.Recording);
                _watch = WatchInputAsync(_recording, _closing.Token);
            }
            catch (Exception error)
            {
                SetStatus(LiveRecordingState.Failed, ClassifyFailure(error));
                if (_recording is { } late) { _recording = null; await late.DisposeAsync().ConfigureAwait(false); }
                throw;
            }
        }
        finally { lock (_gate) _startup = null; _commands.Release(); }
    }
    public void CancelPendingStart() { lock (_gate) _startup?.Cancel(); }
    private async Task WatchInputAsync(LivePcmRecording recording, CancellationToken closing)
    {
        try
        {
            while (ReferenceEquals(_recording, recording) && recording.State == PcmRecordingState.Capturing
                && RecordingStatus.State == LiveRecordingState.Recording)
                await Task.Delay(100, closing).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (closing.IsCancellationRequested) { return; }
        if (recording.State != PcmRecordingState.Failed) return;
        // Persist the input failure and retire its subscription promptly, while the conversation stays connected.
        await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            if (ReferenceEquals(_recording, recording))
            {
                try { await StopCoreAsync().ConfigureAwait(false); }
                catch (Exception) { } // StopCore reports the failure through RecordingChanged.
            }
        }
        finally { _commands.Release(); }
    }
    public async Task StopLiveAsync(CancellationToken cancellationToken)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await StopCoreAsync().ConfigureAwait(false); }
        finally { _commands.Release(); }
    }
    private async Task StopCoreAsync()
    {
        if (_recording is not { } recording) return;
        SetStatus(LiveRecordingState.Stopping);
        try
        {
            try { await recording.StopAsync().ConfigureAwait(false); }
            finally { await recording.DisposeAsync().ConfigureAwait(false); }
            SetStatus(LiveRecordingState.Completed);
        }
        catch (Exception error)
        {
            SetStatus(LiveRecordingState.Failed, ClassifyFailure(error));
            throw;
        }
        finally { _recording = null; }
    }
    private static LiveRecordingFailure ClassifyFailure(Exception error) => error switch
    {
        OperationCanceledException => LiveRecordingFailure.Cancelled,
        TimeoutException => LiveRecordingFailure.SignalMissing,
        PcmRecordingException { Failure: PcmRecordingFailure.FileFailure or PcmRecordingFailure.MixingFailed }
            or WawiCallPro.Database.DatabaseOperationException or WawiCallPro.Database.Contracts.RepositoryConflictException
            or RecordingPersistenceException or IOException or UnauthorizedAccessException => LiveRecordingFailure.StorageFailed,
        PcmRecordingException => LiveRecordingFailure.InputFailed,
        TelephonyException { Code: "RecordingUnavailable" } => LiveRecordingFailure.Unavailable,
        _ => LiveRecordingFailure.Unexpected
    };

    public async Task<IDuplexPcmRecording> StartRecordingAsync(DuplexPcmRecordingRequest request, CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_closing.IsCancellationRequested) throw new ObjectDisposedException(nameof(LiveRecordingMediaSession));
            if (_recording is not null) throw new TelephonyException("RecordingAlreadyActive");
            return _legacyRecording = await _legacy.StartRecordingAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally { _commands.Release(); }
    }
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_dispose is not null) return new(_dispose);
            _closing.Cancel();
            return new(_dispose ??= DisposeCoreAsync());
        }
    }
    private async Task DisposeCoreAsync()
    {
        await _commands.WaitAsync().ConfigureAwait(false);
        try { try { await StopCoreAsync().ConfigureAwait(false); } finally { await _legacy.DisposeAsync().ConfigureAwait(false); } }
        finally { _commands.Release(); await _watch.ConfigureAwait(false); _closing.Dispose(); }
    }
}

/// <summary>Production hosts share one factory; each call receives one independently owned media/recording scope.</summary>
public sealed class LivePersistedSipMediaSessionFactory(WindowsSipMediaSessionFactory media,
    IPersistedDuplexPcmRecordingFactory recordings, LivePcmRecordingFactory live, ApplicationPaths paths) : ISipMediaSessionFactory
{
    public ISipMediaSession Create(SipAccountOptions account, AudioDeviceSettings devices) =>
        new LiveRecordingMediaSession((ISipPcmMediaSession)media.Create(account, devices), recordings, live, paths);
}
