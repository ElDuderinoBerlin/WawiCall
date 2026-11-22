using System.Collections.Immutable;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Lexware.Pro;

/// <summary>Serializes credential replacement, drains old requests and discards results from an obsolete account.</summary>
public sealed class ReloadingProBusinessDirectory(IPluginConnectionConfiguration? configuration, IProDirectoryFactory factory,
    ProBusinessDirectory fallback) : IBusinessDirectory, IBusinessDirectoryConnectionState, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1);
    private IProDirectoryLease? _session;
    private long _revision = -1;
    private bool _disposed;
    public long ConnectionRevision => configuration?.Revision ?? 0;
    public bool IsConnectionConfigured => configuration is null ? fallback.IsConnectionConfigured : configuration.ReadPro() is not null;
    public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default) => Read(directory => directory.GetCompaniesAsync(cancellationToken), cancellationToken);
    public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => Read(directory => directory.GetCapabilitiesAsync(company, cancellationToken), cancellationToken);
    public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default) => Read(directory => directory.GetPartiesAsync(query, cancellationToken), cancellationToken);
    public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => Read(directory => directory.GetPartyAsync(key, cancellationToken), cancellationToken);
    private async Task<T> Read<T>(Func<IBusinessDirectory, Task<T>> read, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (configuration is null) return await read(fallback).ConfigureAwait(false);
            var revision = configuration.Revision;
            if (_session is null || _revision != revision)
            {
                if (_session is not null) { await _session.DisposeAsync().ConfigureAwait(false); _session = null; }
                var settings = configuration.ReadPro() ?? throw new ProAdapterException(ProFailure.ConfigurationRequired);
                _session = factory.Create(settings);
                _revision = revision;
            }
            var result = await read(_session.Directory).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (configuration.Revision != revision) throw new ProAdapterException(ProFailure.ForeignCompany);
            return result;
        }
        finally { _gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { if (_disposed) return; _disposed = true; if (_session is not null) await _session.DisposeAsync().ConfigureAwait(false); _session = null; }
        finally { _gate.Release(); }
    }
}
