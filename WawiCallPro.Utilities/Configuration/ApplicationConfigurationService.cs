using Microsoft.Extensions.Hosting;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Loads once before business workers start. Only committed ERP connection changes are published live; other service settings retain their startup semantics.</summary>
public sealed class ApplicationConfigurationService : IHostedService
{
    private readonly ConfigurationStore _store;
    private readonly WawiCallPro.Utilities.Operations.RuntimeLoggingSettings? _logging;
    public ApplicationConfigurationService(ConfigurationStore store) { _store = store; _store.ErpSaved += ApplyErp; }
    public ApplicationConfigurationService(ConfigurationStore store, WawiCallPro.Utilities.Operations.RuntimeLoggingSettings logging)
        : this(store) { _logging = logging; }
    private ConfigurationSnapshot? _current;

    private long _erpRevision;
    public long ErpRevision => Volatile.Read(ref _erpRevision);
    public ConfigurationSnapshot Current => Volatile.Read(ref _current) ?? throw new InvalidOperationException("Configuration has not been loaded.");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var loaded = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        _logging?.Apply(loaded.Options.User);
        _current = loaded;
    }

    private void ApplyErp(ErpConnectionSettings settings, bool keyChanged)
    {
        var current = Volatile.Read(ref _current);
        if (current is null) return;
        if (!keyChanged && System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(current.Options.Erp).AsSpan().SequenceEqual(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(settings))) return;
        Volatile.Write(ref _current, current with { Options = current.Options with { Erp = settings } });
        Interlocked.Increment(ref _erpRevision);
    }
    public Task StopAsync(CancellationToken cancellationToken) { _store.ErpSaved -= ApplyErp; return Task.CompletedTask; }
}
