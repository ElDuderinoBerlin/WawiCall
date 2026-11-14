using System.Drawing.Imaging;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task SettingsCancelAndWindowCloseDiscardUnsavedLoggingEdits(bool useButton) => WithWorkspace(async (workspace, services, plugin) =>
    {
        var initial = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(initial.Options, initial);
        var file = services.GetRequiredService<ConfigurationLocation>().FilePath;
        var before = await File.ReadAllBytesAsync(file);
        using var form = new FrmSettings(workspace);
        form.PrepareForBackgroundRun(); form.Show();
        var page = Field<SettingsPage>(form, "settingsPage"); await page.ReloadAsync();
        Field<ComboBoxEdit>(page, "logLevelEdit").SelectedIndex = (int)LogLevel.Critical;
        Field<TextEdit>(page, "logDirectoryEdit").Text = Path.Combine(workspace.Paths.RootDirectory, "discarded");
        if (useButton) Field<SimpleButton>(page, "cancelButton").PerformClick(); else form.Close();
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (form.Visible && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsFalse(form.Visible);
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(file));
    });

    [TestMethod]
    public Task SettingsLoggingRoundTripsReloadsAndLeavesRunningConfigurationUnchanged() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new FrmSettings(workspace);
        var page = Field<SettingsPage>(form, "settingsPage");
        var level = Field<ComboBoxEdit>(page, "logLevelEdit");
        var directory = Field<TextEdit>(page, "logDirectoryEdit");
        Assert.AreEqual((int)LogLevel.Information, level.SelectedIndex);
        Assert.AreEqual(workspace.Paths.LogsDirectory, directory.Text);
        level.SelectedIndex = (int)LogLevel.Debug;
        directory.Text = Path.Combine(workspace.Paths.RootDirectory, "custom-logs");
        await page.SaveAsync(default);
        var saved = await workspace.Settings.LoadAsync();
        Assert.AreEqual(LogLevel.Debug, saved.Options.User.LogLevel);
        Assert.AreEqual(directory.Text, saved.Options.User.LogDirectory);
        Assert.AreEqual(LogLevel.Information, workspace.Configuration.Current.Options.User.LogLevel);
        level.SelectedIndex = (int)LogLevel.Error;
        directory.Text = "relative";
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => page.SaveAsync(default));
        await page.ReloadAsync();
        Assert.AreEqual((int)LogLevel.Debug, level.SelectedIndex);
        Assert.AreEqual(saved.Options.User.LogDirectory, directory.Text);
        directory.Text = "";
        await page.SaveAsync(default);
        Assert.IsNull((await workspace.Settings.LoadAsync()).Options.User.LogDirectory);
        Assert.AreEqual(workspace.Paths.LogsDirectory, directory.Text);
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task SettingsSaveClosesOnlyAfterSuccessfulPersistence(bool invalid) => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new FrmSettings(workspace);
        form.PrepareForBackgroundRun(); form.Show();
        var page = Field<SettingsPage>(form, "settingsPage");
        await page.ReloadAsync();
        Field<TextEdit>(page, "logDirectoryEdit").Text = invalid ? "relative" : Path.Combine(workspace.Paths.RootDirectory, "selected-logs");
        Field<SimpleButton>(page, "saveButton").PerformClick();
        await WaitForOperationAsync(page);
        if (invalid)
        {
            Assert.IsTrue(form.Visible);
            Assert.AreEqual(DialogResult.None, form.DialogResult);
            Assert.IsNull((await workspace.Settings.LoadAsync()).Options.User.LogDirectory);
            Field<SimpleButton>(page, "cancelButton").PerformClick();
        }
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (form.Visible && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsFalse(form.Visible);
        Assert.AreEqual(invalid ? DialogResult.Cancel : DialogResult.OK, form.DialogResult);
        if (!invalid) Assert.AreEqual(Path.Combine(workspace.Paths.RootDirectory, "selected-logs"), (await workspace.Settings.LoadAsync()).Options.User.LogDirectory);
    });

    [TestMethod]
    public Task SettingsGeneralAndFooterRenderLikeReferenceWithoutVisibleWindows() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.Combine(".artifacts", "settings-reference", workspace.Application.ProductLine);
        Directory.CreateDirectory(output);
        try
        {
            foreach (var populated in new[] { false, true })
            foreach (var (width, fontSize, scale, skin) in new[] { (980, 10F, 1F, "Basic"), (760, 16F, 1F, "Office 2019 Black"), (980, 10F, 1.5F, "Basic") })
            {
                if (skin == "Basic") DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin, "Blue Dark");
                else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new FrmSettings(workspace);
                form.PrepareForBackgroundRun(); form.Font = font;
                _ = form.Handle; form.MinimumSize = Size.Empty; form.ClientSize = new(width, 700);
                var page = Field<SettingsPage>(form, "settingsPage");
                var current = workspace.Configuration.Current;
                page.Apply(current with { Options = current.Options with { User = current.Options.User with
                {
                    LogLevel = populated ? LogLevel.Debug : LogLevel.Information,
                    LogDirectory = populated ? @"C:\Users\Beispiel\AppData\Local\WawiCallPro\Eigene Protokolle" : null
                } } });
                CreateHandles(form);
                if (scale != 1F) form.Scale(new SizeF(scale, scale));
                form.PerformLayout(); Application.DoEvents(); CheckTableBounds(form);
                var save = Field<SimpleButton>(page, "saveButton"); var cancel = Field<SimpleButton>(page, "cancelButton");
                Assert.AreEqual(save.Size, cancel.Size); Assert.AreEqual(save.Top, cancel.Top);
                Assert.AreEqual(Field<TextEdit>(page, "logDirectoryEdit").Left, Field<ComboBoxEdit>(page, "logLevelEdit").Left);
                Assert.AreEqual(3, Field<TableLayoutPanel>(page, "footer").Controls.Count);
                Assert.AreEqual(1, Field<TableLayoutPanel>(form, "layout").RowCount);
                Assert.IsFalse(form.Visible);
                using var shot = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                shot.Save(Path.Combine(output, $"general-{populated}-{width}-{fontSize:0}-{scale:0.0}-{skin}.png"), ImageFormat.Png);
                if (!populated && width == 980 && scale == 1F)
                {
                    foreach (var field in new[] { "sipTab", "pluginTab" })
                    {
                        form.SettingsTabs.SelectedTabPage = Field<DevExpress.XtraTab.XtraTabPage>(page, field);
                        CreateHandles(form); form.PerformLayout(); CheckTableBounds(form);
                        using var extra = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                        extra.Save(Path.Combine(output, field + ".png"), ImageFormat.Png);
                    }
                }
                await form.StopAsync();
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
