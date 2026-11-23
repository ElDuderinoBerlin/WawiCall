using System;
using System.Collections.Generic;
using System.Linq;
using CSCore;

namespace DtmfDetection.CSCore
{
    public class StaticSampleSource : ISampleSource, IDisposable
    {
        private readonly SampleBlockProvider _samples;
        private readonly global::CSCore.ISampleSource _pipeline;

        private int _numSamplesRead;

        public StaticSampleSource(DetectorConfig config, IWaveSource source, bool forceMono = true)
        {
            var sampleProvider = new BorrowedWaveSource(source).ToSampleSource();

            if (forceMono)
                sampleProvider = new MonoSampleProvider(sampleProvider);

            _pipeline = sampleProvider.WaveFormat.SampleRate > config.MaxSampleRate ? sampleProvider.ChangeSampleRate(config.MaxSampleRate) : sampleProvider;
            _samples = _pipeline.Blockwise(config.SampleBlockSize);

            // Optimistically assume that we are going to read at least BlockSize bytes.
            _numSamplesRead = _samples.BlockSize;
        }

        public long SamplesRead { get; private set; }
        public void Dispose() => _pipeline.Dispose();

        public bool HasSamples => _numSamplesRead >= _samples.BlockSize;

        public int SampleRate => _samples.SampleRate;

        public int Channels => _samples.Channels;

        public IEnumerable<float> Samples
        {
            get
            {
                if (!HasSamples)
                    throw new InvalidOperationException("No more data available");

                _numSamplesRead = _samples.ReadNextBlock();
                _numSamplesRead -= _numSamplesRead % Channels;
                SamplesRead += _numSamplesRead;
                return _samples.CurrentBlock.Take(_numSamplesRead);
            }
        }
    }
}