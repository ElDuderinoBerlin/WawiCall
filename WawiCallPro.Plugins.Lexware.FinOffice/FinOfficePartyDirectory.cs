using System.Collections.Immutable;
using System.Data;
using System.Globalization;
using Dapper;
using Quantarix.Pcfk.DataAccess.Entities;
using Quantarix.Pcfk.DataAccess.Repositories;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Plugins.Lexware.FinOffice;

/// <summary>Reads master data and contact ownership inside a single explicitly scoped read-only operation.</summary>
public sealed class FinOfficePartyDirectory(IFinOfficeCompanySession sessions)
{
    public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Company);
        ArgumentNullException.ThrowIfNull(query.Page);
        if (query.Kind is { } kind && !Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(query));
        return sessions.ReadAsync(query.Company, async (connection, token) =>
        {
            var parties = await ReadSnapshotAsync(connection, query.Company, token).ConfigureAwait(false);
            var page = parties.Where(party => (query.Kind is null || party.Key.Kind == query.Kind)
                && (string.IsNullOrEmpty(query.SearchText) || Contains(party.DisplayName, query.SearchText)
                    || Contains(party.Number, query.SearchText) || Contains(party.Email, query.SearchText)))
                .OrderBy(party => party.Key.Kind).ThenBy(party => party.Key.OwnerExternalId, StringComparer.Ordinal)
                .ThenBy(party => party.Key.ExternalId, StringComparer.Ordinal)
                .Skip(query.Page.Offset).Take(query.Page.Limit + 1).ToImmutableArray();
            return new PartyPage(query.Company, new Page<Party>(page.Take(query.Page.Limit).ToImmutableArray(), page.Length > query.Page.Limit));
        }, cancellationToken);
    }

    public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return sessions.ReadAsync<Party?>(key.Company, async (connection, token) =>
            (await ReadSnapshotAsync(connection, key.Company, token).ConfigureAwait(false)).SingleOrDefault(party => party.Key == key), cancellationToken);
    }

    private static async Task<ImmutableArray<Party>> ReadSnapshotAsync(IDbConnection connection, CompanyKey company, CancellationToken token)
    {
        var parties = new List<Party>();
        // Both roles share the physical fk_kd source and its unique lNr; business numbers are not keys.
        foreach (var customer in await new PcfkKundeRepository(connection).GetAllAsync().ConfigureAwait(false))
        {
            token.ThrowIfCancellationRequested();
            if (customer.Nr <= 0 || customer.Kennung != 0) throw InvalidCatalog();
            parties.Add(MapOwner(company, PartyKind.Customer, customer.Nr, customer.KundenNr, customer.Firma,
                customer.Vorname, customer.Name, customer.Matchcode, customer.EMail, customer.Geloescht));
        }
        foreach (var supplier in await new PcfkLieferantRepository(connection).GetAllAsync().ConfigureAwait(false))
        {
            token.ThrowIfCancellationRequested();
            if (supplier.Nr <= 0 || supplier.Kennung != 1) throw InvalidCatalog();
            parties.Add(MapOwner(company, PartyKind.Supplier, supplier.Nr, supplier.LieferantenNr, supplier.Firma,
                supplier.Vorname, supplier.Name, supplier.Matchcode, supplier.EMail, supplier.Geloescht));
        }
        if (parties.Select(party => party.Key.ExternalId).Distinct(StringComparer.Ordinal).Count() != parties.Count) throw InvalidCatalog();
        await FinOfficePartyAddresses.ApplyAsync(connection, parties, token).ConfigureAwait(false);
        var owners = parties.ToDictionary(party => party.Key.ExternalId, StringComparer.Ordinal);
        var people = (await new PcfkKontaktRepository(connection).GetKontaktpersonenAsync().ConfigureAwait(false)).ToArray();
        if (people.Any(person => person.Id <= 0) || people.Select(person => person.Id).Distinct().Count() != people.Length) throw InvalidCatalog();
        var contacts = people.ToDictionary(person => person.Id);
        // lIdZuordnung in fk_kontaktperson2zuordnung is not an owner type. Only this explicit FK binds a contact.
        var links = await connection.QueryAsync<ContactOwner>(new CommandDefinition(
            "SELECT lIdKunde AS OwnerId, lIdKontaktperson AS ContactId FROM fk_kunde2kontaktperson", cancellationToken: token)).ConfigureAwait(false);
        var contactKeys = new HashSet<PartyKey>();
        foreach (var link in links)
        {
            token.ThrowIfCancellationRequested();
            if (!owners.TryGetValue(Id(link.OwnerId), out var owner) || !contacts.TryGetValue(link.ContactId, out var contact)) throw InvalidCatalog();
            var key = new PartyKey(company, owner.Key.Kind == PartyKind.Customer ? PartyKind.CustomerContact : PartyKind.SupplierContact,
                Id(contact.Id), owner.Key.ExternalId);
            if (!contactKeys.Add(key)) throw InvalidCatalog();
            parties.Add(new Party
            {
                Key = key, Source = new SourceRecord("fk_kontaktperson", Id(contact.Id)),
                DisplayName = Name(null, contact.Vorname, contact.Name, contact.EMail1, null, Id(contact.Id)),
                Email = Clean(contact.EMail1), IsInactive = owner.IsInactive,
                Phones = FinOfficePartyAddresses.ContactPhones(Id(contact.Id),
                    (contact.Telefon1, PhoneKind.Telephone, "szTel1"), (contact.Telefon2, PhoneKind.Telephone, "szTel2"),
                    (contact.Mobil, PhoneKind.Mobile, "szMobil"), (contact.Fax, PhoneKind.Fax, "szFax"))
            });
        }
        token.ThrowIfCancellationRequested();
        return parties.ToImmutableArray();
    }

    private static Party MapOwner(CompanyKey company, PartyKind kind, int nr, string? number, string? organization,
        string? first, string? last, string? matchcode, string? email, int? deleted) => new()
    {
        Key = new PartyKey(company, kind, Id(nr)), Source = new SourceRecord("fk_kd_view", Id(nr)),
        Number = Clean(number), DisplayName = Name(organization, first, last, matchcode, number, Id(nr)),
        Email = Clean(email), IsInactive = deleted is not null and not 0
        // Date-only modification fields cannot be represented as a source UTC timestamp.
    };

    private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string? Clean(string? value) => value?.TrimEnd('\0', ' ');
    private static bool Contains(string? value, string search) => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    private static string Name(string? company, string? first, string? last, string? matchcode, string? number, string id)
    {
        var person = string.Join(" ", new[] { Clean(first), Clean(last) }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
        return new[] { Clean(company), person, Clean(matchcode), Clean(number), id }.First(value => !string.IsNullOrWhiteSpace(value))!;
    }
    private static FinOfficeAdapterException InvalidCatalog() => new(FinOfficeFailure.InvalidCatalog);
    private sealed class ContactOwner { public int OwnerId { get; set; } public int ContactId { get; set; } }
}
