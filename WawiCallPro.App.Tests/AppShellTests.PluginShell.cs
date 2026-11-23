using DevExpress.XtraBars;
using DevExpress.XtraBars.Navigation;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraGrid.Views.Items;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task PluginShellSwitchReplacesRibbonNavigationAndClosesTheOldWorkspace() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var selection = (IProductPluginSelection)workspace.Plugins;
        IPluginShellUi? borrowed = null;
        plugin.ShellRegistration = ui =>
        {
            borrowed = ui;
            var dummy = selection.ActivePluginId == "Test.Dummy";
            ui.RegisterGroup(new("search", dummy ? "Testbetrieb" : "Stammdaten"));
            ui.RegisterNavigation(new("search", dummy ? "Testkunden suchen" : "Kunden und Lieferanten",
                "Geschäftspartnersuche öffnen.", PluginShellIcon.Parties, PluginNavigationTarget.PartySearch));
            ui.RegisterButton(new("search", "search", "search"));
        };
        var access = new ShellPartyAccess();
        using var main = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>(),
            new LogWindowService(), new AudioCatalogDouble(), new UnconfiguredMailViewSource(), access);
        main.PrepareForBackgroundRun();
        main.Show();
        await main.ReloadPluginShellAsync();
        var ribbon = Field<RibbonControl>(main, "ribbon");
        var navigation = Field<AccordionControl>(main, "navigation");
        var lexware = Field<PluginShellSession>(main, "_pluginShell");
        Assert.AreEqual("Stammdaten", Field<RibbonPage>(main, "_pluginRibbonPage").Groups[0].Text);
        Assert.IsNotNull(borrowed);
        Assert.ThrowsExactly<InvalidOperationException>(() => borrowed.RegisterGroup(new("late", "Verspätet")));
        Assert.AreEqual(BarItemVisibility.Never, Field<BarButtonItem>(main, "partiesItem").Visibility);
        Assert.IsFalse(Field<AccordionControlElement>(main, "partiesNav").Visible);

        var search = Field<List<BarButtonItem>>(main, "_pluginRibbonButtons").Single();
        Assert.IsNotNull(search.ImageOptions.SvgImage);
        Assert.IsFalse(string.IsNullOrWhiteSpace(search.Hint));
        search.PerformClick();
        await Field<Task>(main, "_selectionTask");
        Assert.AreEqual(1, access.Opened);
        var firstView = main.GetActiveDocumentView();
        Assert.IsNotNull(firstView);
        Field<BarButtonItem>(main, "navigateBackItem").PerformClick();
        Assert.AreNotSame(firstView, main.GetActiveDocumentView());
        Field<BarButtonItem>(main, "navigateForwardItem").PerformClick();
        Assert.AreSame(firstView, main.GetActiveDocumentView());

        using var settings = main.CreateSettingsDialog(plugins: true);
        settings.PrepareForBackgroundRun();
        settings.Show();
        var page = Field<SettingsPage>(settings, "settingsPage");
        await page.ReloadAsync();
        var cards = Field<PluginPage>(page, "pluginPage");
        await cards.HandleCardActionAsync("activate", 1);
        Assert.AreEqual("Test.Dummy", selection.ActivePluginId);
        Assert.IsTrue(firstView.IsDisposed);
        Assert.IsFalse(lexware.IsActive);
        Assert.IsFalse(main.InvokePluginNavigation(lexware, lexware.Navigation.Single()));
        Assert.AreEqual("Testbetrieb", Field<RibbonPage>(main, "_pluginRibbonPage").Groups[0].Text);
        Assert.AreEqual("Testkunden suchen", Field<List<AccordionControlElement>>(main, "_pluginNavigationItems").Single().Text);
        Assert.AreSame(Field<RibbonPage>(main, "_pluginRibbonPage"), ribbon.SelectedPage);
        Assert.AreEqual(1, ribbon.Pages.Cast<RibbonPage>().Count(entry => entry.Name == "activePluginRibbon"));
        Assert.AreEqual(1, navigation.Elements.Cast<AccordionControlElement>().Count(entry => entry.Name == "activePluginNavigation"));
        CapturePluginShell(main, workspace.Application.ProductLine, "dummy");

        var dummy = Field<PluginShellSession>(main, "_pluginShell");
        Assert.IsTrue(main.InvokePluginNavigation(dummy, dummy.Navigation.Single()));
        await Field<Task>(main, "_selectionTask");
        Assert.AreEqual(2, access.Opened);
        Assert.IsInstanceOfType<Panel>(main.GetActiveDocumentView());

        var catalog = Field<ItemsView>(cards, "catalogView");
        catalog.FocusedRowHandle = 0;
        await cards.HandleCardActionAsync("activate", 0);
        Assert.AreEqual("Test.Lexware", selection.ActivePluginId);
        Assert.IsFalse(dummy.IsActive);
        Assert.AreEqual("Stammdaten", Field<RibbonPage>(main, "_pluginRibbonPage").Groups[0].Text);
        Assert.AreEqual("Kunden und Lieferanten", Field<List<AccordionControlElement>>(main, "_pluginNavigationItems").Single().Text);
        Assert.AreEqual(1, ribbon.Pages.Cast<RibbonPage>().Count(entry => entry.Name == "activePluginRibbon"));
        CapturePluginShell(main, workspace.Application.ProductLine, "lexware");

        await settings.StopAsync();
        await main.PrepareToCloseAsync();
        Assert.AreEqual(0, ribbon.Pages.Cast<RibbonPage>().Count(entry => entry.Name == "activePluginRibbon"));
        Assert.AreEqual(0, navigation.Elements.Cast<AccordionControlElement>().Count(entry => entry.Name == "activePluginNavigation"));
    });

    [TestMethod]
    public Task InvalidPluginShellRegistrationCannotPublishPartialNavigation() => WithWorkspace(async (workspace, services, plugin) =>
    {
        plugin.ShellRegistration = ui =>
        {
            ui.RegisterGroup(new("group", "Stammdaten"));
            ui.RegisterButton(new("search", "group", "missing-route"));
        };
        using var main = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        await main.ReloadPluginShellAsync();
        Assert.AreEqual(0, Field<List<BarButtonItem>>(main, "_pluginRibbonButtons").Count);
        Assert.AreEqual(BarItemVisibility.Always, Field<BarButtonItem>(main, "partiesItem").Visibility);
    });

    private static void CapturePluginShell(MainForm main, string edition, string plugin)
    {
        var look = DevExpress.LookAndFeel.UserLookAndFeel.Default;
        var oldSkin = look.ActiveSkinName;
        var oldPalette = look.ActiveSvgPaletteName;
        var oldFont = main.Font;
        var oldSize = main.ClientSize;
        var directory = Path.Combine(RenderOutput(), "plugin-shell");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var (skin, points, size) in new[] { ("Basic", 10F, new Size(1100, 740)), ("Office 2019 Black", 16F, new Size(880, 680)) })
            {
                using var font = new Font("Segoe UI", points);
                look.SetSkinStyle(skin);
                main.Font = font;
                main.ClientSize = size;
                main.PerformLayout();
                Application.DoEvents();
                CheckTableBounds(main);
                using var image = WawiCallPro.App.Diagnostics.UiAudit.Capture(main);
                image.Save(Path.Combine(directory, $"{edition}-{plugin}-{points:0}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        finally
        {
            main.Font = oldFont;
            main.ClientSize = oldSize;
            if (string.IsNullOrEmpty(oldPalette)) look.SetSkinStyle(oldSkin); else look.SetSkinStyle(oldSkin, oldPalette);
        }
    }

    private sealed class ShellPartyAccess : IPartySearchWorkspaceAccess
    {
        internal int Opened;
        public Task<IPluginViewHandle> OpenAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            Opened++;
            return Task.FromResult<IPluginViewHandle>(new ShellView());
        }
    }

    private sealed class ShellView : IPluginViewHandle
    {
        public Control View { get; } = new Panel();
        public ValueTask DisposeAsync() { View.Dispose(); return ValueTask.CompletedTask; }
    }
}
