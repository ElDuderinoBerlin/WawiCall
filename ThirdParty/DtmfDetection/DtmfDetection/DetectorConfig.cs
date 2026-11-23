namespace DtmfDetection
{
    public class DetectorConfig
    {
        public int MaxSampleRate { get; } = 16000;

        // Number of samples analyzed together per channel.
        public int SampleBlockSize { get; } = 205;

        public double PowerThreshold { get; } = 100.0;
    }
}
