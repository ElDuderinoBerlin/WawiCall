using System.Net;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Audio;

// SIPSorcery and the PCM bridge keep this endpoint for the entire call. Native devices are replaceable.
internal sealed class RecoveringConversationAudio : IAudioSource, IAudioSink, ISipConversationAudio, IAsyncDisposable
{
    private readonly Lock _commands = new();
    private readonly Lock _render = new();
    private readonly IConversationAudioHardware _hardware;
    private readonly Func<ConversationDeviceSelection> _selection;
    private readonly PcmObservingAudioEncoder _encoder;
    private readonly ILogger _log;
    private int _playbackVolumePercent;
    private int _configuredPlaybackVolumePercent;
    private int _microphoneVolumePercent;
    private bool _microphoneMuted;
    private readonly ConversationPcmPeaks _peaks;
    private readonly IConversationPcmSubscription _peakSubscription;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _worker;
    private List<AudioFormat> _formats;
    private bool _captureStarted, _capturePaused, _captureUnavailable;
    private bool _playbackStarted, _playbackPaused, _playbackUnavailable;
    private AudioFormat? _sendFormat, _receiveFormat;
    private OpenDevice? _capture, _playback;
    private CancellationTokenSource? _playbackChanged;
    private Task? _dispose;

    internal RecoveringConversationAudio(IConversationAudioHardware hardware, Func<ConversationDeviceSelection> selection,
        PcmObservingAudioEncoder encoder, AudioDeviceSettings settings, bool enableG722, TimeProvider clock, ILogger log)
    {
        _hardware = hardware;
        _selection = selection;
        _encoder = encoder;
        _log = log;
        _playbackVolumePercent = _configuredPlaybackVolumePercent = settings.PlaybackVolumePercent;
        _microphoneVolumePercent = settings.MicrophoneVolumePercent;
        _microphoneMuted = settings.MicrophoneMuted;
        _peaks = new(clock);
        _peakSubscription = encoder.Subscribe(_peaks);
        _formats = encoder.SupportedFormats.Where(format => format.Codec is AudioCodecsEnum.PCMA or AudioCodecsEnum.PCMU
            || enableG722 && format.Codec == AudioCodecsEnum.G722).ToList();
        _worker = Task.Run(() => MonitorAsync(clock, _stopping.Token));
    }

    public event EncodedSampleDelegate? OnAudioSourceEncodedSample;
    public event Action<EncodedAudioFrame>? OnAudioSourceEncodedFrameReady;
    public event RawAudioSampleDelegate? OnAudioSourceRawSample { add { } remove { } }
    // Device failures are handled locally and logged while the SIP dialog remains active.
    public event SourceErrorDelegate? OnAudioSourceError { add { } remove { } }
    public event SourceErrorDelegate? OnAudioSinkError { add { } remove { } }

    public bool HasEncodedAudioSubscribers() => OnAudioSourceEncodedSample is not null;
    public bool IsAudioSourcePaused() { lock (_commands) return _capturePaused; }
    public List<AudioFormat> GetAudioSourceFormats() { lock (_commands) return [.. _formats]; }
    public List<AudioFormat> GetAudioSinkFormats() => GetAudioSourceFormats();
    public void RestrictFormats(Func<AudioFormat, bool> filter) { lock (_commands) _formats = _formats.Where(filter).ToList(); }

    internal void SetCodecOrder(string order)
    {
        var codecs = order.Split(',', StringSplitOptions.TrimEntries)
            .Select(Enum.Parse<AudioCodecsEnum>);
        lock (_commands)
            _formats = codecs.SelectMany(codec => _formats.Where(format => format.Codec == codec)).ToList();
    }

