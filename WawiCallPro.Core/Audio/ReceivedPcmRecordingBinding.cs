using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Core.Audio;

internal sealed class ReceivedPcmRecordingBinding : IReceivedPcmRecordingBinding, IReceivedRtpPcmObserver, IConversationPcmFailureObserver
{
    private readonly IRtpPcmRecordingFeed _feed;
    private readonly IPcmRecordingInputGuard _guard;
    private readonly IConversationPcmSubscription _subscription;
    private readonly object _gate = new();
    private Task? _dispose;
    private int _failure;
    public ReceivedPcmRecordingFailure? Failure
    { get { var value = Volatile.Read(ref _failure); return value == 0 ? null : (ReceivedPcmRecordingFailure)value; } }
    public TimeSpan LastPacketEnd => _feed.LastPacketEnd;
    public Task<ReceivedPcmRecordingFailure?> Completion { get; }

    internal ReceivedPcmRecordingBinding(IConversationPcmFailureSource source, IRtpPcmRecordingFeed feed, IPcmRecordingInputGuard guard)
    {
        _feed = feed; _guard = guard;
        _subscription = source.Subscribe(this);
        Completion = CompleteAsync();
    }

    public bool TryObserve(ConversationAudioSide side, int sampleRate, int channels, ReadOnlySpan<short> samples) =>
        side == ConversationAudioSide.Sent || Fail(ReceivedPcmRecordingFailure.MetadataUnavailable);

    public bool TryObserveReceived(RtpPcmPacketPosition packet, int sampleRate, int channels, ReadOnlySpan<short> samples)
    {
        if (Failure is not null) return false;
        if (channels != 1) return Fail(ReceivedPcmRecordingFailure.InvalidPcm);
        var result = _feed.TryAppend(packet, sampleRate, samples);
        return result is RtpPcmAppendResult.Accepted or RtpPcmAppendResult.BeforeRecording or RtpPcmAppendResult.Duplicate or RtpPcmAppendResult.Late
            || Fail(ReceivedPcmRecordingFailure.RecordingRejected);
    }

    public void OnPcmObservationFailed(PcmObservationFailure failure) => Fail(failure switch
    {
        PcmObservationFailure.InvalidFormat => ReceivedPcmRecordingFailure.InvalidPcm,
        PcmObservationFailure.ReceivedMetadataUnavailable => ReceivedPcmRecordingFailure.MetadataUnavailable,
        _ => ReceivedPcmRecordingFailure.ObservationFailed
    });

    private bool Fail(ReceivedPcmRecordingFailure failure)
    {
        Interlocked.CompareExchange(ref _failure, (int)failure, 0);
        try { _guard.RejectInput(failure == ReceivedPcmRecordingFailure.InvalidPcm ? PcmRecordingFailure.InvalidBlock : PcmRecordingFailure.TimelineConflict); }
        catch (Exception) { /* A foreign input guard must not throw into the codec callback. */ }
        return false;
    }

    private async Task<ReceivedPcmRecordingFailure?> CompleteAsync()
    {
        try
        {
            var failure = await _subscription.Completion.ConfigureAwait(false);
            if (failure is { } reason) OnPcmObservationFailed(reason);
        }
        catch (Exception) { Fail(ReceivedPcmRecordingFailure.ObservationFailed); }
        finally
        {
            try { await _feed.DisposeAsync().ConfigureAwait(false); }
            catch (Exception) { Fail(ReceivedPcmRecordingFailure.RecordingRejected); }
        }
        return Failure;
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate) return new(_dispose ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        try { await _subscription.DisposeAsync().ConfigureAwait(false); }
        finally { await Completion.ConfigureAwait(false); }
    }
}
