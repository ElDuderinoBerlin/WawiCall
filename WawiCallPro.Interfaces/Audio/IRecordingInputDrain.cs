namespace WawiCallPro.Interfaces.Audio;

/// <summary>Final input extent in the recording's OUTPUT sample rate, observed after all callbacks have drained.</summary>
public interface IPcmRecordingInputExtent
{
    long InputFrameCount { get; }
}

/// <summary>Retires and drains all source callbacks, reorder buffers and native capture before choosing final duration.
/// Repeated calls share one drain. Does not publish a WAV or release the recording itself.</summary>
public interface IRecordingInputDrain
{
    Task<long> DrainInputAsync();
}
