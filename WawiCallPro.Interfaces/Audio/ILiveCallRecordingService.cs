namespace WawiCallPro.Interfaces.Audio;

public enum LiveRecordingState { Idle, Starting, Recording, Stopping, Completed, Failed }

public enum LiveRecordingFailure { Cancelled, SignalMissing, InputFailed, StorageFailed, Unavailable, Unexpected }

/// <summary>One call's local recording. No filenames or remote identities are exposed in status events.</summary>
public sealed record LiveRecordingSnapshot(Guid CallId, Guid? RecordingId, LiveRecordingState State)
{
    public LiveRecordingFailure? Failure { get; init; }
}

public sealed class LiveRecordingChangedEventArgs(LiveRecordingSnapshot snapshot) : EventArgs
{
    public LiveRecordingSnapshot Snapshot { get; } = snapshot;
}

/// <summary>Optional call capability. Start is explicit; Stop and call termination drain and finalize the owned recording.</summary>
public interface ILiveCallRecordingService
{
    LiveRecordingSnapshot GetRecording(Guid callId);
    event EventHandler<LiveRecordingChangedEventArgs>? RecordingChanged;
    Task StartLiveRecordingAsync(Guid callId, CancellationToken cancellationToken = default);
    Task StopLiveRecordingAsync(Guid callId, CancellationToken cancellationToken = default);
}
