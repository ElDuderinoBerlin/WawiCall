using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using System.Collections.Immutable;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
#if !LX_PRO
    [TestMethod]
    public Task PluginCompanyFailureReachesTheHostAsAClosedCode() => WithWorkspace(async (workspace, _, plugin) =>
    {
        ((TestDirectory)workspace.Directory).ReadCatalog = _ => throw new PrivateCompanyFailure();
        plugin.SettingsFactory = context => new PluginCompanySettingsView(workspace.Directory,
            Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine), context);
        using var page = new SettingsPage(); page.Bind(workspace); await page.ReloadAsync();
        var structured = string.Join("\n", Directory.GetFiles(workspace.Paths.LogsDirectory, "*.jsonl").Select(File.ReadAllText));
        Assert.Contains("SettingsCompaniesClientKeyUnknown", structured);
        Assert.DoesNotContain("synthetic-private-password-token-key", structured);
        Assert.IsNull(workspace.Companies.Current.Company);
        await page.StopAsync();
    });
    private sealed class PrivateCompanyFailure() : Exception("synthetic-private-password-token-key"), IPluginCompanyFailure
    {
        public PluginCompanyFailure CompanyFailure => PluginCompanyFailure.ClientKeyUnknown;
    }

    [TestMethod]
    public Task RealFinOfficeSettingsFactoryExportsWholeUserControlWithoutOpeningSource() => WithWorkspace(async (workspace, services, _) =>
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "WawiCallProSolution.sln"))) root = root.Parent;
        Assert.IsNotNull(root);
        var configuration = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyConfigurationAttribute>(typeof(MainForm).Assembly)!.Configuration;
        var directory = Path.Combine(root.FullName, "WawiCallPro.App", "bin", configuration, "net11.0-windows10.0.19041.0", "plugins", "WawiCallPro.Plugins.Lexware.FinOffice");
        await using var session = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<WawiCallPro.Utilities.Plugins.IPluginSessionFactory>(services).CreateTrusted();
        await ((WawiCallPro.Utilities.Plugins.ISelectedPluginSession)session).LoadSelectedAsync(directory);
        var provider = session.GetPlugins().Single() as IWawiCallPluginSettings;
        Assert.IsNotNull(provider); Assert.AreEqual("Lexware", provider.ConnectionSettingsTitle);
        var snapshot = await workspace.Settings.LoadAsync();
        var draft = workspace.Settings.BeginPluginConnectionEdit(snapshot, workspace.Secrets);
        var context = new PluginSettingsPageContext(draft, workspace, null);
        using var view = provider.CreateConnectionSettings(context);
        Assert.IsInstanceOfType<PluginCompanySettingsPage>(view.View);
        Assert.IsEmpty(view.View.Controls.Find("usernameEdit", true));
        Assert.IsNull(context.SelectedCompany);
        await view.StopAsync();
    });
