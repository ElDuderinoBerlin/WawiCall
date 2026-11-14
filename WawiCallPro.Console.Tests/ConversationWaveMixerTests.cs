using System.Buffers.Binary;
using System.Collections.Immutable;
using CSCore;
using CSCore.Codecs.WAV;
using CSCore.MediaFoundation;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Audio;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class ConversationWaveMixerTests
{
    [TestMethod]
    public async Task StereoPreservesSidesOffsetsGapsAndExactWindowEvenWithUnorderedSegments()
    {
        using var files = new Files();
        var sent = files.Wave("sent", 8000, Enumerable.Repeat((short)1000, 800).ToArray());
        var received = files.Wave("received", 8000, Enumerable.Repeat((short)2000, 800).ToArray());
        var before = files.Snapshot();
        var request = files.Request([new(received, ConversationAudioSide.Received, TimeSpan.FromMilliseconds(50)),
            new(sent, ConversationAudioSide.Sent, TimeSpan.Zero)], TimeSpan.FromMilliseconds(200));
        var result = await Mixer().MixAsync(request);
        Assert.AreEqual(request.CallId, result.CallId); Assert.AreEqual(request.RecordingId, result.RecordingId);
        Assert.AreEqual(1600L, result.FrameCount); Assert.AreEqual(2, result.Channels); Assert.AreEqual(0L, result.ClippedSamples);
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), result.Duration);
        var samples = ReadWave(files.Output, 8000, 2, 1600);
        for (var frame = 0; frame < 1600; frame++)
        {
            Assert.AreEqual(frame < 800 ? (short)1000 : (short)0, samples[frame * 2], "Sent frame " + frame);
            Assert.AreEqual(frame is >= 400 and < 1200 ? (short)2000 : (short)0, samples[frame * 2 + 1], "Received frame " + frame);
        }
        files.AssertUnchanged(before);
    }

    [TestMethod]
    [DataRow(30000, 10000, 32767, true)]
    [DataRow(-30000, -10000, -32768, true)]
    [DataRow(1000, -1000, 0, false)]
    [DataRow(10000, 20000, 30000, false)]
    [DataRow(32767, 0, 32767, false)]
    public async Task MonoSaturatesWithoutWrappingAndReportsClippedSamples(int sentValue, int receivedValue, int expected, bool clipped)
    {
        using var files = new Files();
        var sent = files.Wave("sent", 8000, Enumerable.Repeat((short)sentValue, 800).ToArray());
        var received = files.Wave("received", 8000, Enumerable.Repeat((short)receivedValue, 800).ToArray());
        var request = files.Request([new(sent, ConversationAudioSide.Sent, TimeSpan.Zero),
            new(received, ConversationAudioSide.Received, TimeSpan.Zero)], TimeSpan.FromMilliseconds(100)) with { Layout = ConversationWaveLayout.MixedMono };
        var result = await Mixer().MixAsync(request);
        Assert.AreEqual(clipped ? 800L : 0L, result.ClippedSamples);
        Assert.IsTrue(ReadWave(files.Output, 8000, 1, 800).All(sample => sample == expected));
    }

    [TestMethod]
    public async Task SequentialSegmentsOnSameSideRemainSeparatedBySilence()
    {
        using var files = new Files();
        var first = files.Wave("first", 8000, Enumerable.Repeat((short)1234, 80).ToArray());
        var second = files.Wave("second", 8000, Enumerable.Repeat((short)-4321, 80).ToArray());
        await Mixer().MixAsync(files.Request([new(second, ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(30)),
            new(first, ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(10))], TimeSpan.FromMilliseconds(50)));
        var samples = ReadWave(files.Output, 8000, 2, 400);
        for (var frame = 0; frame < 400; frame++)
        {
            Assert.AreEqual(frame is >= 80 and < 160 ? (short)1234 : frame is >= 240 and < 320 ? (short)-4321 : (short)0, samples[frame * 2]);
            Assert.AreEqual((short)0, samples[frame * 2 + 1]);
        }
    }

    [TestMethod]
    [DataRow(ConversationWaveLayout.SeparatedStereo, 2)]
    [DataRow(ConversationWaveLayout.MixedMono, 1)]
    public async Task MissingMediaProducesAnExplicitSilentRecordingWindow(ConversationWaveLayout layout, int channels)
    {
        using var files = new Files();
        var result = await Mixer().MixAsync(files.Request([], TimeSpan.FromMilliseconds(100)) with { Layout = layout });
        Assert.AreEqual(800L, result.FrameCount);
        Assert.IsTrue(ReadWave(files.Output, 8000, channels, 800).All(sample => sample == 0));
    }

    [TestMethod]
    [DataRow(8000, 16000, 48000)]
    [DataRow(16000, 8000, 44100)]
    [DataRow(44100, 48000, 16000)]
    [DataRow(192000, 8000, 8000)]
    public async Task RealCscoreResamplingUsesPcmRatesAndPreservesSeparateTones(int sentRate, int receivedRate, int outputRate)
    {
        using var files = new Files();
        var sent = files.Wave("sent", sentRate, Tone(sentRate, 440));
        var received = files.Wave("received", receivedRate, Tone(receivedRate, 660), addMetadata: true);
        var before = files.Snapshot();
        var request = files.Request([new(sent, ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(100)),
            new(received, ConversationAudioSide.Received, TimeSpan.FromMilliseconds(200))], TimeSpan.FromMilliseconds(1300)) with { SampleRate = outputRate };
        var result = await Mixer().MixAsync(request);
        var samples = ReadWave(files.Output, outputRate, 2, outputRate * 13 / 10);
        Assert.IsTrue(samples.Take(outputRate / 10 * 2).All(sample => sample == 0));
        Assert.IsTrue(ToneStrength(samples, outputRate, 0, 440) > ToneStrength(samples, outputRate, 0, 660) * 8);
        Assert.IsTrue(ToneStrength(samples, outputRate, 1, 660) > ToneStrength(samples, outputRate, 1, 440) * 8);
        Assert.IsTrue(samples.Skip(outputRate * 12 / 10 * 2).All(sample => sample == 0));
        Assert.AreEqual(TimeSpan.FromMilliseconds(1300), result.Duration); files.AssertUnchanged(before);
    }

    [TestMethod]
    public async Task FractionalFramesRoundOnceOnTheRecordingTimebase()
    {
        using var files = new Files();
        var input = files.Wave("fractional", 44100, Enumerable.Repeat((short)1234, 123).ToArray());
        var result = await Mixer().MixAsync(files.Request([new(input, ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(10))],
            TimeSpan.FromMilliseconds(100)) with { SampleRate = 16000 });
        Assert.AreEqual(1600L, result.FrameCount);
        var samples = ReadWave(files.Output, 16000, 2, 1600);
        Assert.IsTrue(samples.Take(160 * 2).All(sample => sample == 0));
        Assert.IsTrue(samples.Skip(205 * 2).All(sample => sample == 0));
        Assert.IsTrue(samples.Skip(160 * 2).Take(45 * 2).Any(sample => sample != 0));
    }

    [TestMethod]
    public async Task FinalizedMixedWaveFeedsTheExistingMp3Converter()
    {
        using var files = new Files();
        var sent = files.Wave("sent", 8000, Tone(8000, 440));
        await Mixer().MixAsync(files.Request([new(sent, ConversationAudioSide.Sent, TimeSpan.Zero)], TimeSpan.FromSeconds(1)));
        var converter = new WavToMp3Converter(new MediaFoundationMp3Backend());
        var format = (await converter.GetSupportedFormatsAsync()).First(item => item.SampleRate == 44100 && item.Channels == 2 && item.BitRate == 128000);
        var path = Path.Combine(files.Root, "mixed.mp3");
        await converter.ConvertAsync(new(files.Output, path, format));
        using var decoder = new MediaFoundationDecoder(path);
        var buffer = new byte[decoder.WaveFormat.BytesPerSecond];
        Assert.IsTrue(decoder.Read(buffer, 0, buffer.Length) > 0); Assert.AreEqual(2, decoder.WaveFormat.Channels);
        Assert.IsTrue(File.Exists(sent)); Assert.IsTrue(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task ResamplerRetainsAudioAtTheEndOfAShortSegment()
    {
        using var files = new Files();
        var input = new short[160]; Array.Fill(input, (short)20000, 152, 8);
        var source = files.Wave("short-tail", 8000, input);
        await Mixer().MixAsync(files.Request([new(source, ConversationAudioSide.Sent, TimeSpan.Zero)], TimeSpan.FromMilliseconds(30)) with { SampleRate = 48000 });
        var samples = ReadWave(files.Output, 48000, 2, 1440);
        Assert.IsTrue(Enumerable.Range(900, 60).Any(frame => Math.Abs((int)samples[frame * 2]) > 5000));
        Assert.IsTrue(samples.Skip(960 * 2).All(sample => sample == 0));
    }

    [TestMethod]
    public void SourcesStayReadLockedUntilTheWriterHasFinalizedAndAreReleasedAfterward()
    {
        using var files = new Files(); var input = files.Wave("source", 8000, Tone(8000, 440));
        var inspected = false;
        using var output = new InspectingStream(() =>
        {
            inspected = true;
            Assert.ThrowsExactly<IOException>(() => File.Open(input, FileMode.Open, FileAccess.Write).Dispose());
        });
        new ConversationWaveRenderer().Render(files.Request([new(input, ConversationAudioSide.Sent, TimeSpan.Zero)], TimeSpan.FromSeconds(1)), output, default);
        Assert.IsTrue(inspected); Assert.IsTrue(output.CanWrite);
        using var writableAgain = File.Open(input, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [TestMethod]
    public async Task AFailureAfterOpeningAnotherSourceStillReleasesEverySource()
    {
        using var files = new Files(); var input = files.Wave("source", 8000, Tone(8000, 440));
        var error = await Assert.ThrowsExactlyAsync<ConversationWaveMixException>(() => Mixer().MixAsync(files.Request(
            [new(input, ConversationAudioSide.Sent, TimeSpan.Zero), new(Path.Combine(files.Root, "missing.wav"), ConversationAudioSide.Received, TimeSpan.Zero)],
            TimeSpan.FromSeconds(1))));
        Assert.AreEqual(ConversationWaveMixFailure.SourceUnavailable, error.Failure);
        using var writableAgain = File.Open(input, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        files.AssertNoTemporaryFiles(); Assert.IsFalse(File.Exists(files.Output));
    }

    [TestMethod]
    public async Task MissingDestinationDirectoryDoesNotCallRendererOrLeaveOutput()
    {
        using var files = new Files();
        var renderer = new Renderer((request, output, token) => throw new AssertFailedException());
        var error = await Assert.ThrowsExactlyAsync<ConversationWaveMixException>(() => new ConversationWaveMixer(renderer).MixAsync(
            files.Request([], TimeSpan.FromMilliseconds(100)) with { WavePath = Path.Combine(files.Root, "missing", "mixed.wav") }));
        Assert.AreEqual(ConversationWaveMixFailure.DestinationUnavailable, error.Failure);
        Assert.AreEqual(0, renderer.Calls); Assert.AreEqual(0, Directory.GetFiles(files.Root).Length);
    }

    [TestMethod]
    [DataRow("overlap", ConversationWaveMixFailure.TimelineConflict)]
    [DataRow("outside", ConversationWaveMixFailure.TimelineConflict)]
    [DataRow("stereo", ConversationWaveMixFailure.InvalidWave)]
    [DataRow("truncated", ConversationWaveMixFailure.InvalidWave)]
    [DataRow("missing", ConversationWaveMixFailure.SourceUnavailable)]
    [DataRow("locked", ConversationWaveMixFailure.SourceUnavailable)]
    public async Task InvalidSourcesAndTimelinesPublishNothingAndReleaseAllInputs(string mode, ConversationWaveMixFailure expected)
    {
        using var files = new Files();
        var input = files.Wave("source", 8000, Enumerable.Repeat((short)1000, 800).ToArray(), channels: mode == "stereo" ? 2 : 1);
        if (mode == "truncated") { using var change = File.OpenWrite(input); change.SetLength(80); }
        var before = files.Snapshot();
        var segment = new ConversationWaveSegment(mode == "missing" ? Path.Combine(files.Root, "missing.wav") : input, ConversationAudioSide.Sent,
            mode == "outside" ? TimeSpan.FromMilliseconds(90) : TimeSpan.Zero);
        var segments = mode == "overlap" ? ImmutableArray.Create(segment, segment with { Start = TimeSpan.FromMilliseconds(50) }) : [segment];
        using (var locked = mode == "locked" ? File.Open(input, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null)
        {
            var error = await Assert.ThrowsExactlyAsync<ConversationWaveMixException>(() => Mixer().MixAsync(files.Request(segments, TimeSpan.FromMilliseconds(100))));
            Assert.AreEqual(expected, error.Failure); Assert.IsNull(error.InnerException); Assert.DoesNotContain(files.Root, error.ToString());
        }
        Assert.IsFalse(File.Exists(files.Output)); files.AssertUnchanged(before);
        using var writableAgain = File.Open(input, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExistingAndConcurrentTargetsArePreserved(bool concurrent)
    {
        using var files = new Files(); byte[] existing = [7, 9, 11];
        if (!concurrent) File.WriteAllBytes(files.Output, existing);
        var renderer = new Renderer((request, stream, token) =>
        {
            var result = new ConversationWaveRenderer().Render(request, stream, token);
            File.WriteAllBytes(files.Output, existing); return result;
        });
        var error = await Assert.ThrowsExactlyAsync<ConversationWaveMixException>(() => new ConversationWaveMixer(renderer).MixAsync(files.Request([], TimeSpan.FromMilliseconds(100))));
        Assert.AreEqual(ConversationWaveMixFailure.DestinationUnavailable, error.Failure);
        Assert.AreEqual(concurrent ? 1 : 0, renderer.Calls); CollectionAssert.AreEqual(existing, File.ReadAllBytes(files.Output));
        files.AssertNoTemporaryFiles();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellationAwaitsRendererAndCleansItsPartialOutput(bool cooperate)
    {
        using var files = new Files(); using var cts = new CancellationTokenSource(); using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var renderer = new Renderer((request, output, token) =>
        {
            output.WriteByte(1); entered.SetResult(); release.Wait();
            if (cooperate) token.ThrowIfCancellationRequested();
            return new(request.CallId, request.RecordingId, 800, 8000, 2, 0);
        });
        var task = new ConversationWaveMixer(renderer).MixAsync(files.Request([], TimeSpan.FromMilliseconds(100)), cts.Token);
        try { await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); cts.Cancel(); Assert.IsFalse(task.IsCompleted); Assert.IsFalse(File.Exists(files.Output)); }
        finally { release.Set(); }
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        Assert.IsFalse(File.Exists(files.Output)); files.AssertNoTemporaryFiles();
    }

    [TestMethod]
    [DataRow("native")]
    [DataRow("foreign-cancellation")]
    [DataRow("invalid-header")]
    [DataRow("wrong-result")]
    public async Task RendererFailuresCannotBecomePublishedSuccess(string mode)
    {
        using var files = new Files();
        var renderer = new Renderer((request, output, token) =>
        {
            if (mode == "native") throw new IOException("synthetic-private-detail");
            if (mode == "foreign-cancellation") throw new OperationCanceledException("synthetic-private-detail");
            var result = new ConversationWaveRenderer().Render(request, output, token);
            if (mode == "invalid-header") { output.Position = 0; output.WriteByte(0); }
            return result with { FrameCount = mode == "wrong-result" ? 1 : result.FrameCount };
        });
        var error = await Assert.ThrowsExactlyAsync<ConversationWaveMixException>(() => new ConversationWaveMixer(renderer).MixAsync(files.Request([], TimeSpan.FromMilliseconds(100))));
        Assert.AreEqual(ConversationWaveMixFailure.MixingFailed, error.Failure); Assert.IsNull(error.InnerException);
        Assert.DoesNotContain("synthetic-private-detail", error.ToString()); Assert.IsFalse(File.Exists(files.Output)); files.AssertNoTemporaryFiles();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NativeWriterAndHeaderFinalizationErrorsRemainObservable(bool finalization)
    {
        using var files = new Files();
        using var output = new FailingStream(finalization);
        Assert.Throws<Exception>(() => new ConversationWaveRenderer().Render(files.Request([], TimeSpan.FromMilliseconds(100)), output, CancellationToken.None));
        Assert.IsTrue(output.CanWrite);
        GC.Collect(); GC.WaitForPendingFinalizers();
    }

    [TestMethod]
    public async Task PreCanceledRequestDoesNotCreateFiles()
    {
        using var files = new Files(); using var cts = new CancellationTokenSource(); cts.Cancel();
        var renderer = new Renderer((request, output, token) => throw new AssertFailedException());
        await Assert.ThrowsAsync<OperationCanceledException>(() => new ConversationWaveMixer(renderer).MixAsync(files.Request([], TimeSpan.FromMilliseconds(100)), cts.Token));
        Assert.AreEqual(0, renderer.Calls); Assert.AreEqual(0, Directory.GetFiles(files.Root).Length);
    }

    [TestMethod]
    public void ValidatesWindowPathsLimitsAndIdentitiesBeforeOpeningFiles()
    {
        using var files = new Files(); var good = files.Request([], TimeSpan.FromMilliseconds(100));
        ConversationWaveMixRequest[] invalid = [good with { CallId = Guid.Empty }, good with { RecordingId = Guid.Empty },
            good with { WavePath = "relative.wav" }, good with { WavePath = @"\\server\share\output.wav" },
            good with { WavePath = Path.Combine(files.Root, "wrong.mp3") }, good with { Duration = TimeSpan.Zero },
            good with { Duration = TimeSpan.FromHours(9) }, good with { Duration = TimeSpan.FromTicks(1) },
            good with { Duration = TimeSpan.FromHours(8), SampleRate = 48000 }, good with { SampleRate = 12345 },
            good with { Layout = (ConversationWaveLayout)77 }, good with { Segments = default },
            good with { Segments = Enumerable.Repeat(new ConversationWaveSegment(files.Output, ConversationAudioSide.Sent, TimeSpan.Zero), 129).ToImmutableArray() },
            good with { Segments = [new(files.Output, ConversationAudioSide.Sent, TimeSpan.FromMilliseconds(-1))] },
            good with { Segments = [new(files.Output, (ConversationAudioSide)77, TimeSpan.Zero)] }];
        foreach (var request in invalid) Assert.ThrowsExactly<ArgumentException>(() => Mixer().MixAsync(request));
        Assert.AreEqual(0, Directory.GetFiles(files.Root).Length); Assert.DoesNotContain(files.Root, good.ToString());
    }

    [TestMethod]
    public void ConsoleHostResolvesStatelessMixerAndRendererWithoutNativeWork()
    {
        using var host = ConsoleCompositionRoot.Build(loadPlugins: false);
        var mixer = host.Services.GetRequiredService<IConversationWaveMixer>();
        Assert.IsInstanceOfType<ConversationWaveMixer>(mixer); Assert.AreSame(mixer, host.Services.GetRequiredService<IConversationWaveMixer>());
        Assert.IsInstanceOfType<ConversationWaveRenderer>(host.Services.GetRequiredService<IConversationWaveRenderer>());
    }

    private static IConversationWaveMixer Mixer() => new ConversationWaveMixer(new ConversationWaveRenderer());
    private static short[] Tone(int rate, int frequency) => Enumerable.Range(0, rate)
        .Select(frame => (short)(Math.Sin(2 * Math.PI * frequency * frame / rate) * 10000)).ToArray();
    private static double ToneStrength(short[] pcm, int rate, int channel, int frequency)
    {
        double sine = 0, cosine = 0;
        for (var frame = rate / 3; frame < rate * 9 / 10; frame++)
        {
            var sample = pcm[frame * 2 + channel];
            sine += sample * Math.Sin(2 * Math.PI * frequency * frame / rate);
            cosine += sample * Math.Cos(2 * Math.PI * frequency * frame / rate);
        }
        return sine * sine + cosine * cosine;
    }
    private static short[] ReadWave(string path, int rate, int channels, int frames)
    {
        using var reader = new WaveFileReader(path);
        Assert.AreEqual(rate, reader.WaveFormat.SampleRate); Assert.AreEqual(channels, reader.WaveFormat.Channels);
        Assert.AreEqual(16, reader.WaveFormat.BitsPerSample); Assert.AreEqual(frames * channels * 2L, reader.Length);
        var bytes = new byte[reader.Length]; var read = reader.Read(bytes, 0, bytes.Length);
        Assert.AreEqual(bytes.Length, read);
        return Enumerable.Range(0, bytes.Length / 2).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2, 2))).ToArray();
    }
    private sealed class Renderer(Func<ConversationWaveMixRequest, Stream, CancellationToken, ConversationWaveMixResult> render) : IConversationWaveRenderer
    {
        internal int Calls;
        public ConversationWaveMixResult Render(ConversationWaveMixRequest request, Stream output, CancellationToken cancellationToken)
        { Interlocked.Increment(ref Calls); return render(request, output, cancellationToken); }
    }
    private sealed class FailingStream(bool finalization) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!finalization && Position >= 44) throw new IOException("synthetic-write-failure");
            base.Write(buffer, offset, count);
        }
        public override long Position
        {
            get => base.Position;
            set
            {
                if (finalization && value == 0 && Length > 44) throw new IOException("synthetic-header-failure");
                base.Position = value;
            }
        }
    }
    private sealed class InspectingStream(Action inspect) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            inspect(); base.Write(buffer, offset, count);
        }
    }
    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "WawiCallPro-wave-mix-" + Guid.NewGuid().ToString("N"));
        internal string Output => Path.Combine(Root, "mixed.wav");
        internal Files() => Directory.CreateDirectory(Root);
        internal string Wave(string name, int rate, short[] samples, int channels = 1, bool addMetadata = false)
        {
            var path = Path.Combine(Root, name + ".wav");
            using (var writer = new WaveWriter(path, new WaveFormat(rate, 16, channels)))
            {
                var bytes = new byte[samples.Length * 2];
                for (var index = 0; index < samples.Length; index++) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2, 2), samples[index]);
                writer.Write(bytes, 0, bytes.Length);
            }
            if (addMetadata)
            {
                var bytes = File.ReadAllBytes(path);
                byte[] metadata = [(byte)'J', (byte)'U', (byte)'N', (byte)'K', 3, 0, 0, 0, 1, 2, 3, 0];
                var extended = bytes.Take(36).Concat(metadata).Concat(bytes.Skip(36)).ToArray();
                BinaryPrimitives.WriteInt32LittleEndian(extended.AsSpan(4, 4), extended.Length - 8); File.WriteAllBytes(path, extended);
            }
            return path;
        }
        internal ConversationWaveMixRequest Request(ImmutableArray<ConversationWaveSegment> segments, TimeSpan duration) =>
            new(Guid.NewGuid(), Guid.NewGuid(), segments, Output, 8000, duration);
        internal Dictionary<string, byte[]> Snapshot() => Directory.GetFiles(Root).ToDictionary(path => path, File.ReadAllBytes);
        internal void AssertUnchanged(Dictionary<string, byte[]> original)
        {
            foreach (var file in original) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key));
            AssertNoTemporaryFiles();
        }
        internal void AssertNoTemporaryFiles() => Assert.AreEqual(0, Directory.GetFiles(Root, ".wawi-mix-*").Length);
        public void Dispose() { foreach (var file in Directory.GetFiles(Root)) File.Delete(file); Directory.Delete(Root); }
    }
}
