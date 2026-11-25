using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using System.Runtime.ExceptionServices;
using System.Collections.Immutable;

namespace WawiCallPro.Audio;

internal sealed class PersistedDuplexPcmRecording(IDuplexPcmRecording inner, RecordingJournalWriter writer,
    PcmRecordingRequest request, DateTimeOffset epoch, TimeProvider clock) : IDuplexPcmRecording, IRecordingPersistenceStatus, IRecordingInputDrain
{
    private readonly object _gate = new();
    private Task<PcmRecordingResult>? _stop;
    private Task? _dispose;
    private bool _closing;
    private int _failure;
    internal bool ReleaseCompletedSegments { get; init; }
    private int Channels => request.Layout == ConversationWaveLayout.SeparatedStereo ? 2 : 1;
    public PcmRecordingState State => Volatile.Read(ref _failure) != 0 ? PcmRecordingState.Failed : inner.State;
    public PcmRecordingFailure? Failure
    {
        get { var code = Volatile.Read(ref _failure); return inner.Failure ?? (code != 0 ? (PcmRecordingFailure)code : null); }
    }
    public Task InputCompletion => inner.InputCompletion;
    public Task<long> DrainInputAsync() => inner is IRecordingInputDrain drain ? drain.DrainInputAsync()
        : throw new PcmRecordingException(PcmRecordingFailure.TimelineConflict);
    public bool HasPendingPersistence => writer.HasPending;
    public Task RetryPersistenceAsync(CancellationToken cancellationToken = default) => writer.RetryAsync(cancellationToken);

    public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_stop is not null) return _stop;
            if (_closing) return Task.FromException<PcmRecordingResult>(new ObjectDisposedException(nameof(IDuplexPcmRecording)));
            if (duration <= TimeSpan.Zero || duration > TimeSpan.FromHours(8)) throw new ArgumentOutOfRangeException(nameof(duration));
            var frames = ConversationWaveMixValidation.FrameAt(duration, request.SampleRate);
            if (frames == 0 || frames * Channels * 2 > int.MaxValue - 44) throw new ArgumentOutOfRangeException(nameof(duration));
            var ticks = (frames * TimeSpan.TicksPerSecond + request.SampleRate / 2) / request.SampleRate;
            var end = epoch.AddTicks(ticks);
            if (end > clock.GetUtcNow()) throw new ArgumentOutOfRangeException(nameof(duration), "Das berechnete Medienende darf nicht in der Zukunft liegen.");
            return _stop = StopCoreAsync(duration, frames, end, cancellationToken);
        }
    }

    private async Task<PcmRecordingResult> StopCoreAsync(TimeSpan duration, long frames, DateTimeOffset end, CancellationToken token)
    {
        PcmRecordingResult result;
        try
        {
            result = await inner.StopAsync(duration, token).ConfigureAwait(false);
            var wave = result.Wave;
            if (wave.CallId != request.CallId || wave.RecordingId != request.RecordingId || wave.SampleRate != request.SampleRate || wave.Channels != Channels || wave.FrameCount != frames)
                throw new PcmRecordingException(PcmRecordingFailure.MixingFailed);
        }
        catch (Exception error)
        {
            if (error is not OperationCanceledException) Volatile.Write(ref _failure, (int)(error is PcmRecordingException pcm ? pcm.Failure : PcmRecordingFailure.MixingFailed));
            await SaveFailureAsync(error).ConfigureAwait(false);
            if (error is PcmRecordingException or OperationCanceledException) throw;
            throw new PcmRecordingException(PcmRecordingFailure.MixingFailed);
        }
        await writer.SaveAsync(new(request.RecordingId, request.CallId, epoch, end, PcmRecordingState.Completed, request.SampleRate, Channels, frames)).ConfigureAwait(false);
        if (ReleaseCompletedSegments && !writer.HasPending && !result.Segments.IsEmpty && File.Exists(request.WavePath))
            result = result with { Segments = ReleaseSegments(result.Segments) };
        return result;
    }

    private ImmutableArray<ConversationWaveSegment> ReleaseSegments(ImmutableArray<ConversationWaveSegment> segments)
    {
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(request.WavePath));
        var directory = Path.GetDirectoryName(segments[0].WavePath);
        var prefix = ".wawi-record-" + request.RecordingId.ToString("N") + "-";
        if (directory is null || !string.Equals(Path.GetDirectoryName(directory), outputDirectory, StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(directory) is not { } name || !name.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(name[prefix.Length..], "N", out _)
            || segments.Any(segment => !string.Equals(Path.GetDirectoryName(segment.WavePath), directory, StringComparison.OrdinalIgnoreCase)))
            return segments;

        try
        {
            if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint)) return segments;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return segments; }

        var retained = ImmutableArray.CreateBuilder<ConversationWaveSegment>();
        foreach (var segment in segments)
        {
            try { File.Delete(segment.WavePath); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { retained.Add(segment); }
        }
        if (retained.Count == 0)
        {
            try { Directory.Delete(directory); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
        return retained.ToImmutable();
    }

    private Task SaveFailureAsync(Exception? error)
    {
        var failure = inner.Failure is { } code ? (RecordingCaptureFailure)(int)code
            : error is PcmRecordingException pcm ? (RecordingCaptureFailure)(int)pcm.Failure
            : error is null or OperationCanceledException ? RecordingCaptureFailure.Cancelled : RecordingCaptureFailure.MixingFailed;
        return writer.SaveAsync(new(request.RecordingId, request.CallId, epoch, null,
            failure == RecordingCaptureFailure.Cancelled ? PcmRecordingState.Aborted : PcmRecordingState.Failed, request.SampleRate, Channels, failure: failure));
    }

    public ValueTask DisposeAsync() { lock (_gate) { _closing = true; return new(_dispose ??= DisposeCoreAsync()); } }
    private async Task DisposeCoreAsync()
    {
        if (_stop is not null)
        {
            Exception? failure = null;
            try { await _stop.ConfigureAwait(false); } catch (Exception error) { failure = error; }
            try { await inner.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error)
            {
                Volatile.Write(ref _failure, (int)(error is PcmRecordingException pcm ? pcm.Failure : PcmRecordingFailure.MixingFailed));
                failure ??= error is PcmRecordingException ? error : new PcmRecordingException(PcmRecordingFailure.MixingFailed);
            }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
            return;
        }
        Exception? cleanup = null;
        try { await inner.DisposeAsync().ConfigureAwait(false); } catch (Exception error) { cleanup = error; }
        await SaveFailureAsync(cleanup).ConfigureAwait(false);
        if (cleanup is not null) throw new PcmRecordingException(inner.Failure ?? PcmRecordingFailure.MixingFailed);
    }
}
