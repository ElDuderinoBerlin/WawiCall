using Microsoft.Extensions.Hosting;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Loads once before business workers start. A subsequent settings change takes effect on the next host start.</summary>
public sealed class ApplicationConfigurationService(ConfigurationStore store) : IHostedService
{
    private ConfigurationSnapshot? _current;

    public ConfigurationSnapshot Current => _current ?? throw new InvalidOperationException("Configuration has not been loaded.");

    public async Task StartAsync(CancellationToken cancellationToken) =>
        _current = await store.LoadAsync(cancellationToken).ConfigureAwait(false);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
