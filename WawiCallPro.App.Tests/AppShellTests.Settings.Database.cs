using System.Drawing.Imaging;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task SettingsDatabaseEntryUsesTheRealWizardAndPreservesDraftsAndRunningRepositories(bool accept) => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new FrmSettings(workspace); form.PrepareForBackgroundRun(); CreateHandles(form);
        var page = Field<SettingsPage>(form, "settingsPage"); var before = workspace.Configuration.Current;
        await page.ReloadAsync();
        Field<ComboBoxEdit>(page, "logLevelEdit").SelectedIndex = (int)Microsoft.Extensions.Logging.LogLevel.Trace;
        var dialogCount = 0;
        await page.SetupDatabaseAsync(async dialog =>
        {
            dialogCount++; dialog.PrepareForBackgroundRun(); dialog.Show(); await dialog.CurrentOperation;
            Assert.IsTrue(Field<SimpleButton>(dialog, "btnNext").Enabled);
            if (!accept) { await dialog.StopAsync(); return DialogResult.Cancel; }
            Field<SimpleButton>(dialog, "btnNext").PerformClick();
            Field<SimpleButton>(dialog, "btnNext").PerformClick(); await dialog.CurrentOperation;
            Field<SimpleButton>(dialog, "btnFinish").PerformClick(); await dialog.CurrentOperation;
            return dialog.DialogResult;
        });
        Assert.AreEqual(1, dialogCount); Assert.AreEqual(Microsoft.Extensions.Logging.LogLevel.Trace, page.ReadOptions().User.LogLevel);
        Assert.AreSame(before, workspace.Configuration.Current);
        var saved = await workspace.Settings.LoadAsync(); Assert.AreEqual(accept, saved.FileFound);
        Assert.AreEqual("communications.db", Field<LabelControl>(page, "sqlitePathValue").Text);
        if (accept)
        {
            await page.SaveAsync(CancellationToken.None);
            var updated = await workspace.Settings.LoadAsync(); Assert.AreEqual(saved.Options.Database.SqliteFileName, updated.Options.Database.SqliteFileName);
            Assert.AreEqual(Microsoft.Extensions.Logging.LogLevel.Trace, updated.Options.User.LogLevel);
        }
        await form.StopAsync();
    });

    [TestMethod]
    public Task SettingsDatabaseTabRendersCompactSetupAtDifferentSizesFontsAndSkins() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var look = DevExpress.LookAndFeel.UserLookAndFeel.Default; var oldSkin = look.ActiveSkinName; var oldPalette = look.ActiveSvgPaletteName;
        var output = Path.Combine(".artifacts", "settings-database", workspace.Application.ProductLine); Directory.CreateDirectory(output);
        try
        {
            foreach (var (width, fontSize, scale, skin) in new[] { (980, 10F, 1F, "Basic"), (760, 16F, 1F, "Office 2019 Black"), (980, 10F, 1.5F, "Basic") })
            {
                look.SetSkinStyle(skin); using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new FrmSettings(workspace); form.PrepareForBackgroundRun(); form.Font = font;
                _ = form.Handle; form.MinimumSize = Size.Empty; form.ClientSize = new(width, 700); CreateHandles(form);
                var page = Field<SettingsPage>(form, "settingsPage");
                await page.ReloadAsync();
                form.SettingsTabs.SelectedTabPage = Field<DevExpress.XtraTab.XtraTabPage>(page, "databaseTab");
                if (scale != 1F) form.Scale(new SizeF(scale, scale)); form.PerformLayout(); Application.DoEvents(); CheckTableBounds(form);
                var button = Field<SimpleButton>(page, "databaseSetupButton"); var table = Field<TableLayoutPanel>(page, "databaseLayout");
                using var shot = WawiCallPro.App.Diagnostics.UiAudit.Capture(form); shot.Save(Path.Combine(output, $"database-{width}-{fontSize:0}-{scale:0.0}-{skin}.png"), ImageFormat.Png);
                Assert.IsTrue(button.Enabled); Assert.AreEqual(Field<LabelControl>(page, "sqlitePathValue").Left, button.Left);
                Assert.IsLessThan(page.Height, table.Height);
                var hint = Field<LabelControl>(page, "databaseHint");
                Assert.IsTrue(hint.Top - button.Bottom <= button.Margin.Bottom + hint.Margin.Top + 2,
                    $"Hint top {hint.Top}, button bottom {button.Bottom}, table {table.Bounds}, font {fontSize}, scale {scale}.");
                Assert.IsFalse(form.Visible);
                await form.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) look.SetSkinStyle(oldSkin); else look.SetSkinStyle(oldSkin, oldPalette);
        }
    });
}
