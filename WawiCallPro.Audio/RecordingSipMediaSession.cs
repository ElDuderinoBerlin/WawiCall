using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Audio;

/// <summary>Owns one media session and at most one live recording. Closing never invents a final duration.</summary>
public sealed class RecordingSipMediaSession(ISipPcmMediaSession inner, IDuplexPcmRecordingFactory recordings) : ISipRecordingMediaSession
{
    private readonly SemaphoreSlim _commands = new(1);
    private readonly object _gate = new();
    private IDuplexPcmRecording? _recording;
    private Task? _dispose;
    private int _closing;

    public SIPSorcery.SIP.App.IMediaSession Session => inner.Session;
    public IConversationPcmSource PcmSource => inner.PcmSource;
    public void ApplyPlaybackSettings() => inner.ApplyPlaybackSettings();

    public async Task<IDuplexPcmRecording> StartRecordingAsync(DuplexPcmRecordingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closing) != 0, this);
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _closing) != 0, this);
            if (_recording is { State: PcmRecordingState.Capturing or PcmRecordingState.Stopping })
                throw new TelephonyException("RecordingAlreadyActive");
            if (_recording is { } previous)
            {
                try { await previous.DisposeAsync().ConfigureAwait(false); }
                finally { _recording = null; }
            }
            if (inner.PcmSource is not IConversationPcmFailureSource source)
                throw new TelephonyException("RecordingUnavailable");
            _recording = await recordings.StartAsync(source, request, cancellationToken).ConfigureAwait(false);
            if (Volatile.Read(ref _closing) != 0)
                throw new ObjectDisposedException(nameof(RecordingSipMediaSession));
            return _recording;
        }
        finally { _commands.Release(); }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            Volatile.Write(ref _closing, 1);
            return new(_dispose ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _commands.WaitAsync().ConfigureAwait(false);
        try
        {
            try { if (_recording is { } recording) await recording.DisposeAsync().ConfigureAwait(false); }
            finally
            {
                _recording = null;
                await inner.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally { _commands.Release(); }
    }
}
