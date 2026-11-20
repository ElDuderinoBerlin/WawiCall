using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow(1280, 720, 24F)]
    [DataRow(1024, 600, 16F)]
    [DataRow(1280, 720, 9F)]
    [DataRow(800, 600, 24F)]
    public Task SettingsFitSmallWorkAreasKeepActionsVisibleAndScrollLongPages(int width, int height, float fontSize) => WithWorkspace(async (workspace, _, _) =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.Combine(RenderOutput(), "dpi-r2-work-area"); Directory.CreateDirectory(output);
        var area = new Rectangle(0, 0, width, height);
        try
        {
            foreach (var skin in new[] { "Basic", "Office 2019 Black" })
            {
                UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var dialog = new FrmSettings(workspace);
                dialog.PrepareForBackgroundRun(); dialog.Font = font;
                WindowWorkArea.Attach(dialog, _ => area);
                dialog.StartPosition = FormStartPosition.Manual; dialog.Bounds = new Rectangle(5, 5, width - 20, height - 20);
                dialog.Show(); Application.DoEvents();
                var page = Field<SettingsPage>(dialog, "settingsPage"); await page.ReloadAsync();
                var input = Field<TextEdit>(page, "logDirectoryEdit"); input.Text = @"C:\DPI-Test\Logs";
                foreach (var tabName in new[] { "generalTab", "hotkeysTab", "databaseTab", "audioTab", "mailTab", "pluginTab" })
                {
                    var tab = Field<XtraTabPage>(page, tabName); dialog.SettingsTabs.SelectedTabPage = tab;
                    dialog.PerformLayout(); Application.DoEvents();
                    Assert.IsTrue(area.Contains(dialog.Bounds), $"{tabName}: window {dialog.Bounds} escapes {area}");
                    Assert.IsTrue(dialog.MinimumSize.Width <= width && dialog.MinimumSize.Height <= height);
                    foreach (var name in new[] { "saveButton", "cancelButton", "importButton", "exportButton" })
                    {
                        var button = Field<SimpleButton>(page, name);
                        var bounds = dialog.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
                        Assert.IsTrue(dialog.ClientRectangle.Contains(bounds), $"{tabName}/{name}: {bounds} outside {dialog.ClientRectangle}");
                    }
                    CheckTableBounds(page);
                    using var image = WawiCallPro.App.Diagnostics.UiAudit.Capture(dialog);
                    image.Save(Path.Combine(output, $"settings-{width}-{height}-{fontSize}-{skin}-{tabName}.png"));
                }
                dialog.SettingsTabs.SelectedTabPage = Field<XtraTabPage>(page, "generalTab");
                var general = Field<XtraTabPage>(page, "generalTab");
                var contents = Field<TableLayoutPanel>(page, "generalLayout");
                var hint = Field<LabelControl>(page, "generalHint");
                general.ScrollControlIntoView(hint); Application.DoEvents();
                Assert.IsTrue(hint.RectangleToScreen(hint.ClientRectangle).Bottom <= general.RectangleToScreen(general.ClientRectangle).Bottom + 1);
                Assert.IsTrue(contents.Height <= general.ClientSize.Height || general.VerticalScroll.Visible);
                Assert.AreEqual(@"C:\DPI-Test\Logs", input.Text, "Layout and tab changes must preserve user input.");
                using var bottom = WawiCallPro.App.Diagnostics.UiAudit.Capture(dialog);
                bottom.Save(Path.Combine(output, $"settings-{width}-{height}-{fontSize}-{skin}-general-bottom.png"));
                Assert.AreEqual(0d, dialog.Opacity); Assert.IsFalse(dialog.ShowInTaskbar);
                await dialog.StopAsync();
            }
        }
        finally { WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance; UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette); }
    });

    [TestMethod]
    public Task WindowPlacementClampsOversizedMinimumsAndHandlesNegativeMonitorCoordinates()
    {
        foreach (var area in new[] { new Rectangle(0, 0, 1280, 720), new Rectangle(-1920, -200, 1920, 1040) })
        {
            var minimum = WindowWorkArea.LimitMinimum(new Size(3000, 2000), area);
            Assert.AreEqual(area.Size, minimum);
            var restored = WindowWorkArea.Fit(new Rectangle(50000, 50000, 4000, 3000), minimum, area);
            Assert.IsTrue(area.Contains(restored));
            Assert.AreEqual(area, restored);
        }
        return Task.CompletedTask;
    }
}
