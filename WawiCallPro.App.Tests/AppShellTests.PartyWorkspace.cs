using System.Collections.Immutable;
using System.Drawing.Imaging;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.App.Hosting;
using WawiCallPro.Core.Directory;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task PartyWorkspaceSearchAndMasterDataKeepCompanyAndRoleAndUseSourceFindText() => RunStaAsync(async () =>
    {
        var source = new PartySource(); using var page = new PartySearchPage(source, source, new PartyPresentation());
        CreateHandles(page); await page.ReloadAsync();
        Assert.IsNull(Field<LookUpEdit>(page, "companyEdit").EditValue);
        Field<LookUpEdit>(page, "companyEdit").EditValue = source.Company;
        Field<ComboBoxEdit>(page, "roleEdit").SelectedIndex = (int)PartyKind.Supplier;
        Field<GridView>(page, "view").ApplyFindFilter("example.test");
        await page.SearchAsync();
        Assert.AreEqual("example.test", source.Query!.Text); Assert.AreEqual(PartyKind.Supplier, source.Query.Kind);
        Assert.AreEqual(source.Company, source.Query.Company); Assert.AreEqual(1, Field<GridView>(page, "view").DataRowCount);
        Assert.IsFalse(Field<GridView>(page, "view").OptionsFind.AlwaysVisible);
        var message = Message.Create(page.Handle, 0, IntPtr.Zero, IntPtr.Zero);
        var method = typeof(PartySearchPage).GetMethod("ProcessCmdKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        method.Invoke(page, [message, Keys.Control | Keys.F]); Assert.IsTrue(Field<GridView>(page, "view").IsFindPanelVisible);
        method.Invoke(page, [message, Keys.Control | Keys.F]); Assert.IsFalse(Field<GridView>(page, "view").IsFindPanelVisible);
        Assert.AreEqual("example.test", Field<GridView>(page, "view").FindFilterText);
        await page.OpenFocusedAsync();
        Assert.AreEqual(source.Company, source.ReadKey!.Company); Assert.AreEqual(PartyKind.Supplier, source.ReadKey.Kind);
        Assert.Contains("Firma A", Field<LabelControl>(page, "detailTitle").Text);
        Assert.Contains("10115 Berlin", Field<MemoEdit>(page, "detailText").Text);
        Assert.IsTrue(Field<MemoEdit>(page, "detailText").Properties.ReadOnly);
        Assert.AreEqual("Stammdaten", Field<XtraTabControl>(page, "tabs").SelectedTabPage.Text);
        await page.StopAsync();
    });

    [TestMethod]
    public Task PartyWorkspaceRejectsForeignResultsAndDetailsAndShowsSafeFailures() => RunStaAsync(async () =>
    {
        var source = new PartySource(); using var page = new PartySearchPage(source, source, new PartyPresentation());
        CreateHandles(page); await page.ReloadAsync(); Field<LookUpEdit>(page, "companyEdit").EditValue = source.Company;
        Field<GridView>(page, "view").ApplyFindFilter("Beispiel");
        source.Search = (query, _) => Task.FromResult(new PartySearchResult(query, [source.Row() with { Key = new(source.Other, PartyKind.Customer, "1") }]));
        await page.SearchAsync(); Assert.AreEqual(0, Field<GridView>(page, "view").DataRowCount);
        Assert.IsFalse(Field<SimpleButton>(page, "openButton").Enabled);
        source.Search = null; await page.SearchAsync();
        source.Detail = (_, _) => Task.FromResult<Party?>(source.Row() with { Key = new(source.Other, PartyKind.Customer, "1") });
        await page.OpenFocusedAsync(); Assert.IsFalse(Field<XtraTabPage>(page, "detailsTab").PageEnabled);
        source.Search = (_, _) => throw new InvalidOperationException("private-secret"); await page.SearchAsync();
        Assert.DoesNotContain("private-secret", Field<LabelControl>(page, "statusLabel").Text);
        source.Search = null; await page.SearchAsync(); Assert.AreEqual(1, Field<GridView>(page, "view").DataRowCount);
        await page.StopAsync();
    });

    [TestMethod]
    public Task PartyWorkspaceDiscardsLateSearchAfterCompanyChange() => RunStaAsync(async () =>
    {
        var source = new PartySource(); using var page = new PartySearchPage(source, source, new PartyPresentation());
        CreateHandles(page); await page.ReloadAsync(); Field<LookUpEdit>(page, "companyEdit").EditValue = source.Company;
        Field<GridView>(page, "view").ApplyFindFilter("Beispiel");
        var release = new TaskCompletionSource<PartySearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        PartySearchQuery? firstQuery = null; CancellationToken captured = default;
        source.Search = (query, token) => { firstQuery = query; captured = token; return release.Task; };
        var first = page.SearchAsync();
        Field<LookUpEdit>(page, "companyEdit").EditValue = source.Other;
        Assert.IsTrue(captured.IsCancellationRequested); source.Search = null; await page.SearchAsync();
        release.SetResult(new(firstQuery!, [source.Row()])); await first;
        await page.OpenFocusedAsync(); Assert.AreEqual(source.Other, source.ReadKey!.Company);
        Assert.Contains("Firma B", Field<LabelControl>(page, "detailTitle").Text); await page.StopAsync();
    });

    [TestMethod]
    [DataRow("catalog", false)]
    [DataRow("search", false)]
    [DataRow("details", false)]
    [DataRow("catalog", true)]
    [DataRow("search", true)]
    [DataRow("details", true)]
    public Task PartyWorkspaceShutdownWaitsForEachNonCooperativeLane(string lane, bool failsLate) => RunStaAsync(async () =>
    {
        var source = new PartySource(); using var page = new PartySearchPage(source, source, new PartyPresentation());
        CreateHandles(page); await page.ReloadAsync(); Field<LookUpEdit>(page, "companyEdit").EditValue = source.Company;
        Field<GridView>(page, "view").ApplyFindFilter("Beispiel"); await page.SearchAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); CancellationToken captured = default;
        if (lane == "catalog") source.Catalog = async token => { captured = token; await release.Task; return [new(source.Company, "Firma A")]; };
        if (lane == "search") source.Search = async (query, token) => { captured = token; await release.Task; return new(query, [source.Row()]); };
        if (lane == "details") source.Detail = async (key, token) => { captured = token; await release.Task; return source.Row(); };
        var pending = lane == "catalog" ? page.ReloadAsync() : lane == "search" ? page.SearchAsync() : page.OpenFocusedAsync();
        var stop = page.StopAsync();
        try { Assert.IsTrue(captured.IsCancellationRequested); Assert.IsFalse(stop.IsCompleted); }
        finally
        {
            if (failsLate) release.SetException(new IOException("private-late-error")); else release.SetResult();
            await stop; await pending;
        }
        Assert.IsFalse(Field<System.Windows.Forms.TableLayoutPanel>(page, "actions").Enabled);
    });

    [TestMethod]
    public Task PartyWorkspaceIsOwnedByPluginAndReusedInShellWithoutChangingGlobalCompany() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var factory = new PartySearchWorkspaceFactory(workspace.Directory,
            new SearchAdapter(workspace.Directory), new PartyPresentation(), workspace.Dispatcher);
        var access = new PartySearchWorkspaceAccess(workspace.Plugins, factory, workspace.Dispatcher);
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>(),
            new LogWindowService(), new AudioCatalogDouble(), new UnconfiguredMailViewSource(), access);
        form.PrepareForBackgroundRun(); form.Show(); form.Navigate("parties");
        await Field<Task>(form, "_selectionTask");
        var page = (PartySearchPage)form.GetActiveDocumentView()!; await page.ReloadAsync();
        form.Navigate("parties"); Assert.AreSame(page, form.GetActiveDocumentView());
        Assert.IsNotNull(Field<DevExpress.XtraBars.BarButtonItem>(form, "partiesItem").ImageOptions.SvgImage);
        Assert.IsNull(workspace.Companies.Current.Company);
        await form.PrepareToCloseAsync(); Assert.IsTrue(page.IsDisposed);
    });

    [TestMethod]
    public Task PartyWorkspacePendingCreationIsCancelledAndDisposedBeforeShellShutdown() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var access = new PendingPartyAccess();
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>(),
            new LogWindowService(), new AudioCatalogDouble(), new UnconfiguredMailViewSource(), access);
        form.PrepareForBackgroundRun(); form.Show(); form.Navigate("parties");
        var stop = form.PrepareToCloseAsync();
        Assert.IsTrue(access.Token.IsCancellationRequested); Assert.IsFalse(stop.IsCompleted);
        var factory = new PartySearchWorkspaceFactory(workspace.Directory, new SearchAdapter(workspace.Directory), new PartyPresentation(), workspace.Dispatcher);
        var handle = (IPluginViewHandle)factory.Create(); var page = (PartySearchPage)handle.View;
        access.Release.SetResult(handle); await stop;
        Assert.IsTrue(page.IsDisposed); Assert.IsFalse(form.GetActiveDocumentView() is PartySearchPage);
    });

    [TestMethod]
    public Task PartyWorkspaceRibbonAndNavigationRenderWithCurrentFontsAndSkins() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.Combine(".artifacts", "party-search-ui", workspace.Application.ProductLine); Directory.CreateDirectory(output);
        try
        {
            foreach (var (width, fontSize, scale, skin) in new[] { (1240, 10F, 1F, "Basic"), (760, 16F, 1F, "Office 2019 Black"), (1240, 10F, 1.5F, "Basic") })
            {
                if (skin == "Basic") DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin, "Blue Dark"); else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
                form.PrepareForBackgroundRun(); form.Font = font; form.Show(); Application.DoEvents();
                form.MinimumSize = Size.Empty; form.ClientSize = new(width, 760); form.PerformLayout(); Application.DoEvents();
                if (scale != 1F) form.Scale(new SizeF(scale, scale));
                form.PerformLayout(); Application.DoEvents();
                Assert.IsTrue(Field<DevExpress.XtraBars.BarButtonItem>(form, "partiesItem").Enabled);
                using var shot = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                shot.Save(Path.Combine(output, $"shell-{width}-{fontSize:0}-{scale:0.0}-{skin}.png"), ImageFormat.Png);
                await form.PrepareToCloseAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin); else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });

    [TestMethod]
    public Task PartyWorkspaceRendersFilledEmptyErrorAndDetailsWithoutVisibleWindows() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        var line = WawiCallPro.Utilities.Hosting.ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly).ProductLine;
        var output = Path.Combine(".artifacts", "party-search-ui", line); Directory.CreateDirectory(output);
        try
        {
            foreach (var state in new[] { "filled", "empty", "failure", "details" })
            foreach (var (width, fontSize, scale, skin) in new[] { (980, 10F, 1F, "Basic"), (640, 16F, 1F, "Office 2019 Black"), (980, 10F, 1.5F, "Basic") })
            {
                if (skin == "Basic") DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin, "Blue Dark"); else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                var source = new PartySource(); using var page = new PartySearchPage(source, source, new PartyPresentation()) { Font = font, Size = new(width, 650) };
                using var host = new MainForm(); host.PrepareForBackgroundRun(); host.Controls.Add(page); host.Show();
                page.BringToFront(); page.Size = new(width, 650); CreateHandles(page); if (scale != 1F) page.Scale(new SizeF(scale, scale));
                await page.ReloadAsync(); Field<LookUpEdit>(page, "companyEdit").EditValue = source.Company;
                var view = Field<GridView>(page, "view"); view.ShowFindPanel(); view.ApplyFindFilter("Beispiel");
                if (state == "empty") source.Search = (query, _) => Task.FromResult(new PartySearchResult(query, []));
                if (state == "failure") source.Search = (_, _) => throw new IOException("private-network-failure");
                await page.SearchAsync(); if (state == "details") await page.OpenFocusedAsync();
                page.PerformLayout(); Application.DoEvents(); CheckTableBounds(page);
                Assert.IsTrue(Field<SimpleButton>(page, "openButton").Right <= Field<System.Windows.Forms.TableLayoutPanel>(page, "actions").ClientSize.Width);
                using var shot = new Bitmap(page.Width, page.Height);
                page.DrawToBitmap(shot, new Rectangle(Point.Empty, page.Size));
                shot.Save(Path.Combine(output, $"party-{state}-{width}-{fontSize:0}-{scale:0.0}-{skin}.png"), ImageFormat.Png);
                await page.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin); else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });

    private sealed class PartyPresentation : IPartySearchPresentation
    {
        public void SizeButtons(params SimpleButton[] buttons) => UiButtonSizing.Apply(buttons);
        public void Restore(GridView view) { }
        public void Save(GridView view) { }
    }
    private sealed class PendingPartyAccess : IPartySearchWorkspaceAccess
    {
        internal CancellationToken Token;
        internal TaskCompletionSource<IPluginViewHandle> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IPluginViewHandle> OpenAsync(CancellationToken cancellationToken = default) { Token = cancellationToken; return Release.Task; }
    }
    private sealed class SearchAdapter(IBusinessDirectory directory) : IPartySearch
    { public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default) => new PartySearchReader().ReadAsync(directory, query, cancellationToken); }
    private sealed class PartySource : IBusinessDirectory, IPartySearch
    {
        internal readonly CompanyKey Company = new(LexwareProductLine.Pro, "fixture-A", "1");
        internal readonly CompanyKey Other = new(LexwareProductLine.FinOffice, "fixture-B", "1");
        internal PartySearchQuery? Query; internal PartyKey? ReadKey;
        internal Func<CancellationToken, Task<ImmutableArray<Company>>>? Catalog;
        internal Func<PartySearchQuery, CancellationToken, Task<PartySearchResult>>? Search;
        internal Func<PartyKey, CancellationToken, Task<Party?>>? Detail;
        internal Party Row(CompanyKey? company = null, PartyKind kind = PartyKind.Customer) => new()
        {
            Key = new(company ?? Company, kind, "7", kind is PartyKind.CustomerContact or PartyKind.SupplierContact ? "1" : null),
            DisplayName = "Beispielunternehmen mit längerem Namen", Number = "0007", Email = "kontakt@example.test",
            Addresses = [new() { ExternalId = "1", Kind = AddressKind.Billing, Street = "Beispielstraße", HouseNumber = "7", PostalCode = "10115", City = "Berlin", Country = "Deutschland" }]
        };
        public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default) => Catalog?.Invoke(cancellationToken) ?? Task.FromResult(ImmutableArray.Create(new Company(Company, "Firma A"), new Company(Other, "Firma B")));
        public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default)
        { Query = query; return Search?.Invoke(query, cancellationToken) ?? Task.FromResult(new PartySearchResult(query, [Row(query.Company, query.Kind ?? PartyKind.Customer)])); }
        public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default)
        { ReadKey = key; return Detail?.Invoke(key, cancellationToken) ?? Task.FromResult<Party?>(Row(key.Company, key.Kind)); }
        public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
