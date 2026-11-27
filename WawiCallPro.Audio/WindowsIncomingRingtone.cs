using System.Buffers.Binary;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.Streams;
using WawiCallPro.Core.Audio;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Opens a dedicated local WASAPI stream for the incoming-call signal.</summary>
public sealed class WindowsIncomingRingtone : IIncomingRingtone
{
    private readonly IHistoryRecordingOutput _output;
    private readonly WindowsRingtoneFiles _files;

    public WindowsIncomingRingtone(IHistoryRecordingOutput output) : this(output, new WindowsRingtoneFiles()) { }

    internal WindowsIncomingRingtone(IHistoryRecordingOutput output, string? mediaDirectory)
        : this(output, new WindowsRingtoneFiles(mediaDirectory)) { }

    private WindowsIncomingRingtone(IHistoryRecordingOutput output, WindowsRingtoneFiles files)
    {
        _output = output;
        _files = files;
    }

    public IReadOnlyList<string> ReadWindowsRingtones() => _files.ReadNames();

    public static PcmWaveHeader? ReadCustomRingtoneFormat(string path, CancellationToken token)
    {
        using var file = WindowsRingtoneFiles.TryOpenCustom(path, token, out var format);
        return format;
    }

    /// <summary>Checks whether a local ringtone can be decoded and repeated.</summary>
    public static WaveFormat? ProbeCustomRingtone(string path, CancellationToken token)
    {
        using var source = WindowsRingtoneFiles.TryOpenCustomAudio(path, token);
        return source?.WaveFormat;
    }

    public Task PlayAsync(string? deviceId, int volumePercent, CancellationToken cancellationToken) =>
        PlayAsync(new AudioDeviceSettings { RingDeviceId = deviceId, RingVolumePercent = volumePercent }, cancellationToken);

    public Task PlayAsync(AudioDeviceSettings settings, CancellationToken cancellationToken)
    {
        Play(settings, preview: false, cancellationToken);
        return Task.CompletedTask;
    }

    public Task<bool> PreviewAsync(AudioDeviceSettings settings, CancellationToken token) => Task.Run(() =>
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
        limit.CancelAfter(TimeSpan.FromSeconds(4));
        return Play(settings, preview: true, limit.Token, token);
    }, token);

    public Task<bool> PreviewAsync(AudioDeviceSettings settings, AudioPlaybackVolume volume, CancellationToken token) => Task.Run(() =>
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        return Play(settings, preview: true, limit.Token, token, volume);
    }, token);

    private bool Play(AudioDeviceSettings settings, bool preview, CancellationToken token, CancellationToken caller = default,
        AudioPlaybackVolume? volume = null)
    {
        token.ThrowIfCancellationRequested();
        using var file = settings.CustomRingtonePath is null ? _files.TryOpen(settings.WindowsRingtoneFile, token) : null;
        using var wave = settings.CustomRingtonePath is { } custom
            ? WindowsRingtoneFiles.TryOpenCustomAudio(custom, token) : file is null ? null : new WaveFileReader(file);
        using var loop = wave is not null && (!preview || volume is not null)
            ? new LoopStream(wave) { DisposeBaseSource = false } : null;
        using var fallback = wave is null ? new RingtoneWaveSource() : null;
        var source = loop ?? wave ?? fallback!;
        var playback = settings with { PlaybackDeviceId = settings.RingDeviceId, PlaybackVolumePercent = settings.RingVolumePercent };
        try
        {
            if (volume is null) _output.Play(source, playback, token);
            else _output.Play(source, playback, volume, token);
        }
        catch (OperationCanceledException) when (preview && token.IsCancellationRequested && !caller.IsCancellationRequested) { }
        return settings is { WindowsRingtoneFile: null, CustomRingtonePath: null } || wave is not null;
    }

    private sealed class RingtoneWaveSource : IWaveSource
    {
        private const int SampleRate = 48000;
        private const int BytesPerSample = sizeof(short);
        private static readonly byte[] Cadence = CreateCadence();
        private int _offset;

        public WaveFormat WaveFormat { get; } = new(SampleRate, 16, 1);
        public bool CanSeek => false;
        public long Length => 0;
        public long Position { get => _offset; set => throw new NotSupportedException(); }

        public int Read(byte[] buffer, int offset, int count)
        {
            count -= count % BytesPerSample;
            var remaining = count;
            while (remaining > 0)
            {
                var length = Math.Min(remaining, Cadence.Length - _offset);
                Cadence.AsSpan(_offset, length).CopyTo(buffer.AsSpan(offset, length));
                _offset = (_offset + length) % Cadence.Length;
                offset += length;
                remaining -= length;
            }
            return count;
        }

        private static byte[] CreateCadence()
        {
            const int soundingSamples = SampleRate * 4 / 5;
            const int fadeSamples = SampleRate / 100;
            var bytes = new byte[SampleRate * 3 * BytesPerSample];
            for (var sample = 0; sample < soundingSamples; sample++)
            {
                var gain = Math.Min(1.0, Math.Min(sample, soundingSamples - sample - 1) / (double)fadeSamples);
                var phase = 2 * Math.PI * 440 * sample / SampleRate;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(sample * BytesPerSample), (short)(5200 * gain * Math.Sin(phase)));
            }
            return bytes;
        }

        public void Dispose() { }
    }
}
