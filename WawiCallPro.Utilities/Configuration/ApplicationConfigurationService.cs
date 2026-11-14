using Microsoft.Extensions.Hosting;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Loads once before business workers start. A subsequent settings change takes effect on the next host start.</summary>
public sealed class ApplicationConfigurationService : IHostedService
{
    private readonly ConfigurationStore _store;
    private readonly WawiCallPro.Utilities.Operations.RuntimeLoggingSettings? _logging;
    public ApplicationConfigurationService(ConfigurationStore store) { _store = store; }
    public ApplicationConfigurationService(ConfigurationStore store, WawiCallPro.Utilities.Operations.RuntimeLoggingSettings logging)
        : this(store) { _logging = logging; }
    private ConfigurationSnapshot? _current;

    public ConfigurationSnapshot Current => _current ?? throw new InvalidOperationException("Configuration has not been loaded.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var loaded = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        _logging?.Apply(loaded.Options.User);
        _current = loaded;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
