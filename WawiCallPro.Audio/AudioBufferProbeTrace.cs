namespace WawiCallPro.Audio;

internal sealed class AudioBufferProbeTrace : IPlaybackPcmObserver
{
    private readonly List<short> _samples = [];
    private TimeSpan? _start;
    private int _rate;
    private int _missing;

    public bool TryObserve(TimeSpan position, int sampleRate, ReadOnlySpan<short> samples)
    {
        _start ??= position;
        _rate = sampleRate;
        var expected = (int)Math.Round((position - _start.Value).TotalSeconds * sampleRate);
        var gap = expected - _samples.Count;
        if (gap >= sampleRate * 3) return false;
        if (gap > 1)
        {
            _missing += gap;
            _samples.AddRange(new short[gap]);
        }
        foreach (var sample in samples) _samples.Add(sample);
        return true;
    }

    public void OnFailure() { }

    internal BufferProbeResult Measure(int frequency)
    {
        if (_samples.Count == 0) return new(false, 0, 0, 0, 0);
        var length = _rate / 100;
        var peaks = new List<int>();
        for (var start = 0; start + length <= _samples.Count; start += length)
        {
            var maximum = 0;
            for (var i = start; i < start + length; i++) maximum = Math.Max(maximum, Math.Abs((int)_samples[i]));
            peaks.Add(maximum);
        }
        var maximumPeak = peaks.Count == 0 ? 0 : peaks.Max();
        if (maximumPeak < 20) return new(false, 0, 0, _missing, maximumPeak);
        var first = peaks.FindIndex(peak => peak >= maximumPeak * 0.55);
        var last = peaks.FindLastIndex(peak => peak >= maximumPeak * 0.55);
        // A partial tone must not pass even when its remaining samples are clean.
        var lowBlocks = last - first + 1 < 185 ? 1 : 0;
        for (var block = first + 2; block < last - 1; block++)
            if (peaks[block] < maximumPeak * 0.40) lowBlocks++;

        // A phase-continuous sinusoid satisfies this recurrence. A click or a skipped sample produces a residual.
        var coefficient = 2 * Math.Cos(2 * Math.PI * frequency / _rate);
        var residualPeaks = 0;
        var previousImpulse = int.MinValue / 2;
        for (var i = 2; i < _samples.Count; i++)
        {
            var residual = Math.Abs(_samples[i] - coefficient * _samples[i - 1] + _samples[i - 2]);
            if (residual < Math.Max(12, maximumPeak * 0.22)) continue;
            if (i - previousImpulse > length) residualPeaks++;
            previousImpulse = i;
        }
        return new(true, lowBlocks, residualPeaks, _missing, maximumPeak);
    }
}

internal readonly record struct BufferProbeResult(bool SignalFound, int Low10MsBlocks, int ImpulseGroups, int MissingFrames, int TonePeak)
{
    internal bool IsClean => SignalFound && Low10MsBlocks == 0 && ImpulseGroups == 0 && MissingFrames == 0;
}
