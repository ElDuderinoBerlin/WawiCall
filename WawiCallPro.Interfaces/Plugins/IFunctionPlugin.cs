using Quantarix.Plugin.Abstractions;

namespace WawiCallPro.Interfaces.Plugins;

/// <summary>A command plugin with an independent lifetime alongside the active Warenwirtschaft plugin.</summary>
public interface IFunctionPlugin : IPlugin
{
    /// <summary>Runs the function on a worker thread. The host signals cancellation before unloading.</summary>
    Task ExecuteAsync(CancellationToken cancellationToken);

    void IPlugin.Execute() => ExecuteAsync(CancellationToken.None).GetAwaiter().GetResult();
}
