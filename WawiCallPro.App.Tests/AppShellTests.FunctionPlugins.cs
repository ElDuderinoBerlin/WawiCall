using System.Drawing.Imaging;
using System.Reflection;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task FunctionPluginsActivateIndependentlyAndRestoreAcrossRestart() => WithWorkspace(async (workspace, services, _) =>
    {
        const string clock = "WawiCallPro.Functions.DummyClock";
        const string status = "WawiCallPro.Functions.DummyStatus";
        var runtime = services.GetRequiredService<FunctionPluginRuntime>();
        await runtime.StartAsync(default);
        var catalog = await runtime.ListAsync();
        CollectionAssert.AreEquivalent(new[] { clock, status }, catalog.Select(plugin => plugin.Id).ToArray());
        Assert.IsTrue(catalog.All(plugin => !plugin.Enabled));
        Assert.IsTrue(catalog.All(plugin => plugin.Name.StartsWith("Dummy", StringComparison.Ordinal)));
        await Assert.ThrowsExactlyAsync<FunctionPluginException>(() => runtime.SetEnabledAsync("WawiCallPro.Plugins.Dummy", true));

        using var normalFont = new Font("Segoe UI", 10F);
        using var largeFont = new Font("Segoe UI", 16F);
        using var settings = new FrmSettings(workspace);
        settings.PrepareForBackgroundRun();
        settings.SettingsTabs.SelectedTabPage = settings.SettingsTabs.TabPages.Single(tab => tab.Text == "Plugins");
        var settingsPage = Field<SettingsPage>(settings, "settingsPage");
        var pluginPages = Field<XtraTabControl>(settingsPage, "pluginPages");
        CollectionAssert.AreEqual(new[] { "Warenwirtschaft", "Funktionsplugins" }, pluginPages.TabPages.Select(tab => tab.Text).ToArray());
        pluginPages.SelectedTabPage = pluginPages.TabPages.Single(tab => tab.Text == "Funktionsplugins");
        Assert.IsTrue(await settings.PrepareAsync());
        settings.Show();
        var page = settings.Controls.Find("functionPluginsPage", true).OfType<FunctionPluginsPage>().Single();
        var list = Field<ListBoxControl>(page, "plugins");
        Assert.AreEqual(2, ((Array)list.DataSource).Length);
        CollectionAssert.AreEquivalent(new[] { "Dummy – Statusmeldung · Inaktiv", "Dummy – Uhrzeit · Inaktiv" },
            Enumerable.Range(0, list.ItemCount).Select(list.GetItemText).ToArray());
        list.SelectedIndex = 0;
        await page.RunAsync(execute: false);
        list.SelectedIndex = 1;
        await page.RunAsync(execute: false);
        Assert.IsTrue((await runtime.ListAsync()).All(plugin => plugin.Enabled));
        await page.RunAsync(execute: true);
        Assert.AreEqual(OperationEvent.SettingsFunctionPluginExecuted, settingsPage.LastSettingsNotice);
        Assert.IsFalse(page.Controls.Find("status", true).Any());
        await runtime.ExecuteAsync(clock);

        var wawi = (IProductPluginSelection)workspace.Plugins;
        await wawi.SwitchAsync("Test.Dummy");
        Assert.IsTrue((await runtime.ListAsync()).All(plugin => plugin.Enabled));
        await wawi.SwitchAsync("Test.Lexware");
        CaptureFunctionPlugins(settings, workspace.Application.ProductLine, normalFont, largeFont);
        await settings.StopAsync();
        await runtime.StopAsync(default);

        await using var restarted = ActivatorUtilities.CreateInstance<FunctionPluginRuntime>(services);
        await restarted.StartAsync(default);
        Assert.IsTrue((await restarted.ListAsync()).All(plugin => plugin.Enabled));
        await restarted.SetEnabledAsync(clock, false);
        Assert.IsFalse((await restarted.ListAsync()).Single(plugin => plugin.Id == clock).Enabled);
        Assert.IsTrue((await restarted.ListAsync()).Single(plugin => plugin.Id == status).Enabled);
        await restarted.ExecuteAsync(status);
        await Assert.ThrowsExactlyAsync<FunctionPluginException>(() => restarted.ExecuteAsync(clock));
    }, configure: services =>
    {
        var configuration = typeof(MainForm).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var repository = Directory.GetParent(RenderOutput())!.Parent!.Parent!.FullName;
        services.AddSingleton(new FunctionPluginLocation(Path.Combine(repository, "WawiCallPro.App", "bin", configuration,
            "net11.0-windows10.0.19041.0", "function-plugins")));
        services.AddSingleton<FunctionPluginRuntime>();
        services.AddSingleton<IFunctionPluginRuntime>(provider => provider.GetRequiredService<FunctionPluginRuntime>());
    });

    private static void CaptureFunctionPlugins(FrmSettings settings, string edition, Font normalFont, Font largeFont)
    {
        var look = UserLookAndFeel.Default;
        var originalSkin = look.ActiveSkinName;
        var originalPalette = look.ActiveSvgPaletteName;
        var originalFont = settings.Font;
        var originalDefaultFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var output = Path.Combine(RenderOutput(), "function-plugins");
        Directory.CreateDirectory(output);
        try
        {
            foreach (var (skin, font, size) in new[] { ("Basic", normalFont, new Size(1100, 760)), ("Office 2019 Black", largeFont, new Size(780, 640)) })
            {
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                settings.Font = font;
                look.SetSkinStyle(skin);
                settings.ClientSize = size;
                settings.PerformLayout();
                CheckTableBounds(settings);
                using var shot = UiAudit.Capture(settings);
                shot.Save(Path.Combine(output, $"{edition}-{font.Size:0}.png"), ImageFormat.Png);
                if (ReferenceEquals(font, largeFont))
                {
                    settings.Scale(new SizeF(1.5F, 1.5F));
                    settings.PerformLayout();
                    CheckTableBounds(settings);
                    using var scaled = UiAudit.Capture(settings);
                    scaled.Save(Path.Combine(output, $"{edition}-scale150.png"), ImageFormat.Png);
                }
            }
        }
        finally
        {
            settings.Font = originalFont;
            WindowsFormsSettings.DefaultFont = originalDefaultFont;
            AppearanceObject.DefaultFont = originalAppearance;
            if (string.IsNullOrEmpty(originalPalette)) look.SetSkinStyle(originalSkin); else look.SetSkinStyle(originalSkin, originalPalette);
        }
    }
}
