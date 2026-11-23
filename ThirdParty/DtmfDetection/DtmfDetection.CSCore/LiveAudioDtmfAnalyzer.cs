using System.ComponentModel;
using CSCore.SoundIn;
using CSCore.Streams;

namespace DtmfDetection.CSCore;

/// <summary>Analyzes an initialized CSCore capture source. The caller owns the capture device.</summary>
public sealed class LiveAudioDtmfAnalyzer(ISoundIn waveIn, bool forceMono = true) : Component
{
    private readonly object _gate = new();
    private Capture? _capture;
    private bool _disposed;

    public event Action<DtmfToneStart>? DtmfToneStarted;
    public event Action<DtmfToneEnd>? DtmfToneStopped;
    public event Action<Exception>? CaptureFailed;

    public bool IsCapturing => Volatile.Read(ref _capture)?.Worker.IsAlive == true;

    public void StartCapturing()
    {
        RejectWorkerCall();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsCapturing) return;
            StopCore();
            var capture = new Capture(waveIn, Detect);
            _capture = capture;
            try
            {
                waveIn.Start();
                capture.Worker.Start();
            }
            catch
            {
                StopCore();
                throw;
            }
        }
    }

    public void StopCapturing()
    {
        RejectWorkerCall();
        lock (_gate) StopCore();
    }

    private void StopCore()
    {
        if (_capture is not { } capture) return;
        capture.Stopping.Cancel();
        try
        {
            if (waveIn.RecordingState != RecordingState.Stopped) waveIn.Stop();
        }
        finally
        {
            if (capture.Worker.IsAlive) capture.Worker.Join();
            _capture = null;
            capture.Dispose();
        }
    }

    private void RejectWorkerCall()
    {
        if (ReferenceEquals(Volatile.Read(ref _capture)?.Worker, Thread.CurrentThread))
            throw new InvalidOperationException("Capture callbacks must not start or stop capture on the analysis thread.");
    }

    private void Detect(Capture capture)
    {
        try
        {
            var config = new DetectorConfig();
            using var source = new StreamingSampleSource(config, capture.Source, forceMono);
            var audio = DtmfAudio.CreateFrom(source, config);
            var epoch = DateTime.UtcNow;
            long frames = 0;
            while (true)
            {
                var more = source.WaitForBlock(capture.Available, capture.Stopping.Token);
                var position = epoch.AddSeconds((double)frames / source.SampleRate);
                audio.Forward((channel, tone) =>
                {
                    DtmfToneStarted?.Invoke(new DtmfToneStart(tone, channel, position));
                    return position;
                }, (channel, start, tone) =>
                    DtmfToneStopped?.Invoke(new DtmfToneEnd(tone, channel, position - start)));
                if (!more) return;
                frames += config.SampleBlockSize;
            }
        }
        catch (Exception error)
        {
            try { CaptureFailed?.Invoke(error); }
            catch (Exception) { /* Diagnostic subscribers cannot crash the capture thread. */ }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RejectWorkerCall();
            lock (_gate)
            {
                _disposed = true;
                StopCore();
            }
        }
        base.Dispose(disposing);
    }

    private sealed class Capture : IDisposable
    {
        private readonly ISoundIn _input;
        internal SoundInSource Source { get; }
        internal CancellationTokenSource Stopping { get; } = new();
        internal AutoResetEvent Available { get; } = new(false);
        internal Thread Worker { get; }

        internal Capture(ISoundIn input, Action<Capture> run)
        {
            _input = input;
            Source = new SoundInSource(input) { FillWithZeros = false };
            Source.DataAvailable += DataAvailable;
            input.Stopped += InputStopped;
            Worker = new Thread(() => run(this)) { IsBackground = true, Name = "DTMF analysis" };
        }

        private void DataAvailable(object? sender, DataAvailableEventArgs e) => Available.Set();
        private void InputStopped(object? sender, RecordingStoppedEventArgs e) => Stopping.Cancel();

        public void Dispose()
        {
            _input.Stopped -= InputStopped;
            Source.DataAvailable -= DataAvailable;
            Source.Dispose();
            Available.Dispose();
            Stopping.Dispose();
        }
    }
}
