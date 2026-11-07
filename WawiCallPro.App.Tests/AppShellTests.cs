using System.Collections.Immutable;
using System.Reflection;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraTab;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Core.Threading;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.App.Tests;

[TestClass, DoNotParallelize]
public sealed class AppShellTests
{
    [TestMethod]
    public Task SettingsDialogRendersEveryPageWithEmptyAndPopulatedDataWithoutVisibleWindows() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        try
        {
            foreach (var populated in new[] { false, true })
            foreach (var fontSize in new[] { 10F, 16F })
            foreach (var small in new[] { false, true })
            {
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new FrmSettings();
                var page = Field<SettingsPage>(form, "settingsPage");
                page.Bind(workspace);
                if (populated)
                    page.Apply(new ConfigurationSnapshot(new ApplicationOptions
                    {
                        SipAccount = new SipAccountOptions { Server = "telefonanlage.test.invalid", Username = "Telefon-Arbeitsplatz", Port = 5061, Transport = SipTransport.Tls },
                        Companies = [new CompanySettings { Company = new(LexwareProductLine.Pro, "fixture", "01"), IncludeInPhoneCache = true, DisplayName = "Musterunternehmen mit längerem Mandantennamen" }]
                    }, true));
                form.Font = font;
                _ = form.Handle;
                form.MinimumSize = Size.Empty;
                form.ClientSize = small ? new Size(720, 650) : new Size(980, 760);
                var tabs = Field<XtraTabControl>(page, "tabs");
                foreach (XtraTabPage tab in tabs.TabPages)
                {
                    tabs.SelectedTabPage = tab;
                    CreateHandles(form);
                    form.PerformLayout();
                    CheckTableBounds(form);
                    using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                    var output = RenderOutput();
                    Directory.CreateDirectory(output);
                    bitmap.Save(Path.Combine(output, $"Settings-{tab.Text}-{populated}-{fontSize:0}-{small}.png"), ImageFormat.Png);
                }
                form.Scale(new SizeF(1.5F, 1.5F));
                form.PerformLayout();
                CheckTableBounds(form);
                using var scaled = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                scaled.Save(Path.Combine(RenderOutput(), $"Settings-Telefon-Cache-{populated}-{fontSize:0}-{small}-scale150.png"), ImageFormat.Png);
                Assert.IsFalse(form.Visible);
            }
        }
        finally { WindowsFormsSettings.DefaultFont = originalFont; AppearanceObject.DefaultFont = originalAppearance; }
        await Task.CompletedTask;
    });

    private static string RenderOutput([CallerFilePath] string source = "") => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(source))!, ".artifacts", "app-shell", "settings-ui");
    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls) CreateHandles(child);
    }
    private static void CheckTableBounds(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (parent is XtraTabControl tabs && control is XtraTabPage tab && tabs.SelectedTabPage != tab) continue;
            if (parent is TableLayoutPanel { AutoScroll: false } && control is LabelControl or SimpleButton or TextEdit)
                Assert.IsTrue(control.Left >= 0 && control.Top >= 0 && control.Right <= parent.ClientSize.Width + 1 && control.Bottom <= parent.ClientSize.Height + 1,
                    $"Clipped {control.Name}: {control.Bounds} in {parent.ClientSize}");
            CheckTableBounds(control);
        }
    }

    [TestMethod]
    public Task CacheCompaniesAreSelectedOnlyInSettingsAndRemainIndependentOfTheActiveCompany() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var page = new SettingsPage(); page.Bind(workspace);
        await InvokeActionAsync(page, "LoadCompanies_Click");
        var view = Field<DevExpress.XtraGrid.Views.Grid.GridView>(page, "cacheView");
        view.SelectRow(0);
        await page.SaveAsync(CancellationToken.None);
        var saved = await workspace.Settings.LoadAsync();
        var selected = saved.Options.Companies.Where(company => company.IncludeInPhoneCache).ToArray();
        Assert.HasCount(1, selected);
        Assert.AreEqual("Firma A", selected[0].DisplayName);
        Assert.IsNull(workspace.Companies.Current.Company);
        view.ClearSelection();
        await page.SaveAsync(CancellationToken.None);
        Assert.IsFalse((await workspace.Settings.LoadAsync()).Options.Companies.Any(company => company.IncludeInPhoneCache));
        Assert.IsNull(typeof(MainForm).GetField("companyLookup", BindingFlags.Instance | BindingFlags.NonPublic));
    });

    [TestMethod]
    public Task CacheRefreshSavesSelectionReportsResultsAndRestoresButtonsAfterCancellation() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        using var page = new SettingsPage();
        page.Bind(workspace);
        page.ConfigureLayoutPersistence(services.GetRequiredService<AppUiState>());
        await InvokeActionAsync(page, "LoadCompanies_Click");
        var view = Field<DevExpress.XtraGrid.Views.Grid.GridView>(page, "cacheView");
        view.SelectRow(0);
        await InvokeActionAsync(page, "RefreshCache_Click");
        var selected = (await workspace.Settings.LoadAsync()).Options.Companies.Single(company => company.IncludeInPhoneCache);
        var cache = services.GetRequiredService<IPartyCacheRepository>();
        Assert.AreEqual(1, (await cache.GetStateAsync(selected.Company)).Revision);
        Assert.AreEqual("+49301234567", (await cache.GetAsync(new(selected.Company, PartyKind.Customer, "01")))!.Phones.Single().E164);
        Assert.Contains("1 von 1", Field<LabelControl>(page, "statusLabel").Text);
        Assert.Contains("1 Nummern", view.GetRowCellValue(0, "CacheStatus").ToString()!);
        Assert.IsNull(workspace.Companies.Current.Company);

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var directory = (TestDirectory)workspace.Directory;
        directory.Read = async query =>
        {
            entered.SetResult();
            await release.Task;
            return new(query.Company, new([], false));
        };
        typeof(SettingsPage).GetMethod("RefreshCache_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [null, EventArgs.Empty]);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsFalse(Field<SimpleButton>(page, "refreshCacheButton").Enabled);
            Assert.IsFalse(Field<GridControl>(page, "cacheGrid").Enabled);
            Assert.IsTrue(Field<SimpleButton>(page, "cancelCacheButton").Enabled);
            typeof(SettingsPage).GetMethod("CancelCache_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [null, EventArgs.Empty]);
            Assert.IsTrue(Field<PageOperation>(page, "_operation").IsRunning, "Cancellation must await the source response.");
        }
        finally { release.TrySetResult(); }
        // StopAsync also exercises closing the dialog while a cancelled source call is still completing.
        await page.StopAsync();
        Assert.AreEqual(1, (await cache.GetStateAsync(selected.Company)).Revision);
        Assert.IsTrue(Field<SimpleButton>(page, "refreshCacheButton").Enabled);
        Assert.IsTrue(Field<SimpleButton>(page, "saveButton").Enabled);
        Assert.IsTrue(Field<GridControl>(page, "cacheGrid").Enabled);
        Assert.IsFalse(Field<SimpleButton>(page, "cancelCacheButton").Enabled);
        Assert.Contains("abgebrochen", Field<LabelControl>(page, "statusLabel").Text);
    });

    [TestMethod]
    public Task SettingsRoundtripPreservesSecretsDeviceAndCompanyScopesAndRejectsAStaleEditor() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var original = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(original.Options with
        {
            SipAccount = new SipAccountOptions { Server = "test.invalid", Username = "fixture", Password = workspace.Secrets.Protect("test-secret-sentinel", SipAccountOptions.PasswordPurpose) },
            Devices = original.Options.Devices with { CaptureDeviceId = "capture-fixture", PlaybackDeviceId = "playback-fixture" },
            Companies = [new CompanySettings { Company = new(LexwareProductLine.Pro, "fixture", "01"), DefaultPhoneRegion = "DE", LocalAreaCode = "030" }]
        }, original);
        var snapshot = await workspace.Settings.LoadAsync();
        using var first = new SettingsPage(); first.Bind(workspace); first.Apply(snapshot);
        using var second = new SettingsPage(); second.Bind(workspace); second.Apply(snapshot);
        Find<CheckEdit>(first, "notificationsEdit").Checked = false;
        await first.SaveAsync(CancellationToken.None);
        var saved = await workspace.Settings.LoadAsync();
        Assert.IsFalse(saved.Options.User.ShowCallNotifications);
        Assert.AreEqual("capture-fixture", saved.Options.Devices.CaptureDeviceId);
        Assert.AreEqual("playback-fixture", saved.Options.Devices.PlaybackDeviceId);
        Assert.AreEqual(snapshot.Options.Companies[0], saved.Options.Companies[0]);
        using var password = workspace.Secrets.Unprotect(saved.Options.SipAccount!.Password!, SipAccountOptions.PasswordPurpose);
        Assert.AreEqual("test-secret-sentinel", password.Reveal());
        Assert.DoesNotContain("test-secret-sentinel", await File.ReadAllTextAsync(services.GetRequiredService<ConfigurationLocation>().FilePath));
        await Assert.ThrowsAsync<ConfigurationException>(() => second.SaveAsync(CancellationToken.None));
        Find<ComboBoxEdit>(first, "providerEdit").SelectedIndex = 1;
        await Assert.ThrowsAsync<ConfigurationException>(() => first.SaveAsync(CancellationToken.None));
        Assert.AreEqual(CommunicationDatabaseProvider.Sqlite, (await workspace.Settings.LoadAsync()).Options.Database.Provider);
    });

    [TestMethod]
    public Task DatabaseAndHistoryPagesUseRealStorageWithoutCreatingOnReadAndSaveANote() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var database = new DatabasePage(); database.Bind(workspace);
        await InvokeActionAsync(database, "Status_Click");
        Assert.IsFalse(File.Exists(Path.Combine(workspace.Paths.RootDirectory, "communications.db")));
        await InvokeActionAsync(database, "Create_Click");
        Assert.AreEqual(DatabaseSchemaState.Current, (await workspace.Database.GetStatusAsync()).State);
        var now = DateTimeOffset.UtcNow;
        var call = await services.GetRequiredService<ICallJournal>().BeginAsync(new CallSnapshot
        {
            Id = Guid.NewGuid(), Revision = 0, Direction = CallDirection.Incoming, State = CallState.Ringing,
            LocalAddress = new(NumberPresentation.Available, "100"), RemoteAddress = new(NumberPresentation.Available, "+49301234567"),
            StartedAtUtc = now, StateChangedAtUtc = now
        });
        using var history = new HistoryPage(); history.Bind(workspace, services.GetRequiredService<AppUiState>());
        await InvokeActionAsync(history, "Refresh_Click");
        Assert.AreEqual(1, ((Array)Field<GridControl>(history, "grid").DataSource).Length);
        Assert.Contains("+49301234567", Field<MemoEdit>(history, "detailsEdit").Text);
        Field<MemoEdit>(history, "noteEdit").Text = "Isolierte UI-Notiz";
        await InvokeActionAsync(history, "SaveNote_Click");
        Assert.AreEqual("Isolierte UI-Notiz", (await workspace.History.GetCallAsync(call.Id))!.Notes.Single().Text);
        history.Size = new Size(900, 650);
        CreateHandles(history);
        var historyTabs = Field<XtraTabControl>(history, "historyTabs");
        foreach (XtraTabPage tab in historyTabs.TabPages)
        {
            historyTabs.SelectedTabPage = tab;
            history.PerformLayout();
            using var bitmap = new Bitmap(history.Width, history.Height);
            history.DrawToBitmap(bitmap, new Rectangle(Point.Empty, history.Size));
            Directory.CreateDirectory(RenderOutput());
            bitmap.Save(Path.Combine(RenderOutput(), "History-populated-" + historyTabs.SelectedTabPageIndex + ".png"), ImageFormat.Png);
        }
    });

    [TestMethod]
    public Task NavigationReusesTabsAndCompanyBoundPluginViewsAreDisposedBeforeShutdown() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        _ = form.Handle;
        typeof(Form).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [EventArgs.Empty]);
        foreach (var name in new[] { "settings", "database", "plugin", "history", "help", "about", "settings" }) form.Navigate(name);
        Assert.AreEqual(7, Field<DevExpress.XtraBars.Docking2010.Views.Tabbed.TabbedView>(form, "tabbedView").Documents.Count);
        Assert.AreEqual(0, plugin.Created);
        var company = new Company(new(LexwareProductLine.Pro, "fixture", "1"), "Testmandant");
        await workspace.Companies.SelectAsync(company);
        await form.OpenPartyAsync(new(company.Key, PartyKind.Customer, "1"));
        Assert.AreEqual(2, plugin.Created);
        Assert.AreEqual(9, Field<DevExpress.XtraBars.Docking2010.Views.Tabbed.TabbedView>(form, "tabbedView").Documents.Count);
        await form.PrepareToCloseAsync();
        Assert.AreEqual(2, plugin.Disposed);
        Assert.IsFalse(form.Visible);
        Assert.IsTrue(new[] { "homeItem", "pluginItem", "historyItem", "closeItem", "trayItem", "exitItem", "databaseItem", "settingsItem", "resetItem", "fontItem", "helpItem", "aboutItem" }
            .All(name => Field<DevExpress.XtraBars.BarButtonItem>(form, name).ImageOptions.SvgImage is not null));
    });

    [TestMethod]
    public Task ClosingAPageCancelsAndAwaitsItsOutstandingWork() => RunStaAsync(async () =>
    {
        using var owner = new Panel(); using var label = new LabelControl(); using var operation = new PageOperation(owner, label);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = operation.RunAsync(async token =>
        {
            started.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { await released.Task; }
        });
        await started.Task;
        var stop = operation.StopAsync();
        Assert.IsFalse(stop.IsCompleted);
        released.SetResult();
        await stop; await work;
        Assert.IsFalse(operation.IsRunning);
    });

    private static Task WithWorkspace(Func<AppWorkspace, IServiceProvider, TestPlugin, Task> action) => RunStaAsync(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-shell-" + Guid.NewGuid().ToString("N"));
        var plugin = new TestPlugin();
        using var context = new WindowsFormsSynchronizationContext();
        var host = WawiCallHost.CreateBuilder(typeof(MainForm).Assembly).ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(root));
            services.AddSingleton<IHostLifetime, AppHostLifetime>();
            services.AddSingleton<IUiDispatcher>(new SynchronizationContextUiDispatcher(context));
            services.AddSingleton<IBusinessDirectory, TestDirectory>();
            services.AddSingleton<IProductPluginRuntime>(new TestRuntime(plugin));
            services.AddSingleton<WawiCallPro.Interfaces.PhoneNumbers.IPhoneCacheService, WawiCallPro.Utilities.Persistence.PhoneCacheService>();
            services.AddSingleton<IPluginViewAccess, PluginViewAccess>();
            services.AddSingleton(new StartupExecutionMode(true));
            services.AddSingleton<AppUiState>(); services.AddSingleton<AppWorkspace>();
        }).Build();
        try
        {
            await host.StartAsync();
            await action(host.Services.GetRequiredService<AppWorkspace>(), host.Services, plugin);
        }
        finally
        {
            await host.StopAsync();
            await ((IAsyncDisposable)host).DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    });

    private static async Task InvokeActionAsync(Control page, string handler)
    {
        page.GetType().GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [null, EventArgs.Empty]);
        var operation = Field<PageOperation>(page, "_operation");
        var timeout = DateTime.UtcNow.AddSeconds(15);
        while (operation.IsRunning && DateTime.UtcNow < timeout) await Task.Delay(10);
        Assert.IsFalse(operation.IsRunning, "UI action did not finish.");
    }
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
    private static T Find<T>(Control parent, string name) where T : Control => parent.Controls.Find(name, true).OfType<T>().Single();
    private static async Task RunStaAsync(Func<Task> action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);
            EventHandler? start = null;
            start = async (_, _) =>
            {
                Application.Idle -= start;
                try { await action(); done.SetResult(); }
                catch (Exception exception) { done.SetException(exception); }
                finally { Application.ExitThread(); }
            };
            Application.Idle += start;
            Application.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(40));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)));
    }
    private sealed class TestDirectory(ApplicationDescriptor application) : IBusinessDirectory
    {
        internal Func<PartyQuery, Task<PartyPage>>? Read;
        private LexwareProductLine ProductLine => Enum.Parse<LexwareProductLine>(application.ProductLine);
        public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default) => Task.FromResult(ImmutableArray.Create(
            new Company(new(ProductLine, "installation-A", "1"), "Firma A"),
            new Company(new(ProductLine, "installation-B", "1"), "Firma B")));
        public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default) => Read is not null ? Read(query) : Task.FromResult(new PartyPage(query.Company, new([new Party
        {
            Key = new(query.Company, PartyKind.Customer, "01"), DisplayName = "Testkunde",
            Phones = [new("+49 30 1234567", PhoneKind.Telephone, new("fixture", "01", "telephone"))]
        }], false)));
        public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class TestRuntime(TestPlugin plugin) : IProductPluginRuntime
    {
        public bool IsLoaded => true;
        public Task<T> UseAsync<T>(Func<IWawiCallPlugin, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(plugin, cancellationToken);
    }
    private sealed class TestPlugin : IWawiCallPlugin, IWawiCallPluginViews
    {
        internal int Created;
        internal int Disposed;
        public string Name => "Isoliertes Ansichtsplugin";
        public Version Version { get; } = new(1, 0);
        public LexwareProductLine ProductLine => LexwareProductLine.Pro;
        public IBusinessDirectory Directory => throw new NotSupportedException();
        public void Initialize(Quantarix.Plugin.Abstractions.IPluginHost host) { }
        public void Configure(IWawiCallHostServices services) { }
        public void Execute() => throw new NotSupportedException();
        public void Shutdown() { }
        public IReadOnlyList<PluginViewDescriptor> GetViews() => [new("master", "Stammdaten"), new("orders", "Aufträge")];
        public IPluginViewLease CreateView(string id, PartyKey party) { Created++; return new Lease(new Panel(), () => Disposed++); }
        private sealed class Lease(Control view, Action disposed) : IPluginViewLease
        {
            public Control View => view;
            public void Dispose() { view.Dispose(); disposed(); }
        }
    }
}
