using System.Collections.Frozen;
using System.Collections.Immutable;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Core.PhoneNumbers;

/// <summary>Builds immutable exact and country/destination/subscriber-prefix maps. A rule change creates a new index.</summary>
public sealed class CompanyPhoneMatchIndexFactory(IPhoneNumberParser parser) : ICompanyPhoneMatchIndexFactory
{
    public ICompanyPhoneMatchIndex Create(CompanyPhoneMatchSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(source.Profile);
        ArgumentNullException.ThrowIfNull(source.Profile.Settings);
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Profile.Settings.StartWidth is < 1 or > 15 || source.CacheRevision < 0 || source.Profile.RuleRevision < 0
            || source.Parties.IsDefault || source.Blocks.IsDefault)
            throw new ArgumentException("Invalid phone index snapshot.", nameof(source));
        var parties = source.Parties.ToFrozenDictionary(party => party.Key);
        if (parties.Keys.Any(key => key.Company != source.Profile.Settings.Company)
            || source.Blocks.Any(block => block.Organisation.Company != source.Profile.Settings.Company))
            throw new ArgumentException("Phone index entries must belong to the specified company.", nameof(source));
        var phones = new List<IndexedPhone>();
        var complete = source.IsComplete && source.CacheParserVersion == parser.Version;
        foreach (var party in source.Parties)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (party.IsInactive) continue;
            var organisation = Organisation(party.Key);
            if (organisation != party.Key && (!parties.TryGetValue(organisation, out var owner) || owner.IsInactive)) complete = false;
            foreach (var phone in party.Phones.Concat(party.Addresses.SelectMany(address => address.Phones)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (phone.E164 is null) continue;
                var parsed = parser.Parse(phone.E164, source: phone.Source);
                if (parsed.Status != PhoneNumberParseStatus.Valid || parsed.E164 != phone.E164) { complete = false; continue; }
                phones.Add(new(party, phone, parsed));
            }
        }
        var blocks = new List<PhoneNumberBlockRule>();
        foreach (var block in source.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!block.IsEnabled) continue;
            if (block.Organisation.Kind is not (PartyKind.Customer or PartyKind.Supplier)
                || !Enum.IsDefined(block.Kind) || block.CountryCallingCode is < 1 or > 999
                || !Digits(block.DestinationCode) || !Digits(block.Stem) || block.Stem.Length > 15
                || block.Id == Guid.Empty || block.DestinationCode.Length > 10 || source.Profile.RuleRevision == 0
                || block.MinimumSuffixDigits < 0 || block.MaximumSuffixDigits < block.MinimumSuffixDigits
                || block.MaximumSuffixDigits > 15
                || block.CountryCallingCode.ToString(System.Globalization.CultureInfo.InvariantCulture).Length
                    + block.DestinationCode.Length + block.Stem.Length + block.MaximumSuffixDigits > 15)
                throw new ArgumentException("Invalid phone block rule.", nameof(source));
            if (!parties.TryGetValue(block.Organisation, out var owner) || owner.IsInactive) { complete = false; continue; }
            blocks.Add(block);
        }
        var index = new Index(source, parties, phones, blocks, parser.Version, complete);
        cancellationToken.ThrowIfCancellationRequested();
        return index;
    }

    private static bool Digits(string? value) => !string.IsNullOrEmpty(value) && value.All(char.IsAsciiDigit);
    private static PartyKey Organisation(PartyKey key) => key.Kind switch
    {
        PartyKind.CustomerContact => new(key.Company, PartyKind.Customer, key.OwnerExternalId!),
        PartyKind.SupplierContact => new(key.Company, PartyKind.Supplier, key.OwnerExternalId!), _ => key
    };
    private sealed record IndexedPhone(Party Party, PartyPhone Phone, PhoneNumberParseResult Parsed);
    private readonly record struct Prefix(int Country, string Destination, string Stem);

    private sealed class Index : ICompanyPhoneMatchIndex
    {
        private readonly FrozenDictionary<PartyKey, Party> _parties;
        private readonly FrozenDictionary<string, ImmutableArray<IndexedPhone>> _exact;
        private readonly FrozenDictionary<Prefix, ImmutableArray<IndexedPhone>> _stems;
        private readonly FrozenDictionary<Prefix, ImmutableArray<PhoneNumberBlockRule>> _blocks;
        private readonly string _parserVersion;
        public PhoneMatchingProfile Profile { get; }
        public long CacheRevision { get; }
        public string Version { get; }
        public bool IsComplete { get; }

        internal Index(CompanyPhoneMatchSource source, FrozenDictionary<PartyKey, Party> parties, List<IndexedPhone> phones,
            List<PhoneNumberBlockRule> blocks, string parserVersion, bool complete)
        {
            Profile = source.Profile; CacheRevision = source.CacheRevision; _parties = parties;
            _parserVersion = parserVersion; Version = parserVersion + ";matching/1"; IsComplete = complete;
            _exact = phones.GroupBy(phone => phone.Parsed.E164!).ToFrozenDictionary(group => group.Key, group => group.ToImmutableArray(), StringComparer.Ordinal);
            var width = Profile.Settings.StartWidth;
            _stems = phones.Where(phone => phone.Parsed.CountryCallingCode is not null && Digits(phone.Parsed.NationalDestinationCode)
                    && Digits(phone.Parsed.SubscriberNumber) && phone.Parsed.SubscriberNumber!.Length >= width)
                .GroupBy(phone => new Prefix(phone.Parsed.CountryCallingCode!.Value, phone.Parsed.NationalDestinationCode!, phone.Parsed.SubscriberNumber![..width]))
                .ToFrozenDictionary(group => group.Key, group => group.ToImmutableArray());
            _blocks = blocks.GroupBy(block => new Prefix(block.CountryCallingCode, block.DestinationCode, block.Stem))
                .ToFrozenDictionary(group => group.Key, group => group.ToImmutableArray());
        }

        public ImmutableArray<PhoneMatchCandidate> Find(PhoneNumberInterpretation incoming, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(incoming);
            cancellationToken.ThrowIfCancellationRequested();
            if (incoming.Kind != PhoneNumberAddressKind.Public || incoming.Parsed is not { Status: PhoneNumberParseStatus.Valid, E164: { } number } parsed
                || parsed.ParserVersion != _parserVersion || incoming.Address.E164 != number) return [];
            var matches = new Dictionary<PartyKey, List<PhoneMatchObservation>>();
            void Add(PartyKey target, PartyKey sourceParty, string? extension, PhoneNumberMatchKind kind,
                PhoneNumberParseResult? registered = null, string? stem = null, string? apparatus = null, Guid? rule = null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_parties.TryGetValue(target, out var party) || party.IsInactive) return;
                if (!matches.TryGetValue(target, out var evidence)) matches.Add(target, evidence = []);
                evidence.Add(new(sourceParty, extension, new()
                {
                    Kind = kind, ParserVersion = _parserVersion, CacheRevision = CacheRevision, RuleRevision = Profile.RuleRevision,
                    RuleId = rule, CountryCallingCode = parsed.CountryCallingCode, DestinationCode = parsed.NationalDestinationCode,
                    Stem = stem, StartWidth = kind == PhoneNumberMatchKind.StemPrefix ? Profile.Settings.StartWidth : null,
                    ApparatusNumber = apparatus, MatchedE164 = registered?.E164, Source = registered?.Source
                }));
            }
            if (_exact.TryGetValue(number, out var exact))
                foreach (var item in exact)
                {
                    // A shared base number never identifies a contact with an unknown/different explicit extension.
                    var target = item.Phone.Extension is not null && item.Phone.Extension != incoming.Address.Extension
                        ? Organisation(item.Party.Key) : item.Party.Key;
                    Add(target, item.Party.Key, item.Phone.Extension, PhoneNumberMatchKind.Exact, item.Parsed);
                }
            if (parsed.CountryCallingCode is { } country && Digits(parsed.NationalDestinationCode) && Digits(parsed.SubscriberNumber))
            {
                var subscriber = parsed.SubscriberNumber!;
                var destination = parsed.NationalDestinationCode!;
                var width = Profile.Settings.StartWidth;
                if (Profile.Settings.StemMatchingEnabled && subscriber.Length >= width
                    && _stems.TryGetValue(new(country, destination, subscriber[..width]), out var stems))
                    foreach (var item in stems)
                        Add(Organisation(item.Party.Key), item.Party.Key, item.Phone.Extension, PhoneNumberMatchKind.StemPrefix,
                            item.Parsed, subscriber[..width], subscriber[width..]);
                // At most fifteen probes, independent of the number of configured blocks.
                for (var length = 1; length <= subscriber.Length; length++)
                    if (_blocks.TryGetValue(new(country, destination, subscriber[..length]), out var blocks))
                        foreach (var block in blocks)
                        {
                            var rest = subscriber.Length - length;
                            if (rest >= block.MinimumSuffixDigits && rest <= block.MaximumSuffixDigits)
                                Add(block.Organisation, block.Organisation, null, block.Kind == PhoneNumberBlockKind.Confirmed
                                    ? PhoneNumberMatchKind.ConfirmedBlock : PhoneNumberMatchKind.InferredBlock,
                                    stem: block.Stem, apparatus: subscriber[length..], rule: block.Id);
                        }
            }
            return matches.Select(pair => new PhoneMatchCandidate(pair.Key, _parties[pair.Key].DisplayName,
                    pair.Value.Distinct().OrderBy(item => Rank(item.Match.Kind)).ThenByDescending(item => item.Match.Stem?.Length ?? 0).ToImmutableArray()))
                .OrderBy(candidate => Rank(candidate.Evidence[0].Match.Kind)).ThenByDescending(candidate => candidate.Evidence[0].Match.Stem?.Length ?? 0)
                .ThenBy(candidate => candidate.Party.Kind).ThenBy(candidate => candidate.Party.ExternalId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Party.OwnerExternalId, StringComparer.Ordinal).ToImmutableArray();
        }
        private static int Rank(PhoneNumberMatchKind kind) => kind switch
        { PhoneNumberMatchKind.Exact => 0, PhoneNumberMatchKind.ConfirmedBlock => 1, PhoneNumberMatchKind.StemPrefix => 2, _ => 3 };
    }
}
