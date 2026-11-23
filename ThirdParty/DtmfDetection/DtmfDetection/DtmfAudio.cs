namespace DtmfDetection
{
    using System;
    using System.Linq;

    public class DtmfAudio
    {
        private readonly DtmfChangeHandler[] _dtmfChangeHandlers;

        private readonly ISampleSource _source;

        private readonly DtmfDetector _dtmfDetector;

        private readonly int _numChannels;

        public DtmfAudio(DtmfDetector dtmfDetector, ISampleSource source, DtmfChangeHandler[] dtmfChangeHandlers)
        {
            _dtmfDetector = dtmfDetector;
            _source = source;
            _dtmfChangeHandlers = dtmfChangeHandlers;
            _numChannels = source.Channels;
        }

        public static DtmfAudio CreateFrom(ISampleSource source, DetectorConfig config)
        {
            var pureTones = Enumerable
                .Range(0, source.Channels)
                .Select(c => new PureTones(new AmplitudeEstimatorFactory(source.SampleRate, config.SampleBlockSize)))
                .ToArray();

            var dtmfChangeHandlers = Enumerable
                .Range(0, source.Channels)
                .Select(c => new DtmfChangeHandler())
                .ToArray();

            return new DtmfAudio(new DtmfDetector(config, pureTones), source, dtmfChangeHandlers);
        }

        public bool Forward<TState>(Func<int, DtmfTone, TState> dtmfStarting, Action<int, TState, DtmfTone> dtmfStopping)
        {
            // Save value of HasSamples, because it might be different after analyzing (i.e. reading).
            var canAnalyze = _source.HasSamples;

            var dtmfTones = canAnalyze
                ? _dtmfDetector.Analyze(_source.Samples)
                // Reached end of data: generate DtmfTone.None's and flush the state machines to handle cut-off tones.
                : Enumerable.Repeat(DtmfTone.None, _numChannels).ToArray();

            for (var channel = 0; channel < _numChannels; channel++)
            {
                _dtmfChangeHandlers[channel].Handle(
                    dtmfTones[channel],
                    dtmfStarting.Apply(channel),
                    dtmfStopping.Apply(channel));
            }

            return canAnalyze;
        }
    }
}