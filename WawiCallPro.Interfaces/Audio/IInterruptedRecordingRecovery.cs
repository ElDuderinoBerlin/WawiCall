namespace WawiCallPro.Interfaces.Audio;

public sealed record InterruptedRecordingRecoveryResult(int Interrupted, int Deferred, int Failed);

/// <summary>Closes only unfinished local captures without a WAV or pending completion. Never reconstructs audio.</summary>
public interface IInterruptedRecordingRecovery
{
    Task<InterruptedRecordingRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default);
}
