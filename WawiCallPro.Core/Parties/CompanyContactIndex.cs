using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Core.Parties;

/// <summary>A contact way always retains its owner, its source party and its original source field.</summary>
public sealed record PartyContactMatch(PartyKey Owner, PartyKey SourceParty, SourceField Source, string OriginalValue);
public sealed record PartyMailGroup(Party Owner, string Version, ImmutableArray<PartyKey> Members)
{
    public bool IsFromCache { get; init; }
    public int InvalidMailFieldCount { get; init; }
}

/// <summary>Immutable exact contact-way index for one complete company snapshot; shared values never imply uniqueness.</summary>
public sealed class CompanyContactIndex
{
    private readonly FrozenDictionary<PartyKey, Party> _parties;
    private readonly FrozenDictionary<PartyKey, ImmutableArray<Party>> _groups;
    private readonly FrozenDictionary<string, ImmutableArray<PartyContactMatch>> _emails;
    private readonly FrozenDictionary<string, ImmutableArray<PartyContactMatch>> _phones;
    public bool IsComplete { get; }
    public ImmutableArray<PartyEmail> InvalidMailFields { get; }

    public CompanyContactIndex(CompanyKey company, ImmutableArray<Party> parties)
    {
        ArgumentNullException.ThrowIfNull(company);
        if (parties.IsDefault || parties.Any(party => party is null || party.Key.Company != company || party.Emails.IsDefault || party.Phones.IsDefault || party.Addresses.IsDefault))
            throw new ArgumentException("Invalid company contact snapshot.", nameof(parties));
        _parties = parties.ToFrozenDictionary(party => party.Key);
        if (parties.Any(party => !_parties.ContainsKey(OwnerKey(party.Key)))) throw new ArgumentException("Contact owner is missing.", nameof(parties));
        IsComplete = parties.All(party => party.ContactFieldsVersion == Party.CurrentContactFieldsVersion);
        _groups = parties.GroupBy(party => OwnerKey(party.Key)).ToFrozenDictionary(group => group.Key, group => group.ToImmutableArray());
        var emails = new List<(string Value, PartyContactMatch Match)>(); var phones = new List<(string Value, PartyContactMatch Match)>(); var invalid = ImmutableArray.CreateBuilder<PartyEmail>();
        foreach (var party in parties.Where(party => !party.IsInactive && !_parties[OwnerKey(party.Key)].IsInactive))
        {
            var owner = OwnerKey(party.Key);
            foreach (var email in MailAddressFields.OriginalFields(party).Where(field => field.UseForMatching))
            {
                var parsed = MailAddressFields.Parse(email.OriginalValue); if (parsed.HasInvalidValues) invalid.Add(email);
                foreach (var value in parsed.Addresses) emails.Add((value, new(owner, party.Key, email.Source, email.OriginalValue)));
            }
            foreach (var phone in party.Phones.Concat(party.Addresses.SelectMany(address => address.Phones)).Where(phone => phone.E164 is not null))
                phones.Add((phone.E164!, new(owner, party.Key, phone.Source, phone.OriginalValue)));
        }
        _emails = Freeze(emails); _phones = Freeze(phones); InvalidMailFields = invalid.ToImmutable();
    }

    public ImmutableArray<PartyContactMatch> FindEmail(string email) => MailAddressFields.CanonicalSingle(email) is { } value ? _emails.GetValueOrDefault(value, []) : [];
    public ImmutableArray<PartyContactMatch> FindPhone(string e164) => _phones.GetValueOrDefault(e164, []);

    public PartyMailGroup? GetMailGroup(PartyKey selected)
    {
        if (!_parties.TryGetValue(selected, out var source) || source.IsInactive) return null;
        var key = OwnerKey(selected); var owner = _parties[key]; if (owner.IsInactive) return null;
        var members = _groups[key].Where(party => !party.IsInactive).ToArray();
        var keys = members.Select(party => party.Key).OrderBy(item => item.Kind).ThenBy(item => item.ExternalId, StringComparer.Ordinal).ToImmutableArray();
        var fields = members.SelectMany(party => MailAddressFields.OriginalFields(party)).Distinct().ToImmutableArray();
        var combined = owner with { Emails = fields, Email = null, Addresses = [], ContactFieldsVersion = IsComplete ? Party.CurrentContactFieldsVersion : 0 };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { Owner = key, Members = keys, Emails = MailAddressFields.Normalized(combined).Order(StringComparer.Ordinal).ToArray() });
        try { return new(combined, Convert.ToHexString(SHA256.HashData(bytes)), keys)
            { InvalidMailFieldCount = fields.Count(field => field.UseForMatching && MailAddressFields.Parse(field.OriginalValue).HasInvalidValues) }; }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static PartyKey OwnerKey(PartyKey key) => key.Kind switch
    {
        PartyKind.CustomerContact => new(key.Company, PartyKind.Customer, key.OwnerExternalId!),
        PartyKind.SupplierContact => new(key.Company, PartyKind.Supplier, key.OwnerExternalId!), _ => key
    };
    private static FrozenDictionary<string, ImmutableArray<PartyContactMatch>> Freeze(List<(string Value, PartyContactMatch Match)> values) => values
        .GroupBy(item => item.Value, StringComparer.Ordinal).ToFrozenDictionary(group => group.Key, group => group.Select(item => item.Match).Distinct().ToImmutableArray(), StringComparer.Ordinal);
}
