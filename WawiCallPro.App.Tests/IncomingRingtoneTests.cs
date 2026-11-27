using CSCore;
using CSCore.Codecs.WAV;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class IncomingRingtoneTests
{
    [TestMethod]
    public async Task RingtoneUsesSelectedOutputAndRepeatsItsPcmCadence()
    {
        var output = new CadenceProbe();
        await new WindowsIncomingRingtone(output).PlayAsync("ring-output", 35, CancellationToken.None);
        Assert.IsTrue(output.Played);
    }

    [TestMethod]
    public async Task EndingRingtoneFadesBothChannelsBeforeReleasingOutput()
    {
        var media = Directory.CreateTempSubdirectory("WawiCallPro-ring-fade-");
        try
        {
            const int rate = 16000;
            var frames = Enumerable.Range(0, rate).SelectMany(_ => new short[] { 16000, -12000 }).ToArray();
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(frames.AsSpan()).ToArray();
            using (var writer = new WaveWriter(Path.Combine(media.FullName, "Ring99.wav"), new WaveFormat(rate, 16, 2)))
                writer.Write(bytes, 0, bytes.Length);
            using var stop = new CancellationTokenSource();
            var output = new FadeOutProbe(stop, rate);
            var player = new WindowsIncomingRingtone(output, media.FullName);
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await player.PlayAsync(new AudioDeviceSettings { WindowsRingtoneFile = "Ring99.wav" }, stop.Token));
            Assert.IsTrue(output.Drained);
        }
        finally { media.Delete(recursive: true); }
    }

    private sealed class FadeOutProbe(CancellationTokenSource stop, int rate) : IHistoryRecordingOutput
    {
        internal bool Drained { get; private set; }

        public void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token)
        {
            var fadeFrames = rate * 15 / 1000;
            var silentFrames = rate / 10;
            var buffer = new byte[(fadeFrames + silentFrames) * 4 + 4];
            Assert.AreEqual(4, source.Read(buffer, 0, 4));
            stop.Cancel();
            Assert.IsFalse(token.IsCancellationRequested);
            var read = source.Read(buffer, 4, buffer.Length - 4);
            Assert.AreEqual((fadeFrames + silentFrames) * 4, read);
            var samples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(buffer.AsSpan(4, read));
            Assert.AreEqual((short)16000, samples[0]);
            Assert.AreEqual((short)-12000, samples[1]);
            Assert.IsTrue(samples[fadeFrames] is > 0 and < 16000);
            Assert.IsTrue(samples[fadeFrames + 1] is > -12000 and < 0);
            Assert.IsFalse(samples.Slice((fadeFrames - 1) * 2).ContainsAnyExcept((short)0));
            Assert.AreEqual(0, source.Read(buffer, 0, 4));
            Drained = true;
        }
    }

    [TestMethod]
    public async Task WindowsRingtonesAreDiscoveredRepeatedForCallsAndPreviewedOnce()
    {
        var media = Path.Combine(Path.GetTempPath(), "WawiCallPro-ringtones-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(media);
        try
        {
            byte[] samples = [1, 2, 3, 4, 5, 6, 7, 8];
            using (var writer = new WaveWriter(Path.Combine(media, "Ring42.wav"), new WaveFormat(22050, 16, 1)))
                writer.Write(samples, 0, samples.Length);
            File.WriteAllBytes(Path.Combine(media, "Windows RingNew.wav"), [1]);
            File.WriteAllBytes(Path.Combine(media, "Windows Message.wav"), [1]);
            var output = new Probe();
            var player = new WindowsIncomingRingtone(output, media);
            CollectionAssert.AreEqual(new[] { "Ring42.wav", "Windows RingNew.wav" }, player.ReadWindowsRingtones().ToArray());
            var settings = new AudioDeviceSettings { WindowsRingtoneFile = "ring42.WAV", RingDeviceId = "test-headset", RingVolumePercent = 45 };
            await player.PlayAsync(settings, default);
            Assert.AreEqual("test-headset", output.Settings?.PlaybackDeviceId);
            Assert.AreEqual(45, output.Settings?.PlaybackVolumePercent);
            Assert.AreEqual(22050, output.SampleRate);
            CollectionAssert.AreEqual(samples.Concat(samples).ToArray(), output.Bytes);
            Assert.IsTrue(await player.PreviewAsync(settings, default));
            CollectionAssert.AreEqual(samples, output.Bytes);
            var own = Path.Combine(media, "custom.wav");
            File.Copy(Path.Combine(media, "Ring42.wav"), own);
            var custom = settings with { WindowsRingtoneFile = null, CustomRingtonePath = own };
            Assert.AreEqual(22050, WindowsIncomingRingtone.ReadCustomRingtoneFormat(own, default)?.SampleRate);
            await player.PlayAsync(custom, default);
            CollectionAssert.AreEqual(samples.Concat(samples).ToArray(), output.Bytes);
            File.Delete(own);
            Assert.IsFalse(await player.PreviewAsync(custom, default));
            File.Delete(Path.Combine(media, "Ring42.wav"));
            foreach (var name in new[] { "Ring42.wav", "Windows RingNew.wav", "../Ring42.wav" })
            {
                Assert.IsFalse(await player.PreviewAsync(settings with { WindowsRingtoneFile = name }, default));
                Assert.AreEqual(48000, output.SampleRate);
            }
        }
        finally { Directory.Delete(media, true); }
    }

    [TestMethod]
    [DataRow(RecordingArtifactFormat.Mp3)]
    [DataRow(RecordingArtifactFormat.OggVorbis)]
    [DataRow(RecordingArtifactFormat.Wma)]
    public async Task CompressedRingtonePreviewsOnceLoopsForCallsAndReleasesTheFile(RecordingArtifactFormat format)
    {
        var folder = Directory.CreateTempSubdirectory("WawiCallPro-test-ringtone-").FullName;
        try
        {
            const int sampleRate = 16000;
            var input = Path.Combine(folder, "source.wav");
            var samples = new byte[sampleRate * sizeof(short)];
            for (var sample = 0; sample < sampleRate; sample++)
                System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan(sample * sizeof(short)),
                    (short)(8000 * Math.Sin(2 * Math.PI * 440 * sample / sampleRate)));
            using (var writer = new WaveWriter(input, new WaveFormat(sampleRate, 16, 1)))
                writer.Write(samples, 0, samples.Length);

            using var services = new ServiceCollection().AddAudioConversion().BuildServiceProvider();
            var encoder = services.GetRequiredService<IRecordingFormatConverters>().GetConverter(format);
            var profile = (await encoder.GetSupportedFormatsAsync()).First(option => option.SampleRate == sampleRate && option.Channels == 1);
            var ringtone = Path.Combine(folder, "Klingelton." + format.Extension());
            await encoder.ConvertAsync(new(input, ringtone, profile));
            Assert.IsTrue(ApplicationOptionsValidator.IsLocalRingtonePath(ringtone.ToUpperInvariant()));
            var decodedFormat = WindowsIncomingRingtone.ProbeCustomRingtone(ringtone, default);
            Assert.IsNotNull(decodedFormat);
            Assert.AreEqual(1, decodedFormat.Channels);
            Assert.IsGreaterThan(0, decodedFormat.SampleRate);

            var output = new Probe { BufferSize = 128 * 1024 };
            var player = new WindowsIncomingRingtone(output, null);
            var settings = new AudioDeviceSettings { CustomRingtonePath = ringtone, RingDeviceId = "headset", RingVolumePercent = 42 };
            Assert.IsTrue(await player.PreviewAsync(settings, default));
            Assert.AreEqual(decodedFormat.SampleRate, output.SampleRate);
            Assert.IsTrue(output.Bytes.Any(value => value != 0));
            Assert.IsLessThan(output.BufferSize, output.Bytes.Length);
            await player.PlayAsync(settings, default);
            Assert.AreEqual(decodedFormat.SampleRate, output.SampleRate);
            Assert.AreEqual(output.BufferSize, output.Bytes.Length);
            Assert.AreEqual("headset", output.Settings?.PlaybackDeviceId);
            Assert.AreEqual(42, output.Settings?.PlaybackVolumePercent);
            using (File.Open(ringtone, FileMode.Open, FileAccess.Read, FileShare.None)) { }

            File.WriteAllBytes(ringtone, [1, 2, 3, 4]);
            Assert.IsNull(WindowsIncomingRingtone.ProbeCustomRingtone(ringtone, default));
            Assert.IsFalse(await player.PreviewAsync(settings, default));
            Assert.AreEqual(48000, output.SampleRate);
            using (File.Open(ringtone, FileMode.Open, FileAccess.Read, FileShare.None)) { }
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task InstalledWindowsRingtonePlaysOnRequestedNativeOutput()
    {
        var deviceName = Environment.GetEnvironmentVariable("WCP_TEST_RINGTONE_DEVICE");
        if (string.IsNullOrWhiteSpace(deviceName)) Assert.Inconclusive("Set WCP_TEST_RINGTONE_DEVICE to opt in to playback.");
        using var services = new ServiceCollection().AddAudioConversion().BuildServiceProvider();
        var audio = services.GetRequiredService<WindowsIncomingRingtone>();
        var devices = await new WindowsAudioDeviceCatalog(new StartupExecutionMode(false)).ReadAsync();
        var device = devices.Playback.Single(item => item.DisplayName == deviceName);
        var files = audio.ReadWindowsRingtones();
        Assert.IsNotEmpty(files);
        var selected = files[0];
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Assert.IsTrue(await audio.PreviewAsync(new() { WindowsRingtoneFile = selected, RingDeviceId = device.Id, RingVolumePercent = 50 }, stop.Token));
        Console.WriteLine($"Windows {Environment.OSVersion.Version}: {files.Count} Klingeltöne, {selected} auf {device.DisplayName} vorgehört.");
    }

    [TestMethod]
    public async Task MutedNativeRingtoneStopsAndCanReopenOutput()
    {
        if (Environment.GetEnvironmentVariable("WCP_VERIFY_RECORDING_OUTPUT_DEVICE") != "1")
            Assert.Inconclusive("Native WASAPI output is opt-in.");

        using var services = new ServiceCollection().AddAudioConversion().BuildServiceProvider();
        var output = services.GetRequiredService<IHistoryRecordingOutput>();
        var ringtone = new WindowsIncomingRingtone(output);
        for (var run = 0; run < 2; run++)
        {
            using var stop = new CancellationTokenSource();
            var playback = Task.Run(() => ringtone.PlayAsync(null, 0, stop.Token));
            var elapsed = Task.Delay(TimeSpan.FromMilliseconds(600));
            try
            {
                if (await Task.WhenAny(playback, elapsed) == playback)
                {
                    await playback;
                    Assert.Fail("WASAPI stopped before cancellation.");
                }
            }
            finally { await stop.CancelAsync(); }
            await Assert.ThrowsAsync<OperationCanceledException>(() => playback.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    private sealed class CadenceProbe : IHistoryRecordingOutput
    {
        internal bool Played { get; private set; }

        public void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token)
        {
            Assert.AreEqual("ring-output", settings.PlaybackDeviceId);
            Assert.AreEqual(35, settings.PlaybackVolumePercent);
            Assert.AreEqual(1, source.WaveFormat.Channels);
            Assert.AreEqual(16, source.WaveFormat.BitsPerSample);
            var second = source.WaveFormat.SampleRate * source.WaveFormat.BlockAlign;
            var samples = new byte[second * 4];
            Assert.AreEqual(samples.Length, source.Read(samples, 0, samples.Length));
            Assert.IsTrue(samples[..second].Any(value => value != 0));
            Assert.IsTrue(samples[second..(second * 3)].All(value => value == 0));
            CollectionAssert.AreEqual(samples[..second], samples[(second * 3)..]);
            Played = true;
        }
    }

    private sealed class Probe : IHistoryRecordingOutput
    {
        internal int BufferSize { get; init; } = 16;
        internal AudioDeviceSettings? Settings { get; private set; }
        internal byte[] Bytes { get; private set; } = [];
        internal int SampleRate { get; private set; }

        public void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token)
        {
            Settings = settings;
            SampleRate = source.WaveFormat.SampleRate;
            var buffer = new byte[BufferSize];
            Bytes = buffer[..source.Read(buffer, 0, buffer.Length)];
        }
    }
}
