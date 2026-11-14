using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using SIPSorcery.Media;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class PcmObservationTests
{
    private static AudioFormat Format => new(SDPWellKnownMediaFormatsEnum.PCMU);

    [TestMethod]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMA, 8000)]
    [DataRow(SDPWellKnownMediaFormatsEnum.PCMU, 8000)]
    [DataRow(SDPWellKnownMediaFormatsEnum.G722, 16000)]
    public async Task ActualSipsorceryCodecsRemainBitIdenticalAndExposeTheirActualPcmRate(SDPWellKnownMediaFormatsEnum codec, int pcmRate)
    {
        var format = new AudioFormat(codec);
        if (codec == SDPWellKnownMediaFormatsEnum.G722) { Assert.AreEqual(8000, format.RtpClockRate); Assert.AreEqual(16000, format.ClockRate); }
        var baseline = new AudioEncoder(); await using var tap = new PcmObservingAudioEncoder(new AudioEncoder());
        var observer = new Observer(); await using var subscription = tap.Subscribe(observer);
        for (var block = 0; block < 3; block++)
        {
            var input = Enumerable.Range(block * pcmRate / 50, pcmRate / 50).Select(frame => (short)(Math.Sin(2 * Math.PI * 440 * frame / pcmRate) * 10000)).ToArray();
            var expected = baseline.EncodeAudio(input, format); var actual = tap.EncodeAudio(input, format);
            CollectionAssert.AreEqual(expected, actual);
            var expectedPcm = baseline.DecodeAudio(expected, format); var actualPcm = tap.DecodeAudio(actual, format);
            CollectionAssert.AreEqual(expectedPcm, actualPcm);
            var observed = observer.Frames.ToArray(); Assert.AreEqual((block + 1) * 2, observed.Length);
            var sent = observed[block * 2]; var received = observed[block * 2 + 1];
            Assert.AreEqual(ConversationAudioSide.Sent, sent.Side); Assert.AreEqual(ConversationAudioSide.Received, received.Side);
            Assert.AreEqual(pcmRate, sent.Rate); Assert.AreEqual(pcmRate, received.Rate); Assert.AreEqual(1, received.Channels);
            CollectionAssert.AreEqual(input, sent.Samples); CollectionAssert.AreEqual(actualPcm, received.Samples);
        }
        Assert.AreEqual(0L, tap.FailureCount);
    }

    [TestMethod]
    public async Task WithoutObserversTheExistingCodecResultsFormatsAndFailuresPassThroughUnchanged()
    {
        var native = new Encoder(); await using var tap = new PcmObservingAudioEncoder(native);
        Assert.AreSame(native.Formats, tap.SupportedFormats);
        Assert.AreSame(native.Encoded, tap.EncodeAudio([1, 2], Format)); Assert.AreSame(native.Decoded, tap.DecodeAudio([3, 4], Format));
        var failure = new InvalidOperationException("synthetic-codec-failure"); native.Error = failure;
        Assert.AreSame(failure, Assert.ThrowsExactly<InvalidOperationException>(() => tap.EncodeAudio([1, 2], Format)));
        Assert.AreSame(failure, Assert.ThrowsExactly<InvalidOperationException>(() => tap.DecodeAudio([1, 2], Format)));
        Assert.AreEqual(0L, tap.FailureCount); Assert.IsNull(tap.LastFailure);
    }

    [TestMethod]
    [DataRow(false, PcmObservationFailure.ObserverRejected)]
    [DataRow(true, PcmObservationFailure.ObserverFailed)]
    public async Task AFailingObserverIsRetiredWithoutChangingAudioOrOtherObservers(bool throws, PcmObservationFailure reason)
    {
        var native = new Encoder(); await using var tap = new PcmObservingAudioEncoder(native);
        var failed = new Observer { Behavior = () => throws ? throw new IOException("synthetic-private-detail") : false };
        var good = new Observer(); await using var badSubscription = tap.Subscribe(failed); await using var goodSubscription = tap.Subscribe(good);
        Assert.AreSame(native.Encoded, tap.EncodeAudio([11, 22], Format));
        Assert.AreEqual(reason, await badSubscription.Completion); Assert.AreEqual(reason, tap.LastFailure); Assert.AreEqual(1L, tap.FailureCount);
        Assert.AreSame(native.Decoded, tap.DecodeAudio([1, 2], Format));
        Assert.AreEqual(1, failed.Calls); Assert.AreEqual(2, good.Calls); Assert.IsFalse(goodSubscription.Completion.IsCompleted);
        CollectionAssert.AreEqual(new short[] { 11, 22 }, good.Frames.First().Samples);
    }

    [TestMethod]
    [DataRow("rate")]
    [DataRow("channels")]
    [DataRow("alignment")]
    public async Task InvalidObservationFormatsAreReportedWithoutThrowingIntoTheCodec(string mode)
    {
        var native = new Encoder(); await using var tap = new PcmObservingAudioEncoder(native); var observer = new Observer();
        await using var subscription = tap.Subscribe(observer);
        var format = Format;
        if (mode == "rate") format.ClockRate = 0;
        if (mode == "channels") format.ChannelCount = 0;
        if (mode == "alignment") format.ChannelCount = 2;
        Assert.AreSame(native.Encoded, tap.EncodeAudio([1, 2, 3], format));
        Assert.AreEqual(PcmObservationFailure.InvalidFormat, await subscription.Completion); Assert.AreEqual(0, observer.Calls);
        Assert.AreEqual(1L, tap.FailureCount);
    }

    [TestMethod]
    public async Task EmptyCodecOutputProducesNoObservationOrFailure()
    {
        var native = new Encoder { Decoded = [] }; await using var tap = new PcmObservingAudioEncoder(native); var observer = new Observer();
        await using var subscription = tap.Subscribe(observer);
        tap.EncodeAudio([], Format); tap.DecodeAudio([], Format);
        Assert.AreEqual(0, observer.Calls); Assert.AreEqual(0L, tap.FailureCount); Assert.IsFalse(subscription.Completion.IsCompleted);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RetirementWaitsForInFlightCallbacksAndPreservesALateCallbackFailure(bool failAfterRetirement)
    {
        using var gate = new Gate(); await using var tap = new PcmObservingAudioEncoder(new Encoder());
        var observer = new Observer { Behavior = () => { gate.Enter(); if (failAfterRetirement) throw new IOException("synthetic-private-detail"); return true; } };
        var subscription = tap.Subscribe(observer); var publish = Task.Run(() => tap.EncodeAudio([1, 2], Format));
        Task retirement;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); retirement = subscription.DisposeAsync().AsTask();
            Assert.IsFalse(retirement.IsCompleted); Assert.IsFalse(subscription.Completion.IsCompleted);
            tap.EncodeAudio([3, 4], Format); Assert.AreEqual(1, observer.Calls);
        }
        finally { gate.Release.Set(); }
        await publish; await retirement;
        Assert.AreEqual(failAfterRetirement ? PcmObservationFailure.ObserverFailed : (PcmObservationFailure?)null, await subscription.Completion);
        Assert.AreEqual(failAfterRetirement ? 1L : 0L, tap.FailureCount);
        await subscription.DisposeAsync(); Assert.AreEqual(1, observer.Calls);
    }

    [TestMethod]
    public async Task ObserverCanRetireItselfWithoutWaitingInsideTheCallback()
    {
        await using var tap = new PcmObservingAudioEncoder(new Encoder()); IConversationPcmSubscription? subscription = null;
        var observer = new Observer { Behavior = () => { _ = subscription!.DisposeAsync(); return true; } };
        subscription = tap.Subscribe(observer); tap.EncodeAudio([1, 2], Format);
        Assert.IsNull(await subscription.Completion); tap.EncodeAudio([3, 4], Format); Assert.AreEqual(1, observer.Calls);
    }

    [TestMethod]
    public async Task SourceDisposalRetiresAllObserversAndStillAllowsNativeAudioUntilEndpointClose()
    {
        var native = new Encoder(); var tap = new PcmObservingAudioEncoder(native); var first = new Observer(); var second = new Observer();
        var one = tap.Subscribe(first); var two = tap.Subscribe(second);
        await tap.DisposeAsync(); await tap.DisposeAsync(); Assert.IsNull(await one.Completion); Assert.IsNull(await two.Completion);
        Assert.AreSame(native.Encoded, tap.EncodeAudio([1, 2], Format)); Assert.AreEqual(0, first.Calls); Assert.AreEqual(0, second.Calls);
        Assert.ThrowsExactly<ObjectDisposedException>(() => tap.Subscribe(first));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MediaDisposalDrainsObserversBeforeNativeCloseAndIsAwaitablyIdempotent(bool previouslyRetired)
    {
        using var gate = new Gate(); var tap = new PcmObservingAudioEncoder(new Encoder()); var native = new Media();
        await using var media = new PcmSipMediaSession(native, tap); Assert.AreSame(tap, ((ISipPcmMediaSession)media).PcmSource);
        media.ApplyPlaybackSettings(); Assert.AreEqual(1, native.Applied);
        var observer = new Observer { Behavior = () => { gate.Enter(); return true; } };
        var subscription = media.PcmSource.Subscribe(observer); var publish = Task.Run(() => tap.EncodeAudio([1, 2], Format));
        Task first;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (previouslyRetired) Assert.IsFalse(subscription.DisposeAsync().IsCompleted);
            first = media.DisposeAsync().AsTask();
            Assert.AreSame(first, media.DisposeAsync().AsTask()); Assert.IsFalse(first.IsCompleted); Assert.AreEqual(0, native.Closed);
            tap.DecodeAudio([5, 6], Format); Assert.AreEqual(1, observer.Calls);
        }
        finally { gate.Release.Set(); }
        await publish; await first; Assert.IsNull(await subscription.Completion); Assert.AreEqual(1, native.Closed);
    }

    [TestMethod]
    public async Task NativeCleanupErrorsRemainObservableAfterObserverRetirement()
    {
        var tap = new PcmObservingAudioEncoder(new Encoder()); var native = new Media { Error = new IOException("synthetic-cleanup-error") };
        var media = new PcmSipMediaSession(native, tap); var subscription = media.PcmSource.Subscribe(new Observer());
        var error = await Assert.ThrowsExactlyAsync<IOException>(() => media.DisposeAsync().AsTask());
        Assert.AreSame(native.Error, error); Assert.IsNull(await subscription.Completion); Assert.AreEqual(1, native.Closed);
        await Assert.ThrowsExactlyAsync<IOException>(() => media.DisposeAsync().AsTask()); Assert.AreEqual(1, native.Closed);
    }

    [TestMethod]
    public async Task ConcurrentSidesCanPublishWithoutSharingOrMutatingBuffers()
    {
        await using var tap = new PcmObservingAudioEncoder(new Encoder()); var observer = new Observer(); await using var subscription = tap.Subscribe(observer);
        await Task.WhenAll(Task.Run(() => { for (var frame = 0; frame < 100; frame++) tap.EncodeAudio([1234, -1234], Format); }),
            Task.Run(() => { for (var frame = 0; frame < 100; frame++) tap.DecodeAudio([7, 9], Format); }));
        Assert.AreEqual(100, observer.Frames.Count(frame => frame.Side == ConversationAudioSide.Sent));
        Assert.AreEqual(100, observer.Frames.Count(frame => frame.Side == ConversationAudioSide.Received));
        Assert.IsTrue(observer.Frames.Where(frame => frame.Side == ConversationAudioSide.Sent).All(frame => frame.Samples.SequenceEqual(new short[] { 1234, -1234 })));
    }

    [TestMethod]
    public async Task BoundedSubscriptionsCanBeReusedAfterRetirement()
    {
        await using var tap = new PcmObservingAudioEncoder(new Encoder());
        var observers = Enumerable.Range(0, 8).Select(_ => tap.Subscribe(new Observer())).ToArray();
        Assert.ThrowsExactly<InvalidOperationException>(() => tap.Subscribe(new Observer()));
        await observers[0].DisposeAsync(); await using var replacement = tap.Subscribe(new Observer());
        foreach (var observer in observers) await observer.DisposeAsync();
        Assert.AreEqual(0L, tap.FailureCount);
    }

    [TestMethod]
    public async Task RetiredLeaseDoesNotKeepItsObserverAlive()
    {
        await using var tap = new PcmObservingAudioEncoder(new Encoder()); var (weak, lease) = AddRetiredObserver(tap);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.IsFalse(weak.TryGetTarget(out _)); GC.KeepAlive(lease);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<IConversationPcmObserver>, IConversationPcmSubscription) AddRetiredObserver(PcmObservingAudioEncoder tap)
    {
        var observer = new Observer(); var lease = tap.Subscribe(observer); lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return (new WeakReference<IConversationPcmObserver>(observer), lease);
    }

    private sealed record Frame(ConversationAudioSide Side, int Rate, int Channels, short[] Samples);
    private sealed class Observer : IConversationPcmObserver
    {
        internal ConcurrentQueue<Frame> Frames { get; } = new(); internal Func<bool>? Behavior; internal int Calls;
        public bool TryObserve(ConversationAudioSide side, int sampleRate, int channels, ReadOnlySpan<short> samples)
        {
            Interlocked.Increment(ref Calls); Frames.Enqueue(new(side, sampleRate, channels, samples.ToArray())); return Behavior?.Invoke() ?? true;
        }
    }
    private sealed class Encoder : IAudioEncoder
    {
        internal List<AudioFormat> Formats { get; } = [Format]; internal byte[] Encoded = [1, 3, 5]; internal short[] Decoded = [2, 4, 6];
        internal Exception? Error;
        public List<AudioFormat> SupportedFormats => Formats;
        public byte[] EncodeAudio(short[] pcm, AudioFormat format) { if (Error is { } error) throw error; return Encoded; }
        public short[] DecodeAudio(byte[] encoded, AudioFormat format) { if (Error is { } error) throw error; return Decoded; }
    }
    private sealed class Media : ISipMediaSession
    {
        internal int Applied, Closed; internal Exception? Error;
        public SIPSorcery.SIP.App.IMediaSession Session => throw new NotSupportedException();
        public void ApplyPlaybackSettings() => Applied++;
        public ValueTask DisposeAsync() { Interlocked.Increment(ref Closed); return Error is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask; }
    }
    private sealed class Gate : IDisposable
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new();
        internal void Enter() { Entered.TrySetResult(); Release.Wait(); }
        public void Dispose() { Release.Set(); Release.Dispose(); }
    }
}
