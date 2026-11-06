using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Serialization;
using Quantarix.Lexware.APIService.Models;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Plugins.Lexware.Pro;

/// <summary>Reads detached master data with explicit company scope and without financial or bank fields.</summary>
public sealed class ProPartyDirectory(IProApiSession session)
{
    private const string Fields = """
        id number matchcode inactive modifiedDate
        invoiceAddress { company firstName lastName email }
        contactPersons { id firstName lastName email1 }
        """;

    public async Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Company);
        ArgumentNullException.ThrowIfNull(query.Page);
        if (query.Kind is { } kind && !Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(query));
        var parties = new List<Party>();
        foreach (var ownerKind in OwnerKinds(query.Kind))
        {
            var root = ownerKind == PartyKind.Customer ? "customers" : "suppliers";
            var response = await session.ReadCompanyAsync<PartyListResponse>(query.Company,
                (client, token, ct) => client.CallApiWithTokenAsync<PartyListResponse>(token,
                    "query { parties: " + root + " { " + Fields + " } }", ct), cancellationToken).ConfigureAwait(false);
            var owners = response.Data?.Parties ?? throw InvalidResponse();
            var ids = new HashSet<int>();
            foreach (var owner in owners)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (owner is null || !ids.Add(owner.Id)) throw InvalidResponse();
                parties.AddRange(Map(query.Company, ownerKind, owner));
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        var filtered = parties.Where(party => (query.Kind is null || party.Key.Kind == query.Kind)
            && (string.IsNullOrEmpty(query.SearchText) || Contains(party.DisplayName, query.SearchText)
                || Contains(party.Number, query.SearchText) || Contains(party.Email, query.SearchText)))
            .OrderBy(party => party.Key.Kind).ThenBy(party => party.Key.OwnerExternalId, StringComparer.Ordinal)
            .ThenBy(party => party.Key.ExternalId, StringComparer.Ordinal)
            .Skip(query.Page.Offset).Take(query.Page.Limit + 1).ToImmutableArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new PartyPage(query.Company, new Page<Party>(filtered.Take(query.Page.Limit).ToImmutableArray(), filtered.Length > query.Page.Limit));
    }

    public async Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
        var ownerKind = key.Kind is PartyKind.Customer or PartyKind.CustomerContact ? PartyKind.Customer : PartyKind.Supplier;
        var ownerId = key.OwnerExternalId ?? key.ExternalId;
        // Numeric source IDs are canonical; do not alias opaque keys such as "01" to another record.
        if (!int.TryParse(ownerId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0
            || id.ToString(CultureInfo.InvariantCulture) != ownerId)
            throw new ProAdapterException(ProFailure.InvalidResponse);
        var root = ownerKind == PartyKind.Customer ? "customer" : "supplier";
        var response = await session.ReadCompanyAsync<PartyDetailResponse>(key.Company,
            (client, token, ct) => client.CallApiWithTokenAsync<PartyDetailResponse>(token,
                "query { party: " + root + "(id: " + ownerId + ") { " + Fields + " } }", ct), cancellationToken).ConfigureAwait(false);
        var data = response.Data ?? throw InvalidResponse();
        cancellationToken.ThrowIfCancellationRequested();
        if (data.Party is null) return null;
        if (data.Party.Id != id) throw InvalidResponse();
        var party = Map(key.Company, ownerKind, data.Party).SingleOrDefault(candidate => candidate.Key == key);
        cancellationToken.ThrowIfCancellationRequested();
        return party;
    }

    private static IEnumerable<Party> Map(CompanyKey company, PartyKind kind, PartyRecord owner)
    {
        if (owner.Id <= 0 || owner.Number is null || owner.ContactPersons is null) throw InvalidResponse();
        var id = owner.Id.ToString(CultureInfo.InvariantCulture);
        var sourceType = kind == PartyKind.Customer ? "fk_kunde" : "fk_lieferant";
        var address = owner.InvoiceAddress;
        var inactive = owner.Inactive is not null and not 0;
        var parties = new List<Party>
        {
            new()
            {
                Key = new PartyKey(company, kind, id), Number = owner.Number,
                Source = new SourceRecord(sourceType, id),
                DisplayName = DisplayName(address?.Company, address?.FirstName, address?.LastName, owner.Matchcode, owner.Number, id),
                Email = address?.Email, IsInactive = inactive,
                // The source has no declared timezone for unspecified/local timestamps.
                SourceUpdatedAtUtc = owner.ModifiedDate is { Kind: DateTimeKind.Utc } utc ? new DateTimeOffset(utc) : null
            }
        };
        var contactIds = new HashSet<int>();
        foreach (var contact in owner.ContactPersons)
        {
            if (contact is null || contact.Id <= 0 || !contactIds.Add(contact.Id)) throw InvalidResponse();
            var contactId = contact.Id.ToString(CultureInfo.InvariantCulture);
            parties.Add(new Party
            {
                Key = new PartyKey(company, kind == PartyKind.Customer ? PartyKind.CustomerContact : PartyKind.SupplierContact, contactId, id),
                Source = new SourceRecord("fk_kontaktperson", contactId),
                DisplayName = DisplayName(null, contact.FirstName, contact.LastName, contact.Email1, null, contactId),
                Email = contact.Email1, IsInactive = inactive
            });
        }
        return parties;
    }

    private static IEnumerable<PartyKind> OwnerKinds(PartyKind? kind)
        => kind switch
        {
            PartyKind.Customer or PartyKind.CustomerContact => [PartyKind.Customer],
            PartyKind.Supplier or PartyKind.SupplierContact => [PartyKind.Supplier],
            _ => [PartyKind.Customer, PartyKind.Supplier]
        };

    private static bool Contains(string? value, string search) => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    private static string DisplayName(string? company, string? first, string? last, string? matchcode, string? number, string id)
    {
        var person = string.Join(" ", new[] { first, last }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));
        return new[] { company, person, matchcode, number, id }.First(value => !string.IsNullOrWhiteSpace(value))!;
    }
    private static ProAdapterException InvalidResponse() => new(ProFailure.InvalidResponse);

    // Required fields distinguish a confirmed empty/not-found result from a malformed successful response.
    public sealed class PartyListResponse { [JsonPropertyName("data")] public PartyListData? Data { get; set; } }
    public sealed class PartyListData { [JsonRequired, JsonPropertyName("parties")] public List<PartyRecord>? Parties { get; set; } }
    public sealed class PartyDetailResponse { [JsonPropertyName("data")] public PartyDetailData? Data { get; set; } }
    public sealed class PartyDetailData { [JsonRequired, JsonPropertyName("party")] public PartyRecord? Party { get; set; } }
    public sealed class PartyRecord
    {
        [JsonRequired, JsonPropertyName("id")] public int Id { get; set; }
        [JsonRequired, JsonPropertyName("number")] public string? Number { get; set; }
        [JsonPropertyName("matchcode")] public string? Matchcode { get; set; }
        [JsonPropertyName("inactive")] public int? Inactive { get; set; }
        [JsonPropertyName("modifiedDate")] public DateTime? ModifiedDate { get; set; }
        [JsonRequired, JsonPropertyName("invoiceAddress")] public PartyName? InvoiceAddress { get; set; }
        [JsonRequired, JsonPropertyName("contactPersons")] public List<ContactPerson>? ContactPersons { get; set; }
    }
    public sealed class PartyName
    {
        [JsonPropertyName("company")] public string? Company { get; set; }
        [JsonPropertyName("firstName")] public string? FirstName { get; set; }
        [JsonPropertyName("lastName")] public string? LastName { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
    }
}
