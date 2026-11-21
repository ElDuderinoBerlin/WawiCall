using System.Collections.Immutable;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Automation;
using WawiCallPro.Core.Directory;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AutomationRecordEntryRendersAcrossFontDpiAndSkins() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply();
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearanceFont = DevExpress.Utils.AppearanceObject.DefaultFont;
        var line = WawiCallPro.Utilities.Hosting.ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly).ProductLine;
        var output = Path.Combine(RenderOutput(), "com-views", line); Directory.CreateDirectory(output);
        try
        {
            foreach (var (width, size, scale, skin) in new[] { (980, 10F, 1F, "Basic"), (640, 16F, 1F, "Office 2019 Black"), (980, 10F, 1.5F, "Basic") })
            {
                if (skin == "Basic") DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin, "Blue Dark");
                else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = font;
                var source = new PartySource();
                using var page = new PartySearchPage(source, source, new PartyPresentation()) { Font = font, Size = new(width, 650) };
                using var host = new MainForm(); host.PrepareForBackgroundRun(); host.Controls.Add(page); host.Show();
                page.BringToFront(); page.Font = font; page.Size = new(width, 650); CreateHandles(page);
                await page.ShowRecordAsync(source.Row().Key, "0007", default);
                if (scale != 1F) page.Scale(new SizeF(scale, scale));
                page.PerformLayout(); CheckTableBounds(page);
                Assert.AreEqual(source.Row().Key, page.DisplayedParty);
                Assert.IsTrue(Field<MemoEdit>(page, "detailText").Properties.ReadOnly);
                Assert.AreEqual(size, page.Font.Size);
                Assert.AreEqual(size, Field<MemoEdit>(page, "detailText").Font.Size);
                using var shot = new Bitmap(page.Width, page.Height); page.DrawToBitmap(shot, new Rectangle(Point.Empty, page.Size));
                shot.Save(Path.Combine(output, $"record-{width}-{size:0}-{scale:0.0}-{skin}.png"), System.Drawing.Imaging.ImageFormat.Png);
                await page.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; DevExpress.Utils.AppearanceObject.DefaultFont = oldAppearanceFont;
            if (string.IsNullOrEmpty(oldPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });
    [TestMethod]
    public Task AutomationSavedCallReusesHistoricalContextAndKeepsNoteDraft() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        var call = await CreateWorkspaceCallAsync(services);
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show();
        var context = System.Text.Json.JsonSerializer.Serialize(new { callId = call.Id.ToString("D") });
        await form.PresentAutomationAsync(new("call", ContextJson: context), default);
        var documents = Field<Dictionary<string, DevExpress.XtraBars.Docking2010.Views.BaseDocument>>(form, "_documents");
        var page = ((Panel)documents["call:" + call.Id.ToString("D")].Control).Controls.OfType<HistoryPage>().Single();
        Field<MemoEdit>(page, "noteEdit").Text = "COM-Entwurf erhalten";
        await form.PresentAutomationAsync(new("history"), default);
        await form.PresentAutomationAsync(new("call", ContextJson: context), default);
        Assert.AreEqual("COM-Entwurf erhalten", Field<MemoEdit>(page, "noteEdit").Text);
        Assert.AreEqual(1, documents.Keys.Count(id => id.StartsWith("call:", StringComparison.Ordinal)));
        await Assert.ThrowsAsync<UnsavedCallNotesException>(() => form.PrepareToCloseAsync());
        Field<MemoEdit>(page, "noteEdit").Text = "";
        var missing = System.Text.Json.JsonSerializer.Serialize(new { callId = Guid.NewGuid().ToString("D") });
        var error = await Assert.ThrowsAsync<AutomationFault>(() => form.PresentAutomationAsync(new("call", ContextJson: missing), default));
        Assert.AreEqual(AutomationFailure.NotFound, error.Failure);
        await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task AutomationCatalogRejectsStalePluginAndUnavailableBusinessAccess() => WithWorkspace(async (workspace, services, plugin) =>
    {
        plugin.ProductLine = Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine);
        var state = new AppAutomationState(workspace.Application, TimeProvider.System);
        await using var catalog = new AppAutomationCatalog(workspace.Plugins, workspace.Directory, workspace.Dispatcher, state, new LogWindowService());
        await catalog.StartAsync(default); state.Ready();
        Assert.AreEqual(2, state.GetCompanies().Length);
        Assert.IsTrue(state.GetViews().Any(view => view.Id == "plugin:orders"));
        ((TestRuntime)workspace.Plugins).BusinessAvailable = false;
        Assert.Throws<AutomationFault>(() => state.RequireAccess(AutomationAccess.Business));
        Assert.IsFalse(state.GetViews().Any(view => view.Id.StartsWith("plugin:", StringComparison.Ordinal)));
        ((TestRuntime)workspace.Plugins).BusinessAvailable = true;
        await ((TestRuntime)workspace.Plugins).SwitchAsync("Test.Dummy");
        Assert.Throws<AutomationFault>(() => state.RequireAccess(AutomationAccess.Business));
        await catalog.RefreshAsync(default);
        state.RequireAccess(AutomationAccess.Business);
        await catalog.StopAsync(default);
    });
    [TestMethod]
    public Task AutomationCustomerSupplierViewsPreserveCompanyRoleNumbersAndExistingEdits() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var source = (TestDirectory)workspace.Directory;
        var companies = await source.GetCompaniesAsync(); plugin.ProductLine = companies[0].Key.ProductLine;
        var rows = new[]
        {
            new Party { Key = new(companies[0].Key, PartyKind.Customer, "c-a"), Number = "00123", DisplayName = "Müller 東京" },
            new Party { Key = new(companies[0].Key, PartyKind.Supplier, "s-a"), Number = "00123", DisplayName = "Lieferant A" },
            new Party { Key = new(companies[1].Key, PartyKind.Customer, "c-b"), Number = "00123", DisplayName = "Kunde B" },
            new Party { Key = new(companies[0].Key, PartyKind.Customer, "d1"), Number = "00777", DisplayName = "Doppelt 1" },
            new Party { Key = new(companies[0].Key, PartyKind.Customer, "d2"), Number = "00777", DisplayName = "Doppelt 2" }
        };
        source.Read = query => Task.FromResult(new PartyPage(query.Company, new(rows.Where(row => row.Key.Company == query.Company
            && (query.Kind is null || row.Key.Kind == query.Kind) && row.Number!.Contains(query.SearchText ?? "", StringComparison.Ordinal)).ToImmutableArray(), false)));
        source.ReadParty = (key, _) => Task.FromResult(rows.SingleOrDefault(row => row.Key == key));
        var search = new AutomationSearchSource(source);
        var access = new PartySearchWorkspaceAccess(workspace.Plugins, new PartySearchWorkspaceFactory(source, search,
            new PartyPresentation(), workspace.Dispatcher), workspace.Dispatcher);
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>(),
            new LogWindowService(), new AudioCatalogDouble(), services.GetRequiredService<WawiCallPro.Interfaces.Office.IMailViewSource>(), access);
        form.PrepareForBackgroundRun(); form.Show();
        await workspace.Companies.SelectAsync(companies[0]);
        var state = new AppAutomationState(workspace.Application, TimeProvider.System);
        await using var catalog = new AppAutomationCatalog(workspace.Plugins, source, workspace.Dispatcher, state, new LogWindowService());
        await catalog.StartAsync(default); state.Ready();
        var registrations = new ServiceCollection();
        registrations.AddSingleton<IAutomationEnvironment>(state); registrations.AddSingleton<IPartySearch>(search);
        registrations.AddSingleton<IAutomationTelephony, PendingAutomationAdapters>();
        registrations.AddSingleton<IAutomationPresentation>(new DispatchedAutomationPresentation(workspace.Dispatcher, new AutomationTestViewHost(form), state));
        registrations.AddWawiAutomation();
        await using var automation = registrations.BuildServiceProvider();
        var app = automation.GetRequiredService<IAutomationApplicationFactory>().Create();
        using var json = System.Text.Json.JsonDocument.Parse(app.GetCompaniesJson());
        var keys = json.RootElement.GetProperty("Items").EnumerateArray().Select(item => item.GetProperty("Key").GetString()!).ToArray();
        Assert.IsTrue(app.GetViewsJson().Contains("plugin:orders", StringComparison.Ordinal));
        Assert.AreEqual("Succeeded", (await AutomationDone(app, app.ShowCustomer(keys[0], "00123"))).State);
        Assert.AreEqual("Succeeded", (await AutomationDone(app, app.ShowSupplier(keys[0], "00123"))).State);
        Assert.AreEqual("Succeeded", (await AutomationDone(app, app.ShowCustomer(keys[1], "00123"))).State);
        var displayed = Field<Dictionary<DevExpress.XtraBars.Docking2010.Views.BaseDocument, IPluginViewHandle>>(form, "_pluginPages")
            .Values.Select(handle => handle.View).OfType<PartySearchPage>().Select(page => page.DisplayedParty).ToArray();
        CollectionAssert.AreEquivalent(rows.Take(3).Select(row => row.Key).ToArray(), displayed);
        Assert.AreEqual(companies[0].Key, workspace.Companies.Current.Company!.Key);
        Assert.AreEqual("NotFound", (await AutomationDone(app, app.ShowCustomer(keys[0], "123"))).ErrorCode);
        Assert.AreEqual("Ambiguous", (await AutomationDone(app, app.ShowCustomer(keys[0], "00777"))).ErrorCode);
        Assert.AreEqual("Succeeded", (await AutomationDone(app, app.ShowCustomer(keys[0], "00123"))).State);
        Assert.AreEqual(3, Field<Dictionary<DevExpress.XtraBars.Docking2010.Views.BaseDocument, IPluginViewHandle>>(form, "_pluginPages").Count);
        var pages = Field<Dictionary<DevExpress.XtraBars.Docking2010.Views.BaseDocument, IPluginViewHandle>>(form, "_pluginPages");
        var changedPage = pages.Values.Select(handle => handle.View).OfType<PartySearchPage>().Single(page => page.DisplayedParty == rows[0].Key);
        await changedPage.ShowRecordAsync(rows[2].Key, "00123", default);
        Assert.AreEqual("Succeeded", (await AutomationDone(app, app.ShowCustomer(keys[0], "00123"))).State);
        Assert.AreEqual(rows[2].Key, changedPage.DisplayedParty, "An already redirected workspace must remain intact.");
        Assert.AreEqual(4, pages.Count);
        Assert.AreEqual(1, pages.Values.Select(handle => handle.View).OfType<PartySearchPage>().Count(page => page.DisplayedParty == rows[0].Key));
        var context = System.Text.Json.JsonSerializer.Serialize(new { companyKey = keys[0], customerNumber = "00123" });
        Assert.AreEqual("Succeeded", (await AutomationDone(app, app.ShowView("plugin:orders", context))).State);
        Assert.AreEqual(rows[0].Key, plugin.CreatedParties.Last());
        plugin.CreatedControls.Last().Tag = true;
        Assert.AreEqual("Succeeded", (await AutomationDone(app, app.ShowView("mail", ""))).State);
        var view = Field<DevExpress.XtraBars.Docking2010.Views.Tabbed.TabbedView>(form, "tabbedView");
        var active = view.ActiveDocument;
        Assert.AreEqual("Succeeded", (await AutomationDone(app, app.Show())).State);
        Assert.AreSame(active, view.ActiveDocument, "Show activates the window without navigating away from the current view.");
        await Assert.ThrowsAsync<UnsavedPluginChangesException>(() => form.PrepareToCloseAsync());
        foreach (var control in plugin.CreatedControls) control.Tag = false;
        await catalog.StopAsync(default); await form.PrepareToCloseAsync();
        Assert.AreEqual(0, Field<Dictionary<(PartyKey Party, string View), DevExpress.XtraBars.Docking2010.Views.BaseDocument>>(form, "_automationParties").Count);
    });

    [TestMethod]
    public Task AutomationModalDialogsReturnAfterShownAndRejectConflictingViews() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show();
        var shown = form.PresentAutomationAsync(new("settings-mail"), default);
        using var timer = new System.Windows.Forms.Timer { Interval = 200 };
        var verified = false; Task verification = Task.CompletedTask;
        timer.Tick += (_, _) => { timer.Stop(); verification = VerifyAsync(); };
        async Task VerifyAsync()
        {
            try
            {
                Assert.IsTrue(shown.IsCompletedSuccessfully);
                var dialog = form.OwnedForms.OfType<FrmSettings>().Single();
                Assert.AreSame(form, dialog.Owner); Assert.IsTrue(dialog.Modal);
                Assert.AreEqual("Office / Mail", dialog.SettingsTabs.SelectedTabPage.Text);
                var denied = await Assert.ThrowsAsync<AutomationFault>(() => form.PresentAutomationAsync(new("history"), default));
                Assert.AreEqual(AutomationFailure.Busy, denied.Failure);
                await form.PresentAutomationAsync(new("settings-hotkeys"), default);
                Assert.AreEqual("Tastenkürzel", dialog.SettingsTabs.SelectedTabPage.Text); verified = true;
            }
            finally { foreach (var dialog in form.OwnedForms.ToArray()) dialog.Close(); }
        }
        timer.Start(); await shown;
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (form.OwnedForms.Length > 0 && DateTime.UtcNow < deadline) await Task.Delay(20);
        await verification;
        Assert.IsTrue(verified); Assert.AreEqual(0, form.OwnedForms.Length);
        await form.PrepareToCloseAsync();
    });

    private static async Task<WawiCallPro.Automation.Contracts.IOperationV1> AutomationDone(WawiCallPro.Automation.Contracts.IApplicationV1 app, string id)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var result = app.GetOperation(id);
            if (result.State is "Succeeded" or "Failed" or "Canceled") return result;
            await Task.Delay(10);
        }
        throw new TimeoutException("Automation did not finish.");
    }
    private sealed class AutomationSearchSource(IBusinessDirectory source) : IPartySearch
    {
        public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default) => new PartySearchReader().ReadAsync(source, query, cancellationToken);
    }
    private sealed class AutomationTestViewHost(MainForm form) : IAutomationViewHost
    {
        public Task ShowAsync(PresentationRequest request, CancellationToken cancellationToken) => form.PresentAutomationAsync(request, cancellationToken);
    }
    [TestMethod]
    public Task AutomationDatabaseModalClosesAfterOneRequest() => RunStaAsync(() =>
    {
        using var owner = new MainForm(); owner.PrepareForBackgroundRun(); owner.Show();
        using var dialog = new FrmDatabaseSetup(); dialog.PrepareForBackgroundRun();
        using var timeout = new System.Windows.Forms.Timer { Interval = 2000 };
        var repeated = false;
        timeout.Tick += (_, _) => { repeated = true; dialog.Close(); };
        dialog.Shown += (_, _) => { timeout.Start(); dialog.BeginInvoke(new Action(dialog.Close)); };
        dialog.ShowDialog(owner); timeout.Stop();
        Assert.IsFalse(repeated, "The first close must finish after the pending FormClosing handler returns.");
        return Task.CompletedTask;
    });
}
