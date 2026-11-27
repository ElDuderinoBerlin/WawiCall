using System.Drawing.Imaging;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Views;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task RecordingStoragePreviewAndSavedPendingDirectoryUseTheSamePattern() => WithWorkspace(async (workspace, _, _) =>
    {
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var look = UserLookAndFeel.Default;
        var originalSkin = look.ActiveSkinName;
        var originalPalette = look.ActiveSvgPaletteName;
        var output = Path.Combine(RenderOutput(), "recording-storage");
        Directory.CreateDirectory(output);
        try
        {
            look.SetSkinStyle("Basic", "Blue Dark");
            foreach (var (width, fontSize, scale) in new[] { (980, 10F, 1F), (680, 16F, 1F), (880, 10F, 1.5F) })
            {
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var settings = new FrmSettings(workspace);
                settings.PrepareForBackgroundRun();
                settings.Font = font;
                settings.SelectAutomationTab("settings-audio-levels");
                await settings.PrepareAsync();
                settings.Show();
                settings.MinimumSize = Size.Empty;
                settings.ClientSize = new(width, 760);
                if (scale != 1F) settings.Scale(new SizeF(scale, scale));
                var page = Field<SettingsPage>(settings, "settingsPage");
                var microphoneBuffer = Field<SpinEdit>(page, "captureBufferEdit");
                var outputBuffer = Field<SpinEdit>(page, "playbackBufferEdit");
                microphoneBuffer.Value = 70;
                outputBuffer.Value = 110;
                var folder = Field<TextEdit>(page, "recordingFolderEdit");
                var pattern = Field<TextEdit>(page, "recordingPatternEdit");
                var preview = Field<LabelControl>(page, "recordingPreviewValue");
                var destination = Path.Combine(workspace.Paths.RootDirectory, "Aufnahmen");
                folder.Text = destination;
                var choices = Field<ComboBoxEdit>(page, "recordingPatternChoices");
                var format = Field<ComboBoxEdit>(page, "recordingFormatEdit");
                format.SelectedIndex = 0;
                choices.SelectedIndex = 1;
                Assert.AreEqual("{Mandant}_{Datum}_{Timestamp}", pattern.Text);
                Assert.Contains("Musterfirma_2026-11-25_10-44-16-000.wav", preview.Text);
                choices.SelectedIndex = 3;
                Assert.Contains("{Rufnummer}", pattern.Text);
                pattern.Text = "{Datum}_{Rufnummer}";
                foreach (var (index, extension) in new[] { (0, "wav"), (1, "mp3"), (2, "ogg"), (3, "wma"), (0, "wav") })
                {
                    format.SelectedIndex = index;
                    var expectedPath = Path.Combine(destination, "2026-11-25_03025776817." + extension);
                    Assert.AreEqual(expectedPath, preview.Text);
                    Assert.AreEqual(expectedPath, preview.ToolTip);
                }
                format.SelectedIndex = 1;
                settings.PerformLayout();
                Application.DoEvents();
                CheckTableBounds(settings);
                Assert.IsGreaterThanOrEqualTo(48, microphoneBuffer.Width);
                Assert.IsGreaterThanOrEqualTo(48, outputBuffer.Width);
                using var shot = UiAudit.Capture(settings);
                shot.Save(Path.Combine(output, $"recording-{width}-{fontSize:0}-{scale:0.0}.png"), ImageFormat.Png);

                await page.SaveAsync(CancellationToken.None);
                var savedOptions = (await workspace.Settings.LoadAsync()).Options;
                var saved = savedOptions.RecordingStorage;
                Assert.AreEqual(70, savedOptions.Devices.CaptureBufferMilliseconds);
                Assert.AreEqual(110, savedOptions.Devices.PlaybackBufferMilliseconds);
                Assert.AreEqual(70, workspace.Configuration.ConversationDevices.CaptureBufferMilliseconds);
                Assert.AreEqual(110, workspace.Configuration.ConversationDevices.PlaybackBufferMilliseconds);
                Assert.AreEqual(destination, saved.PendingDirectory);
                Assert.AreEqual("{Datum}_{Rufnummer}", saved.FileNamePattern);
                Assert.AreEqual(workspace.Paths.DefaultRecordingsDirectory, workspace.Paths.RecordingsDirectory);
                await settings.StopAsync();
            }
        }
        finally
        {
            look.SetSkinStyle(originalSkin, originalPalette);
            WindowsFormsSettings.DefaultFont = originalFont;
            AppearanceObject.DefaultFont = originalAppearance;
        }
    });
}
