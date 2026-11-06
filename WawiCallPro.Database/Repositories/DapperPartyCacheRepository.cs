using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperPartyCacheRepository(DatabaseRepositoryContext context) : IPartyCacheRepository
{
    public Task<PartyCacheState> GetStateAsync(CompanyKey company, CancellationToken cancellationToken = default)
    {
        StorageContract.CompanyKey(company);
        return context.RunAsync(false, async db => State(company, await RepositoryData.RequireCompanyAsync(db, company).ConfigureAwait(false)), cancellationToken);
    }
    public Task<Party?> GetAsync(PartyKey key, CancellationToken cancellationToken = default)
    {
        StorageContract.PartyKey(key);
        return context.RunAsync(false, async db =>
        {
            var company = await RepositoryData.FindCompanyAsync(db, key.Company).ConfigureAwait(false);
            if (company is null) return null;
            var row = await db.OneAsync($"SELECT * FROM {db.T("parties")} WHERE sourcehash=@hash", new { hash = SourceIdentityHash.Party(key) }).ConfigureAwait(false);
            if (row is null) return null;
            if (row.Text("companyid") != company.Text("id") || RepositoryData.PartyKey(row, key.Company) != key) RepositoryData.Collision();
            return (await LoadAsync(db, key.Company, [row]).ConfigureAwait(false))[0];
        }, cancellationToken);
    }
    public Task<PartyPage> GetPageAsync(PartyQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query); StorageContract.CompanyKey(query.Company); ArgumentNullException.ThrowIfNull(query.Page);
        if (query.Kind is { } kind && !Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(query));
        StorageContract.Text(query.SearchText, 1024, nameof(query.SearchText));
        return context.RunAsync(false, async db =>
        {
            var company = await RepositoryData.RequireCompanyAsync(db, query.Company).ConfigureAwait(false);
            var where = "companyid=@company" + (query.Kind is null ? "" : " AND kind=@kind");
            var pattern = query.SearchText?.ToUpperInvariant().Replace("!", "!!", StringComparison.Ordinal).Replace("%", "!%", StringComparison.Ordinal).Replace("_", "!_", StringComparison.Ordinal).Replace("[", "![", StringComparison.Ordinal);
            if (query.SearchText is not null) where += " AND (" + db.Contains("searchname", "@search") + " OR " + db.Contains("searchnumber", "@search") + ")";
            var rows = await db.RowsAsync(db.PageSql($"SELECT * FROM {db.T("parties")} WHERE {where}", "sourcehash ASC", query.Page),
                new { company = company.Text("id"), kind = (int?)query.Kind, search = "%" + pattern + "%" }).ConfigureAwait(false);
            var parties = await LoadAsync(db, query.Company, rows.Take(query.Page.Limit).ToArray()).ConfigureAwait(false);
            return new PartyPage(query.Company, new(parties.ToImmutableArray(), rows.Count > query.Page.Limit));
        }, cancellationToken);
    }
    public Task<PartyPage> FindByE164Async(CompanyKey company, string e164, PageRequest page, CancellationToken cancellationToken = default)
    {
        StorageContract.CompanyKey(company); ArgumentNullException.ThrowIfNull(e164); ArgumentNullException.ThrowIfNull(page);
        StorageContract.E164(e164);
        return context.RunAsync(false, async db =>
        {
            var owner = await RepositoryData.RequireCompanyAsync(db, company).ConfigureAwait(false);
            var sql = $"SELECT p.* FROM {db.T("parties")} p WHERE p.companyid=@company AND EXISTS(SELECT 1 FROM {db.T("partyphones")} n WHERE n.partyid=p.id AND n.e164=@e164)";
            var rows = await db.RowsAsync(db.PageSql(sql, "p.sourcehash ASC", page), new { company = owner.Text("id"), e164 }).ConfigureAwait(false);
            var parties = await LoadAsync(db, company, rows.Take(page.Limit).ToArray()).ConfigureAwait(false);
            return new PartyPage(company, new(parties.ToImmutableArray(), rows.Count > page.Limit));
        }, cancellationToken);
    }

    public Task<PartyCacheState> ReplaceAsync(PartyCacheReplacement replacement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        return context.RunAsync(true, async db =>
        {
            var company = await RepositoryData.RequireCompanyAsync(db, replacement.Company).ConfigureAwait(false);
            if (company.Long("cacheversion") != replacement.ExpectedRevision) throw new RepositoryConflictException(RepositoryConflictKind.VersionMismatch);
            if (company.OptionalUtc("cacherefreshedatutc") is { } previous) RepositoryData.Chronology(replacement.RefreshedAtUtc, previous);
            var old = await db.RowsAsync($"SELECT * FROM {db.T("parties")} WHERE companyid=@id", new { id = company.Text("id") }).ConfigureAwait(false);
            var oldParties = old.ToDictionary(row => row.Text("sourcehash"));
            foreach (var row in old) if (SourceIdentityHash.Party(RepositoryData.PartyKey(row, replacement.Company)) != row.Text("sourcehash")) RepositoryData.Collision();
            var oldAddresses = await db.RowsAsync($"SELECT a.* FROM {db.T("partyaddresses")} a JOIN {db.T("parties")} p ON p.id=a.partyid WHERE p.companyid=@id", new { id = company.Text("id") }).ConfigureAwait(false);
            var addressIds = oldAddresses.ToDictionary(row => row.Text("sourcehash"), row => row.Text("id"));
            var incoming = replacement.Parties.ToDictionary(party => SourceIdentityHash.Party(party.Key));
            foreach (var batch in incoming.Keys.Chunk(500))
            {
                var existing = await db.RowsAsync($"SELECT * FROM {db.T("parties")} WHERE {db.In("sourcehash", "@hashes")}", new { hashes = batch }).ConfigureAwait(false);
                foreach (var row in existing)
                    if (row.Text("companyid") != company.Text("id") || RepositoryData.PartyKey(row, replacement.Company) != incoming[row.Text("sourcehash")].Key) RepositoryData.Collision();
            }
            await CheckAddressHashesAsync(db, replacement.Parties, replacement.Company).ConfigureAwait(false);
            await db.ExecuteAsync($"DELETE FROM {db.T("partyphones")} WHERE partyid IN (SELECT id FROM {db.T("parties")} WHERE companyid=@id)", new { id = company.Text("id") }).ConfigureAwait(false);
            await db.ExecuteAsync($"DELETE FROM {db.T("partyaddresses")} WHERE partyid IN (SELECT id FROM {db.T("parties")} WHERE companyid=@id)", new { id = company.Text("id") }).ConfigureAwait(false);
            await db.ExecuteAsync($"DELETE FROM {db.T("parties")} WHERE companyid=@id", new { id = company.Text("id") }).ConfigureAwait(false);
            foreach (var (hash, party) in incoming)
            {
                var id = oldParties.TryGetValue(hash, out var existing) ? existing.Text("id") : DatabaseId.Encode(Guid.NewGuid());
                await db.InsertAsync("parties", new()
                {
                    ["id"] = id, ["companyid"] = company.Text("id"), ["sourcehash"] = hash, ["kind"] = (int)party.Key.Kind,
                    ["externalid"] = party.Key.ExternalId, ["ownerexternalid"] = party.Key.OwnerExternalId, ["displayname"] = party.DisplayName,
                    ["number"] = party.Number, ["email"] = party.Email, ["isinactive"] = party.IsInactive,
                    ["sourcerecordtype"] = party.Source?.RecordType, ["sourcerecordid"] = party.Source?.RecordId,
                    ["sourceupdatedatutc"] = party.SourceUpdatedAtUtc is { } changed ? DatabaseUtc.Encode(changed) : null,
                    ["refreshedatutc"] = DatabaseUtc.Encode(replacement.RefreshedAtUtc), ["searchname"] = party.DisplayName.ToUpperInvariant(), ["searchnumber"] = party.Number?.ToUpperInvariant()
                }).ConfigureAwait(false);
                var position = 0;
                foreach (var phone in party.Phones) await InsertPhoneAsync(db, id, null, position++, phone).ConfigureAwait(false);
                for (var index = 0; index < party.Addresses.Length; index++)
                {
                    var address = party.Addresses[index]; var addressHash = SourceIdentityHash.Address(party.Key, address.Kind, address.ExternalId);
                    var addressId = addressIds.TryGetValue(addressHash, out var oldId) ? oldId : DatabaseId.Encode(Guid.NewGuid());
                    await db.InsertAsync("partyaddresses", new()
                    {
                        ["id"] = addressId, ["partyid"] = id, ["sourcehash"] = addressHash, ["externalid"] = address.ExternalId, ["kind"] = (int)address.Kind, ["position"] = index,
                        ["companyname"] = address.CompanyName, ["firstname"] = address.FirstName, ["lastname"] = address.LastName,
                        ["street"] = address.Street, ["housenumber"] = address.HouseNumber, ["addressline2"] = address.AddressLine2,
                        ["postalcode"] = address.PostalCode, ["city"] = address.City, ["countrycode"] = address.CountryCode, ["email"] = address.Email,
                        ["sourcecountry"] = address.Country, ["sourcerecordtype"] = address.Source?.RecordType,
                        ["sourcerecordid"] = address.Source?.RecordId, ["sourcefield"] = address.Source?.FieldName
                    }).ConfigureAwait(false);
                    foreach (var phone in address.Phones) await InsertPhoneAsync(db, id, addressId, position++, phone).ConfigureAwait(false);
                }
            }
            var version = RepositorySession.Next(replacement.ExpectedRevision);
            if (await db.ExecuteAsync($"UPDATE {db.T("companies")} SET cacheversion=@version,cacherefreshedatutc=@time,cacheparserversion=@parser,cachecontextversion=@context WHERE id=@id AND cacheversion=@old",
                new { version, time = DatabaseUtc.Encode(replacement.RefreshedAtUtc), parser = replacement.ParserVersion, context = replacement.ContextVersion, id = company.Text("id"), old = replacement.ExpectedRevision }).ConfigureAwait(false) != 1)
                throw new RepositoryConflictException(RepositoryConflictKind.VersionMismatch);
            return new PartyCacheState(replacement.Company, version, replacement.RefreshedAtUtc) { ParserVersion = replacement.ParserVersion, ContextVersion = replacement.ContextVersion };
        }, cancellationToken);
    }

    private static PartyCacheState State(CompanyKey company, RepositoryRow row) => new(company, row.Long("cacheversion"), row.OptionalUtc("cacherefreshedatutc"))
    { ParserVersion = row.OptionalText("cacheparserversion"), ContextVersion = row.OptionalText("cachecontextversion") };

    private static Task<int> InsertPhoneAsync(RepositorySession db, string party, string? address, int position, PartyPhone phone) => db.InsertAsync("partyphones", new()
    {
        ["id"] = DatabaseId.Encode(Guid.NewGuid()), ["partyid"] = party, ["addressid"] = address, ["position"] = position, ["kind"] = (int)phone.Kind,
        ["originalvalue"] = phone.OriginalValue, ["e164"] = phone.E164, ["extension"] = phone.Extension,
        ["sourcerecordtype"] = phone.Source.RecordType, ["sourcerecordid"] = phone.Source.RecordId, ["sourcefieldname"] = phone.Source.FieldName
    });

    private static async Task CheckAddressHashesAsync(RepositorySession db, IEnumerable<Party> parties, CompanyKey company)
    {
        var expected = parties.SelectMany(party => party.Addresses.Select(address => (Hash: SourceIdentityHash.Address(party.Key, address.Kind, address.ExternalId), party.Key, Address: address)))
            .ToDictionary(item => item.Hash);
        foreach (var batch in expected.Keys.Chunk(500))
        {
            var rows = await db.RowsAsync($"SELECT a.*,p.companyid,p.kind AS partykind,p.externalid AS partyexternalid,p.ownerexternalid AS partyowner FROM {db.T("partyaddresses")} a JOIN {db.T("parties")} p ON p.id=a.partyid WHERE {db.In("a.sourcehash", "@hashes")}", new { hashes = batch }).ConfigureAwait(false);
            foreach (var row in rows)
            {
                var owner = await RepositoryData.CompanyByIdAsync(db, row.Text("companyid")).ConfigureAwait(false);
                var key = new PartyKey(owner.Key, (PartyKind)checked((int)row.Long("partykind")), row.Text("partyexternalid"), row.OptionalText("partyowner"));
                var wanted = expected[row.Text("sourcehash")];
                if (owner.Key != company || key != wanted.Key || row.Long("kind") != (int)wanted.Address.Kind || row.Text("externalid") != wanted.Address.ExternalId) RepositoryData.Collision();
            }
        }
    }

    private static async Task<List<Party>> LoadAsync(RepositorySession db, CompanyKey company, IReadOnlyList<RepositoryRow> rows)
    {
        if (rows.Count == 0) return [];
        var ids = rows.Select(row => row.Text("id")).ToArray();
        var addresses = await db.RowsAsync($"SELECT * FROM {db.T("partyaddresses")} WHERE {db.In("partyid", "@ids")} ORDER BY partyid,{db.Q("position")}", new { ids }).ConfigureAwait(false);
        var phones = await db.RowsAsync($"SELECT * FROM {db.T("partyphones")} WHERE {db.In("partyid", "@ids")} ORDER BY partyid,{db.Q("position")}", new { ids }).ConfigureAwait(false);
        PartyPhone Phone(RepositoryRow row) => new(row.Text("originalvalue"), (PhoneKind)checked((int)row.Long("kind")),
            new(row.Text("sourcerecordtype"), row.Text("sourcerecordid"), row.Text("sourcefieldname")), row.OptionalText("e164"), row.OptionalText("extension"));
        var result = new List<Party>();
        foreach (var row in rows)
        {
            var key = RepositoryData.PartyKey(row, company);
            if (SourceIdentityHash.Party(key) != row.Text("sourcehash")) RepositoryData.Collision();
            var ownedAddresses = addresses.Where(address => address.Text("partyid") == row.Text("id")).ToArray();
            var ownedPhones = phones.Where(phone => phone.Text("partyid") == row.Text("id")).ToArray();
            if (ownedPhones.Any(phone => phone.OptionalText("addressid") is { } id && !ownedAddresses.Any(address => address.Text("id") == id))) throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
            var party = new Party
            {
                Key = key, DisplayName = row.Text("displayname"), Number = row.OptionalText("number"), Email = row.OptionalText("email"), IsInactive = row.Bool("isinactive"), SourceUpdatedAtUtc = row.OptionalUtc("sourceupdatedatutc"),
                Source = row.OptionalText("sourcerecordtype") is null && row.OptionalText("sourcerecordid") is null
                    ? null : new SourceRecord(row.Text("sourcerecordtype"), row.Text("sourcerecordid")),
                Phones = ownedPhones.Where(phone => phone.Value("addressid") is null).Select(Phone).ToImmutableArray(),
                Addresses = ownedAddresses.Select(address =>
                {
                    var kind = (AddressKind)checked((int)address.Long("kind"));
                    if (SourceIdentityHash.Address(key, kind, address.Text("externalid")) != address.Text("sourcehash")) RepositoryData.Collision();
                    return new PartyAddress
                    {
                        ExternalId = address.Text("externalid"), Kind = kind, CompanyName = address.OptionalText("companyname"), FirstName = address.OptionalText("firstname"), LastName = address.OptionalText("lastname"),
                        Street = address.OptionalText("street"), HouseNumber = address.OptionalText("housenumber"), AddressLine2 = address.OptionalText("addressline2"), PostalCode = address.OptionalText("postalcode"), City = address.OptionalText("city"), CountryCode = address.OptionalText("countrycode"), Email = address.OptionalText("email"),
                        Country = address.OptionalText("sourcecountry"),
                        Source = address.OptionalText("sourcerecordtype") is null && address.OptionalText("sourcerecordid") is null && address.OptionalText("sourcefield") is null
                            ? null : new SourceField(address.Text("sourcerecordtype"), address.Text("sourcerecordid"), address.Text("sourcefield")),
                        Phones = ownedPhones.Where(phone => phone.OptionalText("addressid") == address.Text("id")).Select(Phone).ToImmutableArray()
                    };
                }).ToImmutableArray()
            };
            StorageContract.Party(party); result.Add(party);
        }
        return result;
    }
}
