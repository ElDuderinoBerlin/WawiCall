using System.Drawing.Imaging;
using System.IO;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Views;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow(1240, 10F, 1F, "Office 2019 Colorful")]
    [DataRow(900, 16F, 1F, "Office 2019 Black")]
    [DataRow(1240, 10F, 1.5F, "Office 2019 Colorful")]
    public Task RecordingOutputRoundtripsAndRendersWithoutVisibleWindows(int width, float size, float scale, string skin) => WithWorkspace(async (workspace, services, plugin) =>
    {
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName; var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
            using var font = new Font("Segoe UI", size); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
            using var form = new FrmSettings(workspace); form.PrepareForBackgroundRun(); form.Font = font; form.ClientSize = new(width, 800); form.Show();
            var page = Field<SettingsPage>(form, "settingsPage"); await page.ReloadAsync();
            page.SettingsTabs.SelectedTabPage = Field<XtraTabPage>(page, "audioTab"); Field<XtraTabControl>(page, "audioPages").SelectedTabPage = Field<XtraTabPage>(page, "audioLevelsTab");
            var automatic = Field<CheckEdit>(page, "automaticRecordingEdit");
            Assert.IsFalse(automatic.Checked);
            automatic.Checked = true;
            await page.SaveAsync(CancellationToken.None);
            await page.ReloadAsync();
            Assert.IsTrue(automatic.Checked);
            Assert.IsTrue((await workspace.Settings.LoadAsync()).Options.Devices.RecordConnectedCallsAutomatically);
            var option = Field<ComboBoxEdit>(page, "recordingFormatEdit");
            CollectionAssert.AreEqual(new[] { "Nur WAV", "MP3", "Ogg/Vorbis", "WMA" }, option.Properties.Items.Cast<string>().ToArray());
            Assert.AreEqual(0, option.SelectedIndex);
            var snapshot = await workspace.Settings.LoadAsync();
            var activeSince = DateTimeOffset.UtcNow.AddDays(-1);
            await workspace.Settings.SaveAsync(snapshot.Options with { RecordingOutput = null, AutomaticMp3 = new() { Enabled = true, SinceUtc = activeSince } }, snapshot);
            await page.ReloadAsync();
            Assert.AreEqual(1, option.SelectedIndex);
            Assert.AreEqual(activeSince, page.ReadOptions().GetRecordingOutput().SinceUtc);
            foreach (var format in Enum.GetValues<RecordingArtifactFormat>())
            {
                option.SelectedIndex = (int)format - 1;
                var requested = page.ReadOptions();
                Assert.AreEqual(format, requested.GetRecordingOutput().Format);
                Assert.AreEqual(format == RecordingArtifactFormat.Wave, requested.GetRecordingOutput().SinceUtc is null);
                await workspace.Settings.SaveAsync(requested, await workspace.Settings.LoadAsync());
                await page.ReloadAsync();
                Assert.AreEqual((int)format - 1, option.SelectedIndex);
                Assert.AreEqual(requested.GetRecordingOutput(), page.ReadOptions().GetRecordingOutput());
            }
            if (scale != 1F) form.Scale(new SizeF(scale, scale)); form.PerformLayout(); Application.DoEvents(); CheckTableBounds(form);
            Assert.IsTrue(option.Width >= TextRenderer.MeasureText("Ogg/Vorbis", option.Font).Width + 20,
                "The format editor must provide room for its longest option and dropdown arrow.");
            var output = Path.Combine(RenderOutput(), "recording-output"); Directory.CreateDirectory(output);
            using var shot = UiAudit.Capture(form); shot.Save(Path.Combine(output, $"auto-{workspace.Application.ProductLine}-{width}-{size:0}-{scale:0.0}.png"), ImageFormat.Png);
            await form.StopAsync();
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });
}
