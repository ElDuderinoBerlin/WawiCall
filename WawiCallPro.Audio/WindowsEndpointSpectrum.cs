using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Owns a shared-mode, memory-only stream for an open endpoint meter.</summary>
internal sealed class WindowsEndpointSpectrum : IDisposable
{
    private readonly CscoreSpectrumAnalyzer _analyzer = new(TimeProvider.System);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;

    internal WindowsEndpointSpectrum(AudioEndpointDirection direction, string deviceId) =>
        _worker = Task.Run(() => RunAsync(direction, deviceId));

    internal ImmutableArray<int> Read() => _analyzer.Read();

    private async Task RunAsync(AudioEndpointDirection direction, string deviceId)
    {
        try
        {
            _stop.Token.ThrowIfCancellationRequested();
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDevice(deviceId);
            using WasapiCapture capture = direction == AudioEndpointDirection.Playback
                ? new WasapiLoopbackCapture() : new WasapiCapture(true, AudioClientShareMode.Shared);
            capture.Device = device;
            capture.Initialize();
            _ = PlaybackPacketPcm.GetEncoding(capture.WaveFormat);
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            capture.Stopped += (_, _) => stopped.TrySetResult();
            capture.DataAvailable += OnDataAvailable;
            _stop.Token.ThrowIfCancellationRequested();
            capture.Start();
            await stopped.Task.WaitAsync(_stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception error) when (error is CoreAudioAPIException or COMException or InvalidOperationException or PcmRecordingException) { }
        finally { _analyzer.Clear(); }
    }

    private void OnDataAvailable(object? sender, DataAvailableEventArgs args)
    {
        var format = args.Format;
        var count = args.ByteCount / format.BlockAlign;
        if (count == 0 || _stop.IsCancellationRequested) return;
        var samples = ArrayPool<short>.Shared.Rent(count);
        try
        {
            var pcm = samples.AsSpan(0, count);
            PlaybackPacketPcm.Decode(args.Data.AsSpan(args.Offset, args.ByteCount), format, pcm, silent: false);
            _analyzer.Add(format.SampleRate, 1, pcm);
        }
        catch (PcmRecordingException) { _analyzer.Clear(); }
        finally { ArrayPool<short>.Shared.Return(samples, clearArray: true); }
    }

    public void Dispose()
    {
        try { _stop.Cancel(); _worker.GetAwaiter().GetResult(); }
        finally { _stop.Dispose(); }
    }
}
