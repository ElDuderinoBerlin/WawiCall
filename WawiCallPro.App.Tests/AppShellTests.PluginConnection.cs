using System.Reflection;
using System.Text.Json;
using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using Microsoft.Extensions.Logging;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task PluginConnectionSaveIsAtomicEncryptedAndActivatesWithoutRestart() => WithWorkspace(async (workspace, _, plugin) =>
    {
        ConnectionViewDouble? editor = null;
        plugin.SettingsFactory = context => editor = new(context);
        using var page = new SettingsPage(); page.Bind(workspace); await page.ReloadAsync();
        Assert.IsNotNull(editor); Assert.IsFalse(editor.Snapshot.Configured);
        page.SelectAutomationTab("settings-wawi"); Assert.AreEqual("Zugang", page.SettingsTabs.SelectedTabPage.Text);
        var password = "isolated-password-" + Guid.NewGuid().ToString("N");
        var key = Guid.NewGuid().ToString("B");
        editor.Patch = ConnectionChange(password, key);
        Field<ComboBoxEdit>(page, "logLevelEdit").SelectedIndex = (int)LogLevel.Warning;
        var confirmations = 0;
        await page.SaveAndOfferRestartAsync(() => { confirmations++; return false; }, CancellationToken.None);
        Assert.AreEqual(0, confirmations);
        var snapshot = await workspace.Settings.LoadAsync(); var pro = snapshot.Options.Erp.Pro!;
        Assert.AreEqual("test-api", pro.Username); Assert.AreEqual(LogLevel.Warning, snapshot.Options.User.LogLevel);
        using (var decrypted = workspace.Secrets.Unprotect(pro.Password!, ProErpSettings.PasswordPurpose)) Assert.AreEqual(password, decrypted.Reveal());
        using (var decrypted = workspace.Secrets.Unprotect(pro.ClientKey!, ProErpSettings.ClientKeyPurpose)) Assert.AreEqual(key, decrypted.Reveal());
        var file = Path.Combine(workspace.Paths.RootDirectory, "appsettings.json");
        var json = await File.ReadAllTextAsync(file);
        Assert.DoesNotContain(password, json); Assert.DoesNotContain(key, json); Assert.Contains("dpapi-user-v1:", json);
        Assert.IsNotNull(workspace.Configuration.Current.Options.Erp.Pro, "Committed ERP settings are available without restarting.");
        Assert.IsTrue(editor.Snapshot.PasswordStored); Assert.IsTrue(editor.Snapshot.ClientKeyStored); Assert.IsFalse(editor.HasUnsavedChanges);
        Assert.AreEqual(WawiCallPro.Utilities.Operations.OperationEvent.SettingsSaved, page.LastSettingsNotice);
        var export = Path.Combine(workspace.Paths.RootDirectory, "portable.json");
        await workspace.Settings.ExportPortableAsync(export);
        var portable = await File.ReadAllTextAsync(export);
        Assert.DoesNotContain(password, portable); Assert.DoesNotContain(key, portable); Assert.DoesNotContain("lexware.example.invalid", portable);
        await page.StopAsync(); Assert.IsTrue(editor.Disposed);
    });

    [TestMethod]
    public Task PluginConnectionRetainsBlankSecretsAndCanExplicitlyRemoveOnlyProConfiguration() => WithWorkspace(async (workspace, _, plugin) =>
    {
        var password = "keep-this-isolated-password"; var key = Guid.NewGuid().ToString("B");
        var initial = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(initial.Options with { Erp = new()
        {
            FinOfficeInstallationId = "keep-other-line", Pro = new()
            {
                Username = "test-api", Password = workspace.Secrets.Protect(password, ProErpSettings.PasswordPurpose),
                ClientKey = workspace.Secrets.Protect(key, ProErpSettings.ClientKeyPurpose)
            }
        } }, initial);
        ConnectionViewDouble? editor = null; plugin.SettingsFactory = context => editor = new(context);
        using var page = new SettingsPage(); page.Bind(workspace); await page.ReloadAsync();
        var before = (await workspace.Settings.LoadAsync()).Options.Erp.Pro!;
        editor!.Patch = ConnectionChange(null, null);
        await page.SaveAsync(CancellationToken.None);
        var saved = await workspace.Settings.LoadAsync();
        Assert.AreEqual(JsonSerializer.Serialize(before.Password), JsonSerializer.Serialize(saved.Options.Erp.Pro!.Password));
        Assert.AreEqual(JsonSerializer.Serialize(before.ClientKey), JsonSerializer.Serialize(saved.Options.Erp.Pro.ClientKey));
        editor.Patch = new() { Remove = true }; await page.SaveAsync(CancellationToken.None);
        saved = await workspace.Settings.LoadAsync(); Assert.IsNull(saved.Options.Erp.Pro);
        Assert.AreEqual("keep-other-line", saved.Options.Erp.FinOfficeInstallationId);
        await page.StopAsync();
    });

    [TestMethod]
    public Task StalePluginConnectionEditorCannotPartiallyOverwriteNewerSettings() => WithWorkspace(async (workspace, _, plugin) =>
    {
        ConnectionViewDouble? editor = null; plugin.SettingsFactory = context => editor = new(context);
        using var page = new SettingsPage(); page.Bind(workspace); await page.ReloadAsync();
        editor!.Patch = ConnectionChange("isolated-new-password", Guid.NewGuid().ToString("B"));
        var external = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(external.Options with { User = external.Options.User with { LogLevel = LogLevel.Critical } }, external);
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => page.SaveAsync(CancellationToken.None));
        var saved = await workspace.Settings.LoadAsync(); Assert.IsNull(saved.Options.Erp.Pro);
        Assert.AreEqual(LogLevel.Critical, saved.Options.User.LogLevel); Assert.IsTrue(editor.HasUnsavedChanges);
        await page.StopAsync();
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task PluginConnectionCancelOrWindowCloseNeverSavesTheDraft(bool useButton) => WithWorkspace(async (workspace, _, plugin) =>
    {
        var initial = await workspace.Settings.LoadAsync(); await workspace.Settings.SaveAsync(initial.Options, initial);
        var file = Path.Combine(workspace.Paths.RootDirectory, "appsettings.json"); var before = await File.ReadAllBytesAsync(file);
        ConnectionViewDouble? editor = null; plugin.SettingsFactory = context => editor = new(context);
        using var form = new FrmSettings(workspace); form.PrepareForBackgroundRun(); form.Show();
        var page = Field<SettingsPage>(form, "settingsPage"); await page.ReloadAsync();
        editor!.Patch = ConnectionChange("isolated-draft", Guid.NewGuid().ToString("B"));
        if (useButton) Field<SimpleButton>(page, "cancelButton").PerformClick(); else form.Close();
        var limit = DateTime.UtcNow.AddSeconds(5); while (form.Visible && DateTime.UtcNow < limit) await Task.Delay(10);
        Assert.IsFalse(form.Visible); Assert.IsTrue(editor.Disposed);
        CollectionAssert.AreEqual(before, await File.ReadAllBytesAsync(file));
    });

    [TestMethod]
    public Task PendingPluginConnectionDraftPreventsImportReloadAndPluginSwitch() => WithWorkspace(async (workspace, _, plugin) =>
    {
        ConnectionViewDouble? editor = null; plugin.SettingsFactory = context => editor = new(context);
        using var page = new SettingsPage(); page.Bind(workspace); await page.ReloadAsync();
        editor!.Patch = ConnectionChange("isolated-draft", Guid.NewGuid().ToString("B"));
        var previousPlugin = ((IProductPluginSelection)workspace.Plugins).ActivePluginId;
        var method = typeof(SettingsPage).GetMethod("ActivatePluginAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await Assert.ThrowsExactlyAsync<PluginSettingsValidationException>(() => (Task)method.Invoke(page, ["Test.Dummy", CancellationToken.None])!);
        Assert.AreEqual(previousPlugin, ((IProductPluginSelection)workspace.Plugins).ActivePluginId); Assert.IsFalse(editor.Disposed);
        await page.ReloadAsync(); Assert.IsTrue(editor.HasUnsavedChanges);
        Assert.AreEqual(WawiCallPro.Utilities.Operations.OperationEvent.SettingsValidationFailed, page.LastSettingsNotice);
        await page.ImportSettingsAsync(Path.Combine(workspace.Paths.RootDirectory, "not-read.json"));
        Assert.AreEqual(WawiCallPro.Utilities.Operations.OperationEvent.SettingsValidationFailed, page.LastSettingsNotice);
        await page.StopAsync();
    });

    private static ProConnectionEditorChange ConnectionChange(string? password, string? key) => new()
    {
        ApiUrl = new("https://lexware.example.invalid/api"), Username = "test-api", Password = password,
        ClientKey = key, UseDefaultClientKey = false, TimeoutSeconds = 30, TokenTimeoutSeconds = 10
    };

    [TestMethod]
    public Task PluginConnectionTabIsSuppliedOnlyByTheActivePluginAndCanBeRequestedBeforeLoading() => WithWorkspace(async (workspace, _, plugin) =>
    {
        using var page = new SettingsPage(); page.Bind(workspace); await page.ReloadAsync();
        var tab = Field<DevExpress.XtraTab.XtraTabPage>(page, "wawiTab");
        Assert.IsFalse(page.SettingsTabs.TabPages.Contains(tab), "No placeholder tab for a plugin without a connection view.");
        await page.StopAsync();
        plugin.SettingsFactory = context => new ConnectionViewDouble(context);
        using var configured = new SettingsPage(); configured.Bind(workspace);
        configured.SelectAutomationTab("settings-wawi"); await configured.ReloadAsync();
        var suppliedTab = Field<DevExpress.XtraTab.XtraTabPage>(configured, "wawiTab");
        Assert.IsTrue(configured.SettingsTabs.TabPages.Contains(suppliedTab)); Assert.AreSame(suppliedTab, configured.SettingsTabs.SelectedTabPage);
        Assert.AreEqual("Zugang", suppliedTab.Text); await configured.StopAsync();
        Assert.IsFalse(configured.SettingsTabs.TabPages.Contains(suppliedTab));
    });

    [TestMethod]
    public Task PluginConnectionCatalogUsesPluginCapabilityEvenBeforeAuthenticationAndRejectsStalePlugin() => WithWorkspace(async (workspace, _, plugin) =>
    {
        plugin.SettingsFactory = context => new ConnectionViewDouble(context);
        ((TestDirectory)workspace.Directory).ReadCatalog = _ => throw new InvalidOperationException("Synthetic source not configured.");
        var state = new WawiCallPro.App.Hosting.AppAutomationState(workspace.Application, TimeProvider.System);
        await using var catalog = new WawiCallPro.App.Hosting.AppAutomationCatalog(workspace.Plugins, workspace.Directory, workspace.Dispatcher, state,
            new WawiCallPro.App.Hosting.LogWindowService());
        await catalog.StartAsync(default); state.Ready();
        Assert.AreEqual("Zugang", state.GetViews().Single(view => view.Id == "settings-wawi").Caption);
        Assert.IsFalse(state.GetViews().Any(view => view.Id == "parties")); Assert.IsEmpty(state.GetCompanies());
        ((TestRuntime)workspace.Plugins).BusinessAvailable = false;
        Assert.IsFalse(state.GetViews().Any(view => view.Id == "settings-wawi"));
        ((TestRuntime)workspace.Plugins).BusinessAvailable = true;
        await ((TestRuntime)workspace.Plugins).SwitchAsync("Test.Dummy");
        Assert.IsFalse(state.GetViews().Any(view => view.Id == "settings-wawi"));
        await catalog.StopAsync(default);
    });

    [TestMethod]
    public Task RestartingAfterPluginSettingsDoesNotReloadCompaniesWithOldCredentials() => WithWorkspace(async (workspace, _, _) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace,
            new WawiCallPro.App.Hosting.AppUiState(new ApplicationDescriptor("Restart editor", "RestartEditor-" + Guid.NewGuid().ToString("N")), new StartupExecutionMode(true)));
        form.PrepareForBackgroundRun(); form.Show(); await form.ReloadCompaniesAsync();
        var reads = 0;
        ((TestDirectory)workspace.Directory).ReadCatalog = _ => { reads++; return Task.FromResult(System.Collections.Immutable.ImmutableArray<WawiCallPro.Interfaces.Companies.Company>.Empty); };
        using var settings = form.CreateSettingsDialog(); settings.PrepareForBackgroundRun(); settings.Show(form);
        typeof(FrmSettings).GetMethod("Settings_RestartRequested", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(settings, [null, EventArgs.Empty]);
        var limit = DateTime.UtcNow.AddSeconds(5); while (settings.Visible && DateTime.UtcNow < limit) await Task.Delay(10);
        Assert.IsFalse(settings.Visible); Assert.AreEqual(0, reads);
        await form.PrepareToCloseAsync();
    });

    private sealed class ConnectionViewDouble(IPluginConnectionEditContext context) : IPluginSettingsView
    {
        public Control View { get; } = new Panel { AutoSize = true };
        internal ProConnectionEditorSnapshot Snapshot = context.ReadPro();
        internal ProConnectionEditorChange? Patch;
        internal bool Disposed;
        public bool HasUnsavedChanges => Patch is not null;
        public void Reload(IPluginConnectionEditContext current) { Snapshot = current.ReadPro(); Patch = null; }
        public void ApplyChanges(IPluginConnectionEditContext current) { if (Patch is not null) current.StagePro(Patch); }
        public void Dispose() { Disposed = true; Patch = null; View.Dispose(); }
    }
}
