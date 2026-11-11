using System.Collections.Immutable;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Stages complete source pages before publishing a version-checked company snapshot.</summary>
public sealed class PhoneCacheService(
    ConfigurationStore settings,
    ApplicationConfigurationService activeSettings,
    ApplicationDescriptor application,
    ICommunicationDatabase database,
    IBusinessDirectory directory,
    ICompanyRepository companies,
    IPartyCacheRepository cache,
    INumberParseLogRepository errors,
    IPhoneNumberParser parser,
    IPhoneNumberInterpreter interpreter,
    TimeProvider clock,
    IPhoneCacheMetadata metadata) : IPhoneCacheService
{
    private readonly SemaphoreSlim _gate = new(1);

    public Task<PhoneCacheRefreshResult> RefreshAsync(IProgress<PhoneCacheProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => RefreshCoreAsync(progress, cancellationToken), cancellationToken);

    private async Task<PhoneCacheRefreshResult> RefreshCoreAsync(IProgress<PhoneCacheProgress>? progress, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var snapshot = await settings.LoadAsync(token).ConfigureAwait(false);
            var selected = snapshot.Options.Companies.Where(company => company.IncludeInPhoneCache).ToArray();
            if (selected.Length == 0) return new([]);
            if (!SameDatabase(snapshot.Options.Database, activeSettings.Current.Options.Database))
                throw new PhoneCacheRefreshException(PhoneCacheRefreshFailure.RestartRequired);
            if ((await database.GetStatusAsync(token).ConfigureAwait(false)).State != DatabaseSchemaState.Current)
                throw new PhoneCacheRefreshException(PhoneCacheRefreshFailure.DatabaseNotReady);

            ImmutableArray<Company> authorized;
            try { authorized = await directory.GetCompaniesAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch { throw new PhoneCacheRefreshException(PhoneCacheRefreshFailure.CatalogUnavailable); }
            if (authorized.IsDefault || authorized.Any(company => company?.Key is null) || authorized.Select(company => company.Key).Distinct().Count() != authorized.Length)
                throw new PhoneCacheRefreshException(PhoneCacheRefreshFailure.InvalidSource);
            var catalog = authorized.ToDictionary(company => company.Key);
            var results = ImmutableArray.CreateBuilder<PhoneCacheCompanyResult>();
            for (var index = 0; index < selected.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                var selection = selected[index];
                if (selection.Company.ProductLine.ToString() != application.ProductLine || !catalog.TryGetValue(selection.Company, out var company))
                {
                    results.Add(new(selection.Company, selection.DisplayName ?? "Gespeicherter Mandant", PhoneCacheRefreshState.NotAuthorized));
                    continue;
                }
                try
                {
                    results.Add(await RefreshCompanyAsync(company, selection, index + 1, selected.Length, progress, token).ConfigureAwait(false));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception failure)
                {
                    results.Add(new(company.Key, company.DisplayName, PhoneCacheRefreshState.Failed, Failure: failure switch
                    {
                        RepositoryConflictException => PhoneCacheRefreshFailure.Conflict,
                        DatabaseOperationException => PhoneCacheRefreshFailure.StorageUnavailable,
                        PhoneCacheRefreshException refreshFailure => refreshFailure.Failure,
                        NotSupportedException => PhoneCacheRefreshFailure.Unsupported,
                        _ => PhoneCacheRefreshFailure.SourceUnavailable
                    }));
                }
            }
            return new(results.ToImmutable());
        }
        finally { _gate.Release(); }
    }

    private async Task<PhoneCacheCompanyResult> RefreshCompanyAsync(Company company, CompanySettings selection,
        int number, int total, IProgress<PhoneCacheProgress>? progress, CancellationToken token)
    {
        var existing = await companies.GetAsync(company.Key, token).ConfigureAwait(false);
        // An unregistered company has no published cache. Registration happens only after a complete source read.
        var revision = existing is null ? 0 : (await cache.GetStateAsync(company.Key, token).ConfigureAwait(false)).Revision;
        var parties = ImmutableArray.CreateBuilder<Party>();
        var keys = new HashSet<PartyKey>();
        var failures = new List<NumberParseFailure>();
        var context = new CompanyPhoneNumberContext(company.Key, selection.DefaultPhoneRegion, selection.LocalAreaCode);
        var phoneCount = 0;
        var observedAt = clock.GetUtcNow();
        var offset = 0;
        while (true)
        {
            progress?.Report(new(company.DisplayName, number, total, PhoneCacheRefreshPhase.Reading, parties.Count));
            var response = await directory.GetPartiesAsync(new(company.Key, new PageRequest(offset, 500)), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (response is null || response.Company != company.Key || response.Page is null || response.Page.Items.IsDefault ||
                response.Page.Items.Length > 500 || (response.Page.HasMore && response.Page.Items.IsEmpty))
                throw new PhoneCacheRefreshException(PhoneCacheRefreshFailure.InvalidSource);
            progress?.Report(new(company.DisplayName, number, total, PhoneCacheRefreshPhase.Normalizing, parties.Count));
            foreach (var party in response.Page.Items)
            {
                token.ThrowIfCancellationRequested();
                if (party?.Key.Company != company.Key || party.Addresses.IsDefault || party.Addresses.Any(address => address is null) || !keys.Add(party.Key))
                    throw new PhoneCacheRefreshException(PhoneCacheRefreshFailure.InvalidSource);
                parties.Add(party with
                {
                    Phones = Normalize(party.Key, party.Phones),
                    Addresses = party.Addresses.Select(address => address with { Phones = Normalize(party.Key, address.Phones) }).ToImmutableArray()
                });
            }
            if (!response.Page.HasMore) break;
            offset = checked(offset + response.Page.Items.Length);
        }

        if (!await IsSelectionCurrentAsync(selection, token).ConfigureAwait(false))
            return new(company.Key, company.DisplayName, PhoneCacheRefreshState.SelectionChanged);

        var replacement = new PartyCacheReplacement(company.Key, revision, parties.ToImmutable(), observedAt, parser.Version, ContextVersion(selection));
        progress?.Report(new(company.DisplayName, number, total, PhoneCacheRefreshPhase.Saving, parties.Count));
        if (existing is null || existing.Company != company)
            await companies.WriteAsync(new(company, existing?.Version, observedAt), token).ConfigureAwait(false);
        // These are independently observed source errors, also retained if a later cache commit conflicts.
        foreach (var failure in failures) await errors.AppendAsync(failure, token).ConfigureAwait(false);
        if (!await IsSelectionCurrentAsync(selection, token).ConfigureAwait(false))
            return new(company.Key, company.DisplayName, PhoneCacheRefreshState.SelectionChanged);
        var saved = await cache.ReplaceAsync(replacement, token).ConfigureAwait(false);
        return new(company.Key, company.DisplayName, PhoneCacheRefreshState.Updated, parties.Count, phoneCount, failures.Count, saved.Revision);

        ImmutableArray<PartyPhone> Normalize(PartyKey party, ImmutableArray<PartyPhone> phones)
        {
            var values = PhoneCacheNormalization.Normalize(party, phones, context, interpreter, observedAt, failures, token);
            phoneCount += values.Length;
            return values;
        }
    }

    private async Task<bool> IsSelectionCurrentAsync(CompanySettings selection, CancellationToken token)
    {
        var latest = await settings.LoadAsync(token).ConfigureAwait(false);
        var current = latest.Options.FindCompanySettings(selection.Company);
        return current?.IncludeInPhoneCache == true && current.DefaultPhoneRegion == selection.DefaultPhoneRegion && current.LocalAreaCode == selection.LocalAreaCode &&
            SameDatabase(latest.Options.Database, activeSettings.Current.Options.Database);
    }

    private string ContextVersion(CompanySettings company) => metadata.GetContextVersion(company);

    private static bool SameDatabase(CommunicationDatabaseSettings first, CommunicationDatabaseSettings second) =>
        PhoneCacheMetadata.SameDatabase(first, second);
}
