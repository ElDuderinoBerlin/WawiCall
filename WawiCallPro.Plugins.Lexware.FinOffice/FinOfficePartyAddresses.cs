using System.Collections.Immutable;
using System.Data;
using Dapper;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Plugins.Lexware.FinOffice;

internal static class FinOfficePartyAddresses
{
    internal static async Task ApplyAsync(IDbConnection connection, List<Party> parties, CancellationToken token)
    {
        var rows = await connection.QueryAsync<AddressRow>(new CommandDefinition("""
            SELECT lNr AS OwnerId, szFirma AS Company, szVorName AS FirstName, szName AS LastName,
              szStrasse AS Street, szZusatz AS Extension, szPLZ AS Postalcode, szOrt AS City,
              szLand AS CountryCode, szLandBez AS Country, szEMail AS Email,
              szTel1 AS Telephone, szTel2 AS Telephone2, szFax AS Fax,
              szLieferStrasse AS ShippingStreet, szLieferZusatz AS ShippingExtension,
              szLieferPLZ AS ShippingPostalcode, szLieferOrt AS ShippingCity, szLieferLand AS ShippingCountryCode,
              szLandBezLiefer AS ShippingCountry, szLieferTel AS ShippingTelephone
            FROM fk_kd_view WHERE iKennung IN (0,1)
            """, cancellationToken: token)).ConfigureAwait(false);
        var byId = new Dictionary<string, AddressRow>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested();
            if (!byId.TryAdd(row.OwnerId.ToString(System.Globalization.CultureInfo.InvariantCulture), row)) throw Invalid();
        }
        if (byId.Count != parties.Count) throw Invalid();
        for (var index = 0; index < parties.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            var owner = parties[index];
            if (!byId.TryGetValue(owner.Key.ExternalId, out var row)) throw Invalid();
            var id = owner.Key.ExternalId;
            var addresses = ImmutableArray.CreateBuilder<PartyAddress>();
            addresses.Add(new PartyAddress
            {
                ExternalId = id + "/main", Kind = AddressKind.Billing, Source = new SourceField("fk_kd_view", id, "szStrasse"),
                CompanyName = Clean(row.Company), FirstName = Clean(row.FirstName), LastName = Clean(row.LastName),
                Street = Clean(row.Street), AddressLine2 = Clean(row.Extension), PostalCode = Clean(row.Postalcode), City = Clean(row.City),
                CountryCode = Code(row.CountryCode), Country = Country(row.Country, row.CountryCode), Email = Clean(row.Email),
                Phones = Phones(id, (row.Telephone, PhoneKind.Telephone, "szTel1"), (row.Telephone2, PhoneKind.Telephone, "szTel2"), (row.Fax, PhoneKind.Fax, "szFax"))
            });
            if (new[] { row.ShippingStreet, row.ShippingExtension, row.ShippingPostalcode, row.ShippingCity,
                row.ShippingCountryCode, row.ShippingCountry, row.ShippingTelephone }.Any(value => !string.IsNullOrWhiteSpace(Clean(value))))
                addresses.Add(new PartyAddress
                {
                    ExternalId = id + "/shipping", Kind = AddressKind.Shipping, Source = new SourceField("fk_kd_view", id, "szLieferStrasse"),
                    Street = Clean(row.ShippingStreet), AddressLine2 = Clean(row.ShippingExtension), PostalCode = Clean(row.ShippingPostalcode), City = Clean(row.ShippingCity),
                    CountryCode = Code(row.ShippingCountryCode), Country = Country(row.ShippingCountry, row.ShippingCountryCode),
                    Phones = Phones(id, (row.ShippingTelephone, PhoneKind.Telephone, "szLieferTel"))
                });
            parties[index] = owner with { Addresses = addresses.ToImmutable() };
        }
    }

    private static ImmutableArray<PartyPhone> Phones(string id, params (string? Value, PhoneKind Kind, string Field)[] values)
        => values.Where(value => !string.IsNullOrWhiteSpace(Clean(value.Value)))
            .Select(value => new PartyPhone(Clean(value.Value)!, value.Kind, new SourceField("fk_kd_view", id, value.Field))).ToImmutableArray();
    internal static ImmutableArray<PartyPhone> ContactPhones(string id, params (string? Value, PhoneKind Kind, string Field)[] values)
        => values.Where(value => !string.IsNullOrWhiteSpace(Clean(value.Value)))
            .Select(value => new PartyPhone(Clean(value.Value)!, value.Kind, new SourceField("fk_kontaktperson", id, value.Field))).ToImmutableArray();
    private static string? Clean(string? value) => value?.TrimEnd('\0', ' ');
    private static string? Country(string? name, string? code) => !string.IsNullOrWhiteSpace(Clean(name)) ? Clean(name) : Clean(code);
    private static string? Code(string? value)
    {
        var code = Clean(value)?.Trim();
        return code is { Length: 2 } && code.All(char.IsAsciiLetter) ? code.ToUpperInvariant() : null;
    }
    private static FinOfficeAdapterException Invalid() => new(FinOfficeFailure.InvalidCatalog);
    private sealed class AddressRow
    {
        public int OwnerId { get; set; }
        public string? Company { get; set; } public string? FirstName { get; set; } public string? LastName { get; set; }
        public string? Street { get; set; } public string? Extension { get; set; } public string? Postalcode { get; set; }
        public string? City { get; set; } public string? CountryCode { get; set; } public string? Country { get; set; }
        public string? Email { get; set; } public string? Telephone { get; set; } public string? Telephone2 { get; set; } public string? Fax { get; set; }
        public string? ShippingStreet { get; set; } public string? ShippingExtension { get; set; } public string? ShippingPostalcode { get; set; }
        public string? ShippingCity { get; set; } public string? ShippingCountryCode { get; set; } public string? ShippingCountry { get; set; }
        public string? ShippingTelephone { get; set; }
    }
}
