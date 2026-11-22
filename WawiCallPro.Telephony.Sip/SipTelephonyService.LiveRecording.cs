using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;

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
            if (agent.Media is not ISipLiveRecordingMediaSession media) throw new TelephonyException("RecordingUnavailable");
            // Repeated starts retain a single handler; the media owner also reports its termination flush.
            media.RecordingChanged -= LiveRecording_Changed;
            media.RecordingChanged += LiveRecording_Changed;
            await media.StartLiveAsync(callId, cancellationToken).ConfigureAwait(false);
        }), cancellationToken);

    public async Task StopLiveRecordingAsync(Guid callId, CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (GetRecording(callId).State is LiveRecordingState.Completed or LiveRecordingState.Idle) return;
            if (_calls.TryGetValue(callId, out var call) && call.State is CallState.Ended or CallState.Failed
                && GetRecording(callId).State == LiveRecordingState.Failed) return;
            var agent = GetAgent(callId);
            await EnqueueAsync(async () =>
            {
                if (GetRecording(callId).State == LiveRecordingState.Completed) return;
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
        Publish(RecordingChanged, e);
    }
}
