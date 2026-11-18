using System.Collections.Immutable;
using System.Globalization;
using System.Net.Mail;
using System.Text;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Core.Parties;

public sealed record MailFieldParseResult(ImmutableArray<string> Addresses, bool HasInvalidValues);

/// <summary>Parses original Lexware mail fields. Quoted display names are preserved while separators are recognized.</summary>
public static class MailAddressFields
{
    public static MailFieldParseResult Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new([], false);
        var addresses = ImmutableArray.CreateBuilder<string>(); var token = new StringBuilder(); var quoted = false; var escaped = false; var invalid = false;
        foreach (var character in value)
        {
            if (escaped) { token.Append(character); escaped = false; continue; }
            if (character == '\\' && quoted) { token.Append(character); escaped = true; continue; }
            if (character == '"') quoted = !quoted;
            if (!quoted && character is ',' or ';') { Add(); continue; }
            token.Append(character);
        }
        Add(); invalid |= quoted || escaped;
        return new(addresses.Distinct(StringComparer.Ordinal).ToImmutableArray(), invalid);

        void Add()
        {
            var text = token.ToString().Trim(); token.Clear(); if (text.Length == 0) return;
            if (!MailAddress.TryCreate(text, out var address) || !address.Address.Contains('@')) { invalid = true; return; }
            try
            {
                var host = address.Host.StartsWith('[') ? address.Host : new IdnMapping().GetAscii(address.Host);
                addresses.Add((address.User + "@" + host).ToLowerInvariant());
            }
            catch (ArgumentException) { invalid = true; }
        }
    }

    public static string? CanonicalSingle(string? value)
    { var parsed = Parse(value); return !parsed.HasInvalidValues && parsed.Addresses.Length == 1 ? parsed.Addresses[0] : null; }

    public static ImmutableArray<PartyEmail> OriginalFields(Party party)
    {
        if (party.ContactFieldsVersion != 0 || !party.Emails.IsEmpty) return party.Emails;
        var fields = ImmutableArray.CreateBuilder<PartyEmail>();
        if (!string.IsNullOrWhiteSpace(party.Email)) fields.Add(new(party.Email, new("legacy-party", party.Key.ExternalId, "email")));
        foreach (var address in party.Addresses)
            if (!string.IsNullOrWhiteSpace(address.Email)) fields.Add(new(address.Email, new("legacy-address", address.ExternalId, "email")));
        return fields.ToImmutable();
    }

    public static ImmutableArray<string> Normalized(Party party) => OriginalFields(party).Where(field => field.UseForMatching)
        .SelectMany(field => Parse(field.OriginalValue).Addresses).Distinct(StringComparer.Ordinal).ToImmutableArray();
}
