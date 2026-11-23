using System;
using CSCore;

namespace DtmfDetection.CSCore
{
    public class MonoSampleProvider : global::CSCore.ISampleSource
    {
        private readonly global::CSCore.ISampleSource _sourceProvider;

        private readonly int _sourceChannels;

        private float[] _sourceBuffer = [];

        public MonoSampleProvider(global::CSCore.ISampleSource sourceProvider)
        {
            _sourceProvider = sourceProvider;
            _sourceChannels = sourceProvider.WaveFormat.Channels;
            WaveFormat = new WaveFormat(sourceProvider.WaveFormat.SampleRate, 32, 1, AudioEncoding.IeeeFloat);
        }

        public bool CanSeek => _sourceProvider.CanSeek;
        public WaveFormat WaveFormat { get; }
        public long Position { get => _sourceProvider.Position / _sourceChannels; set => _sourceProvider.Position = checked(value * _sourceChannels); }
        public long Length => _sourceProvider.Length / _sourceChannels;

        public int Read(float[] buffer, int offset, int count)
        {
            var sourceBytesRequired = count * _sourceChannels;
            _sourceBuffer = BufferHelpers.Ensure(_sourceBuffer, sourceBytesRequired);

            var samplesRead = _sourceProvider.Read(_sourceBuffer, 0, sourceBytesRequired);
            var sampleFramesRead = samplesRead / _sourceChannels;

            for (var sampleIndex = 0; sampleIndex < sampleFramesRead * _sourceChannels; sampleIndex += _sourceChannels)
                buffer[offset++] = Clamp(AverageOfSampleFrame(sampleIndex));

            return sampleFramesRead;
        }

        private float AverageOfSampleFrame(int frameStart)
        {
            var mixedValue = 0.0f;

            for (var channel = 0; channel < _sourceChannels; channel++)
                mixedValue += _sourceBuffer[frameStart + channel];

            return mixedValue / _sourceChannels;
        }

        private static float Clamp(float value) => Math.Max(-1.0f, Math.Min(value, 1.0f));

        public void Dispose() => _sourceProvider.Dispose();
    }
}
