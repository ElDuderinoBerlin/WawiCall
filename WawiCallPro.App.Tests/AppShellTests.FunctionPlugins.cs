using System.Drawing.Imaging;
using System.Reflection;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
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
        var grid = Field<GridControl>(page, "pluginsGrid");
        var view = Field<GridView>(page, "pluginsView");
        grid.ForceInitialize();
        Assert.AreEqual(2, view.DataRowCount);
        CollectionAssert.AreEquivalent(new[] { "Dummy – Statusmeldung", "Dummy – Uhrzeit" },
            Enumerable.Range(0, view.DataRowCount).Select(handle => view.GetRowCellDisplayText(handle, "Name")).ToArray());
        CollectionAssert.AreEqual(Array.Empty<int>(), view.GetSelectedRows());
        view.FocusedRowHandle = 1;
        CollectionAssert.AreEqual(Array.Empty<int>(), view.GetSelectedRows(), "Moving focus must not activate a plugin.");
        var execute = Field<SimpleButton>(page, "executeButton");
        Assert.IsFalse(execute.Enabled);
        var runtimeProbe = (FunctionPluginActivationProbe)services.GetRequiredService<IFunctionPluginRuntime>();
        runtimeProbe.RejectNextChange = true;
        view.SelectRow(0);
        await WaitForOperationAsync(page);
        Assert.AreEqual(OperationEvent.SettingsFunctionPluginActivationFailed, settingsPage.LastSettingsNotice);
        Assert.IsFalse(view.IsRowSelected(0), "A failed activation must restore the unchecked state.");
        Assert.IsTrue((await runtime.ListAsync()).All(plugin => !plugin.Enabled));

        view.SelectRow(0);
        await WaitForOperationAsync(page);
        Assert.AreEqual(OperationEvent.SettingsFunctionPluginEnabled, settingsPage.LastSettingsNotice);
        Assert.IsFalse(execute.Enabled, "Execute follows the focused row, not the last checked row.");
        view.SelectRow(1);
        await WaitForOperationAsync(page);
        Assert.AreEqual(OperationEvent.SettingsFunctionPluginEnabled, settingsPage.LastSettingsNotice);
        Assert.IsTrue((await runtime.ListAsync()).All(plugin => plugin.Enabled));
        Assert.IsTrue(execute.Enabled);
        execute.PerformClick();
        await WaitForOperationAsync(page);
        Assert.AreEqual(OperationEvent.SettingsFunctionPluginExecuted, settingsPage.LastSettingsNotice);
        Assert.IsFalse(page.Controls.Find("status", true).Any());
        await runtime.ExecuteAsync(clock);

        var wawi = (IProductPluginSelection)workspace.Plugins;
        await wawi.SwitchAsync("Test.Dummy");
        Assert.IsTrue((await runtime.ListAsync()).All(plugin => plugin.Enabled));
        await wawi.SwitchAsync("Test.Lexware");
        CaptureFunctionPlugins(settings, workspace.Application.ProductLine, normalFont, largeFont);
        view.UnselectRow(1);
        await WaitForOperationAsync(page);
        Assert.AreEqual(OperationEvent.SettingsFunctionPluginDisabled, settingsPage.LastSettingsNotice);
        Assert.IsFalse(execute.Enabled);
        Assert.IsTrue(view.IsRowSelected(0));
        Assert.IsFalse(view.IsRowSelected(1));
        await settings.StopAsync();
        await runtime.StopAsync(default);

        await using var restarted = ActivatorUtilities.CreateInstance<FunctionPluginRuntime>(services);
        await restarted.StartAsync(default);
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
        services.AddSingleton<IFunctionPluginRuntime, FunctionPluginActivationProbe>();
    });

    private sealed class FunctionPluginActivationProbe(FunctionPluginRuntime inner) : IFunctionPluginRuntime
    {
        public bool RejectNextChange { get; set; }
        public Task<IReadOnlyList<FunctionPluginChoice>> ListAsync(CancellationToken token) => inner.ListAsync(token);
        public Task ExecuteAsync(string id, CancellationToken token) => inner.ExecuteAsync(id, token);

        public Task SetEnabledAsync(string id, bool enabled, CancellationToken token)
        {
            if (!RejectNextChange) return inner.SetEnabledAsync(id, enabled, token);
            RejectNextChange = false;
            throw new FunctionPluginException();
        }
    }

    private static void CaptureFunctionPlugins(FrmSettings settings, string edition, Font normalFont, Font largeFont)
    {
        var look = UserLookAndFeel.Default;
        var originalSkin = look.ActiveSkinName;
        var originalPalette = look.ActiveSvgPaletteName;
        var originalFont = settings.Font;
        var originalDefaultFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var page = Find<FunctionPluginsPage>(settings, "functionPluginsPage");
        var view = Field<GridView>(page, "pluginsView");
        var output = Path.Combine(RenderOutput(), "function-plugins");
        Directory.CreateDirectory(output);
        try
        {
            var previousWidth = 0;
            foreach (var (skin, font, size) in new[] { ("Basic", normalFont, new Size(1100, 760)), ("Office 2019 Black", largeFont, new Size(780, 640)) })
            {
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                settings.Font = font;
                look.SetSkinStyle(skin);
                settings.ClientSize = size;
                settings.PerformLayout();
                CheckTableBounds(settings);
                var width = view.OptionsSelection.CheckBoxSelectorColumnWidth;
                Assert.IsGreaterThan(previousWidth, width, "The checkbox column should grow with the header font.");
                if (ReferenceEquals(font, normalFont))
                    Assert.IsLessThan(page.LogicalToDeviceUnits(64), width, "The checkbox column should stay compact at the default font.");
                previousWidth = width;
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
