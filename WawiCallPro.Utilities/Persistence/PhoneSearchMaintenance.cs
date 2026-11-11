using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Utilities.Persistence;

public sealed record PhoneSearchMaintenanceOptions
{
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan SourceRefreshInterval { get; init; } = TimeSpan.FromMinutes(15);
}

public static class PhoneSearchMaintenanceRegistration
{
    /// <summary>Only the interactive host opts in, after the plugin and licence services. Diagnostics and Console do not.</summary>
    public static void AddPhoneSearchMaintenance(this IServiceCollection services)
    {
        services.AddSingleton<PhoneSearchMaintenanceOptions>();
        services.AddSingleton<PhoneSearchMaintenance>();
        services.AddHostedService(provider => provider.GetRequiredService<PhoneSearchMaintenance>());
    }
}

/// <summary>Coalesces local signals and periodically observes external writes. Shutdown drains even an uncooperative source.</summary>
public sealed class PhoneSearchMaintenance : IHostedService, IAsyncDisposable
{
    private readonly PhoneSearchIndexService _index;
    private readonly IPhoneCacheService _cache;
    private readonly PhoneSearchChanges _changes;
    private readonly TimeProvider _clock;
    private readonly PhoneSearchMaintenanceOptions _options;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _stopping = new();
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });
    private HashSet<CompanyKey> _sourceScope = [];
    private DateTimeOffset _nextSourceRefresh = DateTimeOffset.MinValue;
    private long _sourcePluginEpoch = -1;
    private Task _running = Task.CompletedTask;
    private Task? _stopTask;
    private bool _started;
    private bool _disposed;

    public PhoneSearchMaintenance(PhoneSearchIndexService index, IPhoneCacheService cache, PhoneSearchChanges changes,
        TimeProvider clock, PhoneSearchMaintenanceOptions options)
    {
        if (options.CheckInterval <= TimeSpan.Zero || options.SourceRefreshInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options));
        _index = index; _cache = cache; _changes = changes; _clock = clock; _options = options;
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_started || _disposed || _stopTask is not null) throw new InvalidOperationException("Search maintenance already started or disposed.");
        _started = true; _changes.Changed += Wake;
        _running = Task.Run(RunAsync);
        return Task.CompletedTask;
    }
    private void Wake() => _wake.Writer.TryWrite(true);
    private async Task RunAsync()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                while (_wake.Reader.TryRead(out _)) { }
                await ReconcileAsync(_stopping.Token).ConfigureAwait(false);
                using var waiting = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
                var delay = Task.Delay(_options.CheckInterval, _clock, waiting.Token);
                var signal = _wake.Reader.ReadAsync(waiting.Token).AsTask();
                await Task.WhenAny(delay, signal).ConfigureAwait(false);
                await waiting.CancelAsync().ConfigureAwait(false);
                try { await Task.WhenAll(delay, signal).ConfigureAwait(false); }
                catch (OperationCanceledException) when (waiting.IsCancellationRequested) { }
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
    }
    /// <summary>One serialized cycle, also callable with an isolated clock and source for acceptance.</summary>
    public async Task<PhoneSearchIndexStatus> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
        cancellationToken = linked.Token;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await _index.NeedsRefreshAsync(cancellationToken).ConfigureAwait(false)) await _index.RefreshAsync(cancellationToken).ConfigureAwait(false);
            var status = _index.GetStatus();
            var scope = status.Companies.Where(company => company.State != PhoneSearchCompanyState.NotAuthorized).Select(company => company.Company).ToHashSet();
            if (!scope.SetEquals(_sourceScope) || _sourcePluginEpoch != _changes.PluginEpoch)
            {
                _sourceScope = scope; _sourcePluginEpoch = _changes.PluginEpoch;
                _nextSourceRefresh = DateTimeOffset.MinValue;
            }
            if (status.State is PhoneSearchScopeState.Ready or PhoneSearchScopeState.Partial && scope.Count > 0 && _clock.GetUtcNow() >= _nextSourceRefresh)
            {
                _nextSourceRefresh = _clock.GetUtcNow() + _options.SourceRefreshInterval;
                await _cache.RefreshAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (await _index.NeedsRefreshAsync(cancellationToken).ConfigureAwait(false)) await _index.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            return _index.GetStatus();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { _changes.Invalidate(false); return _index.GetStatus(); }
        finally { _gate.Release(); }
    }
    public Task StopAsync(CancellationToken cancellationToken) => _stopTask ??= StopCoreAsync();
    private async Task StopCoreAsync()
    {
        _changes.Changed -= Wake;
        await _stopping.CancelAsync().ConfigureAwait(false);
        await _running.ConfigureAwait(false);
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        _gate.Release();
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true; _stopping.Dispose(); _gate.Dispose();
    }
}
