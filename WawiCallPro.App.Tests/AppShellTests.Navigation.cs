using System.Drawing.Imaging;
using DevExpress.Utils;
using DevExpress.XtraBars;
using DevExpress.XtraBars.Docking2010.Views;
using DevExpress.XtraBars.Ribbon;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task ViewHistoryQuickAccessArrowsNavigateRealTabsAndManualActivationWithoutOpeningDuplicates() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show();
        var back = Field<BarButtonItem>(form, "navigateBackItem"); var forward = Field<BarButtonItem>(form, "navigateForwardItem");
        var ribbon = Field<RibbonControl>(form, "ribbon");
        Assert.AreEqual(RibbonQuickAccessToolbarLocation.Above, ribbon.ToolbarLocation); Assert.AreSame(back, ribbon.Toolbar.ItemLinks[0].Item); Assert.AreSame(forward, ribbon.Toolbar.ItemLinks[1].Item);
        Assert.IsFalse(back.Enabled); Assert.IsFalse(forward.Enabled); Assert.IsNotNull(back.ImageOptions.SvgImage); Assert.IsNotNull(forward.ImageOptions.SvgImage);
        var home = form.GetActiveDocumentView(); form.Navigate("history"); var history = form.GetActiveDocumentView(); form.Navigate("mail"); var mail = form.GetActiveDocumentView();
        Assert.IsTrue(back.Enabled); Assert.IsFalse(forward.Enabled);
        back.PerformClick(); Assert.AreSame(history, form.GetActiveDocumentView()); Assert.IsTrue(forward.Enabled);
        back.PerformClick(); Assert.AreSame(home, form.GetActiveDocumentView()); Assert.IsFalse(back.Enabled);
        forward.PerformClick(); Assert.AreSame(history, form.GetActiveDocumentView()); forward.PerformClick(); Assert.AreSame(mail, form.GetActiveDocumentView());
        var documents = Field<Dictionary<string, BaseDocument>>(form, "_documents"); var view = Field<DevExpress.XtraBars.Docking2010.Views.Tabbed.TabbedView>(form, "tabbedView");
        view.Controller.Activate(documents["home"]); Assert.AreSame(home, form.GetActiveDocumentView());
        back.PerformClick(); Assert.AreSame(mail, form.GetActiveDocumentView()); Assert.AreEqual(3, documents.Count);
        await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task ViewHistorySkipsClosedTabsAndANewVisitReplacesTheForwardBranch() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); var home = form.GetActiveDocumentView();
        form.Navigate("history"); var history = form.GetActiveDocumentView(); form.Navigate("mail"); var mail = form.GetActiveDocumentView();
        var back = Field<BarButtonItem>(form, "navigateBackItem"); var forward = Field<BarButtonItem>(form, "navigateForwardItem");
        back.PerformClick(); form.Navigate("about"); Assert.IsFalse(forward.Enabled);
        back.PerformClick(); Assert.AreSame(history, form.GetActiveDocumentView());
        var document = Field<Dictionary<string, BaseDocument>>(form, "_documents")["history"];
        var close = typeof(MainForm).GetMethod("CloseDocumentAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        await (Task)close.Invoke(form, [document])!;
        Assert.IsTrue(history!.IsDisposed);
        // Whichever native neighbor receives focus, no traversal may return the disposed view.
        for (var count = 0; count < 8 && back.Enabled; count++) { back.PerformClick(); Assert.AreNotSame(history, form.GetActiveDocumentView()); }
        for (var count = 0; count < 8 && forward.Enabled; count++) { forward.PerformClick(); Assert.AreNotSame(history, form.GetActiveDocumentView()); }
        Assert.IsFalse(mail!.IsDisposed); Assert.IsFalse(home!.IsDisposed); await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task ViewHistoryDoesNotRecordUnvisitedBackgroundCallTabs() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        var snapshot = await workspace.Settings.LoadAsync(); await workspace.Settings.SaveAsync(snapshot.Options with { User = snapshot.Options.User with { ShowCallNotifications = false } }, snapshot);
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); var home = form.GetActiveDocumentView();
        var call = await CreateWorkspaceCallAsync(services); await form.ObserveCallAsync(call);
        var back = Field<BarButtonItem>(form, "navigateBackItem"); var forward = Field<BarButtonItem>(form, "navigateForwardItem");
        Assert.AreSame(home, form.GetActiveDocumentView()); Assert.IsFalse(back.Enabled); Assert.IsFalse(forward.Enabled);
        var callPage = Field<Dictionary<Guid, (BaseDocument Document, CallDetailsPage Page)>>(form, "_callPages")[call.Id];
        Field<DevExpress.XtraBars.Docking2010.Views.Tabbed.TabbedView>(form, "tabbedView").Controller.Activate(callPage.Document);
        Assert.AreSame(callPage.Page, form.GetActiveDocumentView()); Assert.IsTrue(back.Enabled);
        back.PerformClick(); Assert.AreSame(home, form.GetActiveDocumentView()); forward.PerformClick(); Assert.AreSame(callPage.Page, form.GetActiveDocumentView());
        await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task ViewHistoryArrowsRenderInTheRibbonTitleBarAtDifferentFontsDpiAndSkins() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply(); var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var look = DevExpress.LookAndFeel.UserLookAndFeel.Default; var oldSkin = look.ActiveSkinName; var oldPalette = look.ActiveSvgPaletteName;
        var line = ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly).ProductLine;
        var output = Path.Combine(".artifacts", "view-history", line); Directory.CreateDirectory(output);
        try
        {
            foreach (var (width, fontSize, scale, skin) in new[] { (1240, 10F, 1F, "Basic"), (760, 16F, 1F, "Office 2019 Black"), (1240, 10F, 1.5F, "Basic") })
            {
                look.SetSkinStyle(skin); using var font = new Font("Segoe UI", fontSize); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new MainForm(); form.PrepareForBackgroundRun(); form.Font = font; form.MinimumSize = Size.Empty; form.ClientSize = new(width, 720); form.Show();
                form.Navigate("about"); Field<BarButtonItem>(form, "navigateBackItem").PerformClick();
                if (scale != 1F) form.Scale(new SizeF(scale, scale)); form.PerformLayout(); Application.DoEvents();
                Assert.IsFalse(Field<BarButtonItem>(form, "navigateBackItem").Enabled); Assert.IsTrue(Field<BarButtonItem>(form, "navigateForwardItem").Enabled);
                Assert.AreEqual(0D, form.Opacity); Assert.IsFalse(form.ShowInTaskbar);
                using var shot = WawiCallPro.App.Diagnostics.UiAudit.Capture(form); shot.Save(Path.Combine(output, $"navigation-{width}-{fontSize:0}-{scale:0.0}-{skin}.png"), ImageFormat.Png);
                await form.PrepareToCloseAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) look.SetSkinStyle(oldSkin); else look.SetSkinStyle(oldSkin, oldPalette);
        }
    });
}
