using System.Collections.Immutable;
using CSCore.CoreAudioAPI;
using System.Drawing.Imaging;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AudioSelfTestRendersAllStepsAndPersistsOnlyOnApply() => WithWorkspace(async (workspace, _, _) =>
    {
        var look = UserLookAndFeel.Default;
        var originalSkin = look.ActiveSkinName;
        var originalPalette = look.ActiveSvgPaletteName;
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var path = Path.Combine(RenderOutput(), "audio-selftest");
        Directory.CreateDirectory(path);
        try
        {
            look.SetSkinStyle("Basic", "Blue Dark");
            using var settings = new FrmSettings(workspace);
            settings.PrepareForBackgroundRun();
            settings.SelectAutomationTab("settings-audio");
            settings.ConfigureAudioDevices(new AudioSelfTestDeviceCatalog());
            await settings.PrepareAsync();
            settings.Show();
            var page = Field<SettingsPage>(settings, "settingsPage");
            var initial = (await workspace.Settings.LoadAsync()).Options;
            var draft = initial.Devices with { CaptureDeviceId = "self-test-mic", PlaybackDeviceId = "self-test-headset" };
            foreach (var (width, fontSize, scale, apply) in new[] { (760, 10F, 1F, false), (560, 15F, 1F, false), (800, 10F, 1.5F, true) })
            {
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                var audio = new AudioSelfTestProbe();
                using var wizard = new FrmAudioSelfTest(audio, draft, "Sony INZONE Buds – Mikrofon", "Sony INZONE Buds – Kopfhörer",
                    page.SaveAudioSelfTestAsync);
                wizard.ShowInTaskbar = false;
                wizard.Opacity = 0;
                wizard.Font = font;
                wizard.Show(settings);
                wizard.ClientSize = new(width, 510);
                if (scale != 1F) wizard.Scale(new SizeF(scale, scale));
                Field<SpinEdit>(wizard, "playbackBufferEdit").Value = 110;
                var tone = Field<SimpleButton>(wizard, "toneButton");
                tone.PerformClick();
                Assert.AreEqual(1, audio.Playbacks);
                Assert.IsTrue(Field<CheckEdit>(wizard, "heardEdit").Enabled);
                Render("headphones");
                Field<SimpleButton>(wizard, "nextButton").PerformClick();
                Field<SpinEdit>(wizard, "captureBufferEdit").Value = 70;
                await wizard.RecordAsync();
                Assert.AreEqual(70, audio.LastCaptureBuffer);
                Field<SimpleButton>(wizard, "sampleButton").PerformClick();
                Assert.AreEqual(2, audio.Playbacks);
                Render("microphone");
                Field<SimpleButton>(wizard, "nextButton").PerformClick();
                await wizard.CheckCodecsAsync();
                Assert.Contains("G.722", Field<LabelControl>(wizard, "codecDetails").Text);
                Render("codecs");
                Assert.AreEqual(initial.Devices, (await workspace.Settings.LoadAsync()).Options.Devices);
                if (apply)
                {
                    Field<TrackBarControl>(page, "ringVolumeSlider").Value = 42;
                    await wizard.ApplyAsync();
                    Assert.AreEqual(DialogResult.OK, wizard.DialogResult);
                }
                else
                {
                    Field<SimpleButton>(wizard, "cancelButton").PerformClick();
                    Assert.AreEqual(DialogResult.Cancel, wizard.DialogResult);
                    Assert.AreEqual(initial.Devices, (await workspace.Settings.LoadAsync()).Options.Devices);
                }

                void Render(string step)
                {
                    wizard.PerformLayout();
                    Application.DoEvents();
                    CheckTableBounds(wizard);
                    var activePage = Field<Panel>(wizard, "pages").Controls.Cast<Control>().Single(control => control.Visible);
                    Assert.IsLessThanOrEqualTo(Field<Panel>(wizard, "pages").ClientSize.Height, activePage.Height);
                    using var image = UiAudit.Capture(wizard);
                    image.Save(Path.Combine(path, $"{step}-{width}.png"), ImageFormat.Png);
                }
            }
            var saved = (await workspace.Settings.LoadAsync()).Options;
            Assert.AreEqual(initial.Devices with
            {
                CaptureDeviceId = "self-test-mic", PlaybackDeviceId = "self-test-headset",
                CaptureBufferMilliseconds = 70, PlaybackBufferMilliseconds = 110
            }, saved.Devices);
            Assert.AreEqual(42, Field<TrackBarControl>(page, "ringVolumeSlider").Value);
            var button = Field<SimpleButton>(page, "audioSelfTestButton");
            Assert.IsTrue(button.Visible && button.Enabled);
            using var entry = UiAudit.Capture(settings);
            entry.Save(Path.Combine(path, "settings-devices.png"), ImageFormat.Png);
            await settings.StopAsync();
        }
        finally
        {
            look.SetSkinStyle(originalSkin, originalPalette);
            WindowsFormsSettings.DefaultFont = originalFont;
            AppearanceObject.DefaultFont = originalAppearance;
        }
    }, configure: services => services.AddSingleton<IAudioSelfTest, AudioSelfTestProbe>());

    [TestMethod]
    public async Task AudioSelfTestProcessesAllCodecsWithTwentyMillisecondBlocks()
    {
        const int rate = 48000;
        var samples = Enumerable.Range(0, rate).Select(i => (short)(7000 * Math.Sin(i * (2 * Math.PI * 550 / rate)))).ToArray();
        var service = new WindowsAudioSelfTest(TimeProvider.System);
        var codecs = await service.CheckCodecsAsync(new(samples, rate, 7000, AudioSelfTestProbe.Device), CancellationToken.None);
        Assert.HasCount(3, codecs);
        foreach (var codec in codecs)
        {
            Assert.IsTrue(codec.Passed, codec.Name);
            Assert.AreEqual(50, codec.Packets, codec.Name);
        }
    }

    [TestMethod]
    public async Task AudioSelfTestMeasuresNativeInputAndOutputBuffers()
    {
        if (Environment.GetEnvironmentVariable("WCP_VERIFY_AUDIO_SELFTEST_DEVICE") != "1")
            Assert.Inconclusive("Requires a Windows microphone and playback endpoint.");
        const int requestedInput = 70;
        const int requestedOutput = 110;
        var service = new WindowsAudioSelfTest(TimeProvider.System);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(18));
        var options = new AudioDeviceSettings
        {
            CaptureDeviceId = FindNativeEndpoint(DataFlow.Capture),
            PlaybackDeviceId = FindNativeEndpoint(DataFlow.Render),
            CaptureBufferMilliseconds = requestedInput,
            PlaybackBufferMilliseconds = requestedOutput
        };
        var sample = await service.RecordAsync(options, new Progress<ImmutableArray<int>>(), timeout.Token);
        var output = await service.PlayAsync(options, null, new(0), new Progress<AudioSelfTestDevice>(), timeout.Token);
        await service.PlayAsync(options, sample, new(0), new Progress<AudioSelfTestDevice>(), timeout.Token);
        var codecs = await service.CheckCodecsAsync(sample, timeout.Token);
        Assert.IsGreaterThan(sample.SampleRate * 2, sample.Samples.Length);
        Assert.IsTrue(codecs.All(codec => codec.Passed));
        Assert.IsGreaterThanOrEqualTo((double)requestedInput, sample.Device.BufferMs);
        Assert.IsGreaterThanOrEqualTo((double)requestedOutput, output.BufferMs);
        Console.WriteLine($"{sample.Device.Name}: {sample.Device.BufferMs:0.#} ms; {output.Name}: {output.BufferMs:0.#} ms.");
    }

    private static string? FindNativeEndpoint(DataFlow flow)
    {
        var name = Environment.GetEnvironmentVariable("WCP_AUDIO_SELFTEST_ENDPOINT");
        if (string.IsNullOrEmpty(name)) return null;
        using var enumerator = new MMDeviceEnumerator();
        using var endpoints = enumerator.EnumAudioEndpoints(flow, DeviceState.Active);
        foreach (var endpoint in endpoints)
        {
            using (endpoint)
                if (endpoint.FriendlyName.Contains(name, StringComparison.OrdinalIgnoreCase)) return endpoint.DeviceID;
        }
        Assert.Fail($"No active {flow} endpoint matching '{name}'.");
        return null;
    }

    private sealed class AudioSelfTestDeviceCatalog : IAudioDeviceCatalog
    {
        public Task<AudioDeviceCatalog> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AudioDeviceCatalog(
            [new("self-test-mic", "Sony INZONE Buds – Mikrofon")],
            [new("self-test-headset", "Sony INZONE Buds – Kopfhörer")]));
    }

    private sealed class AudioSelfTestProbe : IAudioSelfTest
    {
        internal static readonly AudioSelfTestDevice Device = new("Sony INZONE Buds", 48000, 1, 32, 70, 32, false);
        internal int Playbacks { get; private set; }
        internal int LastCaptureBuffer { get; private set; }
        public Task<AudioSelfTestDevice> PlayAsync(AudioDeviceSettings settings, AudioSelfTestSample? sample, AudioPlaybackVolume volume,
            IProgress<AudioSelfTestDevice> opened, CancellationToken token)
        {
            Playbacks++;
            Assert.AreEqual(110, settings.PlaybackBufferMilliseconds);
            Assert.AreEqual(Playbacks > 1, sample is not null);
            var output = Device with { BufferMs = 110, Channels = 2 };
            opened.Report(output);
            return Task.FromResult(output);
        }
        public Task<AudioSelfTestSample> RecordAsync(AudioDeviceSettings settings, IProgress<ImmutableArray<int>> spectrum, CancellationToken token)
        {
            LastCaptureBuffer = settings.CaptureBufferMilliseconds;
            return Task.FromResult(new AudioSelfTestSample([10000, -10000], 48000, 10000, Device));
        }
        public Task<IReadOnlyList<AudioSelfTestCodec>> CheckCodecsAsync(AudioSelfTestSample sample, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<AudioSelfTestCodec>>([new("G.722", 3, true), new("G.711 A-law", 3, true), new("G.711 μ-law", 3, true)]);
    }
}
