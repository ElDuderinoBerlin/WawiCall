using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Telephony.Sip;

public sealed partial class SipTelephonyService : ILiveCallRecordingService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, LiveRecordingSnapshot> _recordingStates = new();
    public event EventHandler<LiveRecordingChangedEventArgs>? RecordingChanged;
    public LiveRecordingSnapshot GetRecording(Guid callId) => _recordingStates.GetValueOrDefault(callId)
        ?? new(callId, null, LiveRecordingState.Idle);

    public Task StartLiveRecordingAsync(Guid callId, CancellationToken cancellationToken = default) =>
        CommandAsync(callId, agent => EnqueueAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_stopping || !ReferenceEquals(_agent, agent) || agent.State?.Current.State != CallState.Connected)
                throw new TelephonyException("CallNotConnected");
            if (agent.AutomaticRecordingStart is { IsCompleted: false })
                throw new TelephonyException("RecordingAlreadyActive");
            await StartRecordingOnMediaAsync(agent, cancellationToken).ConfigureAwait(false);
        }), cancellationToken);

    private async Task CompleteMediaConnectionAsync(Agent agent)
    {
        await ChangeAsync(agent, CallState.Connected).ConfigureAwait(false);
        if (!_stopping && agent.State?.Current.State == CallState.Connected)
            Track(() => StartAutomaticRecordingAsync(agent));
    }

    private async Task StartAutomaticRecordingAsync(Agent agent)
    {
        var stopping = agent.Stopping.Token;
        try
        {
            var saved = await _settings.LoadAsync(stopping).ConfigureAwait(false);
            if (!saved.Options.Devices.RecordConnectedCallsAutomatically) return;

            await EnqueueAsync(() =>
            {
                if (!_stopping && ReferenceEquals(_agent, agent) && agent.State?.Current.State == CallState.Connected
                    && agent.AutomaticRecordingStart is null && GetRecording(agent.Id).State == LiveRecordingState.Idle)
                {
                    // Waiting for both PCM inputs must not occupy the SIP event queue.
                    agent.AutomaticRecordingStart = Task.Run(() => StartRecordingOnMediaAsync(agent, stopping), stopping);
                }
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            if (agent.AutomaticRecordingStart is { } start) await start.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested || agent.AutomaticRecordingStart is not null) { }
        catch (Exception)
        {
            if (!stopping.IsCancellationRequested && GetRecording(agent.Id).State == LiveRecordingState.Idle)
                LiveRecording_Changed(this, new(new(agent.Id, null, LiveRecordingState.Failed) { Failure = LiveRecordingFailure.Unexpected }));
        }
    }

    private async Task StartRecordingOnMediaAsync(Agent agent, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (agent.Media is not ISipLiveRecordingMediaSession media)
        {
            LiveRecording_Changed(this, new(new(agent.Id, null, LiveRecordingState.Failed) { Failure = LiveRecordingFailure.Unavailable }));
            throw new TelephonyException("RecordingUnavailable");
        }
        media.RecordingChanged -= LiveRecording_Changed;
        media.RecordingChanged += LiveRecording_Changed;
        await media.StartLiveAsync(agent.Id, token).ConfigureAwait(false);
    }

    public async Task StopLiveRecordingAsync(Guid callId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (GetRecording(callId).State == LiveRecordingState.Starting) CancelRecordingStartup(callId);
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (GetRecording(callId).State is LiveRecordingState.Completed or LiveRecordingState.Idle) return;
            if (_calls.TryGetValue(callId, out var call) && call.State is CallState.Ended or CallState.Failed
                && GetRecording(callId).State == LiveRecordingState.Failed) return;
            var agent = GetAgent(callId);
            await EnqueueAsync(async () =>
            {
                if (GetRecording(callId) is { State: LiveRecordingState.Completed } or
                    { State: LiveRecordingState.Failed, Failure: LiveRecordingFailure.Cancelled }) return;
                cancellationToken.ThrowIfCancellationRequested();
                if (agent.Media is not ISipLiveRecordingMediaSession media) throw new TelephonyException("RecordingUnavailable");
                await media.StopLiveAsync(cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        finally { _commands.Release(); }
    }
    private void CancelRecordingStartup(Guid callId)
    {
        ISipLiveRecordingMediaSession? media;
        lock (_gate) media = _agent?.Id == callId ? _agent.Media as ISipLiveRecordingMediaSession : null;
        media?.CancelPendingStart();
    }
    private void LiveRecording_Changed(object? sender, LiveRecordingChangedEventArgs e)
    {
        if (e.Snapshot.CallId == Guid.Empty) return;
        _recordingStates[e.Snapshot.CallId] = e.Snapshot;
        foreach (var old in _recordingStates.Keys.Where(id => !_calls.ContainsKey(id) && id != e.Snapshot.CallId))
            _recordingStates.TryRemove(old, out _);
        if (e.Snapshot.State == LiveRecordingState.Failed && e.Snapshot.Failure != LiveRecordingFailure.Cancelled)
        {
            var operation = e.Snapshot.Failure switch
            {
                LiveRecordingFailure.SignalMissing => OperationEvent.LiveRecordingSignalMissing,
                LiveRecordingFailure.InputFailed => OperationEvent.LiveRecordingInputFailed,
                LiveRecordingFailure.StorageFailed => OperationEvent.LiveRecordingStorageFailed,
                LiveRecordingFailure.Unavailable => OperationEvent.LiveRecordingUnavailable,
                _ => OperationEvent.LiveRecordingFailed
            };
            OperationEvents.Write(_log, operation);
        }
        Publish(RecordingChanged, e);
    }
}
