using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Reads one version-checked company snapshot. A concurrent cache/rule write aborts publication.</summary>
public sealed class CompanyPhoneMatchingService(ICompanyRepository companies, IPartyCacheRepository cache,
    IPhoneMatchingRepository rules, ICompanyPhoneMatchIndexFactory indexes, ApplicationDescriptor application,
    TimeProvider clock) : ICompanyPhoneMatchingService
{
    public async Task<PhoneMatchingProfile> GetProfileAsync(CompanyKey company, CancellationToken cancellationToken = default)
    {
        await RequireCompanyAsync(company, cancellationToken).ConfigureAwait(false);
        return await ReadProfileAsync(company, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PhoneMatchingProfile> SaveProfileAsync(PhoneMatchingSettings settings, long? expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var write = new PhoneMatchingSettingsWrite(settings, expectedVersion, clock.GetUtcNow());
        await RequireCompanyAsync(settings.Company, cancellationToken).ConfigureAwait(false);
        var saved = await rules.WriteSettingsAsync(write, cancellationToken).ConfigureAwait(false);
        return new(saved.Settings, saved.Version, saved.RuleRevision);
    }

    public Task<ICompanyPhoneMatchIndex> BuildIndexAsync(CompanyKey company, CancellationToken cancellationToken = default) =>
        Task.Run(() => BuildCoreAsync(company, cancellationToken), cancellationToken);

    private async Task<ICompanyPhoneMatchIndex> BuildCoreAsync(CompanyKey company, CancellationToken token)
    {
        await RequireCompanyAsync(company, token).ConfigureAwait(false);
        var before = await cache.GetStateAsync(company, token).ConfigureAwait(false);
        var profile = await ReadProfileAsync(company, token).ConfigureAwait(false);
        var parties = ImmutableArray.CreateBuilder<Party>();
        var blocks = ImmutableArray.CreateBuilder<PhoneNumberBlockRule>();
        for (var offset = 0; ; offset = checked(offset + 500))
        {
            var page = await cache.GetPageAsync(new PartyQuery(company, new PageRequest(offset, 500)), token).ConfigureAwait(false);
            parties.AddRange(page.Page.Items);
            if (!page.Page.HasMore) break;
        }
        for (var offset = 0; ; offset = checked(offset + 500))
        {
            var page = await rules.GetRulesAsync(company, new PageRequest(offset, 500), token).ConfigureAwait(false);
            blocks.AddRange(page.Items.Select(item => item.Rule));
            if (!page.HasMore) break;
        }
        var after = await cache.GetStateAsync(company, token).ConfigureAwait(false);
        var latestProfile = await ReadProfileAsync(company, token).ConfigureAwait(false);
        if (before != after || profile != latestProfile)
            throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
        return indexes.Create(new(profile, before.Revision, before.ParserVersion,
            before.Revision > 0 && before.RefreshedAtUtc is not null
                && PhoneCacheMetadata.IsContextFingerprint(before.ContextVersion),
            parties.ToImmutable(), blocks.ToImmutable()), token);
    }

    private async Task RequireCompanyAsync(CompanyKey company, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(company);
        if (company.ProductLine.ToString() != application.ProductLine)
            throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
        if (await companies.GetAsync(company, token).ConfigureAwait(false) is null)
            throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
    }
    private async Task<PhoneMatchingProfile> ReadProfileAsync(CompanyKey company, CancellationToken token)
    {
        var stored = await rules.GetSettingsAsync(company, token).ConfigureAwait(false);
        return stored is null ? new(new(company, false, 5), null, 0) : new(stored.Settings, stored.Version, stored.RuleRevision);
    }
}
