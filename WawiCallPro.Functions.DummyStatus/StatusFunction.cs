using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Functions.DummyStatus;

public sealed class StatusFunction : IFunctionPlugin
{
    private IPluginHost? _host;
    public string Name => "Dummy – Statusmeldung";
    public Version Version { get; } = new(0, 1, 0);

    public void Initialize(IPluginHost host) => _host = host;
    public void Shutdown() => _host = null;

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _host!.Logger.LogInfo("Dummy-Statusfunktion ausgeführt.");
        return Task.CompletedTask;
    }
}
