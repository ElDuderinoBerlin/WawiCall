using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Automation;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task CatalogSkipsUnconfiguredSourceKeepsSetupReachableAndLoadsAfterConfiguration() => WithWorkspace(async (workspace, _, plugin) =>
    {
        plugin.ProductLine = Enum.Parse<WawiCallPro.Interfaces.Companies.LexwareProductLine>(workspace.Application.ProductLine);
        plugin.SettingsFactory = context => new ConnectionViewDouble(context);
        plugin.ConnectionConfigured = false;
        var reads = 0;
        var source = (TestDirectory)workspace.Directory;
        source.ReadCatalog = _ => { reads++; throw new AssertFailedException("No source IO before connection setup."); };
        var state = new AppAutomationState(workspace.Application, TimeProvider.System);
        var logs = new LogWindowService();
        await using var catalog = new AppAutomationCatalog(workspace.Plugins, workspace.Directory, workspace.Dispatcher, state, logs);
        await catalog.StartAsync(default); state.Ready();
        await catalog.RefreshAsync(default);
        Assert.AreEqual(0, reads); Assert.IsEmpty(state.GetCompanies());
        Assert.IsTrue(state.GetViews().Any(view => view.Id == "settings-wawi"));
        Assert.Throws<AutomationFault>(() => state.RequireAccess(AutomationAccess.Business));
        var notices = logs.Drain(); Assert.HasCount(1, notices);
        Assert.AreEqual(Microsoft.Extensions.Logging.LogLevel.Information, notices[0].Level);
        Assert.Contains("noch nicht eingerichtet", notices[0].Message);
        plugin.ConnectionConfigured = true; source.ReadCatalog = null;
        await catalog.RefreshAsync(default);
        Assert.HasCount(2, state.GetCompanies());
        Assert.IsTrue(state.GetViews().Any(view => view.Id == "plugin:orders"));
        state.RequireAccess(AutomationAccess.Business);
        Assert.IsEmpty(logs.Drain()); await catalog.StopAsync(default);
    });

    [TestMethod]
    public Task CatalogSourceFailureIsDistinctFromViewFailureRedactedAndRecoversOnRefresh() => WithWorkspace(async (workspace, _, plugin) =>
    {
        plugin.ProductLine = Enum.Parse<WawiCallPro.Interfaces.Companies.LexwareProductLine>(workspace.Application.ProductLine);
        plugin.SettingsFactory = context => new ConnectionViewDouble(context);
        var source = (TestDirectory)workspace.Directory;
        source.ReadCatalog = _ => throw new InvalidOperationException("synthetic-secret-in-upstream-error");
        var state = new AppAutomationState(workspace.Application, TimeProvider.System);
        var logs = new LogWindowService();
        await using var catalog = new AppAutomationCatalog(workspace.Plugins, source, workspace.Dispatcher, state, logs);
        await catalog.StartAsync(default); state.Ready(); await catalog.RefreshAsync(default);
        Assert.IsEmpty(state.GetCompanies()); Assert.IsTrue(state.GetViews().Any(view => view.Id == "settings-wawi"));
        Assert.Throws<AutomationFault>(() => state.RequireAccess(AutomationAccess.Business));
        var warnings = logs.Drain(); Assert.HasCount(1, warnings);
        Assert.AreEqual(Microsoft.Extensions.Logging.LogLevel.Warning, warnings[0].Level);
        Assert.Contains("Mandanten konnten", warnings[0].Message);
        Assert.DoesNotContain("Viewkatalog", warnings[0].Message);
        Assert.DoesNotContain("synthetic-secret", warnings[0].Message);
        source.ReadCatalog = null;
        typeof(AppAutomationCatalog).GetField("_retryAtTicks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(catalog, 1L);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (state.GetCompanies().IsEmpty && DateTime.UtcNow < deadline) await Task.Delay(20);
        state.RequireAccess(AutomationAccess.Business); Assert.HasCount(2, state.GetCompanies());
        await catalog.StopAsync(default);
    });
}
