namespace WawiCallPro.Interfaces.Parties;

/// <summary>Shared search rules for detached phone fields from the party and its addresses.</summary>
public static class PartyPhoneSearch
{
    public static IEnumerable<PartyPhone> DialablePhones(Party party) =>
        party.Phones.Concat(party.Addresses.SelectMany(address => address.Phones))
            .Where(phone => phone.Kind != PhoneKind.Fax && !string.IsNullOrWhiteSpace(phone.OriginalValue));

    public static bool Matches(PartyPhone phone, string text) =>
        phone.OriginalValue.Contains(text, StringComparison.OrdinalIgnoreCase)
        || Normalize(text) is { Length: > 0 } digits
            && (Normalize(phone.OriginalValue).Contains(digits, StringComparison.Ordinal)
                || phone.E164 is { } e164 && Normalize(e164).Contains(digits, StringComparison.Ordinal));

    public static bool Matches(Party party, string text) => DialablePhones(party).Any(phone => Matches(phone, text));

    private static string Normalize(string value)
    {
        if (value.Any(character => !char.IsAsciiDigit(character) && !char.IsWhiteSpace(character)
            && character is not ('+' or '(' or ')' or '/' or '-' or '.'))) return "";
        var digits = string.Concat(value.Where(char.IsAsciiDigit));
        return digits.StartsWith("00", StringComparison.Ordinal) ? digits[2..] : digits;
    }
}
