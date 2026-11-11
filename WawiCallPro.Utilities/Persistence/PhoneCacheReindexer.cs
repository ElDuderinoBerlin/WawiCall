using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Renormalizes an existing complete local source snapshot. Does not fetch ERP or make old data appear newer.</summary>
public sealed class PhoneCacheReindexer(IPartyCacheRepository cache, INumberParseLogRepository errors,
    IPhoneNumberInterpreter interpreter, IPhoneCacheMetadata metadata, ConfigurationStore settings,
    ApplicationConfigurationService activeSettings, TimeProvider clock)
{
    public async Task<PartyCacheState> ReindexAsync(CompanySettings selection, CancellationToken token)
    {
        var before = await cache.GetStateAsync(selection.Company, token).ConfigureAwait(false);
        var parser = metadata.ParserVersion;
        var fingerprint = metadata.GetContextVersion(selection);
        if (before.Revision == 0 || before.RefreshedAtUtc is null || before.ParserVersion == parser && before.ContextVersion == fingerprint) return before;
        var parties = ImmutableArray.CreateBuilder<Party>();
        var failures = new List<NumberParseFailure>();
        var context = new CompanyPhoneNumberContext(selection.Company, selection.DefaultPhoneRegion, selection.LocalAreaCode);
        var observedAt = clock.GetUtcNow();
        for (var offset = 0; ; offset = checked(offset + 500))
        {
            token.ThrowIfCancellationRequested();
            var page = await cache.GetPageAsync(new PartyQuery(selection.Company, new(offset, 500)), token).ConfigureAwait(false);
            if (page.Company != selection.Company || page.Page.Items.IsDefault || page.Page.HasMore && page.Page.Items.IsEmpty)
                throw new PhoneCacheRefreshException(PhoneCacheRefreshFailure.InvalidSource);
            foreach (var party in page.Page.Items)
            {
                if (party.Key.Company != selection.Company || party.Addresses.IsDefault) throw new PhoneCacheRefreshException(PhoneCacheRefreshFailure.InvalidSource);
                parties.Add(party with
                {
                    Phones = PhoneCacheNormalization.Normalize(party.Key, party.Phones, context, interpreter, observedAt, failures, token),
                    Addresses = party.Addresses.Select(address => address with
                    { Phones = PhoneCacheNormalization.Normalize(party.Key, address.Phones, context, interpreter, observedAt, failures, token) }).ToImmutableArray()
                });
            }
            if (!page.Page.HasMore) break;
        }
        var current = (await settings.LoadAsync(token).ConfigureAwait(false)).Options;
        var configured = current.FindCompanySettings(selection.Company);
        if (configured?.IncludeInPhoneCache != true || metadata.GetContextVersion(configured) != fingerprint || metadata.ParserVersion != parser
            || !PhoneCacheMetadata.SameDatabase(current.Database, activeSettings.Current.Options.Database))
            throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
        foreach (var failure in failures) await errors.AppendAsync(failure, token).ConfigureAwait(false);
        return await cache.ReplaceAsync(new(selection.Company, before.Revision, parties.ToImmutable(), before.RefreshedAtUtc.Value, parser, fingerprint), token).ConfigureAwait(false);
    }
}
