namespace DtmfDetection
{
    using System;

    public class AmplitudeEstimator
    {
        private readonly double _c;

        private double _s1;

        private double _s2;

        public AmplitudeEstimator(double targetFrequency, double sampleRate, int numberOfSamples)
        {
            _c = 2.0 * Math.Cos(2.0 * Math.PI * targetFrequency / sampleRate);
        }

        public double AmplitudeSquared => _s1*_s1 + _s2*_s2 - _s1*_s2*_c;

        public void Add(float sample)
        {
            var s0 = sample + _c * _s1 - _s2;
            _s2 = _s1;
            _s1 = s0;
        }

        public void Reset() => _s1 = _s2 = .0;
    }
}