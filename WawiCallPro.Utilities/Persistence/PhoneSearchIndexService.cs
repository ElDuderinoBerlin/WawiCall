using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Freshness bounds for cache data and the last explicit selection/permission check.</summary>
public sealed record PhoneSearchIndexOptions
{
    public TimeSpan MaximumCacheAge { get; init; } = TimeSpan.FromHours(24);
    public TimeSpan MaximumScopeAge { get; init; } = TimeSpan.FromMinutes(5);
}

public static class PhoneSearchIndexRegistration
{
    /// <summary>Register where a real or isolated business directory is available; construction performs no I/O.</summary>
    public static IServiceCollection AddPhoneSearchIndex(this IServiceCollection services)
    {
        services.TryAddSingleton<PhoneSearchIndexOptions>();
        services.TryAddSingleton<IPhoneSearchIndexService, PhoneSearchIndexService>();
        return services;
    }
}

/// <summary>One published cross-company snapshot. Refresh serializes background I/O; lookups never take its gate.</summary>
public sealed class PhoneSearchIndexService : IPhoneSearchIndexService, IAsyncDisposable
{
    private readonly ConfigurationStore _settings;
    private readonly ApplicationConfigurationService _activeSettings;
    private readonly ApplicationDescriptor _application;
    private readonly IBusinessDirectory _directory;
    private readonly IPartyCacheRepository _cache;
    private readonly ICompanyPhoneMatchingService _matching;
    private readonly IPhoneCacheMetadata _metadata;
    private readonly TimeProvider _clock;
    private readonly PhoneSearchIndexOptions _options;
    private readonly SemaphoreSlim _refreshGate = new(1);
    private readonly CancellationTokenSource _stopping = new();
    private readonly object _lifetimeGate = new();
    private readonly Dictionary<CompanyKey, ReusableIndex> _reusable = [];
    private TaskCompletionSource _drained = CompletedDrain();
    private int _operations;
    private long _generation;
    private volatile bool _disposed;
    private Task? _disposeTask;
    private Published _published = new(0, PhoneSearchScopeState.NotReady, null, []);

