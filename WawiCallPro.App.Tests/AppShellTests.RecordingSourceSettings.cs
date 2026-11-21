using DevExpress.Utils;
using DevExpress.XtraEditors;
using WawiCallPro.App.Views;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public partial class AppShellTests
{
    [TestMethod]
    public Task RecordingSourceSettingSavesReloadsAndRendersWithoutOpeningCaptureDevices() => WithWorkspace(async (workspace, _, _) =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            foreach (var (size, skin) in new[] { (10F, "Basic"), (16F, "Office 2019 Black") })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var settings = new FrmSettings(workspace); settings.PrepareForBackgroundRun();
                settings.Font = font; settings.ConfigureAudioDevices(new AudioCatalogDouble());
                settings.MinimumSize = Size.Empty; settings.ClientSize = new Size(760, 780);
                settings.Show();
                var page = Field<SettingsPage>(settings, "settingsPage"); await page.ReloadAsync();
                settings.SelectAutomationTab("settings-audio");
                var editor = Field<ComboBoxEdit>(page, "recordingSourceEdit");
                editor.SelectedIndex = 1;
                Assert.AreEqual(RecordingReceiveSource.PlaybackLoopback, page.ReadOptions().Devices.RecordingReceiveSource);
                await page.SaveAsync(CancellationToken.None);
                await page.ReloadAsync();
                Assert.AreEqual(1, editor.SelectedIndex);
                Assert.AreEqual(RecordingReceiveSource.PlaybackLoopback, (await workspace.Settings.LoadAsync()).Options.Devices.RecordingReceiveSource);
                settings.PerformLayout(); Application.DoEvents(); CheckTableBounds(settings);
                var output = Path.Combine(RenderOutput(), "recording-source"); Directory.CreateDirectory(output);
                using (var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(settings))
                    bitmap.Save(Path.Combine(output, $"Loopback-{skin}-{size}.png"), System.Drawing.Imaging.ImageFormat.Png);
                settings.Scale(new SizeF(1.5F, 1.5F)); settings.PerformLayout(); Application.DoEvents(); CheckTableBounds(settings);
                Assert.AreEqual(1, editor.SelectedIndex);
                using (var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(settings))
                    bitmap.Save(Path.Combine(output, $"Loopback-{skin}-{size}-scaled.png"), System.Drawing.Imaging.ImageFormat.Png);
                await settings.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });
}
