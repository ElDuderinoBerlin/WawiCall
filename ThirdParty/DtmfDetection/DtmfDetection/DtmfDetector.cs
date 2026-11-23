namespace DtmfDetection
{
    using System.Collections.Generic;
    using System.Linq;

    public class DtmfDetector
    {
        private readonly DetectorConfig _config;

        private readonly PureTones[] _powers;

        private readonly int _numChannels;

        public DtmfDetector(DetectorConfig config, PureTones[] powers)
        {
            _config = config;
            _powers = powers;
            _numChannels = powers.Length;
        }

        public DtmfTone[] Analyze(IEnumerable<float> samples)
        {
            foreach (var p in _powers)
                p.ResetAmplitudes();

            var channel = 0;
            foreach (var sample in samples.Take(_config.SampleBlockSize * _numChannels))
            {
                _powers[channel].AddSample(sample);
                channel = (channel + 1) % _numChannels;
            }

            return _powers
                .Select(p => GetDtmfToneFromPowers(p, _config.PowerThreshold))
                .ToArray();
        }

        private static DtmfTone GetDtmfToneFromPowers(PureTones powers, double threshold)
        {
            var highTone = powers.FindStrongestHighTone();
            var lowTone = powers.FindStrongestLowTone();

            if (powers[highTone] < threshold || powers[lowTone] < threshold)
                return DtmfTone.None;

            return DtmfClassification.For(highTone, lowTone);
        }
    }
}