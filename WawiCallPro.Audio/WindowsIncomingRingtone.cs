using System.Buffers.Binary;
using CSCore;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Opens a dedicated local WASAPI stream for the incoming-call signal.</summary>
public sealed class WindowsIncomingRingtone(IHistoryRecordingOutput output) : IIncomingRingtone
{
    public Task PlayAsync(string? deviceId, int volumePercent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = new RingtoneWaveSource();
        output.Play(source, new AudioDeviceSettings { PlaybackDeviceId = deviceId, PlaybackVolumePercent = volumePercent }, cancellationToken);
        return Task.CompletedTask;
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
