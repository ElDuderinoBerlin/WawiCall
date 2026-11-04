using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperPhoneMatchingRepository(DatabaseRepositoryContext context) : IPhoneMatchingRepository
{
    public Task<StoredPhoneMatchingSettings?> GetSettingsAsync(CompanyKey company, CancellationToken cancellationToken = default)
    {
        StorageContract.CompanyKey(company);
        return context.RunAsync(false, async db =>
        {
            var owner = await RepositoryData.RequireCompanyAsync(db, company).ConfigureAwait(false);
            var row = await SettingsAsync(db, owner.Text("id")).ConfigureAwait(false);
            return row is null ? null : Settings(row, company);
        }, cancellationToken);
    }

    public Task<StoredPhoneMatchingSettings> WriteSettingsAsync(PhoneMatchingSettingsWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        return context.RunAsync(true, async db =>
        {
            var company = await RepositoryData.RequireCompanyAsync(db, write.Settings.Company).ConfigureAwait(false);
            var id = company.Text("id"); var old = await SettingsAsync(db, id).ConfigureAwait(false);
            RepositorySession.Expected(old, write.ExpectedVersion);
            if (old is not null) RepositoryData.Chronology(write.ChangedAtUtc, old.Utc("changedatutc"));
            var version = old is null ? 1 : RepositorySession.Next(old.Long("writeversion"));
            var revision = old is null ? 1 : RepositorySession.Next(old.Long("rulerevision"));
            var values = new Dictionary<string, object?>
            {
                ["enabled"] = write.Settings.StemMatchingEnabled, ["startwidth"] = write.Settings.StartWidth,
                ["writeversion"] = version, ["rulerevision"] = revision, ["changedatutc"] = DatabaseUtc.Encode(write.ChangedAtUtc)
            };
            if (old is null) { values.Add("companyid", id); await db.InsertAsync("phonematchsettings", values).ConfigureAwait(false); }
            else await db.UpdateAsync("phonematchsettings", "companyid", id, write.ExpectedVersion!.Value, values).ConfigureAwait(false);
            return new StoredPhoneMatchingSettings(write.Settings, version, revision, write.ChangedAtUtc);
        }, cancellationToken);
    }

    public Task<Page<StoredPhoneNumberBlockRule>> GetRulesAsync(CompanyKey company, PageRequest page, CancellationToken cancellationToken = default)
    {
        StorageContract.CompanyKey(company); ArgumentNullException.ThrowIfNull(page);
        return context.RunAsync(false, async db =>
        {
            var owner = await RepositoryData.RequireCompanyAsync(db, company).ConfigureAwait(false);
            var rows = await db.RowsAsync(db.PageSql($"SELECT * FROM {db.T("phonenumberblockrules")} WHERE companyid=@company", "id ASC", page), new { company = owner.Text("id") }).ConfigureAwait(false);
            return RepositorySession.Page(rows.Select(row => Read(row, company)).ToArray(), page);
        }, cancellationToken);
    }

    public Task<StoredPhoneNumberBlockRule?> GetRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var id = DatabaseId.Encode(ruleId);
        return context.RunAsync(false, async db =>
        {
            var row = await db.OneAsync($"SELECT * FROM {db.T("phonenumberblockrules")} WHERE id=@id", new { id }).ConfigureAwait(false);
            return row is null ? null : Read(row, (await RepositoryData.CompanyByIdAsync(db, row.Text("companyid")).ConfigureAwait(false)).Key);
        }, cancellationToken);
    }

    public Task<StoredPhoneNumberBlockRule> WriteRuleAsync(PhoneNumberBlockRuleWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        return context.RunAsync(true, async db =>
        {
            var rule = write.Rule; var id = DatabaseId.Encode(rule.Id);
            var company = await RepositoryData.RequireCompanyAsync(db, rule.Organisation.Company).ConfigureAwait(false);
            var row = await db.OneAsync($"SELECT * FROM {db.T("phonenumberblockrules")} WHERE id=@id", new { id }).ConfigureAwait(false);
            RepositorySession.Expected(row, write.ExpectedVersion);
            if (row is not null)
            {
                var old = Read(row, (await RepositoryData.CompanyByIdAsync(db, row.Text("companyid")).ConfigureAwait(false)).Key);
                if (old.Rule.Organisation != rule.Organisation) throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
                RepositoryData.Chronology(write.ChangedAtUtc, old.ChangedAtUtc);
            }
            var version = row is null ? 1 : RepositorySession.Next(row.Long("writeversion"));
            var values = new Dictionary<string, object?>
            {
                ["kind"] = (int)rule.Kind, ["enabled"] = rule.IsEnabled, ["countrycallingcode"] = rule.CountryCallingCode,
                ["destinationcode"] = rule.DestinationCode, ["stem"] = rule.Stem, ["minsuffixdigits"] = rule.MinimumSuffixDigits,
                ["maxsuffixdigits"] = rule.MaximumSuffixDigits, ["evidence"] = rule.Evidence, ["writeversion"] = version, ["changedatutc"] = DatabaseUtc.Encode(write.ChangedAtUtc)
            };
            if (row is null)
            {
                values.Add("id", id); values.Add("companyid", company.Text("id")); values.Add("partykeyhash", SourceIdentityHash.Party(rule.Organisation));
                values.Add("partykind", (int)rule.Organisation.Kind); values.Add("externalid", rule.Organisation.ExternalId);
                await db.InsertAsync("phonenumberblockrules", values).ConfigureAwait(false);
            }
            else await db.UpdateAsync("phonenumberblockrules", "id", id, write.ExpectedVersion!.Value, values).ConfigureAwait(false);
            var settings = await SettingsAsync(db, company.Text("id")).ConfigureAwait(false);
            if (settings is null)
                await db.InsertAsync("phonematchsettings", new() { ["companyid"] = company.Text("id"), ["enabled"] = false, ["startwidth"] = 5, ["writeversion"] = 1L, ["rulerevision"] = 1L, ["changedatutc"] = DatabaseUtc.Encode(write.ChangedAtUtc) }).ConfigureAwait(false);
            else
                await db.ExecuteAsync($"UPDATE {db.T("phonematchsettings")} SET rulerevision=@revision WHERE companyid=@company",
                    new { revision = RepositorySession.Next(settings.Long("rulerevision")), company = company.Text("id") }).ConfigureAwait(false);
            return new StoredPhoneNumberBlockRule(rule, version, write.ChangedAtUtc);
        }, cancellationToken);
    }

    private static Task<RepositoryRow?> SettingsAsync(RepositorySession db, string company) => db.OneAsync($"SELECT * FROM {db.T("phonematchsettings")} WHERE companyid=@company", new { company });
    private static StoredPhoneMatchingSettings Settings(RepositoryRow row, CompanyKey company) => new(new(company, row.Bool("enabled"), checked((int)row.Long("startwidth"))), row.Long("writeversion"), row.Long("rulerevision"), row.Utc("changedatutc"));
    private static StoredPhoneNumberBlockRule Read(RepositoryRow row, CompanyKey company)
    {
        var key = RepositoryData.PartyKey(row, company, "partykind");
        if (SourceIdentityHash.Party(key) != row.Text("partykeyhash")) RepositoryData.Collision();
        var rule = new PhoneNumberBlockRule
        {
            Id = row.Id("id"), Organisation = key, Kind = (PhoneNumberBlockKind)checked((int)row.Long("kind")), IsEnabled = row.Bool("enabled"),
            CountryCallingCode = checked((int)row.Long("countrycallingcode")), DestinationCode = row.Text("destinationcode"), Stem = row.Text("stem"),
            MinimumSuffixDigits = checked((int)row.Long("minsuffixdigits")), MaximumSuffixDigits = checked((int)row.Long("maxsuffixdigits")), Evidence = row.Text("evidence")
        };
        _ = new PhoneNumberBlockRuleWrite(rule, row.Long("writeversion"), row.Utc("changedatutc"));
        return new(rule, row.Long("writeversion"), row.Utc("changedatutc"));
    }
}
