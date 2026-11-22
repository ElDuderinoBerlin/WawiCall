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
                if (filled) Find<LookUpEdit>(exported, "companyEdit").EditValue = company.Key;
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
    public Task PluginCompanySelectionUsesFullExportedControlAndPublishesOnlyAfterSave() => WithWorkspace(async (workspace, _, plugin) =>
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
        var lookup = Find<LookUpEdit>(exported, "companyEdit");
        var companies = (Company[])lookup.Properties.DataSource;
        Assert.HasCount(2, companies); Assert.IsNull(lookup.EditValue);
        Assert.IsFalse(lookup.Properties.ShowHeader); Assert.IsFalse(lookup.Properties.ShowFooter);
        Assert.HasCount(1, lookup.Properties.Columns);
        Assert.AreEqual("DisplayName", lookup.Properties.Columns[0].FieldName);
        lookup.EditValue = companies[1].Key;
        Assert.IsNull(workspace.Companies.Current.Company);
        Assert.IsNull((await workspace.Settings.LoadAsync()).Options.User.PreferredCompany);
        await page.SaveAsync(CancellationToken.None);
        Assert.AreEqual(companies[1].Key, workspace.Companies.Current.Company?.Key);
        Assert.AreEqual(companies[1].Key, (await workspace.Settings.LoadAsync()).Options.User.PreferredCompany);
        Assert.AreEqual(companies[1].Key, lookup.EditValue);
        await page.StopAsync();
        using var reopened = new SettingsPage(); reopened.Bind(workspace); await reopened.ReloadAsync();
        var again = Field<Control>(reopened, "_pluginSettingsControl");
        Assert.AreEqual(companies[1].Key, Find<LookUpEdit>(again, "companyEdit").EditValue);
        typeof(PluginCompanySettingsPage).GetMethod("Clear_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(again, [null, EventArgs.Empty]);
        Assert.AreEqual(companies[1].Key, workspace.Companies.Current.Company?.Key);
        await reopened.SaveAsync(CancellationToken.None);
        Assert.IsNull(workspace.Companies.Current.Company);
        Assert.IsNull((await workspace.Settings.LoadAsync()).Options.User.PreferredCompany);
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
        var lookup = Find<LookUpEdit>(Field<Control>(page, "_pluginSettingsControl"), "companyEdit");
        lookup.EditValue = ((Company[])lookup.Properties.DataSource)[0].Key;
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
        var loading = page.ReloadAsync(); await entered.Task;
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
        var lookup = Find<LookUpEdit>(exported, "companyEdit");
        Assert.IsNull(lookup.EditValue); Assert.IsFalse(lookup.Enabled);
        Assert.DoesNotContain("synthetic-upstream-secret", Find<LabelControl>(exported, "statusLabel").Text);
        Assert.IsNull(workspace.Companies.Current.Company);
        await page.StopAsync();
    });
}
