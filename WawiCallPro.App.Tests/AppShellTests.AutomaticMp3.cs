using System.Drawing.Imaging;
using System.IO;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Views;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow(1240, 10F, 1F, "Office 2019 Colorful")]
    [DataRow(900, 16F, 1F, "Office 2019 Black")]
    [DataRow(1240, 10F, 1.5F, "Office 2019 Colorful")]
    public Task AutomaticMp3OptInRoundtripsAndRendersWithoutVisibleWindows(int width, float size, float scale, string skin) => WithWorkspace(async (workspace, services, plugin) =>
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
            var option = Field<CheckEdit>(page, "automaticMp3Edit"); Assert.IsFalse(option.Checked);
            option.Checked = true; var requested = page.ReadOptions(); Assert.IsTrue(requested.AutomaticMp3.Enabled); Assert.IsNotNull(requested.AutomaticMp3.SinceUtc);
            await workspace.Settings.SaveAsync(requested, await workspace.Settings.LoadAsync()); await page.ReloadAsync(); Assert.IsTrue(option.Checked);
            Assert.AreEqual(requested.AutomaticMp3.SinceUtc, page.ReadOptions().AutomaticMp3.SinceUtc);
            if (scale != 1F) form.Scale(new SizeF(scale, scale)); form.PerformLayout(); Application.DoEvents(); CheckTableBounds(form);
            var output = Path.Combine(RenderOutput(), "automatic-mp3"); Directory.CreateDirectory(output);
            using var shot = UiAudit.Capture(form); shot.Save(Path.Combine(output, $"auto-{workspace.Application.ProductLine}-{width}-{size:0}-{scale:0.0}.png"), ImageFormat.Png);
            option.Checked = false; Assert.IsNull(page.ReadOptions().AutomaticMp3.SinceUtc); Assert.IsFalse(page.ReadOptions().AutomaticMp3.Enabled);
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
