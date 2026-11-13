namespace WawiCallPro.Interfaces.Plugins;

/// <summary>Cached UI availability; every source operation must also enforce its prerequisite at execution time.</summary>
public interface IProductPluginAvailability
{
    bool CanUseActivePlugin { get; }
    bool CanActivate(string pluginId);
}
