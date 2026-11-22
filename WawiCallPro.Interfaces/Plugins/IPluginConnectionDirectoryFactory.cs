using WawiCallPro.Interfaces.Directory;

namespace WawiCallPro.Interfaces.Plugins;

/// <summary>A caller-owned, read-only connection created from an explicit unsaved account snapshot.</summary>
public interface IPluginConnectionDirectory : IAsyncDisposable
{
    IBusinessDirectory Directory { get; }
}

public interface IPluginConnectionDirectoryFactory
{
    IPluginConnectionDirectory Create(ProConnectionConfiguration settings);
}
