using CSCore;

namespace DtmfDetection.CSCore;

/// <summary>Reads complete blocks from capture, waiting for data without busy-waiting.</summary>
internal sealed class StreamingSampleSource : ISampleSource, IDisposable
{
    private readonly global::CSCore.ISampleSource _pipeline;
    private readonly float[] _block;

    internal StreamingSampleSource(DetectorConfig config, IWaveSource source, bool forceMono)
    {
        var samples = new BorrowedWaveSource(source).ToSampleSource();
        if (forceMono && samples.WaveFormat.Channels > 1) samples = new MonoSampleProvider(samples);
        _pipeline = samples.WaveFormat.SampleRate > config.MaxSampleRate ? samples.ChangeSampleRate(config.MaxSampleRate) : samples;
        _block = new float[checked(config.SampleBlockSize * Channels)];
    }

    public int SampleRate => _pipeline.WaveFormat.SampleRate;
    public int Channels => _pipeline.WaveFormat.Channels;
    public bool HasSamples { get; private set; }
    public IEnumerable<float> Samples => _block;

    internal bool WaitForBlock(WaitHandle available, CancellationToken token)
    {
        var signals = new[] { token.WaitHandle, available };
        var filled = 0;
        while (!token.IsCancellationRequested)
        {
            var read = _pipeline.Read(_block, filled, _block.Length - filled);
            filled += read;
            if (filled == _block.Length) return HasSamples = true;
            if (read == 0) WaitHandle.WaitAny(signals);
        }
        return HasSamples = false;
    }

    public void Dispose() => _pipeline.Dispose();
}