    public void ExternalAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample) =>
        throw new NotSupportedException();

    public void SetAudioSourceFormat(AudioFormat audioFormat) { lock (_commands) _sendFormat = audioFormat; }
    public void SetAudioSinkFormat(AudioFormat audioFormat) { lock (_commands) _receiveFormat = audioFormat; }
    public Task StartAudio() { lock (_commands) _captureStarted = true; return Task.CompletedTask; }
    public Task StartAudioSink() { lock (_commands) _playbackStarted = true; return Task.CompletedTask; }

    internal void EnableNegotiatedAudio(AudioFormat format)
    {
        if (format.IsEmpty()) return;
        lock (_commands)
        {
            _sendFormat ??= format;
            _receiveFormat ??= format;
            _captureStarted = true;
            _playbackStarted = true;
        }
    }

    public ConversationAudioSnapshot GetAudio()
    {
        var volume = Volatile.Read(ref _playbackVolumePercent);
        var muted = Volatile.Read(ref _microphoneMuted);
        var captureActive = !muted && !Volatile.Read(ref _capturePaused) && Volatile.Read(ref _capture) is not null;
        var playbackActive = !Volatile.Read(ref _playbackPaused) && Volatile.Read(ref _playback) is not null;
        return new(volume, muted, captureActive ? _peaks.MicrophonePercent : 0, playbackActive ? _peaks.PlaybackPercent(volume) : 0)
        {
            MicrophoneSpectrum = captureActive ? _peaks.MicrophoneSpectrum : AudioSpectrum.Silence,
            PlaybackSpectrum = playbackActive ? _peaks.PlaybackSpectrum(volume) : AudioSpectrum.Silence
        };
    }

    public void SetPlaybackVolume(int percent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(percent, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        lock (_commands)
        {
            if (_stopping.IsCancellationRequested) return;
            OpenDevice? failed = null;
            lock (_render)
            {
                if (_playback is { } output)
                {
                    try { output.Device.SetVolume(percent / 100F); }
                    catch { failed = output; }
                }
                Volatile.Write(ref _playbackVolumePercent, percent);
            }
            if (failed is not null) ReplacePlayback(null);
        }
    }

    public void SetMicrophoneMuted(bool muted)
    {
        lock (_commands)
        {
            if (_stopping.IsCancellationRequested || _microphoneMuted == muted) return;
            Volatile.Write(ref _microphoneMuted, muted);
            if (muted) ReplaceCapture(null);
        }
    }

    public Task PauseAudio()
    {
        lock (_commands)
        {
            if (_capturePaused) return Task.CompletedTask;
            _capturePaused = true;
            if (_capture is { } current)
            {
                try { current.Device.Source.PauseAudio().GetAwaiter().GetResult(); }
                catch { ReplaceCapture(null); }
            }
        }
        return Task.CompletedTask;
    }

    public Task ResumeAudio()
    {
        lock (_commands)
        {
            if (!_capturePaused) return Task.CompletedTask;
            _capturePaused = false;
            if (_capture is { } current)
            {
                try
                {
                    current.Device.Source.ResumeAudio().GetAwaiter().GetResult();
                    current.Device.SetVolume(_microphoneVolumePercent / 100F);
                }
                catch { ReplaceCapture(null); }
            }
        }
        return Task.CompletedTask;
    }

    public Task PauseAudioSink() { lock (_render) _playbackPaused = true; return Task.CompletedTask; }
    public Task ResumeAudioSink() { lock (_render) _playbackPaused = false; return Task.CompletedTask; }

    public void GotEncodedMediaFrame(EncodedAudioFrame frame)
    {
        var format = frame.AudioFormat;
        if (format.IsEmpty()) return;
        lock (_commands)
        {
            // The SDP selection starts playback provisionally. SIPSorcery supplies the actual received format per frame.
            if (_playbackStarted && !_stopping.IsCancellationRequested && !format.Equals(_receiveFormat) &&
                _formats.Any(candidate => candidate.Codec == format.Codec && candidate.ClockRate == format.ClockRate &&
                    candidate.RtpClockRate == format.RtpClockRate && candidate.ChannelCount == format.ChannelCount))
            {
                _receiveFormat = format;
                RefreshPlayback(_selection().PlaybackId, format);
            }
            lock (_render)
            {
                if (!_playbackPaused && _playback is { } current && current.Format.Equals(format))
                    current.Device.Sink.GotEncodedMediaFrame(frame);
                else
                    _encoder.DecodeAudio(frame.EncodedAudio, format);
            }
        }
    }

    public void GotAudioRtp(IPEndPoint remoteEndPoint, uint ssrc, uint seqnum, uint timestamp, int payloadID, bool marker, byte[] payload)
    {
        AudioFormat? format;
        lock (_commands) format = _receiveFormat;
        if (format is { } negotiated && negotiated.FormatID == payloadID)
            GotEncodedMediaFrame(new EncodedAudioFrame(0, negotiated, 0, payload));
    }

    internal PlaybackDeviceBinding ReadPlaybackBinding()
    {
        lock (_render)
        {
            if (_playback is null || _playbackChanged is null)
                throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
            return new(_playback.Id, _playbackChanged.Token);
        }
    }

    private async Task MonitorAsync(TimeProvider clock, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        try { while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false)) RefreshDevices(); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    internal void RefreshDevices()
    {
        lock (_commands)
        {
            if (_stopping.IsCancellationRequested) return;
            var preferred = _selection();
            if (preferred.PlaybackVolumePercent is { } playbackVolume && playbackVolume != _configuredPlaybackVolumePercent)
            {
                _configuredPlaybackVolumePercent = playbackVolume;
                SetPlaybackVolume(playbackVolume);
            }
            if (preferred.MicrophoneVolumePercent is { } microphoneVolume && microphoneVolume != _microphoneVolumePercent)
            {
                _microphoneVolumePercent = microphoneVolume;
                if (!_capturePaused && _capture is { } input)
                {
                    try { input.Device.SetVolume(microphoneVolume / 100F); }
                    catch { ReplaceCapture(null); }
                }
            }
            if (_captureStarted && !_microphoneMuted && _sendFormat is { } send && !send.IsEmpty()) RefreshCapture(preferred.CaptureId, send);
            if (_playbackStarted && _receiveFormat is { } receive && !receive.IsEmpty()) RefreshPlayback(preferred.PlaybackId, receive);
        }
    }

    private void RefreshCapture(string? preferred, AudioFormat format)
    {
        var id = Resolve(preferred, ConversationAudioDirection.Capture);
        if (!Healthy(_capture, id, format, ConversationAudioDirection.Capture, _capturePaused)) ReplaceCapture(null);
        if (_capture is { } current && current.Id == id)
        {
            ReportAvailability(ref _captureUnavailable, true, "Mikrofon");
            return;
        }

        if (id is not null)
        {
            OpenDevice? candidate = null;
            var previous = _capture;
            try
            {
                candidate = new(id, format, _hardware.Open(id, ConversationAudioDirection.Capture, _encoder, format));
                candidate.Device.SetVolume(_microphoneVolumePercent / 100F);
                if (!_capturePaused && previous is not null) previous.Device.Source.PauseAudio().GetAwaiter().GetResult();
                AttachCapture(candidate);
                if (!_capturePaused)
                {
                    candidate.Device.Source.StartAudio().GetAwaiter().GetResult();
                    if (!candidate.Device.Running) throw new InvalidOperationException("The microphone did not start.");
                }
                ReplaceCapture(candidate);
                ReportAvailability(ref _captureUnavailable, true, "Mikrofon");
                return;
            }
            catch
            {
                ReleaseCapture(candidate);
                if (!_capturePaused && previous is not null && !previous.Device.Running)
                {
                    try { previous.Device.Source.ResumeAudio().GetAwaiter().GetResult(); }
                    catch { ReplaceCapture(null); }
                }
            }
        }
        ReportAvailability(ref _captureUnavailable, false, "Mikrofon");
    }

    private void RefreshPlayback(string? preferred, AudioFormat format)
    {
        var id = Resolve(preferred, ConversationAudioDirection.Playback);
        if (!Healthy(_playback, id, format, ConversationAudioDirection.Playback)) ReplacePlayback(null);
        if (_playback is { } current && current.Id == id)
        {
            ReportAvailability(ref _playbackUnavailable, true, "Gesprächsausgabe");
            return;
        }

        if (id is not null)
        {
            OpenDevice? candidate = null;
            try
            {
                candidate = new(id, format, _hardware.Open(id, ConversationAudioDirection.Playback, _encoder, format));
                candidate.Device.SetVolume(_playbackVolumePercent / 100F);
                if (!candidate.Device.Running) throw new InvalidOperationException("The output did not start.");
                ReplacePlayback(candidate);
                ReportAvailability(ref _playbackUnavailable, true, "Gesprächsausgabe");
                return;
            }
            catch { Release(candidate); }
        }
        ReportAvailability(ref _playbackUnavailable, false, "Gesprächsausgabe");
    }

    private bool Healthy(OpenDevice? current, string? desired, AudioFormat format, ConversationAudioDirection direction, bool paused = false) =>
        current is not null && current.Format.Equals(format) && (paused || current.Device.Running)
        && (current.Id == desired || Resolve(current.Id, direction) == current.Id);

    private string? Resolve(string? id, ConversationAudioDirection direction)
    {
        try { return _hardware.Resolve(id, direction); }
        catch { return null; }
    }

    private void AttachCapture(OpenDevice device)
    {
        device.Device.Source.OnAudioSourceEncodedSample += ForwardSample;
        device.Device.Source.OnAudioSourceEncodedFrameReady += ForwardFrame;
    }

    private void ForwardSample(uint duration, byte[] payload) => OnAudioSourceEncodedSample?.Invoke(duration, payload);
    private void ForwardFrame(EncodedAudioFrame frame) => OnAudioSourceEncodedFrameReady?.Invoke(frame);

    private void ReplaceCapture(OpenDevice? next)
    {
        var previous = _capture;
        _capture = next;
        ReleaseCapture(previous);
        _peaks.ResetMicrophone();
    }

    private void ReleaseCapture(OpenDevice? device)
    {
        if (device is null) return;
        // Keep forwarding while the native capture thread drains its final callback.
        try { Release(device); }
        finally
        {
            device.Device.Source.OnAudioSourceEncodedSample -= ForwardSample;
            device.Device.Source.OnAudioSourceEncodedFrameReady -= ForwardFrame;
        }
    }

    private void ReplacePlayback(OpenDevice? next)
    {
        OpenDevice? previous;
        CancellationTokenSource? changed;
        lock (_render)
        {
            previous = _playback;
            changed = _playbackChanged;
            _playback = next;
            _playbackChanged = next is null ? null : new();
        }
        // SIPSorcery closes the sink at hangup before the recording owner drains loopback.
        // During the call, invalidating the playback binding still ends the current recording segment.
        using (changed)
        {
            if (_playbackStarted || _stopping.IsCancellationRequested) changed?.Cancel();
        }
        Release(previous);
        _peaks.ResetPlayback();
    }

    private void Release(OpenDevice? device)
    {
        try { device?.Device.Dispose(); }
        catch { _log.LogWarning("Ein Gesprächsaudiogerät konnte nicht vollständig geschlossen werden."); }
    }

    private void ReportAvailability(ref bool unavailable, bool available, string direction)
    {
        if (unavailable != available) return;
        unavailable = !available;
        if (available) _log.LogInformation("{AudioDirection} ist wieder verbunden.", direction);
        else OperationEvents.Write(_log, direction == "Mikrofon"
            ? OperationEvent.ConversationMicrophoneUnavailable : OperationEvent.ConversationPlaybackUnavailable);
    }

    public Task CloseAudio()
    {
        lock (_commands) { _captureStarted = false; ReplaceCapture(null); }
        return Task.CompletedTask;
    }

    public Task CloseAudioSink()
    {
        lock (_commands) { _playbackStarted = false; ReplacePlayback(null); }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (_commands) return new(_dispose ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        try { await _worker.ConfigureAwait(false); }
        finally
        {
            await CloseAudio().ConfigureAwait(false);
            await CloseAudioSink().ConfigureAwait(false);
            await _peakSubscription.DisposeAsync().ConfigureAwait(false);
            _stopping.Dispose();
        }
    }

    private sealed record OpenDevice(string Id, AudioFormat Format, IConversationAudioDevice Device);
}
