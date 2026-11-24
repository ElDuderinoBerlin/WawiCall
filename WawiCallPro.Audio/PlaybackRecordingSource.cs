using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Marks the recording input only. No output device is opened until an explicit recording starts.</summary>
public sealed class PlaybackRecordingSource(IConversationPcmFailureSource sent, string? deviceId) : IConversationPcmFailureSource
{
    private readonly Func<PlaybackDeviceBinding>? _binding;
    private PlaybackRecordingSource(IConversationPcmFailureSource sent, Func<PlaybackDeviceBinding> binding) : this(sent, (string?)null) =>
        _binding = binding;

    internal static PlaybackRecordingSource ForActivePlayback(IConversationPcmFailureSource sent, Func<PlaybackDeviceBinding> binding) =>
        new(sent, binding);

    public IConversationPcmFailureSource Sent { get; } = sent;
    public string? DeviceId { get; } = deviceId;
    public long FailureCount => Sent.FailureCount;
    public PcmObservationFailure? LastFailure => Sent.LastFailure;
    public IConversationPcmSubscription Subscribe(IConversationPcmObserver observer) => Sent.Subscribe(observer);
    internal PlaybackDeviceBinding ReadBinding() => _binding?.Invoke() ?? new(DeviceId, CancellationToken.None);
    internal PlaybackRecordingSource WithSent(IConversationPcmFailureSource replacement) =>
        _binding is { } binding ? new(replacement, binding) : new(replacement, DeviceId);
}

internal readonly record struct PlaybackDeviceBinding(string? DeviceId, CancellationToken Changed);

/// <summary>The call keeps its recording mode. Loopback binds to the current output when an individual segment starts.</summary>
public sealed class RecordingInputMediaSession : ISipPcmMediaSession
{
    private readonly ISipPcmMediaSession _inner;
    public RecordingInputMediaSession(ISipPcmMediaSession inner, AudioDeviceSettings devices)
        : this(inner, devices, null) { }

    internal RecordingInputMediaSession(ISipPcmMediaSession inner, AudioDeviceSettings devices, Func<PlaybackDeviceBinding>? binding)
    {
        _inner = inner;
        if (!Enum.IsDefined(devices.RecordingReceiveSource)) throw new ArgumentOutOfRangeException(nameof(devices));
        PcmSource = inner.PcmSource;
        if (devices.RecordingReceiveSource == RecordingReceiveSource.PlaybackLoopback)
        {
            var sent = PcmSource as IConversationPcmFailureSource
                ?? throw new InvalidOperationException("A failure-aware recording source is required.");
            PcmSource = binding is null ? new PlaybackRecordingSource(sent, devices.PlaybackDeviceId)
                : PlaybackRecordingSource.ForActivePlayback(sent, binding);
        }
    }
    public SIPSorcery.SIP.App.IMediaSession Session => _inner.Session;
    public IConversationPcmSource PcmSource { get; }
    public void ApplyPlaybackSettings() => _inner.ApplyPlaybackSettings();
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

/// <summary>Mono PCM16 from a render endpoint, positioned by WASAPI's packet clock on the recording epoch.</summary>
public interface IPlaybackPcmObserver
{
    bool TryObserve(TimeSpan position, int sampleRate, ReadOnlySpan<short> samples);
    void OnFailure();
}

public interface IPlaybackPcmCapture : IAsyncDisposable
{
    Task Completion { get; }
}

public interface IPlaybackPcmCaptureFactory
{
    Task<IPlaybackPcmCapture> StartAsync(string? deviceId, DateTimeOffset epoch, IPlaybackPcmObserver observer,
        CancellationToken cancellationToken = default);
}

/// <summary>The default remains RTP. Loopback replaces only the received side; the sent side stays codec/RTP-bound.</summary>
public sealed class RecordingSourceFactory(WawiCallPro.Core.Audio.DuplexPcmRecordingFactory rtp,
    PlaybackDuplexRecordingFactory playback) : IDuplexPcmRecordingFactory
{
    public Task<IDuplexPcmRecording> StartAsync(IConversationPcmFailureSource source, DuplexPcmRecordingRequest request,
        CancellationToken cancellationToken = default) => source is PlaybackRecordingSource loopback
            ? playback.StartAsync(loopback, request, cancellationToken) : rtp.StartAsync(source, request, cancellationToken);
}
