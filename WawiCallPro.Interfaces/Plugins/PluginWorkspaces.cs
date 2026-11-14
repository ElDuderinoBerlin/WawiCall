namespace WawiCallPro.Interfaces.Plugins;

/// <summary>Optional plugin-owned workspace. The Windows host supplies the factory on its UI thread.</summary>
public interface IWawiCallPluginWorkspaces
{
    IPluginLifetimeResource CreatePartySearch(IPartySearchWorkspaceFactory factory);
}

/// <summary>Typed UI factory without a Windows dependency in the source plugin or shared domain contracts.</summary>
public interface IPartySearchWorkspaceFactory
{
    IPluginLifetimeResource Create();
}
