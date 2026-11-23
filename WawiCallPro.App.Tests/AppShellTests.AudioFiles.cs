using CSCore;
using CSCore.Codecs.WAV;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Ribbon;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Core.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AudioFileConversionAppliesRateChannelsAndGainAndPreviewsBothFiles() => WithWorkspace(async (workspace, _, _) =>
    {
        var folder = workspace.Paths.RootDirectory;
        Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "source.wav");
        var result = Path.Combine(folder, "result.wav");
        WriteAudioSource(source, new WaveFormat(16000, 16, 2), 32000);
        var playback = new AudioPreviewProbe();
        var files = new AudioFileConversion(playback, workspace.Settings);
        await files.PreviewAsync(source, default);
        await files.ConvertAsync(source, result, new(8000, 1, 50), null, default);
        await files.PreviewAsync(result, default);
        CollectionAssert.AreEqual(new[] { (16000, 2), (8000, 1) }, playback.Formats.ToArray());
        using var wave = File.OpenRead(result);
        var header = PcmWaveHeader.Read(wave, default);
        Assert.AreEqual(8000, header.SampleRate);
        Assert.AreEqual(1, header.Channels);
        Assert.IsTrue(header.DataLength is > 31900 and < 32100);
        wave.Position = header.DataOffset + 4000;
        using var reader = new BinaryReader(wave);
        Assert.AreEqual(6000d, reader.ReadInt16(), 150);
    });

    [TestMethod]
    public Task AudioFileConversionCancellationAndDuplicateDestinationProtectOriginals() => WithWorkspace(async (workspace, _, _) =>
    {
        var folder = workspace.Paths.RootDirectory;
        Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "source.wav");
        var destination = Path.Combine(folder, "result.wav");
        WriteAudioSource(source, new WaveFormat(16000, 16, 1), 128000);
        var original = await File.ReadAllBytesAsync(source);
        var files = new AudioFileConversion(new AudioPreviewProbe(), workspace.Settings);
        using var cancelled = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => files.ConvertAsync(source, destination, new(16000, 2, 100),
            new InlineAudioProgress(_ => cancelled.Cancel()), cancelled.Token));
        Assert.IsFalse(File.Exists(destination));
        Assert.IsEmpty(Directory.GetFiles(folder, ".wawi-audio-*.tmp"));
        await File.WriteAllBytesAsync(destination, [1, 2, 3]);
        await Assert.ThrowsExactlyAsync<AudioFileConversionException>(() => files.ConvertAsync(source, destination, new(16000, 2, 100), null, default));
        await Assert.ThrowsExactlyAsync<AudioFileConversionException>(() => files.ConvertAsync(source, source, new(16000, 2, 100), null, default));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(destination));
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(source));
    });

    [TestMethod]
    [DataRow(RecordingArtifactFormat.Mp3)]
    [DataRow(RecordingArtifactFormat.OggVorbis)]
    [DataRow(RecordingArtifactFormat.Wma)]
    public Task AudioFileConversionDecodesCompressedSources(RecordingArtifactFormat format) => WithWorkspace(async (workspace, services, _) =>
    {
        var folder = workspace.Paths.RootDirectory;
        Directory.CreateDirectory(folder);
        var wave = Path.Combine(folder, "source.wav");
        var compressed = Path.Combine(folder, "source." + format.Extension());
        var result = Path.Combine(folder, "result.wav");
        WriteAudioSource(wave, new WaveFormat(16000, 16, 1), 32000);
        var encoder = services.GetRequiredService<IRecordingFormatConverters>().GetConverter(format);
        var profile = (await encoder.GetSupportedFormatsAsync()).First(choice => choice.SampleRate == 16000 && choice.Channels == 1);
        await encoder.ConvertAsync(new(wave, compressed, profile));
        var output = new AudioPreviewProbe();
        var converter = new AudioFileConversion(output, workspace.Settings);
        await converter.PreviewAsync(compressed, default);
        await converter.ConvertAsync(compressed, result, new(16000, 2, 80), null, default);
        using var audio = File.OpenRead(result);
        var header = PcmWaveHeader.Read(audio, default);
        Assert.AreEqual(16000, header.SampleRate);
        Assert.AreEqual(2, header.Channels);
        Assert.IsTrue(header.DataLength > 32000);
    }, configure: services => services.AddAudioConversion());

    [TestMethod]
    public Task AudioFileDialogShowsRibbonEntryAndCancelsWithoutEnablingResultPreview() => WithWorkspace(async (workspace, services, _) =>
    {
        using var main = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        main.PrepareForBackgroundRun();
        var page = Field<RibbonPage>(main, "audioRibbon");
        Assert.AreEqual("Audio", page.Text);
        var link = page.Groups[0].ItemLinks[0];
        Assert.AreEqual("Audiodatei konvertieren", link.Item.Caption);
        Assert.IsNotNull(((BarButtonItem)link.Item).ImageOptions.SvgImage);
        using var form = main.CreateAudioConvertDialog();
        form.Show();
        var convert = Field<SimpleButton>(form, "convertButton");
        var result = Field<SimpleButton>(form, "resultPlay");
        Assert.IsFalse(convert.Enabled);
        Assert.IsFalse(result.Enabled);
        Field<TextEdit>(form, "sourceEdit").Text = "Quelle.wav";
        Field<TextEdit>(form, "targetEdit").Text = "Ergebnis.wav";
        Assert.IsTrue(convert.Enabled);
        var operation = form.ConvertAsync();
        Assert.IsFalse(convert.Enabled);
        Field<SimpleButton>(form, "cancelButton").PerformClick();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("Vorgang abgebrochen.", Field<LabelControl>(form, "status").Text);
        Assert.IsFalse(result.Enabled);
        Assert.IsTrue(convert.Enabled);
        CaptureAudioFileDialog(form, workspace.Application.ProductLine);
        var service = (BlockingAudioFileConversion)services.GetRequiredService<IAudioFileConversion>();
        service.Block = false;
        await form.ConvertAsync();
        Assert.IsTrue(result.Enabled);
        await form.PreviewAsync(result: false);
        await form.PreviewAsync(result: true);
        CollectionAssert.AreEqual(new[] { "Quelle.wav", "Ergebnis.wav" }, service.PreviewPaths.ToArray());
    }, configure: services => services.AddSingleton<IAudioFileConversion, BlockingAudioFileConversion>());

    private static void CaptureAudioFileDialog(FrmAudioConvert form, string edition)
    {
        var skin = DevExpress.LookAndFeel.UserLookAndFeel.Default;
        var previous = skin.ActiveSkinName;
        var palette = skin.ActiveSvgPaletteName;
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var folder = Path.Combine(RenderOutput(), "audio-file-conversion");
        Directory.CreateDirectory(folder);
        try
        {
            foreach (var (theme, size, width, scale) in new[] { ("Basic", 10F, 650, 1F), ("Office 2019 Black", 16F, 740, 1F), ("Basic", 10F, 710, 1.5F) })
            {
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                skin.SetSkinStyle(theme);
                using var capture = new FrmAudioConvert(new BlockingAudioFileConversion(), null);
                capture.PrepareForBackgroundRun();
                capture.Font = font;
                capture.ClientSize = new(width, 320);
                Field<TextEdit>(capture, "sourceEdit").Text = Field<TextEdit>(form, "sourceEdit").Text;
                Field<TextEdit>(capture, "targetEdit").Text = Field<TextEdit>(form, "targetEdit").Text;
                Field<LabelControl>(capture, "status").Text = Field<LabelControl>(form, "status").Text;
                capture.Show();
                if (scale != 1F) capture.Scale(new SizeF(scale, scale));
                capture.PerformLayout();
                Application.DoEvents();
                CheckTableBounds(capture);
                Assert.IsTrue(capture.Width < 1200, "Minimum width must not grow to the entire monitor.");
                using var image = WawiCallPro.App.Diagnostics.UiAudit.Capture(capture);
                image.Save(Path.Combine(folder, $"{edition}-{size:0}-{scale:0.0}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = originalFont;
            AppearanceObject.DefaultFont = originalAppearance;
            if (string.IsNullOrEmpty(palette)) skin.SetSkinStyle(previous); else skin.SetSkinStyle(previous, palette);
        }
    }

    private static void WriteAudioSource(string path, WaveFormat format, int frames)
    {
        using var file = new WaveWriter(path, format);
        var bytes = new byte[checked(frames * format.BlockAlign)];
        for (var index = 0; index < bytes.Length; index += 2)
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index, 2), 12000);
        file.Write(bytes, 0, bytes.Length);
    }

    private sealed class AudioPreviewProbe : IHistoryRecordingOutput
    {
        internal readonly List<(int Rate, int Channels)> Formats = [];
        public void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token)
        {
            Formats.Add((source.WaveFormat.SampleRate, source.WaveFormat.Channels));
            Assert.IsTrue(source.Read(new byte[64], 0, 64) > 0);
        }
    }

    private sealed class InlineAudioProgress(Action<int> update) : IProgress<int>
    {
        public void Report(int value) => update(value);
    }

    private sealed class BlockingAudioFileConversion : IAudioFileConversion
    {
        internal bool Block { get; set; } = true;
        internal List<string> PreviewPaths { get; } = [];
        public Task ConvertAsync(string source, string destination, AudioWaveOptions options, IProgress<int>? progress, CancellationToken token) =>
            Block ? Task.Delay(Timeout.Infinite, token) : Task.CompletedTask;
        public Task PreviewAsync(string path, CancellationToken token)
        {
            PreviewPaths.Add(path);
            return Task.CompletedTask;
        }
    }
}