#endif

    [TestMethod]
    public Task PluginCompanyPageCanBeStoppedAndDisposedRepeatedly() => WithWorkspace(async (workspace, _, plugin) =>
    {
        plugin.SettingsFactory = context => new PluginCompanySettingsView(workspace.Directory,
            Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine), context);
        using var page = new SettingsPage(); page.Bind(workspace); await page.ReloadAsync();
        var exported = (PluginCompanySettingsPage)Field<Control>(page, "_pluginSettingsControl");
        await page.StopAsync(); await page.StopAsync();
        exported.Dispose(); exported.Dispose(); await exported.StopAsync();
        Assert.IsTrue(exported.IsDisposed);
    });

    [TestMethod]
    [DataRow("Basic", 10F, 1F)]
    [DataRow("Office 2019 Black", 16F, 1.5F)]
    public Task PluginCompanySettingsRenderEmptyAndSelectedWithFontsSkinsAndScaling(string skin, float fontSize, float scale) => WithWorkspace(async (workspace, _, plugin) =>
    {
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = DevExpress.Utils.AppearanceObject.DefaultFont;
        try
        {
            DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
            using var font = new Font("Segoe UI", fontSize);
            WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = font;
            var line = Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine);
            var company = new Company(new(line, "fixture", "selected-company"), "Musterfirma – Niederlassung Süd");
            plugin.SettingsFactory = context => new PluginCompanySettingsView(workspace.Directory, line, context);
            plugin.ConnectionSettingsTitle = "Lexware";
            using var form = new FrmSettings(workspace); form.PrepareForBackgroundRun(); form.Font = font; form.Show();
            var page = Field<SettingsPage>(form, "settingsPage"); await page.ReloadAsync(); form.SelectAutomationTab("settings-wawi");
            var exported = Field<Control>(page, "_pluginSettingsControl");
            var view = Field<IPluginSettingsHandle>(page, "_pluginSettings");
            if (scale != 1F) form.Scale(new SizeF(scale, scale));
            foreach (var filled in new[] { false, true })
            {
                ((TestDirectory)workspace.Directory).ReadCatalog = _ => Task.FromResult(filled ? ImmutableArray.Create(company) : ImmutableArray<Company>.Empty);
                await view.LoadAsync();
                if (filled) { CreateHandles(exported); FindCompanyView(exported).SelectRow(0); }
                form.PerformLayout(); Application.DoEvents(); CheckTableBounds(exported);
                Assert.AreEqual(Field<SimpleButton>(page, "saveButton").Height, Find<SimpleButton>(exported, "refreshButton").Height,
                    "Plugin buttons use the host's compact skin/DPI sizing after scaling.");
                var output = Path.Combine(RenderOutput(), "plugin-companies"); Directory.CreateDirectory(output);
                using var image = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                image.Save(Path.Combine(output, $"{skin}-{fontSize}-{scale}-{filled}.png"));
                Assert.IsNull(workspace.Companies.Current.Company);
            }
            await form.StopAsync();
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; DevExpress.Utils.AppearanceObject.DefaultFont = oldAppearance;
            DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
        }
    });

    [TestMethod]
    public Task PluginCompanyMultiSelectionPersistsScopeWithoutChangingActiveCompany() => WithWorkspace(async (workspace, _, plugin) =>
    {
        var line = Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine);
        plugin.ConnectionSettingsTitle = "Lexware";
        plugin.SettingsFactory = context => new PluginCompanySettingsView(workspace.Directory, line, context);
        using var form = new FrmSettings(workspace); form.PrepareForBackgroundRun(); form.Show();
        var page = Field<SettingsPage>(form, "settingsPage"); await page.ReloadAsync();
        form.SelectAutomationTab("settings-wawi");
        Assert.AreEqual("Lexware", form.SettingsTabs.SelectedTabPage.Text);
        var exported = Field<Control>(page, "_pluginSettingsControl");
        Assert.IsInstanceOfType<PluginCompanySettingsPage>(exported);
        Assert.AreEqual(2, Field<TableLayoutPanel>(page, "layout").Controls.Count);
        CreateHandles(exported);
        var view = FindCompanyView(exported);
        Assert.AreEqual(2, view.DataRowCount); Assert.AreEqual(0, view.SelectedRowsCount);
        Assert.IsTrue(view.OptionsSelection.MultiSelect);
        Assert.AreEqual(GridMultiSelectMode.CheckBoxRowSelect, view.OptionsSelection.MultiSelectMode);
        Assert.HasCount(1, view.Columns); Assert.AreEqual("Name", view.Columns[0].FieldName);
        view.SelectRow(0); view.SelectRow(1);
        view.FindFilterText = "Firma A"; Application.DoEvents();
        Assert.AreEqual(2, Field<PluginSettingsPageContext>(page, "_pluginPageContext").SelectedCompanies.Count);
        view.FindFilterText = ""; Application.DoEvents();
        Assert.AreEqual(2, view.SelectedRowsCount);
        Assert.IsNull(workspace.Companies.Current.Company);
        Assert.IsEmpty((await workspace.Settings.LoadAsync()).Options.Companies);
        await page.SaveAsync(CancellationToken.None);
        var selected = (await workspace.Settings.LoadAsync()).Options.Companies.Where(item => item.IncludeInPhoneCache).ToArray();
        Assert.HasCount(2, selected); Assert.IsNull(workspace.Companies.Current.Company);
        Assert.IsNull((await workspace.Settings.LoadAsync()).Options.User.PreferredCompany);
        await page.StopAsync();
        var saved = await workspace.Settings.LoadAsync();
        var legacyPreference = new CompanyKey(line, "fixture", "unavailable-legacy-preference");
        await workspace.Settings.SaveAsync(saved.Options with { User = saved.Options.User with { PreferredCompany = legacyPreference } }, saved);
        using var reopened = new SettingsPage(); reopened.Bind(workspace); await reopened.ReloadAsync();
        var again = Field<Control>(reopened, "_pluginSettingsControl"); CreateHandles(again);
        Assert.AreEqual(2, FindCompanyView(again).SelectedRowsCount);
        typeof(PluginCompanySettingsPage).GetMethod("Clear_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(again, [null, EventArgs.Empty]);
        Assert.HasCount(2, (await workspace.Settings.LoadAsync()).Options.Companies.Where(item => item.IncludeInPhoneCache).ToArray());
        await reopened.SaveAsync(CancellationToken.None);
        Assert.IsEmpty((await workspace.Settings.LoadAsync()).Options.Companies.Where(item => item.IncludeInPhoneCache).ToArray());
        Assert.AreEqual(legacyPreference, (await workspace.Settings.LoadAsync()).Options.User.PreferredCompany);
        Assert.IsNull(workspace.Companies.Current.Company);
        await reopened.StopAsync();
    });

    [TestMethod]
    public Task PluginCompanySelectionRejectsChangedPermissionsWithoutSavingOrPublishing() => WithWorkspace(async (workspace, _, plugin) =>
    {
        var line = Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine);
        plugin.SettingsFactory = context => new PluginCompanySettingsView(workspace.Directory, line, context);
        var initial = await workspace.Settings.LoadAsync(); await workspace.Settings.SaveAsync(initial.Options, initial);
        var file = Path.Combine(workspace.Paths.RootDirectory, "appsettings.json"); var before = await File.ReadAllBytesAsync(file);
        using var page = new SettingsPage(); page.Bind(workspace); await page.ReloadAsync();
        var exported = Field<Control>(page, "_pluginSettingsControl"); CreateHandles(exported);
        FindCompanyView(exported).SelectRow(0);
        ((TestDirectory)workspace.Directory).ReadCatalog = _ => Task.FromResult(ImmutableArray<Company>.Empty);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => page.SaveAsync(CancellationToken.None));
        Assert.IsNull(workspace.Companies.Current.Company);
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(file));
        await page.StopAsync();
    });

    [TestMethod]
    public Task PluginCompanyLoadCancelsAndIsAwaitedWhenSettingsStop() => WithWorkspace(async (workspace, _, plugin) =>
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ((TestDirectory)workspace.Directory).ReadCatalog = async token =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); return []; }
            finally { if (token.IsCancellationRequested) cancelled.TrySetResult(); }
        };
        plugin.SettingsFactory = context => new PluginCompanySettingsView(workspace.Directory,
            Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine), context);
        using var page = new SettingsPage(); page.Bind(workspace);
        var loading = page.ReloadAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsFalse(loading.IsCompleted, "Source IO must remain asynchronous.");
        await page.StopAsync(); await loading;
        Assert.IsTrue(cancelled.Task.IsCompletedSuccessfully);
    });

    [TestMethod]
    [DataRow("empty")]
    [DataRow("error")]
    [DataRow("foreign")]
    [DataRow("duplicate")]
    public Task PluginCompanyLoadHandlesEmptyAndInvalidCatalogsWithoutPublishing(string mode) => WithWorkspace(async (workspace, _, plugin) =>
    {
        var line = Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine);
        ((TestDirectory)workspace.Directory).ReadCatalog = _ => mode switch
        {
            "empty" => Task.FromResult(ImmutableArray<Company>.Empty),
            "error" => throw new InvalidOperationException("synthetic-upstream-secret"),
            "foreign" => Task.FromResult(ImmutableArray.Create(new Company(new(line == LexwareProductLine.Pro ? LexwareProductLine.FinOffice : LexwareProductLine.Pro, "fixture", "1"), "Fremde Firma"))),
            _ => Task.FromResult(ImmutableArray.Create(new Company(new(line, "fixture", "1"), "Firma A"), new Company(new(line, "fixture", "1"), "Firma B")))
        };
        plugin.SettingsFactory = context => new PluginCompanySettingsView(workspace.Directory, line, context);
        using var page = new SettingsPage(); page.Bind(workspace); await page.ReloadAsync();
        var exported = Field<Control>(page, "_pluginSettingsControl");
        Assert.AreEqual(0, FindCompanyView(exported).SelectedRowsCount);
        Assert.IsFalse(Find<GridControl>(exported, "companyGrid").Enabled);
        Assert.DoesNotContain("synthetic-upstream-secret", page.LastSettingsNoticeText);
        Assert.IsEmpty(exported.Controls.Find("statusLabel", true));
        Assert.IsNull(workspace.Companies.Current.Company);
        await page.StopAsync();
    });
    [TestMethod]
    public Task PluginCompanyScopeCancelAndFailedReloadPreserveTheSavedSelection() => WithWorkspace(async (workspace, _, plugin) =>
    {
        var line = Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine);
        var companies = await workspace.Directory.GetCompaniesAsync();
        var initial = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(initial.Options with { Companies = [new() { Company = companies[0].Key, DisplayName = companies[0].DisplayName, IncludeInPhoneCache = true }] }, initial);
        plugin.SettingsFactory = context => new PluginCompanySettingsView(workspace.Directory, line, context);
        var file = Path.Combine(workspace.Paths.RootDirectory, "appsettings.json"); var before = await File.ReadAllBytesAsync(file);
        using var page = new SettingsPage(); page.Bind(workspace); await page.ReloadAsync();
        var exported = Field<Control>(page, "_pluginSettingsControl"); CreateHandles(exported);
        FindCompanyView(exported).SelectAll();
        ((TestDirectory)workspace.Directory).ReadCatalog = _ => throw new InvalidOperationException("synthetic-private-source");
        await Field<IPluginSettingsHandle>(page, "_pluginSettings").LoadAsync();
        Assert.IsFalse(Find<GridControl>(exported, "companyGrid").Enabled);
        await page.StopAsync();
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(file));
        Assert.IsNull(workspace.Companies.Current.Company);
    });

    private static GridView FindCompanyView(Control control) => (GridView)Find<GridControl>(control, "companyGrid").MainView;

}
