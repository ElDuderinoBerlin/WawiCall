using System.Buffers.Binary;
using System.Collections.Concurrent;
using CSCore.Codecs.WAV;
using CSCore.MediaFoundation;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Audio;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class PcmRecordingTests
{
    [TestMethod]
    public async Task CapturesBothSidesOnOneTimelineWithGapsAndFinalizesBeforeMixing()
    {
        using var files = new Files();
        await using var session = await Factory().StartAsync(files.Request());
        Assert.AreEqual(PcmRecordingState.Capturing, session.State); Assert.IsFalse(session.CaptureCompletion.IsCompleted);
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(80, 1000)));
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Received, TimeSpan.FromMilliseconds(10), 8000, Samples(160, 2000)));
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(20), 8000, Samples(80, 3000)));
        var result = await session.StopAsync(TimeSpan.FromMilliseconds(50));
        Assert.AreEqual(PcmRecordingState.Completed, session.State); Assert.IsNull(session.Failure); Assert.IsTrue(session.CaptureCompletion.IsCompletedSuccessfully);
        Assert.AreEqual(2, result.Segments.Length); Assert.AreEqual(400L, result.Wave.FrameCount);
        var samples = Read(files.Output, 8000, 2, 400);
        for (var frame = 0; frame < 400; frame++)
        {
            Assert.AreEqual(frame < 80 ? (short)1000 : frame is >= 160 and < 240 ? (short)3000 : (short)0, samples[frame * 2]);
            Assert.AreEqual(frame is >= 80 and < 240 ? (short)2000 : (short)0, samples[frame * 2 + 1]);
        }
        foreach (var segment in result.Segments)
        {
            Assert.IsTrue(File.Exists(segment.WavePath));
            using var exclusive = File.Open(segment.WavePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        Assert.IsFalse(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(80, 9)));
    }

    [TestMethod]
    public async Task SampleRateChangesCreateSeparateMonoSourcesUsingTheirActualPcmRates()
    {
        using var files = new Files(); await using var session = await Factory().StartAsync(files.Request() with { SampleRate = 16000 });
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(160, 1000)));
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Received, TimeSpan.Zero, 16000, Samples(320, 2000)));
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(20), 16000, Samples(320, 3000)));
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Received, TimeSpan.FromMilliseconds(20), 48000, Samples(960, 4000)));
        var result = await session.StopAsync(TimeSpan.FromMilliseconds(60));
        Assert.AreEqual(4, result.Segments.Length); Assert.AreEqual(960L, result.Wave.FrameCount);
        var rates = result.Segments.Select(segment => { using var source = new WaveFileReader(segment.WavePath); return source.WaveFormat.SampleRate; }).Order().ToArray();
        CollectionAssert.AreEqual(new[] { 8000, 16000, 16000, 48000 }, rates);
        Assert.IsTrue(Read(files.Output, 16000, 2, 960).Any(sample => sample != 0));
    }

    [TestMethod]
    public async Task CallbackCopiesOwnedDataWithoutWaitingForAStalledFileWriter()
    {
        using var files = new Files(); using var gate = new WriteGate();
        var sinks = new Sinks { BeforeWrite = gate.BlockOnce };
        await using var session = await Factory(sinks).StartAsync(files.Request());
        var payload = Samples(80, 1234);
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, payload));
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); Array.Fill(payload, (short)0);
            Assert.IsTrue(await Task.Run(() => session.TryAppend(ConversationAudioSide.Received, TimeSpan.Zero, 8000, Samples(80, 2345))).WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.IsFalse(File.Exists(files.Output));
        }
        finally { gate.Release.Set(); }
        await session.StopAsync(TimeSpan.FromMilliseconds(10));
        var samples = Read(files.Output, 8000, 2, 80);
        for (var frame = 0; frame < 80; frame++) { Assert.AreEqual((short)1234, samples[frame * 2]); Assert.AreEqual((short)2345, samples[frame * 2 + 1]); }
    }

    [TestMethod]
    public async Task QueueOverflowIsExplicitAndNeverPublishesATruncatedRecording()
    {
        using var files = new Files(); using var gate = new WriteGate(); var sinks = new Sinks { BeforeWrite = gate.BlockOnce };
        await using var session = await Factory(sinks).StartAsync(files.Request() with { QueueCapacity = 1 });
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(80, 1)));
        Task<PcmRecordingResult> stop;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(10), 8000, Samples(80, 2)));
            Assert.IsFalse(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(20), 8000, Samples(80, 3)));
            Assert.AreEqual(PcmRecordingState.Failed, session.State); Assert.AreEqual(PcmRecordingFailure.BufferOverflow, session.Failure);
            Assert.IsFalse(session.TryAppend(ConversationAudioSide.Received, TimeSpan.Zero, 8000, Samples(80, 4)));
            stop = session.StopAsync(TimeSpan.FromMilliseconds(40)); Assert.IsFalse(stop.IsCompleted);
        }
        finally { gate.Release.Set(); }
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => stop);
        Assert.AreEqual(PcmRecordingFailure.BufferOverflow, error.Failure); Assert.IsFalse(File.Exists(files.Output));
        Assert.IsTrue(session.CaptureCompletion.IsFaulted); Assert.IsTrue(sinks.Opened.All(sink => sink.Disposed));
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("rate")]
    [DataRow("large")]
    [DataRow("side")]
    [DataRow("negative")]
    [DataRow("outside")]
    [DataRow("output-limit")]
    public async Task InvalidCallbackBlocksFailSafelyWithoutThrowingIntoTheAudioCallback(string mode)
    {
        using var files = new Files(); await using var session = await Factory().StartAsync(files.Request() with { SampleRate = mode == "output-limit" ? 48000 : 8000 });
        var start = mode == "negative" ? TimeSpan.FromTicks(-1) : mode == "outside" ? TimeSpan.FromHours(8) : mode == "output-limit" ? TimeSpan.FromHours(4) : TimeSpan.Zero;
        Assert.IsFalse(session.TryAppend(mode == "side" ? (ConversationAudioSide)77 : ConversationAudioSide.Sent, start,
            mode == "rate" ? 7999 : 8000, Samples(mode == "empty" ? 0 : mode == "large" ? 801 : 80, 1234)));
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(100)));
        Assert.AreEqual(PcmRecordingFailure.InvalidBlock, error.Failure); Assert.IsNull(error.InnerException); Assert.DoesNotContain(files.Root, error.ToString());
        Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task OverlappingOrLateBlocksFailInsteadOfBeingSilentlyDiscarded(bool changeRate)
    {
        using var files = new Files(); await using var session = await Factory().StartAsync(files.Request());
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(160, 1234)));
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(10), changeRate ? 16000 : 8000, Samples(80, 2345)));
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(50)));
        Assert.AreEqual(PcmRecordingFailure.TimelineConflict, error.Failure); Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task TooShortStopWindowPreservesFinalizedSourcesAndReportsTimelineFailure()
    {
        using var files = new Files(); await using var session = await Factory().StartAsync(files.Request());
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(20), 8000, Samples(160, 1234)));
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(30)));
        Assert.AreEqual(PcmRecordingFailure.TimelineConflict, error.Failure); Assert.AreEqual(PcmRecordingState.Failed, session.State);
        Assert.AreEqual(1, Directory.GetFiles(session.SegmentDirectory).Length); Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    [DataRow("write")]
    [DataRow("finalize")]
    [DataRow("dispose")]
    [DataRow("open")]
    public async Task SinkErrorsAreRedactedAllResourcesAreClosedAndSourcesAreKept(string mode)
    {
        using var files = new Files();
        Action fail = () => throw new IOException("synthetic-private-detail");
        var sinks = new Sinks { BeforeOpen = mode == "open" ? fail : null, BeforeWrite = mode == "write" ? fail : null,
            BeforeComplete = mode == "finalize" ? fail : null, BeforeDispose = mode == "dispose" ? fail : null };
        await using var session = await Factory(sinks).StartAsync(files.Request());
        session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(80, 1000));
        session.TryAppend(ConversationAudioSide.Received, TimeSpan.Zero, 8000, Samples(80, 2000));
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(10)));
        Assert.AreEqual(PcmRecordingFailure.FileFailure, error.Failure); Assert.IsNull(error.InnerException); Assert.DoesNotContain("synthetic-private-detail", error.ToString());
        Assert.IsTrue(sinks.Opened.All(sink => sink.Disposed)); Assert.IsFalse(File.Exists(files.Output)); Assert.IsTrue(Directory.Exists(session.SegmentDirectory));
        foreach (var path in Directory.GetFiles(session.SegmentDirectory)) { using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
    }

    [TestMethod]
    public async Task SegmentLimitStopsRepeatedRateChangesBeforeOpeningAnotherSource()
    {
        using var files = new Files(); var sinks = new Sinks();
        await using var session = await Factory(sinks).StartAsync(files.Request() with { QueueCapacity = 256 });
        for (var index = 0; index < 129; index++)
        {
            var rate = index % 2 == 0 ? 8000 : 16000;
            session.TryAppend(ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(index * 10), rate, Samples(rate / 100, 1234));
        }
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => session.StopAsync(TimeSpan.FromMilliseconds(1300)));
        Assert.AreEqual(PcmRecordingFailure.SegmentLimit, error.Failure); Assert.AreEqual(128, sinks.Opened.Count);
        Assert.IsTrue(sinks.Opened.All(sink => sink.Disposed)); Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task StopIsIdempotentDrainsOnceAndRejectsLateCallbacksWithoutChangingSuccess()
    {
        using var files = new Files(); using var gate = new WriteGate(); var sinks = new Sinks { BeforeWrite = gate.BlockOnce };
        await using var session = await Factory(sinks).StartAsync(files.Request());
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(80, 1234)));
        Task<PcmRecordingResult> first;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); first = session.StopAsync(TimeSpan.FromMilliseconds(20));
            Assert.AreSame(first, session.StopAsync(TimeSpan.FromMilliseconds(100))); Assert.AreEqual(PcmRecordingState.Stopping, session.State);
            Assert.IsFalse(session.TryAppend((ConversationAudioSide)77, TimeSpan.Zero, 0, [])); Assert.IsNull(session.Failure);
        }
        finally { gate.Release.Set(); }
        var result = await first; Assert.AreEqual(TimeSpan.FromMilliseconds(20), result.Wave.Duration);
        Assert.AreSame(first, session.StopAsync(TimeSpan.FromMilliseconds(50))); Assert.AreEqual(PcmRecordingState.Completed, session.State);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LifetimeAndStopCancellationWaitForInFlightIoAndDoNotPublish(bool cancelStop)
    {
        using var files = new Files(); using var lifetime = new CancellationTokenSource(); using var stopCancellation = new CancellationTokenSource();
        using var gate = new WriteGate(); var sinks = new Sinks { BeforeWrite = gate.BlockOnce };
        await using var session = await Factory(sinks).StartAsync(files.Request(), lifetime.Token);
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(80, 1234)));
        Task<PcmRecordingResult> stop;
        try
        {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            stop = session.StopAsync(TimeSpan.FromMilliseconds(20), stopCancellation.Token);
            if (cancelStop) stopCancellation.Cancel(); else lifetime.Cancel();
            Assert.IsFalse(stop.IsCompleted); Assert.IsFalse(File.Exists(files.Output));
        }
        finally { gate.Release.Set(); }
        await Assert.ThrowsAsync<OperationCanceledException>(() => stop);
        Assert.AreEqual(PcmRecordingState.Aborted, session.State); Assert.IsTrue(sinks.Opened.All(sink => sink.Disposed));
        Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task DisposingWithoutStopAbortsCaptureAndRetainsWrittenSources()
    {
        using var files = new Files(); var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sinks = new Sinks { AfterWrite = () => written.TrySetResult() };
        var session = await Factory(sinks).StartAsync(files.Request());
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(80, 1234)));
        await written.Task.WaitAsync(TimeSpan.FromSeconds(10)); await session.DisposeAsync(); await session.DisposeAsync();
        Assert.AreEqual(PcmRecordingState.Aborted, session.State); Assert.IsFalse(File.Exists(files.Output));
        Assert.AreEqual(1, Directory.GetFiles(session.SegmentDirectory).Length); Assert.IsTrue(sinks.Opened.All(sink => sink.Disposed));
        Assert.IsFalse(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(80, 3)));
        Assert.ThrowsExactly<ObjectDisposedException>(() => session.StopAsync(TimeSpan.FromMilliseconds(20)));
    }

    [TestMethod]
    public async Task DisposeDuringStopAwaitsTheSameMixInsteadOfCancelingIt()
    {
        using var files = new Files(); var mixer = new BlockingMixer();
        var session = await Factory(mixer: mixer).StartAsync(files.Request());
        var stop = session.StopAsync(TimeSpan.FromMilliseconds(20));
        try
        {
            await mixer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var dispose = session.DisposeAsync().AsTask(); Assert.IsFalse(dispose.IsCompleted);
            mixer.Release.SetResult(); await stop; await dispose;
        }
        finally { mixer.Release.TrySetResult(); await session.DisposeAsync(); }
        Assert.AreEqual(PcmRecordingState.Completed, session.State); Assert.IsTrue(File.Exists(files.Output));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedOrCanceledMixKeepsFinalizedSourcesAndAnyExistingTarget(bool cancel)
    {
        using var files = new Files(); using var stopCancellation = new CancellationTokenSource(); var mixer = new BlockingMixer();
        await using var session = await Factory(mixer: mixer).StartAsync(files.Request());
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(80, 1234)));
        var stop = session.StopAsync(TimeSpan.FromMilliseconds(10), stopCancellation.Token);
        byte[] existing = [7, 9, 11];
        try
        {
            await mixer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel) stopCancellation.Cancel(); else File.WriteAllBytes(files.Output, existing);
            mixer.Release.TrySetResult();
            if (cancel)
            {
                await Assert.ThrowsAsync<OperationCanceledException>(() => stop);
                Assert.AreEqual(PcmRecordingState.Aborted, session.State); Assert.IsFalse(File.Exists(files.Output));
            }
            else
            {
                var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => stop);
                Assert.AreEqual(PcmRecordingFailure.MixingFailed, error.Failure); Assert.AreEqual(PcmRecordingState.Failed, session.State);
                CollectionAssert.AreEqual(existing, File.ReadAllBytes(files.Output));
            }
        }
        finally { mixer.Release.TrySetResult(); }
        Assert.IsTrue(session.CaptureCompletion.IsCompletedSuccessfully);
        var source = Directory.GetFiles(session.SegmentDirectory).Single();
        Assert.IsTrue(Read(source, 8000, 1, 80).All(sample => sample == 1234));
    }

    [TestMethod]
    public async Task InvalidStopWindowLeavesTheSessionAvailableForACorrectStop()
    {
        using var files = new Files(); await using var session = await Factory().StartAsync(files.Request());
        Assert.ThrowsExactly<ArgumentException>(() => session.StopAsync(TimeSpan.Zero));
        Assert.AreEqual(PcmRecordingState.Capturing, session.State);
        Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(80, 1234)));
        await session.StopAsync(TimeSpan.FromMilliseconds(10)); Assert.AreEqual(PcmRecordingState.Completed, session.State);
    }

    [TestMethod]
    public async Task RecordingToMixToMp3RunsWithoutDevicesOrSip()
    {
        using var files = new Files(); await using var session = await Factory().StartAsync(files.Request() with { QueueCapacity = 128 });
        for (var block = 0; block < 50; block++)
        {
            var samples = Enumerable.Range(block * 160, 160).Select(frame => (short)(Math.Sin(2 * Math.PI * 440 * frame / 8000) * 10000)).ToArray();
            Assert.IsTrue(session.TryAppend(ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(block * 20), 8000, samples));
        }
        var result = await session.StopAsync(TimeSpan.FromSeconds(1)); Assert.AreEqual(1, result.Segments.Length);
        var converter = new WavToMp3Converter(new MediaFoundationMp3Backend());
        var format = (await converter.GetSupportedFormatsAsync()).First(item => item.SampleRate == 44100 && item.Channels == 2 && item.BitRate == 128000);
        var mp3 = Path.Combine(files.Root, "recorded.mp3"); await converter.ConvertAsync(new(files.Output, mp3, format));
        using var decoder = new MediaFoundationDecoder(mp3); var buffer = new byte[decoder.WaveFormat.BytesPerSecond];
        Assert.IsTrue(decoder.Read(buffer, 0, buffer.Length) > 0); Assert.IsTrue(buffer.Any(value => value != 0));
        Assert.IsTrue(File.Exists(result.Segments[0].WavePath)); Assert.IsTrue(File.Exists(files.Output));
    }

    [TestMethod]
    [DataRow("exists")]
    [DataRow("missing-parent")]
    public async Task StartupFailuresDoNotOverwriteFilesOrExposeNativeDetails(string mode)
    {
        using var files = new Files(); var request = files.Request(); byte[] existing = [1, 3, 5];
        if (mode == "exists") File.WriteAllBytes(files.Output, existing);
        else request = request with { WavePath = Path.Combine(files.Root, "missing", "recorded.wav") };
        var error = await Assert.ThrowsExactlyAsync<PcmRecordingException>(() => Factory().StartAsync(request));
        Assert.AreEqual(PcmRecordingFailure.FileFailure, error.Failure); Assert.IsNull(error.InnerException); Assert.DoesNotContain(files.Root, error.ToString());
        if (mode == "exists") CollectionAssert.AreEqual(existing, File.ReadAllBytes(files.Output));
        Assert.AreEqual(0, Directory.GetDirectories(files.Root).Length);
    }

    [TestMethod]
    public async Task RequestValidationAndPreCanceledStartDoNotCreateArtifacts()
    {
        using var files = new Files(); var good = files.Request();
        foreach (var request in new[] { good with { QueueCapacity = 0 }, good with { QueueCapacity = 257 }, good with { CallId = Guid.Empty },
            good with { RecordingId = Guid.Empty }, good with { WavePath = "relative.wav" }, good with { SampleRate = 12345 } })
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => Factory().StartAsync(request));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Factory().StartAsync(good, canceled.Token));
        Assert.AreEqual(0, Directory.GetFileSystemEntries(files.Root).Length); Assert.DoesNotContain(files.Root, good.ToString());
    }

    [TestMethod]
    public async Task IndependentSessionsAndFactoriesShareNoRecordingState()
    {
        using var files = new Files(); var factory = Factory(); var firstRequest = files.Request();
        await using var first = await factory.StartAsync(firstRequest);
        var secondPath = Path.Combine(files.Root, "second.wav");
        await using var second = await factory.StartAsync(files.Request() with { WavePath = secondPath });
        Assert.AreNotEqual(first.SegmentDirectory, second.SegmentDirectory);
        Assert.IsTrue(first.TryAppend(ConversationAudioSide.Sent, TimeSpan.Zero, 8000, Samples(80, 1000)));
        Assert.IsTrue(second.TryAppend(ConversationAudioSide.Received, TimeSpan.Zero, 8000, Samples(80, 2000)));
        await Task.WhenAll(first.StopAsync(TimeSpan.FromMilliseconds(10)), second.StopAsync(TimeSpan.FromMilliseconds(10)));
        Assert.AreEqual((short)1000, Read(files.Output, 8000, 2, 80)[0]); Assert.AreEqual((short)0, Read(files.Output, 8000, 2, 80)[1]);
        Assert.AreEqual((short)0, Read(secondPath, 8000, 2, 80)[0]); Assert.AreEqual((short)2000, Read(secondPath, 8000, 2, 80)[1]);
    }

    [TestMethod]
    public async Task ConcurrentDuplexProducersPreserveEachSidesOrderedPcmBlocks()
    {
        using var files = new Files(); await using var session = await Factory().StartAsync(files.Request() with { QueueCapacity = 128 });
        await Task.WhenAll(Produce(ConversationAudioSide.Sent, 1234), Produce(ConversationAudioSide.Received, -2345));
        await session.StopAsync(TimeSpan.FromMilliseconds(500));
        var pcm = Read(files.Output, 8000, 2, 4000);
        for (var frame = 0; frame < 4000; frame++) { Assert.AreEqual((short)1234, pcm[frame * 2]); Assert.AreEqual((short)-2345, pcm[frame * 2 + 1]); }

        Task Produce(ConversationAudioSide side, int value) => Task.Run(() =>
        {
            for (var block = 0; block < 25; block++)
                Assert.IsTrue(session.TryAppend(side, TimeSpan.FromMilliseconds(block * 20), 8000, Samples(160, value)));
        });
    }

    [TestMethod]
    public void ConsoleHostResolvesSingletonFactoryWithoutStartingCapture()
    {
        using var host = ConsoleCompositionRoot.Build(loadPlugins: false);
        var factory = host.Services.GetRequiredService<IPcmRecordingSessionFactory>();
        Assert.IsInstanceOfType<PcmRecordingSessionFactory>(factory); Assert.AreSame(factory, host.Services.GetRequiredService<IPcmRecordingSessionFactory>());
        Assert.IsInstanceOfType<CscorePcmSegmentSinkFactory>(host.Services.GetRequiredService<IPcmSegmentSinkFactory>());
    }

    private static IPcmRecordingSessionFactory Factory(IPcmSegmentSinkFactory? sinks = null, IConversationWaveMixer? mixer = null) =>
        new PcmRecordingSessionFactory(sinks ?? new CscorePcmSegmentSinkFactory(), mixer ?? new ConversationWaveMixer(new ConversationWaveRenderer()));
    private static short[] Samples(int count, int value) => Enumerable.Repeat((short)value, count).ToArray();
    private static short[] Read(string path, int rate, int channels, int frames)
    {
        using var reader = new WaveFileReader(path); Assert.AreEqual(rate, reader.WaveFormat.SampleRate); Assert.AreEqual(channels, reader.WaveFormat.Channels);
        Assert.AreEqual(frames * channels * 2L, reader.Length); var bytes = new byte[reader.Length]; Assert.AreEqual(bytes.Length, reader.Read(bytes, 0, bytes.Length));
        return Enumerable.Range(0, bytes.Length / 2).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2, 2))).ToArray();
    }
    private sealed class WriteGate : IDisposable
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release { get; } = new(); private int _used;
        internal void BlockOnce() { if (Interlocked.Exchange(ref _used, 1) == 0) { Entered.TrySetResult(); Release.Wait(); } }
        public void Dispose() { Release.Set(); Release.Dispose(); }
    }
    private sealed class Sinks : IPcmSegmentSinkFactory
    {
        internal Action? BeforeOpen, BeforeWrite, BeforeComplete, BeforeDispose, AfterWrite;
        internal ConcurrentBag<Sink> Opened { get; } = [];
        public IPcmSegmentSink Open(string path, int sampleRate)
        {
            BeforeOpen?.Invoke(); var sink = new Sink(new CscorePcmSegmentSinkFactory().Open(path, sampleRate), this);
            Opened.Add(sink); return sink;
        }
        internal sealed class Sink(IPcmSegmentSink inner, Sinks owner) : IPcmSegmentSink
        {
            private int _disposed; internal bool Disposed => Volatile.Read(ref _disposed) != 0;
            public void Write(ReadOnlySpan<short> samples) { owner.BeforeWrite?.Invoke(); inner.Write(samples); owner.AfterWrite?.Invoke(); }
            public void Complete() { owner.BeforeComplete?.Invoke(); inner.Complete(); }
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                try { owner.BeforeDispose?.Invoke(); } finally { inner.Dispose(); }
            }
        }
    }
    private sealed class BlockingMixer : IConversationWaveMixer
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ConversationWaveMixResult> MixAsync(ConversationWaveMixRequest request, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult(); await Release.Task.WaitAsync(cancellationToken);
            return await new ConversationWaveMixer(new ConversationWaveRenderer()).MixAsync(request, cancellationToken);
        }
    }
    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "WawiCallPro-pcm-record-" + Guid.NewGuid().ToString("N"));
        internal string Output => Path.Combine(Root, "recorded.wav");
        internal Files() => Directory.CreateDirectory(Root);
        internal PcmRecordingRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(), Output, SampleRate: 8000);
        public void Dispose()
        {
            foreach (var child in Directory.GetDirectories(Root)) { foreach (var file in Directory.GetFiles(child)) File.Delete(file); Directory.Delete(child); }
            foreach (var file in Directory.GetFiles(Root)) File.Delete(file); Directory.Delete(Root);
        }
    }
}
