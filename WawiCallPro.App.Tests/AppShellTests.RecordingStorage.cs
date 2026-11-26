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
                var folder = Field<TextEdit>(page, "recordingFolderEdit");
                var pattern = Field<TextEdit>(page, "recordingPatternEdit");
                var preview = Field<LabelControl>(page, "recordingPreviewValue");
                var destination = Path.Combine(workspace.Paths.RootDirectory, "Aufnahmen");
                folder.Text = destination;
                var choices = Field<ComboBoxEdit>(page, "recordingPatternChoices");
                choices.SelectedIndex = 1;
                Assert.AreEqual("{Mandant}_{Datum}_{Timestamp}", pattern.Text);
                Assert.Contains("Musterfirma_2026-11-25_10-44-16-000.wav", preview.Text);
                choices.SelectedIndex = 3;
                Assert.Contains("{Rufnummer}", pattern.Text);
                pattern.Text = "{Datum}_{Rufnummer}";
                Field<ComboBoxEdit>(page, "recordingFormatEdit").SelectedIndex = 1;
                Assert.StartsWith(destination, preview.Text);
                Assert.Contains("03025776817", preview.Text);
                Assert.Contains(".wav", preview.Text);
                Assert.EndsWith(".mp3", preview.Text);
                settings.PerformLayout();
                Application.DoEvents();
                CheckTableBounds(settings);
                using var shot = UiAudit.Capture(settings);
                shot.Save(Path.Combine(output, $"recording-{width}-{fontSize:0}-{scale:0.0}.png"), ImageFormat.Png);

                await page.SaveAsync(CancellationToken.None);
                var saved = (await workspace.Settings.LoadAsync()).Options.RecordingStorage;
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
