using System.Collections.Immutable;
using WawiCallPro.Core.Parties;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Office;

/// <summary>Resolves the current owner group from a complete, revision-bound cache or a fresh bounded source snapshot.</summary>
public sealed class PartyMailScopeResolver(IPartyCacheRepository cache, IBusinessDirectory directory, TimeProvider clock)
{
    private readonly ConfigurationStore? _settings;
    private readonly ApplicationConfigurationService? _active;
    private readonly IProductPluginRuntime? _plugins;
    public PartyMailScopeResolver(IPartyCacheRepository cache, IBusinessDirectory directory, TimeProvider clock,
        ConfigurationStore settings, ApplicationConfigurationService active, IProductPluginRuntime plugins) : this(cache, directory, clock)
    { _settings = settings; _active = active; _plugins = plugins; }
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Dictionary<CompanyKey, Entry> _entries = [];

    public async Task<PartyMailGroup?> ResolveAsync(PartyKey selected, bool refreshSource = false, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_settings is not null && !PhoneCacheMetadata.SameDatabase((await _settings.LoadAsync(cancellationToken)).Options.Database, _active!.Current.Options.Database))
                throw new MailException(MailFailure.Conflict);
            if (_plugins is not null && (!_plugins.IsLoaded || _plugins is IProductPluginAvailability { CanUseActivePlugin: false } ||
                !await _plugins.UseAsync((plugin, _) => Task.FromResult(plugin.ProductLine == selected.Company.ProductLine), cancellationToken)))
                throw new MailException(MailFailure.AccessDenied);
            PartyCacheState? state = null;
            try { state = await cache.GetStateAsync(selected.Company, cancellationToken); }
            catch (DatabaseOperationException) { /* Mail can use the explicitly scoped live directory before communications storage is configured. */ }
            var now = clock.GetUtcNow();
            _entries.TryGetValue(selected.Company, out var existing);
            if (!refreshSource && existing is not null && existing.State == state &&
                (existing.FromCache || now >= existing.LoadedAt && now - existing.LoadedAt < TimeSpan.FromSeconds(30)))
                return Decorate(existing.Index.GetMailGroup(selected), existing.FromCache);
            CompanyContactIndex? index = null;
            if (state?.RefreshedAtUtc is not null)
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    var rows = await ReadAsync(query => cache.GetPageAsync(query, cancellationToken), selected.Company, cancellationToken);
                    var after = await cache.GetStateAsync(selected.Company, cancellationToken);
                    if (after != state) { state = after; continue; }
                    var candidate = new CompanyContactIndex(selected.Company, rows);
                    if (candidate.IsComplete) index = candidate;
                    break;
                }
            }
            var fromCache = index is not null;
            if (index is null || refreshSource || existing is { FromCache: false } && existing.State == state)
            {
                ImmutableArray<Party>? rows = null;
                try { rows = await ReadAsync(query => directory.GetPartiesAsync(query, cancellationToken), selected.Company, cancellationToken); }
                catch (Exception exception) when (index is not null && exception is not OperationCanceledException && exception is not MailException)
                { /* A complete owned snapshot remains usable when the external source is temporarily unavailable. */ }
                if (rows is { } fresh)
                {
                    var current = new CompanyContactIndex(selected.Company, fresh);
                    if (current.IsComplete) { index = current; fromCache = false; }
                    else if (index is null) throw new MailException(MailFailure.Configuration);
                }
            }
            if (index is null) throw new MailException(MailFailure.Configuration);
            if (_entries.Count >= 32 && !_entries.ContainsKey(selected.Company)) _entries.Remove(_entries.MinBy(pair => pair.Value.LoadedAt).Key);
            _entries[selected.Company] = new(index, state, fromCache, now);
            return Decorate(index.GetMailGroup(selected), fromCache);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (MailException) { throw; }
        catch (Exception) { throw new MailException(MailFailure.ProviderUnavailable); }
        finally { _gate.Release(); }
    }

    private static PartyMailGroup? Decorate(PartyMailGroup? group, bool fromCache) => group is null ? null : group with { IsFromCache = fromCache };

    private static async Task<ImmutableArray<Party>> ReadAsync(Func<PartyQuery, Task<PartyPage>> read, CompanyKey company, CancellationToken token)
    {
        var rows = ImmutableArray.CreateBuilder<Party>();
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var page = await read(new(company, new(rows.Count, 500)));
            if (page.Company != company || page.Page.Items.IsDefault || page.Page.Items.Length > 500 || page.Page.HasMore && page.Page.Items.IsEmpty)
                throw new MailException(MailFailure.InvalidResponse);
            rows.AddRange(page.Page.Items);
            if (rows.Count > 100000) throw new MailException(MailFailure.LimitExceeded);
            if (!page.Page.HasMore) return rows.ToImmutable();
        }
    }

    private sealed record Entry(CompanyContactIndex Index, PartyCacheState? State, bool FromCache, DateTimeOffset LoadedAt);
}

/// <summary>Each provider/cache read is bound to the same current Lexware owner group before and after the await.</summary>
internal sealed class OwnedPartyMailSource(PartyMailScopeResolver scopes, MailCacheService browser, PartyKey selected) : IMailViewSource
{
    private string? _catalogVersion;
    public async Task<MailFolderCatalog> GetFoldersAsync(CancellationToken cancellationToken = default)
    {
        var result = await Read(source => source.GetFoldersAsync(cancellationToken), new MailFolderCatalog([], "Der gewählte Lexware-Datensatz ist nicht mehr freigegeben oder verfügbar."), true, cancellationToken);
        var group = await scopes.ResolveAsync(selected, cancellationToken: cancellationToken);
        _catalogVersion = group?.Version;
        var status = result.Status;
        if (group is not null)
            status = "Zuordnung: " + (group.Owner.Key.Kind == PartyKind.Customer ? "Kunde " : "Lieferant ")
                + (group.Owner.Number ?? "ohne Nummer") + " · " + group.Owner.DisplayName + ". " + status;
        if (group?.IsFromCache == true) status += " Lexware-Zuordnung aus dem gespeicherten Telefoncache.";
        if (group is { InvalidMailFieldCount: > 0 }) status += " " + group.InvalidMailFieldCount + " ungültige Lexware-Mailfelder; gültige Adressen werden berücksichtigt.";
        return result with { Status = status };
    }
    public Task<ImmutableArray<MailListItem>> GetMessagesAsync(MailFolderKey folder, string search, CancellationToken cancellationToken = default) =>
        Read(source => source.GetMessagesAsync(folder, search, cancellationToken), ImmutableArray<MailListItem>.Empty, false, cancellationToken);
    public Task<MailContent?> GetMessageAsync(MailMessageKey message, CancellationToken cancellationToken = default) =>
        Read(source => source.GetMessageAsync(message, cancellationToken), (MailContent?)null, false, cancellationToken);

    private async Task<T> Read<T>(Func<IMailViewSource, Task<T>> read, T empty, bool refresh, CancellationToken token)
    {
        var before = await scopes.ResolveAsync(selected, refresh, token);
        if (before is null) return empty;
        if (!refresh && _catalogVersion is not null && before.Version != _catalogVersion) throw new MailException(MailFailure.Conflict);
        var result = await read(browser.ForParty(before.Owner));
        var after = await scopes.ResolveAsync(selected, cancellationToken: token);
        if (after?.Version != before.Version) throw new MailException(MailFailure.Conflict);
        return result;
    }
}
