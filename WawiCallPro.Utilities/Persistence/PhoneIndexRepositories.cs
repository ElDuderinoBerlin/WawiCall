using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Signals only after the existing Dapper repository has committed the complete cache snapshot.</summary>
public sealed class PhoneIndexCacheRepository(DapperPartyCacheRepository inner, PhoneSearchChanges changes) : IPartyCacheRepository
{
    public Task<PartyCacheState> GetStateAsync(CompanyKey company, CancellationToken cancellationToken = default) => inner.GetStateAsync(company, cancellationToken);
    public Task<Party?> GetAsync(PartyKey key, CancellationToken cancellationToken = default) => inner.GetAsync(key, cancellationToken);
    public Task<PartyPage> GetPageAsync(PartyQuery query, CancellationToken cancellationToken = default) => inner.GetPageAsync(query, cancellationToken);
    public Task<PartyPage> FindByE164Async(CompanyKey company, string e164, PageRequest page, CancellationToken cancellationToken = default) => inner.FindByE164Async(company, e164, page, cancellationToken);
    public async Task<PartyCacheState> ReplaceAsync(PartyCacheReplacement replacement, CancellationToken cancellationToken = default)
    {
        var result = await inner.ReplaceAsync(replacement, cancellationToken).ConfigureAwait(false);
        changes.Invalidate(); return result;
    }
}

/// <summary>Both profile writes and block-rule writes change the company rule revision.</summary>
public sealed class PhoneIndexRuleRepository(DapperPhoneMatchingRepository inner, PhoneSearchChanges changes) : IPhoneMatchingRepository
{
    public Task<StoredPhoneMatchingSettings?> GetSettingsAsync(CompanyKey company, CancellationToken cancellationToken = default) => inner.GetSettingsAsync(company, cancellationToken);
    public Task<Page<StoredPhoneNumberBlockRule>> GetRulesAsync(CompanyKey company, PageRequest page, CancellationToken cancellationToken = default) => inner.GetRulesAsync(company, page, cancellationToken);
    public Task<StoredPhoneNumberBlockRule?> GetRuleAsync(Guid ruleId, CancellationToken cancellationToken = default) => inner.GetRuleAsync(ruleId, cancellationToken);
    public async Task<StoredPhoneMatchingSettings> WriteSettingsAsync(PhoneMatchingSettingsWrite write, CancellationToken cancellationToken = default)
    {
        var result = await inner.WriteSettingsAsync(write, cancellationToken).ConfigureAwait(false);
        changes.Invalidate(); return result;
    }
    public async Task<StoredPhoneNumberBlockRule> WriteRuleAsync(PhoneNumberBlockRuleWrite write, CancellationToken cancellationToken = default)
    {
        var result = await inner.WriteRuleAsync(write, cancellationToken).ConfigureAwait(false);
        changes.Invalidate(); return result;
    }
}
