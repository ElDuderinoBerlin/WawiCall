using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Functions.DummyClock;

public sealed class ClockFunction(TimeProvider clock) : IFunctionPlugin
{
    private IPluginHost? _host;
    public string Name => "Dummy – Uhrzeit";
    public Version Version { get; } = new(0, 1, 0);

    public void Initialize(IPluginHost host) => _host = host;
    public void Shutdown() => _host = null;

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _host!.Logger.LogInfo($"Dummy-Uhrzeit: {clock.GetLocalNow():HH:mm:ss}");
        return Task.CompletedTask;
    }
}
