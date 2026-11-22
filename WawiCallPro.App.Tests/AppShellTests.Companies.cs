using System.Collections.Immutable;
using System.Drawing.Imaging;
using DevExpress.LookAndFeel;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task CompanySelectorLoadsThePluginCatalogAndSelectsOnlyKnownFullKeys() => WithWorkspace(async (workspace, _, _) =>
    {
        using var selector = new CompanySelector();
        selector.Bind(workspace.Directory, workspace.Companies, workspace.Plugins, Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine));
        await selector.ReloadAsync();
        var edit = Field<LookUpEdit>(selector, "companyEdit");
        Assert.AreEqual(SearchMode.AutoSearch, edit.Properties.SearchMode);
        Assert.AreEqual("DisplayName", edit.Properties.DisplayMember);
        Assert.AreEqual(1, edit.Properties.Columns.Count);
        Assert.IsFalse(edit.Properties.ShowHeader); Assert.IsFalse(edit.Properties.ShowFooter);
        Assert.IsTrue(edit.Enabled); Assert.IsNull(workspace.Companies.Current.Company);
        var companies = (Company[])edit.Properties.DataSource;
        Assert.AreEqual(2, companies.Length);
        edit.EditValue = companies[1].Key;
        await Task.Yield();
        Assert.AreEqual(companies[1], workspace.Companies.Current.Company);
        await selector.ReloadAsync();
        Assert.AreEqual(companies[1].Key, edit.EditValue);
        edit.EditValue = new CompanyKey(companies[1].Key.ProductLine, companies[1].Key.InstallationId, "foreign");
        await Task.Yield(); Assert.IsNull(workspace.Companies.Current.Company);
        await selector.StopAsync();
    });

    [TestMethod]
    [DataRow("empty")]
    [DataRow("failure")]
    [DataRow("duplicate")]
    [DataRow("foreign")]
    public Task CompanySelectorClearsStaleSelectionForEmptyFailedOrInvalidCatalogs(string mode) => WithWorkspace(async (workspace, _, _) =>
    {
        using var selector = new CompanySelector();
        var line = Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine);
        selector.Bind(workspace.Directory, workspace.Companies, workspace.Plugins, line);
        await selector.ReloadAsync();
        var edit = Field<LookUpEdit>(selector, "companyEdit");
        var source = (Company[])edit.Properties.DataSource;
        edit.EditValue = source[0].Key;
        await Task.Yield();
        Assert.IsNotNull(workspace.Companies.Current.Company);
        var directory = (TestDirectory)workspace.Directory;
        directory.ReadCatalog = _ => mode switch
        {
            "empty" => Task.FromResult(ImmutableArray<Company>.Empty),
            "duplicate" => Task.FromResult(ImmutableArray.Create(source[0], source[0])),
            "foreign" => Task.FromResult(ImmutableArray.Create(source[0] with { Key = new CompanyKey(
                line == LexwareProductLine.Pro ? LexwareProductLine.FinOffice : LexwareProductLine.Pro,
                source[0].Key.InstallationId, source[0].Key.ExternalId) })),
            _ => Task.FromException<ImmutableArray<Company>>(new InvalidOperationException("PRIVATE CONNECTION DETAILS"))
        };
        var failed = 0; selector.LoadFailed += (_, _) => failed++;
        await selector.ReloadAsync();
        Assert.IsNull(workspace.Companies.Current.Company);
        Assert.IsFalse(edit.Enabled); Assert.AreEqual(0, ((Company[])edit.Properties.DataSource).Length);
        var message = Field<LabelControl>(selector, "statusLabel").Text;
        Assert.DoesNotContain("PRIVATE", message);
        Assert.AreEqual(mode == "empty" ? 0 : 1, failed);
        Assert.IsTrue(Field<SimpleButton>(selector, "refreshButton").Enabled);
        await selector.StopAsync();
    });

    [TestMethod]
    public Task CompanySelectorCancelsAndRejectsOldPluginResultsAndAwaitsShutdown() => WithWorkspace(async (workspace, _, _) =>
    {
        using var selector = new CompanySelector();
        selector.Bind(workspace.Directory, workspace.Companies, workspace.Plugins, Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine));
        var directory = (TestDirectory)workspace.Directory;
        var initial = await directory.GetCompaniesAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ImmutableArray<Company>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        directory.ReadCatalog = token => { captured = token; entered.TrySetResult(); return release.Task; };
        var old = selector.ReloadAsync(); await entered.Task;
        await ((IProductPluginSelection)workspace.Plugins).SwitchAsync("Test.Dummy");
        directory.ReadCatalog = _ => Task.FromResult(ImmutableArray.Create(initial[1]));
        await selector.ReloadAsync();
        Assert.IsTrue(captured.IsCancellationRequested);
        release.SetResult(ImmutableArray.Create(initial[0])); await old;
        Assert.AreEqual(initial[1].Key, ((Company[])Field<LookUpEdit>(selector, "companyEdit").Properties.DataSource).Single().Key);
        var finish = new TaskCompletionSource<ImmutableArray<Company>>(TaskCreationOptions.RunContinuationsAsynchronously);
        entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        directory.ReadCatalog = token => { captured = token; entered.TrySetResult(); return finish.Task; };
        var pending = selector.ReloadAsync(); await entered.Task;
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = captured.Register(() => canceled.TrySetResult());
        var stopped = selector.StopAsync();
        Assert.IsFalse(stopped.IsCompleted);
        await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.IsTrue(captured.IsCancellationRequested);
        finish.SetResult(initial); await pending; await stopped;
    });

    [TestMethod]
    public Task CompanySelectorBlocksUnavailablePluginsBeforeQuerying() => WithWorkspace(async (workspace, _, _) =>
    {
        using var selector = new CompanySelector();
        selector.Bind(workspace.Directory, workspace.Companies, workspace.Plugins, Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine));
        ((TestRuntime)workspace.Plugins).BusinessAvailable = false;
        var reads = 0; ((TestDirectory)workspace.Directory).ReadCatalog = _ => { reads++; throw new InvalidOperationException(); };
        await selector.ReloadAsync();
        Assert.AreEqual(0, reads); Assert.IsFalse(Field<LookUpEdit>(selector, "companyEdit").Enabled);
        Assert.Contains("nicht verfügbar", Field<LabelControl>(selector, "statusLabel").Text);
        await selector.StopAsync();
    });

    [TestMethod]
    public Task MainCompanySelectionRestoresSavedKeyWithoutNavigationControlAndClearsBeforeSwitch() => WithWorkspace(async (workspace, _, _) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace,
            new AppUiState(new ApplicationDescriptor("Company selector test", "CompanySelector-" + Guid.NewGuid().ToString("N")), new StartupExecutionMode(true)));
        form.PrepareForBackgroundRun(); form.Show();
        await form.ReloadCompaniesAsync();
        Assert.IsEmpty(form.Controls.Find("companySelector", true), "The main navigation must not own product-specific company settings.");
        var companies = await workspace.Directory.GetCompaniesAsync();
        var snapshot = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(snapshot.Options with { User = snapshot.Options.User with { PreferredCompany = companies[1].Key } }, snapshot);
        await form.ReloadCompaniesAsync();
        Assert.AreEqual(companies[1].Key, workspace.Companies.Current.Company?.Key);
        await (Task)typeof(MainForm).GetMethod("PreparePluginSwitchAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(form, null)!;
        Assert.IsNull(workspace.Companies.Current.Company);
        await form.PrepareToCloseAsync();
    });

    [TestMethod]
    [DataRow("Basic", 9F, 1F)]
    [DataRow("Office 2019 Black", 16F, 1.5F)]
    public Task CompanySelectorLayoutRendersEmptyAndFilledWithDpiFontsAndSkins(string skin, float fontSize, float scale) => WithWorkspace(async (workspace, _, _) =>
    {
        var previous = UserLookAndFeel.Default.ActiveSkinName;
        var previousFont = WindowsFormsSettings.DefaultFont;
        var previousAppearance = DevExpress.Utils.AppearanceObject.DefaultFont;
        try
        {
            UserLookAndFeel.Default.SetSkinStyle(skin);
            using var font = new Font("Segoe UI", fontSize);
            WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = font;
            using var host = new XtraForm { Opacity = 0, ShowInTaskbar = false, ClientSize = new Size((int)(240 * scale), (int)(340 * scale)), Font = font };
            using var selector = new CompanySelector { Dock = DockStyle.Top, Font = font };
            selector.Bind(workspace.Directory, workspace.Companies, workspace.Plugins, Enum.Parse<LexwareProductLine>(workspace.Application.ProductLine));
            host.Controls.Add(selector); host.Show();
            if (scale != 1F) selector.Scale(new SizeF(scale, scale));
            foreach (var filled in new[] { false, true })
            {
                if (filled) await selector.ReloadAsync();
                else await selector.ResetAsync();
                host.PerformLayout(); selector.PerformLayout(); Application.DoEvents();
                CheckTableBounds(selector);
                foreach (var name in new[] { "companyEdit", "refreshButton", "statusLabel" })
                {
                    var control = Find<Control>(selector, name);
                    Assert.IsTrue(selector.ClientRectangle.Contains(selector.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), name);
                }
                var output = Path.Combine(RenderOutput(), "company-selector"); Directory.CreateDirectory(output);
                using var bitmap = new Bitmap(selector.Width, selector.Height);
                selector.DrawToBitmap(bitmap, selector.ClientRectangle);
                bitmap.Save(Path.Combine(output, $"{skin.Replace(' ', '-')}-{fontSize}-{scale}-{filled}.png"), ImageFormat.Png);
            }
            await selector.StopAsync();
            using var main = new MainForm(workspace.Application, workspace.Configuration, workspace,
                new AppUiState(new ApplicationDescriptor("Company selector render", "CompanySelectorRender-" + Guid.NewGuid().ToString("N")), new StartupExecutionMode(true)));
            main.PrepareForBackgroundRun(); main.Font = font; main.Show(); main.ClientSize = new Size(900, 700);
            await main.ReloadCompaniesAsync(); main.PerformLayout(); Application.DoEvents();
            Assert.IsEmpty(main.Controls.Find("companySelector", true));
            var container = Field<DevExpress.XtraBars.Docking.ControlContainer>(main, "navigationContainer");
            CheckTableBounds(container);
            using var mainBitmap = new Bitmap(main.ClientSize.Width, main.ClientSize.Height);
            main.DrawToBitmap(mainBitmap, main.ClientRectangle);
            mainBitmap.Save(Path.Combine(RenderOutput(), "company-selector", $"MainForm-{skin.Replace(' ', '-')}-{fontSize}.png"), ImageFormat.Png);
            await main.PrepareToCloseAsync();
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = previousFont;
            DevExpress.Utils.AppearanceObject.DefaultFont = previousAppearance;
            UserLookAndFeel.Default.SetSkinStyle(previous);
        }
    });
}
