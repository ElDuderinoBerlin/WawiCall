namespace WawiCallPro.Interfaces.Plugins;

/// <summary>Optional desktop contribution. The desktop host lends a registration interface for the active session.</summary>
public interface IWawiCallPluginShell
{
    void RegisterShell(IPluginShellUi host);
}

/// <summary>Registers metadata for the Ribbon and navigation. Only during RegisterShell.</summary>
public interface IPluginShellUi
{
    void RegisterGroup(PluginRibbonGroup group);
    void RegisterNavigation(PluginNavigationEntry entry);
    void RegisterButton(PluginRibbonButton button);
}

public enum PluginNavigationTarget { PartySearch, PluginSettings }
public enum PluginShellIcon { Parties, Settings, Test }

public sealed record PluginRibbonGroup(string Id, string Title);
public sealed record PluginNavigationEntry(string Id, string Title, string Hint, PluginShellIcon Icon, PluginNavigationTarget Target);
public sealed record PluginRibbonButton(string Id, string GroupId, string NavigationId);
