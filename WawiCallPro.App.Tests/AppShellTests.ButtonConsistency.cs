using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow("Basic", 8F)]
    [DataRow("Basic", 16F)]
    [DataRow("Office 2019 Black", 8F)]
    [DataRow("Office 2019 Black", 16F)]
    public Task SettingsActionsAndBrowseUseTheSameHeightAsViewsAndHaveNoReloadAction(string skin, float points) => WithWorkspace(async (workspace, services, _) =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            UserLookAndFeel.Default.SetSkinStyle(skin);
            using var font = new Font(points == 8F ? "Tahoma" : "Segoe UI", points);
            WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
            using var main = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
            main.PrepareForBackgroundRun(); main.Font = font; main.ClientSize = new Size(1200, 800); main.Show();
            var home = (HomeDashboard)main.GetActiveDocumentView()!;
            var reference = Field<SimpleButton>(home, "refreshButton");
            using var settings = main.CreateSettingsDialog();
            settings.PrepareForBackgroundRun(); settings.Font = font; settings.Show();
            var page = Field<SettingsPage>(settings, "settingsPage"); await page.ReloadAsync(); Application.DoEvents();
            var actions = new[] { "importButton", "exportButton", "saveButton", "cancelButton" }
                .Select(name => Field<SimpleButton>(page, name)).ToArray();
            Assert.AreEqual(4, Field<TableLayoutPanel>(page, "transferActions").Controls.Count + Field<TableLayoutPanel>(page, "dialogActions").Controls.Count);
            Assert.AreEqual(0, page.Controls.Find("reloadButton", true).Length);
            Assert.AreEqual(1, actions.Select(button => button.Size).Distinct().Count());
            foreach (var button in actions.Append(Field<SimpleButton>(page, "logDirectoryBrowseButton")))
            {
                Assert.AreEqual(reference.Height, button.Height, $"{button.Name} differs from the view action at {button.DeviceDpi} DPI.");
                Assert.IsTrue(button.Width >= button.CalcBestSize().Width);
                Assert.AreEqual(reference.Padding, button.Padding);
            }
            CheckTableBounds(page);
            var output = Path.Combine(RenderOutput(), "uniform-buttons"); Directory.CreateDirectory(output);
            using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(settings);
            bitmap.Save(Path.Combine(output, $"settings-{skin}-{points}.png"));
            await settings.StopAsync();
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });
}
