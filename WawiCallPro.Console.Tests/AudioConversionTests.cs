using System.Collections.Immutable;
using System.Security.Cryptography;
using CSCore.Codecs.WAV;
using CSCore.MediaFoundation;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Audio;
using WawiCallPro.ConsoleApp;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class AudioConversionTests
{
    private static readonly Mp3EncodingFormat Format = new(44100, 1, 128000);

    [TestMethod]
    public async Task PublishesOnlyAfterFinalizationAndKeepsSourceLockedAndUnchanged()
    {
        using var files = new Files();
        var before = File.ReadAllBytes(files.Wave);
        using var gate = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new Backend((wave, output, token) =>
        {
            output.Write([1, 2, 3]); entered.SetResult();
            gate.Wait(token);
            output.Write([4, 5, 6]); // Includes the encoder's successful finalization.
            return TimeSpan.FromSeconds(1);
        });
        var task = new WavToMp3Converter(backend).ConvertAsync(files.Request());
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsFalse(task.IsCompleted); Assert.IsFalse(File.Exists(files.Mp3));
            Assert.ThrowsExactly<IOException>(() => File.Open(files.Wave, FileMode.Open, FileAccess.Write).Dispose());
        }
        finally { gate.Set(); }
        var result = await task;
        Assert.AreEqual(TimeSpan.FromSeconds(1), result.SourceDuration); Assert.AreEqual(6L, result.FileSize);
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(files.Mp3))).ToLowerInvariant(), result.Sha256);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4, 5, 6 }, File.ReadAllBytes(files.Mp3));
        files.AssertSourceAndNoTemporaryFiles(before);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellationWaitsForNativeReturnAndNeverPublishesEvenIfBackendIgnoresIt(bool observeToken)
    {
        using var files = new Files();
        var before = File.ReadAllBytes(files.Wave);
        using var cts = new CancellationTokenSource();
        using var gate = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var backend = new Backend((wave, output, token) =>
        {
            output.WriteByte(1); entered.SetResult(); gate.Wait();
            if (observeToken) token.ThrowIfCancellationRequested();
            return TimeSpan.FromSeconds(1);
        });
        var task = new WavToMp3Converter(backend).ConvertAsync(files.Request(), cts.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); cts.Cancel();
            Assert.IsFalse(task.IsCompleted); Assert.IsFalse(File.Exists(files.Mp3));
        }
        finally { gate.Set(); }
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
        Assert.IsFalse(File.Exists(files.Mp3)); files.AssertSourceAndNoTemporaryFiles(before);
    }

    [TestMethod]
    public async Task PreCanceledConversionDoesNotOpenFilesOrCallBackend()
    {
        using var files = new Files(); using var cts = new CancellationTokenSource(); cts.Cancel();
        var backend = new Backend();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new WavToMp3Converter(backend).ConvertAsync(files.Request(), cts.Token));
        Assert.AreEqual(0, backend.Calls); Assert.AreEqual(1, Directory.GetFiles(files.Root).Length);
    }

    [TestMethod]
    [DataRow("failure")]
    [DataRow("empty")]
    [DataRow("foreign-cancellation")]
    [DataRow("unavailable")]
    [DataRow("unsupported")]
    public async Task FailuresKeepWaveRemoveTemporaryFilesAndExposeNoNativeDetails(string failure)
    {
        using var files = new Files(); var before = File.ReadAllBytes(files.Wave);
        var expected = failure switch
        {
            "unavailable" => Mp3ConversionFailure.EncoderUnavailable,
            "unsupported" => Mp3ConversionFailure.UnsupportedFormat,
            _ => Mp3ConversionFailure.EncodingFailed
        };
        var backend = new Backend((wave, output, token) =>
        {
            if (failure == "empty") return TimeSpan.FromSeconds(1);
            output.WriteByte(1);
            if (failure == "unavailable" || failure == "unsupported") throw new Mp3ConversionException(expected);
            if (failure == "foreign-cancellation") throw new OperationCanceledException("synthetic-private-detail");
            throw new IOException("synthetic-private-detail");
        });
        var error = await Assert.ThrowsExactlyAsync<Mp3ConversionException>(() => new WavToMp3Converter(backend).ConvertAsync(files.Request()));
        Assert.AreEqual(expected, error.Failure); Assert.IsNull(error.InnerException);
        Assert.DoesNotContain("synthetic-private-detail", error.ToString()); Assert.DoesNotContain(files.Root, error.ToString());
        Assert.IsFalse(File.Exists(files.Mp3)); files.AssertSourceAndNoTemporaryFiles(before);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExistingAndConcurrentlyCreatedTargetsAreNeverOverwritten(bool createDuringEncoding)
    {
        using var files = new Files();
        var before = File.ReadAllBytes(files.Wave);
        byte[] existing = [19, 21, 23];
        if (!createDuringEncoding) File.WriteAllBytes(files.Mp3, existing);
        var backend = new Backend((wave, output, token) =>
        {
            output.WriteByte(1); File.WriteAllBytes(files.Mp3, existing); return TimeSpan.FromSeconds(1);
        });
        var error = await Assert.ThrowsExactlyAsync<Mp3ConversionException>(() => new WavToMp3Converter(backend).ConvertAsync(files.Request()));
        Assert.AreEqual(Mp3ConversionFailure.DestinationUnavailable, error.Failure);
        Assert.AreEqual(createDuringEncoding ? 1 : 0, backend.Calls);
        CollectionAssert.AreEqual(existing, File.ReadAllBytes(files.Mp3)); files.AssertSourceAndNoTemporaryFiles(before);
    }

    [TestMethod]
    public async Task ConcurrentConversionsToSameTargetPublishExactlyOneCompleteFile()
    {
        using var files = new Files(); var before = File.ReadAllBytes(files.Wave);
        using var barrier = new Barrier(2);
        var backend = new Backend((wave, output, token) =>
        {
            output.Write([3, 5, 7]);
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException();
            return TimeSpan.FromSeconds(1);
        });
        var converter = new WavToMp3Converter(backend);
        var first = converter.ConvertAsync(files.Request()); var second = converter.ConvertAsync(files.Request());
        try { await Task.WhenAll(first, second); } catch (Mp3ConversionException) { }
        Assert.AreEqual(1, new[] { first, second }.Count(task => task.IsCompletedSuccessfully));
        var failed = first.IsFaulted ? first : second;
        Assert.AreEqual(Mp3ConversionFailure.DestinationUnavailable, ((Mp3ConversionException)failed.Exception!.InnerException!).Failure);
        CollectionAssert.AreEqual(new byte[] { 3, 5, 7 }, File.ReadAllBytes(files.Mp3)); files.AssertSourceAndNoTemporaryFiles(before);
    }

    [TestMethod]
    [DataRow("missing-source", Mp3ConversionFailure.SourceUnavailable)]
    [DataRow("locked-source", Mp3ConversionFailure.SourceUnavailable)]
    [DataRow("missing-directory", Mp3ConversionFailure.DestinationUnavailable)]
    public async Task FileFailuresLeaveNoPublishedOrTemporaryOutput(string mode, Mp3ConversionFailure expected)
    {
        using var files = new Files();
        var request = mode switch
        {
            "missing-source" => files.Request() with { WavePath = Path.Combine(files.Root, "missing.wav") },
            "missing-directory" => files.Request() with { Mp3Path = Path.Combine(files.Root, "missing", "output.mp3") },
            _ => files.Request()
        };
        using var locked = mode == "locked-source" ? File.Open(files.Wave, FileMode.Open, FileAccess.ReadWrite, FileShare.None) : null;
        var error = await Assert.ThrowsExactlyAsync<Mp3ConversionException>(() => new WavToMp3Converter(new Backend()).ConvertAsync(request));
        Assert.AreEqual(expected, error.Failure); Assert.IsFalse(File.Exists(files.Mp3));
        Assert.AreEqual(1, Directory.GetFiles(files.Root).Length);
    }

    [TestMethod]
    [DataRow("relative.wav", "output.mp3")]
    [DataRow("C:\\source.wav", "C:\\output.wav")]
    [DataRow("\\\\server\\source.wav", "C:\\output.mp3")]
    [DataRow("C:\\source.wav", "C:\\output.mp3:stream.mp3")]
    public void RejectsAmbiguousPathsWithoutChangingExtensions(string source, string target)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new WavToMp3Converter(new Backend()).ConvertAsync(new(source, target, Format)));
    }

    [TestMethod]
    public async Task CapabilitiesPreserveCancellationAndRedactNativeFailures()
    {
        var backend = new Backend { Capabilities = token => throw new IOException("synthetic-private-detail") };
        var error = await Assert.ThrowsExactlyAsync<Mp3ConversionException>(() => new WavToMp3Converter(backend).GetSupportedFormatsAsync());
        Assert.AreEqual(Mp3ConversionFailure.EncoderUnavailable, error.Failure); Assert.IsNull(error.InnerException);
        using var cts = new CancellationTokenSource();
        backend.Capabilities = token => { cts.Cancel(); return [Format]; };
        await Assert.ThrowsAsync<OperationCanceledException>(() => new WavToMp3Converter(backend).GetSupportedFormatsAsync(cts.Token));
    }

    [TestMethod]
    [DataRow(8000, 1, 44100, 128000)]
    [DataRow(16000, 2, 48000, 192000)]
    [DataRow(44100, 1, 44100, 128000)]
    [DataRow(48000, 2, 48000, 192000)]
    public async Task NativeEncoderConvertsSyntheticPcmWaveToDecodableMp3(int sourceRate, int channels, int targetRate, int bitRate)
    {
        using var files = new Files(sourceRate, channels);
        if (sourceRate < 44100)
        {
            // Legal PCM fmt extension plus an odd-sized unknown metadata chunk and its padding.
            var original = File.ReadAllBytes(files.Wave);
            byte[] metadata = [0, 0, (byte)'J', (byte)'U', (byte)'N', (byte)'K', 3, 0, 0, 0, 1, 2, 3, 0];
            var extended = original.Take(36).Concat(metadata).Concat(original.Skip(36)).ToArray();
            BitConverter.GetBytes(18).CopyTo(extended, 16);
            BitConverter.GetBytes(extended.Length - 8).CopyTo(extended, 4);
            File.WriteAllBytes(files.Wave, extended);
        }
        var hash = SHA256.HashData(File.ReadAllBytes(files.Wave));
        var converter = new WavToMp3Converter(new MediaFoundationMp3Backend());
        var formats = await converter.GetSupportedFormatsAsync();
        Assert.IsTrue(formats.Length > 0, "This native acceptance requires the Windows MP3 encoder.");
        var format = formats.First(item => item.SampleRate == targetRate && item.Channels == channels && item.BitRate == bitRate);
        var result = await converter.ConvertAsync(files.Request() with { Format = format });
        Assert.AreEqual(TimeSpan.FromSeconds(2), result.SourceDuration); Assert.IsTrue(result.FileSize > 1000);
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(files.Mp3))).ToLowerInvariant(), result.Sha256);
        using (var decoder = new MediaFoundationDecoder(files.Mp3))
        {
            Assert.AreEqual(targetRate, decoder.WaveFormat.SampleRate); Assert.AreEqual(channels, decoder.WaveFormat.Channels);
            Assert.AreEqual(16, decoder.WaveFormat.BitsPerSample);
            var buffer = new byte[decoder.WaveFormat.BytesPerSecond / 10]; long bytes = 0; var nonzero = false;
            using var decoded = new MemoryStream();
            int read;
            while ((read = decoder.Read(buffer, 0, buffer.Length)) != 0)
            {
                bytes += read; nonzero |= buffer.AsSpan(0, read).ContainsAnyExcept((byte)0);
                decoded.Write(buffer, 0, read);
            }
            var seconds = bytes / (double)decoder.WaveFormat.BytesPerSecond;
            Assert.IsTrue(seconds is > 1.85 and < 2.2, "Decoded duration: " + seconds);
            Assert.IsTrue(nonzero, "The synthesized tone must survive the MP3 conversion.");
            if (channels == 2)
            {
                var samples = decoded.ToArray();
                Assert.IsTrue(ToneStrength(samples, targetRate, 0, 440) > ToneStrength(samples, targetRate, 0, 660) * 8);
                Assert.IsTrue(ToneStrength(samples, targetRate, 1, 660) > ToneStrength(samples, targetRate, 1, 440) * 8);
            }
        }
        CollectionAssert.AreEqual(hash, SHA256.HashData(File.ReadAllBytes(files.Wave)));
        Assert.AreEqual(2, Directory.GetFiles(files.Root).Length);
        File.Copy(files.Mp3, Path.Combine(files.Root, "copy.mp3")); // Native handles released.
    }

    private static double ToneStrength(byte[] pcm, int rate, int channel, int frequency)
    {
        double sine = 0, cosine = 0;
        for (var frame = rate / 5; frame < rate * 3 / 2; frame++)
        {
            var sample = BitConverter.ToInt16(pcm, (frame * 2 + channel) * 2);
            sine += sample * Math.Sin(2 * Math.PI * frequency * frame / rate);
            cosine += sample * Math.Cos(2 * Math.PI * frequency * frame / rate);
        }
        return sine * sine + cosine * cosine;
    }

    [TestMethod]
    [DataRow("riff")]
    [DataRow("size")]
    [DataRow("truncated")]
    [DataRow("empty")]
    [DataRow("format")]
    [DataRow("alignment")]
    [DataRow("partial-frame")]
    public async Task NativeBackendRejectsIncompleteOrUnsupportedWaveBeforeEncoding(string corruption)
    {
        using var files = new Files(); var data = File.ReadAllBytes(files.Wave);
        switch (corruption)
        {
            case "riff": data[0] = 0; break;
            case "size": data[4] = 0; break;
            case "truncated": Array.Resize(ref data, data.Length - 9); break;
            case "empty": Array.Resize(ref data, 44); BitConverter.GetBytes(36).CopyTo(data, 4); BitConverter.GetBytes(0).CopyTo(data, 40); break;
            case "format": data[20] = 3; break;
            case "alignment": data[32] = 1; break;
            case "partial-frame": Array.Resize(ref data, data.Length - 1); BitConverter.GetBytes(data.Length - 8).CopyTo(data, 4); BitConverter.GetBytes(data.Length - 44).CopyTo(data, 40); break;
        }
        File.WriteAllBytes(files.Wave, data);
        var error = await Assert.ThrowsExactlyAsync<Mp3ConversionException>(() =>
            new WavToMp3Converter(new MediaFoundationMp3Backend()).ConvertAsync(files.Request()));
        Assert.AreEqual(Mp3ConversionFailure.InvalidWave, error.Failure);
        Assert.IsFalse(File.Exists(files.Mp3)); files.AssertSourceAndNoTemporaryFiles(data);
    }

    [TestMethod]
    public async Task NativeBackendRejectsUnadvertisedBitrateAndChannelMixing()
    {
        using var files = new Files(); var before = File.ReadAllBytes(files.Wave);
        var converter = new WavToMp3Converter(new MediaFoundationMp3Backend());
        foreach (var format in new[] { Format with { BitRate = 123456 }, Format with { Channels = 2 } })
        {
            var error = await Assert.ThrowsExactlyAsync<Mp3ConversionException>(() => converter.ConvertAsync(files.Request() with { Format = format }));
            Assert.AreEqual(Mp3ConversionFailure.UnsupportedFormat, error.Failure);
            Assert.IsFalse(File.Exists(files.Mp3)); files.AssertSourceAndNoTemporaryFiles(before);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NativeWriteAndFinalizationFailuresReachCallerAndDoNotBreakFollowingConversions(bool failOnFlush)
    {
        using var files = new Files();
        await Task.Run(() =>
        {
            using var wave = File.OpenRead(files.Wave);
            using var output = new FailingStream(failOnFlush);
            Assert.Throws<Exception>(() => new MediaFoundationMp3Backend().Encode(wave, output, Format, CancellationToken.None));
            Assert.IsTrue(wave.CanRead); Assert.IsTrue(output.CanWrite, "The backend must not dispose borrowed streams.");
        });
        GC.Collect(); GC.WaitForPendingFinalizers();
        var result = await new WavToMp3Converter(new MediaFoundationMp3Backend()).ConvertAsync(files.Request());
        Assert.IsTrue(result.FileSize > 1000);
    }

    [TestMethod]
    public void ConsoleHostResolvesSingletonConverterWithoutOpeningNativeResources()
    {
        using var host = ConsoleCompositionRoot.Build(loadPlugins: false);
        var converter = host.Services.GetRequiredService<IMp3Converter>();
        Assert.IsInstanceOfType<WavToMp3Converter>(converter);
        Assert.AreSame(converter, host.Services.GetRequiredService<IMp3Converter>());
        Assert.IsInstanceOfType<MediaFoundationMp3Backend>(host.Services.GetRequiredService<IMp3EncodingBackend>());
        // Construction must not inspect any devices, configuration, source files or encoder capabilities.
    }

    [TestMethod]
    [DataRow("success")][DataRow("cancel")][DataRow("interrupted")][DataRow("collision")]
    public async Task PublicationHandoffPrecedesRenameAndRetainsPreparedOutputOnUncertainty(string mode)
    {
        using var files = new Files(); using var cancellation = new CancellationTokenSource();
        var source = File.ReadAllBytes(files.Wave); string? temporary = null; Mp3ConversionResult? original = null;
        var observer = new Publication((path, output, result, token) =>
        {
            temporary = path; original = result;
            Assert.IsFalse(File.Exists(files.Mp3)); Assert.AreEqual(result.FileSize, output.Length);
            output.Position = 0;
            Assert.AreEqual(result.Sha256, Convert.ToHexString(SHA256.HashData(output)).ToLowerInvariant());
            Assert.ThrowsExactly<IOException>(() => File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite).Dispose());
            if (mode == "cancel") cancellation.Cancel();
            if (mode == "collision") File.WriteAllBytes(files.Mp3, [77]);
            if (mode == "interrupted") throw new IOException("synthetic-private-handoff-detail");
        });
        var running = new WavToMp3Converter(new Backend()).ConvertAsync(files.Request() with { Publication = observer }, cancellation.Token);
        if (mode is "success" or "cancel")
        {
            var saved = await running; Assert.AreEqual(original, saved);
            Assert.IsTrue(File.Exists(files.Mp3)); Assert.IsFalse(File.Exists(temporary));
        }
        else
        {
            var error = await Assert.ThrowsExactlyAsync<Mp3ConversionException>(() => running);
            Assert.DoesNotContain("synthetic-private-handoff-detail", error.ToString()); Assert.IsNull(error.InnerException);
            Assert.IsNotNull(original); Assert.IsTrue(File.Exists(temporary));
            Assert.AreEqual(original.Sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temporary))).ToLowerInvariant());
            if (mode == "collision") CollectionAssert.AreEqual(new byte[] { 77 }, File.ReadAllBytes(files.Mp3));
            else Assert.IsFalse(File.Exists(files.Mp3));
        }
        CollectionAssert.AreEqual(source, File.ReadAllBytes(files.Wave));
    }
    private sealed class Publication(Action<string, FileStream, Mp3ConversionResult, CancellationToken> action) : IMp3PublicationObserver
    { public void Prepare(string path, FileStream output, Mp3ConversionResult result, CancellationToken token) => action(path, output, result, token); }

    private sealed class Backend(Func<Stream, Stream, CancellationToken, TimeSpan>? encode = null) : IMp3EncodingBackend
    {
        internal int Calls;
        internal Func<CancellationToken, ImmutableArray<Mp3EncodingFormat>> Capabilities = token => [Format];
        public ImmutableArray<Mp3EncodingFormat> GetSupportedFormats(CancellationToken cancellationToken) => Capabilities(cancellationToken);
        public TimeSpan Encode(Stream wave, Stream mp3, Mp3EncodingFormat format, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            if (encode is not null) return encode(wave, mp3, cancellationToken);
            mp3.WriteByte(1); return TimeSpan.FromSeconds(1);
        }
    }

    private sealed class FailingStream(bool failOnFlush) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!failOnFlush) throw new IOException("synthetic-write-failure");
            base.Write(buffer, offset, count);
        }
        public override void Flush()
        {
            if (failOnFlush && Length > 0) throw new IOException("synthetic-finalization-failure");
            base.Flush();
        }
    }

    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "WawiCallPro-mp3-" + Guid.NewGuid().ToString("N"));
        internal string Wave => Path.Combine(Root, "synthetic.wav");
        internal string Mp3 => Path.Combine(Root, "encoded.mp3");
        internal Files(int sampleRate = 44100, int channels = 1)
        {
            Directory.CreateDirectory(Root);
            using var writer = new WaveWriter(Wave, new CSCore.WaveFormat(sampleRate, 16, channels));
            var samples = new byte[sampleRate * channels * 2 * 2];
            for (var frame = 0; frame < sampleRate * 2; frame++)
            {
                for (var channel = 0; channel < channels; channel++)
                {
                    var sample = (short)(Math.Sin(2 * Math.PI * (440 + channel * 220) * frame / sampleRate) * 12000);
                    BitConverter.TryWriteBytes(samples.AsSpan((frame * channels + channel) * 2, 2), sample);
                }
            }
            writer.Write(samples, 0, samples.Length);
        }
        internal Mp3ConversionRequest Request() => new(Wave, Mp3, Format);
        internal void AssertSourceAndNoTemporaryFiles(byte[] expected)
        {
            CollectionAssert.AreEqual(expected, File.ReadAllBytes(Wave));
            Assert.AreEqual(0, Directory.GetFiles(Root, ".wawi-mp3-*").Length);
        }
        public void Dispose()
        {
            foreach (var file in Directory.GetFiles(Root)) File.Delete(file);
            Directory.Delete(Root);
        }
    }
}
