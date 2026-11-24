using System.Buffers.Binary;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.Streams;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Opens a dedicated local WASAPI stream for the incoming-call signal.</summary>
public sealed class WindowsIncomingRingtone : IIncomingRingtone
{
    private readonly IHistoryRecordingOutput _output;
    private readonly string? _mediaDirectory;

    public WindowsIncomingRingtone(IHistoryRecordingOutput output) : this(output, GetMediaDirectory()) { }

    internal WindowsIncomingRingtone(IHistoryRecordingOutput output, string? mediaDirectory)
    {
        _output = output;
        _mediaDirectory = mediaDirectory;
    }

    public IReadOnlyList<string> ReadWindowsRingtones() => EnumerateFiles().Select(path => Path.GetFileName(path)!)
        .Order(StringComparer.OrdinalIgnoreCase).ToArray();

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

    private bool Play(AudioDeviceSettings settings, bool preview, CancellationToken token, CancellationToken caller = default)
    {
        token.ThrowIfCancellationRequested();
        using var file = OpenWindowsRingtone(settings.WindowsRingtoneFile, token);
        using var wave = file is null ? null : new WaveFileReader(file);
        using IWaveSource source = wave is null ? new RingtoneWaveSource() : preview ? wave : new LoopStream(wave);
        var playback = settings with { PlaybackDeviceId = settings.RingDeviceId, PlaybackVolumePercent = settings.RingVolumePercent };
        try { _output.Play(source, playback, token); }
        catch (OperationCanceledException) when (preview && token.IsCancellationRequested && !caller.IsCancellationRequested) { }
        return settings.WindowsRingtoneFile is null || file is not null;
    }

    private FileStream? OpenWindowsRingtone(string? fileName, CancellationToken token)
    {
        if (fileName is null) return null;
        var path = EnumerateFiles().FirstOrDefault(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase));
        if (path is null) return null;
        FileStream? stream = null;
        try
        {
            stream = File.OpenRead(path);
            _ = PcmWaveHeader.Read(stream, token);
            return stream;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Mp3ConversionException or ArgumentException)
        {
            stream?.Dispose();
            return null;
        }
        catch
        {
            stream?.Dispose();
            throw;
        }
    }

    private string[] EnumerateFiles()
    {
        if (_mediaDirectory is null) return [];
        try
        {
            return Directory.EnumerateFiles(_mediaDirectory, "*.wav").Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.StartsWith("Ring", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("Windows Ring", StringComparison.OrdinalIgnoreCase);
            }).ToArray();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return []; }
    }

    private static string? GetMediaDirectory()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return windows.Length > 0 ? Path.Combine(windows, "Media") : null;
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
