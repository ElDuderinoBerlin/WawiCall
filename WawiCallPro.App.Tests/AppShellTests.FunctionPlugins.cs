using System.Drawing.Imaging;
using System.Reflection;
using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Plugins;
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

        using var settings = new FrmSettings(workspace);
        settings.PrepareForBackgroundRun();
        settings.SettingsTabs.SelectedTabPage = settings.SettingsTabs.TabPages.Single(tab => tab.Text == "Funktionsplugins");
        Assert.IsTrue(await settings.PrepareAsync());
        settings.Show();
        var page = settings.Controls.Find("functionPluginsPage", true).OfType<FunctionPluginsPage>().Single();
        var list = Field<ListBoxControl>(page, "plugins");
        Assert.AreEqual(2, ((Array)list.DataSource).Length);
        list.SelectedIndex = 0;
        await page.RunAsync(execute: false);
        list.SelectedIndex = 1;
        await page.RunAsync(execute: false);
        Assert.IsTrue((await runtime.ListAsync()).All(plugin => plugin.Enabled));
        await page.RunAsync(execute: true);
        Assert.AreEqual("Funktion ausgeführt.", Field<LabelControl>(page, "status").Text);
        await runtime.ExecuteAsync(clock);

        var wawi = (IProductPluginSelection)workspace.Plugins;
        await wawi.SwitchAsync("Test.Dummy");
        Assert.IsTrue((await runtime.ListAsync()).All(plugin => plugin.Enabled));
        await wawi.SwitchAsync("Test.Lexware");
        CaptureFunctionPlugins(settings, workspace.Application.ProductLine);
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

    private static void CaptureFunctionPlugins(FrmSettings settings, string edition)
    {
        var look = UserLookAndFeel.Default;
        var originalSkin = look.ActiveSkinName;
        var originalPalette = look.ActiveSvgPaletteName;
        var originalFont = settings.Font;
        var output = Path.Combine(RenderOutput(), "function-plugins");
        Directory.CreateDirectory(output);
        try
        {
            foreach (var (skin, fontSize, size) in new[] { ("Basic", 10F, new Size(1100, 760)), ("Office 2019 Black", 16F, new Size(780, 640)) })
            {
                using var font = new Font("Segoe UI", fontSize);
                settings.Font = font;
                look.SetSkinStyle(skin);
                settings.ClientSize = size;
                settings.PerformLayout();
                CheckTableBounds(settings);
                using var shot = UiAudit.Capture(settings);
                shot.Save(Path.Combine(output, $"{edition}-{fontSize:0}.png"), ImageFormat.Png);
            }
        }
        finally
        {
            settings.Font = originalFont;
            if (string.IsNullOrEmpty(originalPalette)) look.SetSkinStyle(originalSkin); else look.SetSkinStyle(originalSkin, originalPalette);
        }
    }
}
