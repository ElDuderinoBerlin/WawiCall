using System.Collections.Immutable;
using System.Threading.Channels;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

internal sealed class PcmRecordingSession : IPcmRecordingSession, IPcmRecordingInputGuard, IPcmRecordingInputExtent
{
    private readonly PcmRecordingRequest _request;
    private readonly IPcmSegmentSinkFactory _sinks;
    private readonly IConversationWaveMixer _mixer;
    private readonly Channel<Block> _queue;
    private readonly CancellationTokenSource _abort;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task<ImmutableArray<ConversationWaveSegment>> _capture;
    private readonly object _lifecycle = new();
    private Task<PcmRecordingResult>? _stop;
    private Task? _dispose;
    private int _accepting = 1;
    // Ordinary states use their enum value; failures use 100 + failure. One atomic value
    // decides Stop versus callback rejection, so a late callback cannot invalidate a committed stop.
    private int _status;
    private long _inputFrames;
    public long InputFrameCount => Interlocked.Read(ref _inputFrames);

    internal PcmRecordingSession(PcmRecordingRequest request, IPcmSegmentSinkFactory sinks, IConversationWaveMixer mixer, CancellationToken token)
    {
        _request = request; _sinks = sinks; _mixer = mixer;
        _abort = CancellationTokenSource.CreateLinkedTokenSource(token);
        SegmentDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(request.WavePath))!,
            ".wawi-record-" + request.RecordingId.ToString("N") + "-" + Guid.NewGuid().ToString("N"));
        _queue = Channel.CreateBounded<Block>(new BoundedChannelOptions(request.QueueCapacity)
        { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false, FullMode = BoundedChannelFullMode.Wait });
        _capture = Task.Run(CaptureAsync);
    }

    internal Task Ready => _ready.Task;
    public PcmRecordingState State
    {
        get { var status = Volatile.Read(ref _status); return status >= 100 ? PcmRecordingState.Failed : (PcmRecordingState)status; }
    }
    public PcmRecordingFailure? Failure
    {
        get { var status = Volatile.Read(ref _status); return status >= 100 ? (PcmRecordingFailure)(status - 100) : null; }
    }
    public string SegmentDirectory { get; }
    public Task CaptureCompletion => _capture;

    public bool TryAppend(ConversationAudioSide side, TimeSpan start, int sampleRate, ReadOnlySpan<short> samples)
    {
        if (Volatile.Read(ref _accepting) == 0) return false;
        if (!Enum.IsDefined(side) || sampleRate is < 8000 or > 192000 || samples.IsEmpty || samples.Length > sampleRate / 10 ||
            start < TimeSpan.Zero || start > TimeSpan.FromHours(8) ||
            start.Ticks * sampleRate + samples.Length * TimeSpan.TicksPerSecond > TimeSpan.FromHours(8).Ticks * sampleRate)
        { Fail(PcmRecordingFailure.InvalidBlock, capturingOnly: true); return false; }
        var outputEnd = ConversationWaveMixValidation.FrameAt(start, _request.SampleRate) +
            ((long)samples.Length * _request.SampleRate + sampleRate / 2) / sampleRate;
        var channels = _request.Layout == ConversationWaveLayout.SeparatedStereo ? 2 : 1;
        if (outputEnd * channels * 2 > int.MaxValue - 44)
        { Fail(PcmRecordingFailure.InvalidBlock, capturingOnly: true); return false; }
        var block = new Block(side, start, sampleRate, samples.ToArray());
        if (_queue.Writer.TryWrite(block))
        {
            long previous;
            do { previous = Interlocked.Read(ref _inputFrames); if (previous >= outputEnd) break; }
            while (Interlocked.CompareExchange(ref _inputFrames, outputEnd, previous) != previous);
            return true;
        }
        if (Volatile.Read(ref _accepting) != 0) Fail(PcmRecordingFailure.BufferOverflow, capturingOnly: true);
        return false;
    }

    public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_dispose is not null, this);
            if (_stop is not null) return _stop;
            ConversationWaveMixValidation.Validate(new(_request.CallId, _request.RecordingId, [], _request.WavePath,
                _request.SampleRate, duration, _request.Layout));
            Interlocked.Exchange(ref _accepting, 0);
            Interlocked.CompareExchange(ref _status, (int)PcmRecordingState.Stopping, (int)PcmRecordingState.Capturing);
            _queue.Writer.TryComplete();
            _stop = StopCoreAsync(duration, cancellationToken);
            return _stop;
        }
    }

    private async Task<PcmRecordingResult> StopCoreAsync(TimeSpan duration, CancellationToken token)
    {
        using var stopCancellation = token.Register(() => _abort.Cancel());
        try
        {
            var segments = await _capture.ConfigureAwait(false);
            ThrowIfFailed();
            _abort.Token.ThrowIfCancellationRequested();
            var wave = await _mixer.MixAsync(new(_request.CallId, _request.RecordingId, segments, _request.WavePath,
                _request.SampleRate, duration, _request.Layout), _abort.Token).ConfigureAwait(false);
            SetTerminalState(PcmRecordingState.Completed);
            return new(wave, segments);
        }
        catch (OperationCanceledException) when (_abort.IsCancellationRequested)
        { SetTerminalState(PcmRecordingState.Aborted); throw; }
        catch (PcmRecordingException) { throw; }
        catch (ConversationWaveMixException error)
        {
            Fail(error.Failure == ConversationWaveMixFailure.TimelineConflict ? PcmRecordingFailure.TimelineConflict : PcmRecordingFailure.MixingFailed);
            throw new PcmRecordingException(Failure!.Value);
        }
        catch (Exception) { Fail(PcmRecordingFailure.MixingFailed); throw new PcmRecordingException(Failure!.Value); }
    }

    private async Task<ImmutableArray<ConversationWaveSegment>> CaptureAsync()
    {
        var all = new List<Segment>();
        var current = new Segment?[2];
        try
        {
            _abort.Token.ThrowIfCancellationRequested();
            var parent = Path.GetDirectoryName(SegmentDirectory)!;
            if (!Directory.Exists(parent) || File.Exists(_request.WavePath) || Directory.Exists(_request.WavePath))
                throw new PcmRecordingException(PcmRecordingFailure.FileFailure);
            Directory.CreateDirectory(SegmentDirectory);
            _ready.TrySetResult();
            var zeros = new short[1024];
            await foreach (var block in _queue.Reader.ReadAllAsync(_abort.Token).ConfigureAwait(false))
            {
                _abort.Token.ThrowIfCancellationRequested();
                ThrowIfFailed();
                var side = (int)block.Side;
                var segment = current[side];
                if (segment is null || segment.Rate != block.Rate)
                {
                    if (segment is not null)
                    {
                        var previousEnd = ConversationWaveMixValidation.FrameAt(segment.Start, _request.SampleRate) +
                            (segment.Frames * _request.SampleRate + segment.Rate / 2) / segment.Rate;
                        if (ConversationWaveMixValidation.FrameAt(block.Start, _request.SampleRate) < previousEnd)
                            throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
                        segment.Complete();
                    }
                    if (all.Count == 128) throw new PcmRecordingException(PcmRecordingFailure.SegmentLimit);
                    var path = Path.Combine(SegmentDirectory, all.Count.ToString("D3") + "-" + block.Side + ".wav");
                    segment = new Segment(_sinks.Open(path, block.Rate), path, block.Side, block.Start, block.Rate);
                    all.Add(segment); current[side] = segment;
                }
                var position = ConversationWaveMixValidation.FrameAt(block.Start - segment.Start, segment.Rate);
                if (position < segment.Frames) throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
                if ((position + block.Samples.Length) * 2 > int.MaxValue - 44) throw new PcmRecordingException(PcmRecordingFailure.FileFailure);
                while (segment.Frames < position)
                {
                    _abort.Token.ThrowIfCancellationRequested(); ThrowIfFailed();
                    segment.Write(zeros.AsSpan(0, (int)Math.Min(zeros.Length, position - segment.Frames)));
                }
                _abort.Token.ThrowIfCancellationRequested();
                segment.Write(block.Samples);
            }
            _abort.Token.ThrowIfCancellationRequested();
            ThrowIfFailed();
            foreach (var segment in all) segment.Complete();
            return all.Select(segment => new ConversationWaveSegment(segment.Path, segment.Side, segment.Start)).ToImmutableArray();
        }
        catch (OperationCanceledException) when (_abort.IsCancellationRequested)
        {
            SetTerminalState(PcmRecordingState.Aborted);
            _ready.TrySetCanceled(_abort.Token); throw;
        }
        catch (Exception error)
        {
            Fail(error is PcmRecordingException recording ? recording.Failure : PcmRecordingFailure.FileFailure);
            var safe = new PcmRecordingException(Failure!.Value);
            _ready.TrySetException(safe); throw safe;
        }
        finally
        {
            Interlocked.Exchange(ref _accepting, 0);
            _queue.Writer.TryComplete();
            while (_queue.Reader.TryRead(out _)) { }
            var disposeFailed = false;
            foreach (var segment in all)
            {
                try { segment.Dispose(); }
                catch (Exception) { disposeFailed = true; }
            }
            if (disposeFailed) { Fail(PcmRecordingFailure.FileFailure); throw new PcmRecordingException(Failure!.Value); }
        }
    }

    private void ThrowIfFailed() { if (Failure is { } failure) throw new PcmRecordingException(failure); }
    void IPcmRecordingInputGuard.RejectInput(PcmRecordingFailure failure)
    {
        if (!Enum.IsDefined(failure)) throw new ArgumentOutOfRangeException(nameof(failure));
        Fail(failure, capturingOnly: true);
    }
    private void Fail(PcmRecordingFailure failure, bool capturingOnly = false)
    {
        while (true)
        {
            var status = Volatile.Read(ref _status);
            if (status >= 100 || status == (int)PcmRecordingState.Completed || capturingOnly && status != (int)PcmRecordingState.Capturing) return;
            if (Interlocked.CompareExchange(ref _status, 100 + (int)failure, status) == status) break;
        }
        Interlocked.Exchange(ref _accepting, 0);
        _queue.Writer.TryComplete();
    }

    private void SetTerminalState(PcmRecordingState state)
    {
        while (true)
        {
            var status = Volatile.Read(ref _status);
            if (status >= 100 || status == (int)PcmRecordingState.Completed) return;
            if (Interlocked.CompareExchange(ref _status, (int)state, status) == status) return;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifecycle) return new ValueTask(_dispose ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            if (_stop is not null) await _stop.ConfigureAwait(false);
            else
            {
                Interlocked.Exchange(ref _accepting, 0); _queue.Writer.TryComplete(); _abort.Cancel();
                if (Failure is null) SetTerminalState(PcmRecordingState.Aborted);
                await _capture.ConfigureAwait(false);
            }
        }
        catch (Exception) { /* Stop/CaptureCompletion and Failure expose the result; disposal must not mask it. */ }
        finally { _abort.Dispose(); }
    }

    private sealed record Block(ConversationAudioSide Side, TimeSpan Start, int Rate, short[] Samples);
    private sealed class Segment(IPcmSegmentSink sink, string path, ConversationAudioSide side, TimeSpan start, int rate) : IDisposable
    {
        private bool _complete;
        internal string Path => path;
        internal ConversationAudioSide Side => side;
        internal TimeSpan Start => start;
        internal int Rate => rate;
        internal long Frames { get; private set; }
        internal void Write(ReadOnlySpan<short> samples) { sink.Write(samples); Frames += samples.Length; }
        internal void Complete() { if (_complete) return; sink.Complete(); _complete = true; }
        public void Dispose() => sink.Dispose();
    }
}
