namespace DtmfDetection
{
    public class AmplitudeEstimatorFactory
    {
        private readonly int _sampleRate;

        private readonly int _sampleBlockSize;

        public AmplitudeEstimatorFactory(int sampleRate, int sampleBlockSize)
        {
            _sampleRate = sampleRate;
            _sampleBlockSize = sampleBlockSize;
        }

        public AmplitudeEstimator CreateFor(int tone) => new AmplitudeEstimator(tone, _sampleRate, _sampleBlockSize);
    }
}