    public PhoneSearchIndexService(ConfigurationStore settings, ApplicationConfigurationService activeSettings,
        ApplicationDescriptor application, IBusinessDirectory directory, IPartyCacheRepository cache,
        ICompanyPhoneMatchingService matching, IPhoneCacheMetadata metadata, TimeProvider clock, PhoneSearchIndexOptions options)
    {
        if (options.MaximumCacheAge <= TimeSpan.Zero || options.MaximumScopeAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options));
        _settings = settings; _activeSettings = activeSettings; _application = application; _directory = directory;
        _cache = cache; _matching = matching; _metadata = metadata; _clock = clock; _options = options;
    }

    public Task<PhoneSearchIndexStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_operations++ == 0) _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            // Always execute the finally, even when the caller was already cancelled before Task.Run starts.
            return Task.Run(async () =>
            {
                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
                    return await RefreshCoreAsync(linked.Token).ConfigureAwait(false);
                }
                finally { lock (_lifetimeGate) if (--_operations == 0) _drained.TrySetResult(); }
            });
        }
    }

    private async Task<PhoneSearchIndexStatus> RefreshCoreAsync(CancellationToken token)
    {
        await _refreshGate.WaitAsync(token).ConfigureAwait(false);
        var selected = ImmutableArray<CompanySettings>.Empty;
        try
        {
            // Permission/selection changes may be the reason for this refresh. Do not serve the old scope while checking.
            Publish(PhoneSearchScopeState.Refreshing, null, []);
            ConfigurationSnapshot configuration;
            try { configuration = await _settings.LoadAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch { return Fail(PhoneSearchScopeState.ConfigurationUnavailable, selected); }
            selected = configuration.Options.Companies.Where(company => company.IncludeInPhoneCache).ToImmutableArray();
            if (selected.IsEmpty) { _reusable.Clear(); return Describe(Publish(PhoneSearchScopeState.NoSelection, null, []), _clock.GetUtcNow()); }
            if (!PhoneCacheMetadata.SameDatabase(configuration.Options.Database, _activeSettings.Current.Options.Database))
                return Fail(PhoneSearchScopeState.RestartRequired, selected);
            var fingerprint = Fingerprint(configuration.Options);
            ImmutableArray<Company> catalog;
            try { catalog = await CatalogAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch { return Fail(PhoneSearchScopeState.CatalogUnavailable, selected); }
            var allowed = Allowed(selected, catalog);
            foreach (var removed in _reusable.Keys.Where(key => !allowed.Contains(key)).ToArray()) _reusable.Remove(removed);
            var names = catalog.ToDictionary(company => company.Key, company => company.DisplayName);
            var entries = ImmutableArray.CreateBuilder<Entry>();
            foreach (var company in selected)
            {
                token.ThrowIfCancellationRequested();
                if (!allowed.Contains(company.Company))
                {
                    entries.Add(new(company, company.DisplayName ?? "Nicht verfügbarer Mandant", null, null, null, PhoneSearchCompanyState.NotAuthorized));
                    continue;
                }
                entries.Add(await BuildCompanyAsync(company, names[company.Company], token).ConfigureAwait(false));
            }
            // Check earlier companies again: a later, slower build must not hide a concurrent cache/rule update.
            foreach (var entry in entries.Where(entry => entry.Index is not null))
                if (await _cache.GetStateAsync(entry.Selection.Company, token).ConfigureAwait(false) != entry.Cache
                    || await _matching.GetProfileAsync(entry.Selection.Company, token).ConfigureAwait(false) != entry.Profile)
                    return Fail(PhoneSearchScopeState.ScopeChanged, selected);
            try { catalog = await CatalogAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch { return Fail(PhoneSearchScopeState.CatalogUnavailable, selected); }
            var checkedAt = _clock.GetUtcNow();
            if (!allowed.SetEquals(Allowed(selected, catalog))
                || fingerprint != Fingerprint((await _settings.LoadAsync(token).ConfigureAwait(false)).Options))
                return Fail(PhoneSearchScopeState.ScopeChanged, selected);
            token.ThrowIfCancellationRequested();
            return Describe(Publish(PhoneSearchScopeState.Ready, checkedAt, entries.ToImmutable()), _clock.GetUtcNow());
        }
        catch (OperationCanceledException) { Fail(PhoneSearchScopeState.Cancelled, selected); throw; }
        catch { return Fail(PhoneSearchScopeState.ScopeChanged, selected); }
        finally { _refreshGate.Release(); }
    }

    private async Task<Entry> BuildCompanyAsync(CompanySettings selection, string name, CancellationToken token)
    {
        PartyCacheState? state = null;
        PhoneMatchingProfile? profile = null;
        try
        {
            state = await _cache.GetStateAsync(selection.Company, token).ConfigureAwait(false);
            if (state.Revision == 0 || state.RefreshedAtUtc is null)
                return new(selection, name, state, null, null, PhoneSearchCompanyState.CacheMissing);
            profile = await _matching.GetProfileAsync(selection.Company, token).ConfigureAwait(false);
            ICompanyPhoneMatchIndex index;
            if (_reusable.TryGetValue(selection.Company, out var previous) && previous.Cache == state && previous.Profile == profile
                && previous.Index.Version == _metadata.ParserVersion + ";matching/1") index = previous.Index;
            else
            {
                index = await _matching.BuildIndexAsync(selection.Company, token).ConfigureAwait(false);
                if (index.CacheRevision != state.Revision || index.Profile != profile
                    || index.Version != _metadata.ParserVersion + ";matching/1")
                    return new(selection, name, state, profile, null, PhoneSearchCompanyState.Unavailable);
                _reusable[selection.Company] = new(state, profile, index);
            }
            return new(selection, name, state, profile, index, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (RepositoryConflictException exception) when (exception.Kind == RepositoryConflictKind.MissingRecord)
        { return new(selection, name, state, profile, null, PhoneSearchCompanyState.CacheMissing); }
        catch { return new(selection, name, state, profile, null, PhoneSearchCompanyState.Unavailable); }
    }

    private async Task<ImmutableArray<Company>> CatalogAsync(CancellationToken token)
    {
        var companies = await _directory.GetCompaniesAsync(token).ConfigureAwait(false);
        if (companies.IsDefault || companies.Any(company => company?.Key is null)
            || companies.Select(company => company.Key).Distinct().Count() != companies.Length)
            throw new InvalidOperationException("Invalid search catalog.");
        return companies;
    }
    private HashSet<CompanyKey> Allowed(ImmutableArray<CompanySettings> selected, ImmutableArray<Company> catalog)
    {
        var keys = catalog.Select(company => company.Key).ToHashSet();
        return selected.Where(company => company.Company.ProductLine.ToString() == _application.ProductLine && keys.Contains(company.Company))
            .Select(company => company.Company).ToHashSet();
    }
    private string Fingerprint(ApplicationOptions options)
    {
        var selected = options.Companies.Where(company => company.IncludeInPhoneCache)
            .OrderBy(company => company.Company.ProductLine).ThenBy(company => company.Company.InstallationId, StringComparer.Ordinal)
            .ThenBy(company => company.Company.ExternalId, StringComparer.Ordinal)
            .Select(company => new { company.Company, Context = _metadata.GetContextVersion(company) });
        var content = JsonSerializer.Serialize(new { Selected = selected, options.Database, options.User.ActivePluginId, Parser = _metadata.ParserVersion });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    public PhoneSearchIndexStatus GetStatus() => Describe(Volatile.Read(ref _published), _clock.GetUtcNow());
    public PhoneSearchResult Find(PhoneNumberInterpretation incoming, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        cancellationToken.ThrowIfCancellationRequested();
        var published = Volatile.Read(ref _published);
        var status = Describe(published, _clock.GetUtcNow());
        var candidates = ImmutableArray<PhoneMatchCandidate>.Empty;
        if (!_disposed && status.State is PhoneSearchScopeState.Ready or PhoneSearchScopeState.Partial)
            candidates = published.Entries.Where(entry => entry.Index is not null)
                .SelectMany(entry => entry.Index!.Find(incoming, cancellationToken))
                .OrderBy(candidate => Rank(candidate.Evidence[0].Match.Kind)).ToImmutableArray();
        cancellationToken.ThrowIfCancellationRequested();
        var latest = Volatile.Read(ref _published);
        if (latest != published)
        {
            status = Describe(latest, _clock.GetUtcNow()) with { State = PhoneSearchScopeState.ScopeChanged, IsComplete = false };
            candidates = [];
        }
        else
        {
            status = Describe(published, _clock.GetUtcNow());
            if (_disposed || status.State is not (PhoneSearchScopeState.Ready or PhoneSearchScopeState.Partial)) candidates = [];
        }
        return new(status, new()
        {
            ParserVersion = _metadata.ParserVersion, SearchedAtUtc = _clock.GetUtcNow(), IsComplete = status.IsComplete,
            SearchCompanies = status.Companies.Select(company => new CallerSearchCompanySnapshot(company.Company, company.CacheRevision,
                company.RuleRevision, company.State == PhoneSearchCompanyState.Ready && status.State is PhoneSearchScopeState.Ready or PhoneSearchScopeState.Partial,
                company.RefreshedAtUtc, company.ParserVersion)).ToImmutableArray()
        }, candidates);
    }

    private PhoneSearchIndexStatus Describe(Published published, DateTimeOffset now)
    {
        var state = published.State;
        if (_disposed) state = PhoneSearchScopeState.Stopped;
        if (state == PhoneSearchScopeState.Ready && (published.CheckedAt is not { } checkedAt
            || checkedAt > now || now - checkedAt > _options.MaximumScopeAge)) state = PhoneSearchScopeState.ScopeExpired;
        var companies = published.Entries.Select(entry => new PhoneSearchCompanyStatus(entry.Selection.Company, entry.Name,
            CompanyState(entry, now), entry.Cache?.Revision ?? 0, entry.Profile?.RuleRevision ?? 0,
            entry.Cache?.RefreshedAtUtc, entry.Cache?.ParserVersion)).ToImmutableArray();
        var complete = state == PhoneSearchScopeState.Ready && companies.Length > 0 && companies.All(company => company.State == PhoneSearchCompanyState.Ready);
        if (state == PhoneSearchScopeState.Ready && !complete) state = PhoneSearchScopeState.Partial;
        return new(published.Generation, state, published.CheckedAt, companies, complete);
    }
    private PhoneSearchCompanyState CompanyState(Entry entry, DateTimeOffset now)
    {
        if (entry.Forced is { } forced) return forced;
        if (entry.Cache is not { RefreshedAtUtc: { } refreshed } cache || entry.Index is null) return PhoneSearchCompanyState.CacheMissing;
        if (cache.ParserVersion != _metadata.ParserVersion) return PhoneSearchCompanyState.NormalizationOutdated;
        if (cache.ContextVersion != _metadata.GetContextVersion(entry.Selection)) return PhoneSearchCompanyState.ContextChanged;
        if (refreshed > now || now - refreshed > _options.MaximumCacheAge) return PhoneSearchCompanyState.CacheStale;
        return entry.Index.IsComplete ? PhoneSearchCompanyState.Ready : PhoneSearchCompanyState.Incomplete;
    }
    private Published Publish(PhoneSearchScopeState state, DateTimeOffset? checkedAt, ImmutableArray<Entry> entries)
    {
        var published = new Published(Interlocked.Increment(ref _generation), state, checkedAt, entries);
        Volatile.Write(ref _published, published);
        return published;
    }
    private PhoneSearchIndexStatus Fail(PhoneSearchScopeState state, ImmutableArray<CompanySettings> selected)
    {
        _reusable.Clear();
        var entries = selected.Select(company => new Entry(company, company.DisplayName ?? "Ausgewählter Mandant", null, null, null,
            PhoneSearchCompanyState.Unavailable)).ToImmutableArray();
        return Describe(Publish(state, null, entries), _clock.GetUtcNow());
    }
    public ValueTask DisposeAsync()
    {
        lock (_lifetimeGate)
        {
            if (_disposeTask is not null) return new(_disposeTask);
            _disposed = true;
            return new(_disposeTask = DisposeCoreAsync(_drained.Task));
        }
    }
    private async Task DisposeCoreAsync(Task drained)
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        await drained.ConfigureAwait(false);
        _reusable.Clear(); Publish(PhoneSearchScopeState.Stopped, null, []);
        _stopping.Dispose(); _refreshGate.Dispose();
    }
    private static TaskCompletionSource CompletedDrain() { var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); source.SetResult(); return source; }
    private static int Rank(PhoneNumberMatchKind kind) => kind switch { PhoneNumberMatchKind.Exact => 0, PhoneNumberMatchKind.ConfirmedBlock => 1, PhoneNumberMatchKind.StemPrefix => 2, _ => 3 };
    private sealed record ReusableIndex(PartyCacheState Cache, PhoneMatchingProfile Profile, ICompanyPhoneMatchIndex Index);
    private sealed record Entry(CompanySettings Selection, string Name, PartyCacheState? Cache, PhoneMatchingProfile? Profile,
        ICompanyPhoneMatchIndex? Index, PhoneSearchCompanyState? Forced);
    private sealed record Published(long Generation, PhoneSearchScopeState State, DateTimeOffset? CheckedAt, ImmutableArray<Entry> Entries);
}
