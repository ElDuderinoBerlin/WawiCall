namespace DtmfDetection.CSCore
{
    public static class SampleProviderExtensions
    {
        public static SampleBlockProvider Blockwise(this global::CSCore.ISampleSource source, int blockSize)
            => new SampleBlockProvider(source, blockSize);
    }
}