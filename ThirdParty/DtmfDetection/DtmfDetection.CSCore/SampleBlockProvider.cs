namespace DtmfDetection.CSCore
{
    public class SampleBlockProvider
    {
        private readonly global::CSCore.ISampleSource _source;

        public SampleBlockProvider(global::CSCore.ISampleSource source, int blockSize)
        {
            _source = source;
            BlockSize = blockSize * Channels;
            CurrentBlock = new float[BlockSize];
        }

        public int SampleRate => _source.WaveFormat.SampleRate;

        public int BlockSize { get; }

        public int Channels => _source.WaveFormat.Channels;

        public float[] CurrentBlock { get; }

        public int ReadNextBlock()
        {
            var filled = 0;
            while (filled < BlockSize)
            {
                var read = _source.Read(CurrentBlock, filled, BlockSize - filled);
                if (read == 0) break;
                filled += read;
            }
            return filled;
        }
    }
}
