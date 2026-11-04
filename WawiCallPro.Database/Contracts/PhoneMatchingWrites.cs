using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Database.Contracts;

public sealed record StoredPhoneMatchingSettings(PhoneMatchingSettings Settings, long Version, long RuleRevision, DateTimeOffset ChangedAtUtc);
public sealed record StoredPhoneNumberBlockRule(PhoneNumberBlockRule Rule, long Version, DateTimeOffset ChangedAtUtc);

public sealed record PhoneMatchingSettingsWrite
{
    public PhoneMatchingSettingsWrite(PhoneMatchingSettings settings, long? expectedVersion, DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(settings);
        StorageContract.CompanyKey(settings.Company);
        if (settings.StartWidth is < 1 or > 15) throw new ArgumentOutOfRangeException(nameof(settings));
        StorageContract.ExpectedVersion(expectedVersion); StorageContract.Utc(changedAtUtc);
        Settings = settings; ExpectedVersion = expectedVersion; ChangedAtUtc = changedAtUtc;
    }
    public PhoneMatchingSettings Settings { get; }
    public long? ExpectedVersion { get; }
    public DateTimeOffset ChangedAtUtc { get; }
}

public sealed record PhoneNumberBlockRuleWrite
{
    public PhoneNumberBlockRuleWrite(PhoneNumberBlockRule rule, long? expectedVersion, DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _ = DatabaseId.Encode(rule.Id);
        StorageContract.PartyKey(rule.Organisation);
        if (rule.Organisation.Kind is not (PartyKind.Customer or PartyKind.Supplier) || !Enum.IsDefined(rule.Kind))
            throw new ArgumentException("Number blocks belong to a customer/supplier organisation.", nameof(rule));
        if (rule.CountryCallingCode is < 1 or > 999 || rule.MinimumSuffixDigits < 0 || rule.MaximumSuffixDigits > 15 || rule.MaximumSuffixDigits < rule.MinimumSuffixDigits)
            throw new ArgumentOutOfRangeException(nameof(rule));
        Digits(rule.DestinationCode, 10); Digits(rule.Stem, 15);
        if (rule.CountryCallingCode.ToString(System.Globalization.CultureInfo.InvariantCulture).Length + rule.DestinationCode.Length + rule.Stem.Length + rule.MaximumSuffixDigits > 15)
            throw new ArgumentException("Number blocks cannot exceed E.164 capacity.", nameof(rule));
        StorageContract.Text(rule.Evidence, 1024, nameof(rule.Evidence), required: true);
        StorageContract.ExpectedVersion(expectedVersion); StorageContract.Utc(changedAtUtc);
        Rule = rule; ExpectedVersion = expectedVersion; ChangedAtUtc = changedAtUtc;
    }
    public PhoneNumberBlockRule Rule { get; }
    public long? ExpectedVersion { get; }
    public DateTimeOffset ChangedAtUtc { get; }
    private static void Digits(string value, int maximum)
    {
        StorageContract.Text(value, maximum, nameof(value), required: true);
        if (value.Any(character => !char.IsAsciiDigit(character))) throw new ArgumentException("ASCII digits required.", nameof(value));
    }
}
