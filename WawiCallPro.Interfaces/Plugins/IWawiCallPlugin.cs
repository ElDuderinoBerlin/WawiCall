using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;

namespace WawiCallPro.Interfaces.Plugins;

/// <summary>
/// Extends the existing Quantarix plugin contract without a second loader or a replacement IPluginHost.
/// Directory access and Execute are allowed only after Configure succeeds.
/// The runtime integration, including enforcing this order, follows in phase 4.
/// </summary>
public interface IWawiCallPlugin : IPlugin
{
    LexwareProductLine ProductLine { get; }
    IBusinessDirectory Directory { get; }
    void Configure(IWawiCallHostServices services);
}
