using CSCore;
using CSCore.Codecs.WAV;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;

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
        internal AudioDeviceSettings? Settings { get; private set; }
        internal byte[] Bytes { get; private set; } = [];
        internal int SampleRate { get; private set; }

        public void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token)
        {
            Settings = settings;
            SampleRate = source.WaveFormat.SampleRate;
            var buffer = new byte[16];
            Bytes = buffer[..source.Read(buffer, 0, buffer.Length)];
        }
    }
}
