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
    public Task AudioFileConversionWritesALawWaveAndDecodesItsPreview() => WithWorkspace(async (workspace, services, _) =>
    {
        var folder = workspace.Paths.RootDirectory;
        Directory.CreateDirectory(folder);
        var source = Path.Combine(folder, "source.wav");
        var pcm = Path.Combine(folder, "pcm.wav");
        var alaw = Path.Combine(folder, "alaw.wav");
        WriteAudioSource(source, new WaveFormat(8000, 16, 2), 8001);
        var nativeOutput = Environment.GetEnvironmentVariable("WCP_VERIFY_RECORDING_OUTPUT_DEVICE") == "1"
            ? services.GetRequiredService<IHistoryRecordingOutput>() : null;
        var playback = new AudioPreviewProbe(nativeOutput);
        var files = new AudioFileConversion(playback, workspace.Settings);
        await files.ConvertAsync(source, pcm, new(null, null, 100), null, default);
        await files.ConvertAsync(source, alaw, new(16000, 2, 50, AudioWaveEncoding.ALaw), null, default);
        await files.PreviewAsync(pcm, default);
        await files.PreviewAsync(alaw, default);
        CollectionAssert.AreEqual(new[] { (8000, 2), (8000, 1) }, playback.Formats.ToArray());
        Assert.AreEqual(12000d, playback.FirstSamples[0], 1);
        Assert.AreEqual(6000d, playback.FirstSamples[1], 256);

        using var reader = new BinaryReader(File.OpenRead(alaw));
        Assert.AreEqual("RIFF", new string(reader.ReadChars(4)));
        Assert.AreEqual(reader.BaseStream.Length - 8, (long)reader.ReadInt32());
        Assert.AreEqual("WAVEfmt ", new string(reader.ReadChars(8)));
        Assert.AreEqual(18, reader.ReadInt32());
        Assert.AreEqual((ushort)6, reader.ReadUInt16());
        Assert.AreEqual((ushort)1, reader.ReadUInt16());
        Assert.AreEqual(8000, reader.ReadInt32());
        Assert.AreEqual(8000, reader.ReadInt32());
        Assert.AreEqual((ushort)1, reader.ReadUInt16());
        Assert.AreEqual((ushort)8, reader.ReadUInt16());
        Assert.AreEqual((ushort)0, reader.ReadUInt16());
        Assert.AreEqual("fact", new string(reader.ReadChars(4)));
        Assert.AreEqual(4, reader.ReadInt32());
        Assert.AreEqual(8001, reader.ReadInt32());
        Assert.AreEqual("data", new string(reader.ReadChars(4)));
        Assert.AreEqual(8001, reader.ReadInt32());
        reader.BaseStream.Position += 8001;
        Assert.AreEqual((byte)0, reader.ReadByte());
        Assert.AreEqual(reader.BaseStream.Length, reader.BaseStream.Position);
    }, configure: services => services.AddAudioConversion());

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
        var alaw = Path.Combine(folder, "alaw.wav");
        WriteAudioSource(wave, new WaveFormat(16000, 16, 1), 32000);
        var encoder = services.GetRequiredService<IRecordingFormatConverters>().GetConverter(format);
        var profile = (await encoder.GetSupportedFormatsAsync()).First(choice => choice.SampleRate == 16000 && choice.Channels == 1);
        await encoder.ConvertAsync(new(wave, compressed, profile));
        var output = new AudioPreviewProbe();
        var converter = new AudioFileConversion(output, workspace.Settings);
        await converter.PreviewAsync(compressed, default);
        await converter.ConvertAsync(compressed, result, new(16000, 2, 80), null, default);
        await converter.PreviewAsync(result, default);
        await converter.ConvertAsync(compressed, alaw, new(null, null, 80, AudioWaveEncoding.ALaw), null, default);
        await converter.PreviewAsync(alaw, default);
        Assert.HasCount(3, output.Formats);
        Assert.AreEqual(1, output.Formats[0].Channels);
        CollectionAssert.AreEqual(new[] { (16000, 2), (8000, 1) }, output.Formats.Skip(1).ToArray());
        using var audio = File.OpenRead(result);
        var header = PcmWaveHeader.Read(audio, default);
        Assert.AreEqual(16000, header.SampleRate);
        Assert.AreEqual(2, header.Channels);
        Assert.IsTrue(header.DataLength > 32000);
    }, configure: services => services.AddAudioConversion());

    [TestMethod]
    public Task AudioFileVorbisPreservesFormatsAcrossSuccessiveEncodes() => WithWorkspace(async (workspace, services, _) =>
    {
        var encoder = services.GetRequiredService<IRecordingFormatConverters>().GetConverter(RecordingArtifactFormat.OggVorbis);
        var formats = await encoder.GetSupportedFormatsAsync();
        var folder = workspace.Paths.RootDirectory;
        Directory.CreateDirectory(folder);
        var sequence = new[] { (16000, 2), (48000, 2), (8000, 2), (16000, 1), (8000, 2) };
        var output = new AudioPreviewProbe();
        var preview = new AudioFileConversion(output, workspace.Settings);
        for (var index = 0; index < sequence.Length; index++)
        {
            var (rate, channels) = sequence[index];
            var source = Path.Combine(folder, $"vorbis-{index}.wav");
            var result = Path.Combine(folder, $"vorbis-{index}.ogg");
            WriteAudioSource(source, new WaveFormat(rate, 16, channels), rate * channels * 2);
            var profile = formats.Single(item => item.SampleRate == rate && item.Channels == channels);
            await encoder.ConvertAsync(new(source, result, profile));
            await preview.PreviewAsync(result, default);
        }
        CollectionAssert.AreEqual(sequence, output.Formats.ToArray());
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
        var format = Field<ComboBoxEdit>(form, "formatEdit");
        var rate = Field<ComboBoxEdit>(form, "rateEdit");
        var channels = Field<ComboBoxEdit>(form, "channelEdit");
        rate.EditValue = "16000";
        channels.EditValue = "Stereo";
        format.SelectedIndex = 1;
        Assert.IsFalse(result.Enabled);
        Assert.IsFalse(rate.Enabled);
        Assert.IsFalse(channels.Enabled);
        Assert.AreEqual("8000", rate.Text);
        Assert.AreEqual("Mono", channels.Text);
        Field<TextEdit>(form, "targetEdit").Text = "ALaw.wav";
        await form.ConvertAsync();
        Assert.IsTrue(result.Enabled);
        Assert.AreEqual(new AudioWaveOptions(8000, 1, 100, AudioWaveEncoding.ALaw), service.LastOptions);
        await form.PreviewAsync(result: true);
        CollectionAssert.AreEqual(new[] { "Quelle.wav", "Ergebnis.wav", "ALaw.wav" }, service.PreviewPaths.ToArray());
        format.SelectedIndex = 0;
        Assert.IsTrue(rate.Enabled);
        Assert.IsTrue(channels.Enabled);
        Assert.AreEqual("16000", rate.Text);
        Assert.AreEqual("Stereo", channels.Text);
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
                capture.ClientSize = new(width, 365);
                Field<TextEdit>(capture, "sourceEdit").Text = Field<TextEdit>(form, "sourceEdit").Text;
                Field<TextEdit>(capture, "targetEdit").Text = Field<TextEdit>(form, "targetEdit").Text;
                if (theme == "Office 2019 Black") Field<ComboBoxEdit>(capture, "formatEdit").SelectedIndex = 1;
                Field<LabelControl>(capture, "status").Text = Field<LabelControl>(form, "status").Text;
                capture.Show();
                if (scale != 1F) capture.Scale(new SizeF(scale, scale));
                capture.PerformLayout();
                Application.DoEvents();
                CheckTableBounds(capture);
                var path = Field<TextEdit>(capture, "sourceEdit");
                foreach (var name in new[] { "targetEdit", "formatEdit", "rateEdit", "channelEdit", "volumeEdit" })
                {
                    var editor = Field<BaseEdit>(capture, name);
                    Assert.AreSame(path.Parent, editor.Parent);
                    Assert.AreEqual(path.Left, editor.Left, name);
                }
                var table = (TableLayoutPanel)path.Parent!;
                var sourceActions = new[] { Field<SimpleButton>(capture, "sourcePlay"), Field<SimpleButton>(capture, "sourceBrowse") };
                var targetActions = new[] { Field<SimpleButton>(capture, "resultPlay"), Field<SimpleButton>(capture, "targetBrowse") };
                Assert.HasCount(1, sourceActions.Concat(targetActions).Select(button => button.Width).Distinct());
                Assert.AreEqual(sourceActions[0].Left, targetActions[0].Left);
                Assert.AreEqual(sourceActions[1].Left, targetActions[1].Left);
                Assert.IsTrue(sourceActions[0].Right < sourceActions[1].Left);
                Assert.IsTrue(table.GetRow(Field<LabelControl>(capture, "sourceLabel")) < table.GetRow(path));
                Assert.IsTrue(table.GetRow(path) < table.GetRow(Field<LabelControl>(capture, "targetLabel")));
                Assert.IsTrue(table.GetRow(Field<LabelControl>(capture, "targetLabel")) < table.GetRow(Field<TextEdit>(capture, "targetEdit")));
                Assert.IsTrue(Field<SpinEdit>(capture, "volumeEdit").Width < path.Width);
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

    private sealed class AudioPreviewProbe(IHistoryRecordingOutput? device = null) : IHistoryRecordingOutput
    {
        internal readonly List<(int Rate, int Channels)> Formats = [];
        internal readonly List<short> FirstSamples = [];
        public void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token)
        {
            Formats.Add((source.WaveFormat.SampleRate, source.WaveFormat.Channels));
            var bytes = new byte[64];
            Assert.IsTrue(source.Read(bytes, 0, bytes.Length) > 0);
            if (source.WaveFormat.BitsPerSample == 16)
                FirstSamples.Add(System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes));
            if (device is not null)
            {
                source.Position = 0;
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                device.Play(source, settings with { PlaybackVolumePercent = 0 }, deadline.Token);
            }
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
        internal AudioWaveOptions? LastOptions { get; private set; }
        public Task ConvertAsync(string source, string destination, AudioWaveOptions options, IProgress<int>? progress, CancellationToken token)
        {
            LastOptions = options;
            return Block ? Task.Delay(Timeout.Infinite, token) : Task.CompletedTask;
        }
        public Task PreviewAsync(string path, CancellationToken token)
        {
            PreviewPaths.Add(path);
            return Task.CompletedTask;
        }
    }
}
