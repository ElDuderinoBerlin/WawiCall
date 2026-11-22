using DevExpress.Utils;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Diagnostics;
using Quantarix.DevExpressExtensions.Controls;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task StartupProgressReportsActualServiceBoundariesAndOnlyTheLoadedPlugin() => RunStaAsync(async () =>
    {
        var plugin = new TestPlugin(); var runtime = new TestRuntime(plugin);
        await using var splash = new StartupSplash();
        var progress = new StartupProgress(splash, runtime);
        await progress.ReportAsync(typeof(StartupSplashHost), 0, 20, false, default);
        var first = progress.Percent;
        Assert.IsTrue(first > 0 && first < 10);
        Assert.AreEqual("Geladene Plugins: " + plugin.Name + " (v" + plugin.Version + ")", progress.LoadedPlugins);
        await progress.ReportAsync(typeof(LicenseStartupGate), 1, 20, true, default);
        Assert.IsTrue(progress.Percent > first);
        var previous = progress.Percent;
        await progress.ReportAsync(typeof(StartupSplashHost), 0, 20, false, default);
        Assert.AreEqual(previous, progress.Percent);
        Assert.AreEqual(nint.Zero, splash.WindowHandle, "Progress reports alone never create a COM splash.");
        Assert.AreEqual("Lizenzprüfung", StartupProgress.ServiceTitle(typeof(LicenseStartupGate)));
    });

    [TestMethod]
    public Task StartupProgressMarkersPreserveServiceInstancesLifecycleAndStopOrder() => RunStaAsync(async () =>
    {
        var order = new List<string>(); var first = new ProgressService("first", order); var second = new ProgressService("second", order);
        var services = new ServiceCollection(); services.AddSingleton<StartupSplash>();
        services.AddSingleton<IProductPluginRuntime>(new TestRuntime(new TestPlugin()));
        services.AddSingleton<IHostedService>(first); services.AddSingleton<IHostedService>(second);
        services.AddStartupProgress(new StartupExecutionMode(true));
        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().ToArray();
        Assert.AreEqual(6, hosted.Length); Assert.AreSame(first, hosted[1]); Assert.AreSame(second, hosted[4]);
        foreach (var service in hosted) await service.StartAsync(default);
        foreach (var service in hosted.Reverse()) await service.StopAsync(default);
        CollectionAssert.AreEqual(new[] { "start:first", "start:second", "stop:second", "stop:first" }, order);
        Assert.AreEqual(90, provider.GetRequiredService<StartupProgress>().Percent);
    });

    [TestMethod]
    public Task StartupProgressRendersPluginNamesWithoutOverlapInBothSkinsAndLargeFonts() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply(); var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var look = DevExpress.LookAndFeel.UserLookAndFeel.Default; var oldSkin = look.ActiveSkinName; var oldPalette = look.ActiveSvgPaletteName;
        var output = Path.Combine(RenderOutput(), "startup-progress"); Directory.CreateDirectory(output);
        try
        {
            foreach (var (size, skin, scale) in new[] { (10F, "Basic", 1F), (16F, "Office 2019 Black", 1F), (10F, "Basic", 1.5F) })
            foreach (var loaded in new[] { false, true })
            {
                look.SetSkinStyle(skin); using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new FrmStartupSplashScreen(); form.Font = font; form.PrepareForBackgroundRun();
                form.SetStatus("Mandanten- und Ansichtskatalogdienst wird gestartet … (16/20)", 73, 100);
                form.SetLoadedPlugins(loaded ? "Geladene Plugins: Lexware Financial Office (v0.1.0.0)" : "Geladene Plugins: noch keine");
                form.Show(); if (scale != 1F) form.Scale(new SizeF(scale, scale));
                form.PerformLayout(); Application.DoEvents(); CheckTableBounds(form);
                var progress = Field<PulsingProgressBarControl>(form, "progressBar");
                for (var frame = 0; progress.DisplayedPosition != 73 && frame < 70; frame++) await Task.Delay(30);
                Assert.AreEqual(73, progress.DisplayedPosition);
                var status = Field<LabelControl>(form, "lblStatus"); var plugins = Field<LabelControl>(form, "lblPlugins");
                Assert.IsTrue(status.Bottom <= plugins.Top); Assert.IsTrue(plugins.Bottom <= Field<LabelControl>(form, "lblVersion").Top);
                Assert.AreEqual(73, Field<PulsingProgressBarControl>(form, "progressBar").Position);
                using var picture = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                picture.Save(Path.Combine(output, $"{size}-{skin}-{scale}-{loaded}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) look.SetSkinStyle(oldSkin); else look.SetSkinStyle(oldSkin, oldPalette);
        }
        await Task.CompletedTask;
    });

    private sealed class ProgressService(string name, List<string> order) : IHostedService
    {
        public Task StartAsync(CancellationToken token) { order.Add("start:" + name); return Task.CompletedTask; }
        public Task StopAsync(CancellationToken token) { order.Add("stop:" + name); return Task.CompletedTask; }
    }
}
