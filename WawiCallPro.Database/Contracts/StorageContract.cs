using System.Collections.Immutable;
using System.Text;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony;

namespace WawiCallPro.Database.Contracts;

internal static class StorageContract
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static void CompanyKey(CompanyKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        Text(key.InstallationId, 256, nameof(key.InstallationId), required: true);
        Text(key.ExternalId, 256, nameof(key.ExternalId), required: true);
    }

    internal static void PartyKey(PartyKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        CompanyKey(key.Company);
        Text(key.ExternalId, 256, nameof(key.ExternalId), required: true);
        Text(key.OwnerExternalId, 256, nameof(key.OwnerExternalId));
    }

    internal static void Text(string? value, int maximum, string field, bool required = false, bool multiline = false)
    {
        if (value is null && !required) return;
        if (value is null || required && string.IsNullOrWhiteSpace(value) || value.Length > maximum
            || value.Any(character => char.IsControl(character) && !(multiline && character is '\r' or '\n' or '\t')))
            throw new ArgumentException("Text is missing, too long or contains unsupported control characters.", field);
        try { _ = Utf8.GetByteCount(value); }
        catch (EncoderFallbackException) { throw new ArgumentException("Text must contain valid Unicode.", field); }
    }

    internal static void Utc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Repository timestamps must be UTC.", nameof(value));
    }

    internal static void ExpectedVersion(long? value)
    {
        if (value is <= 0) throw new ArgumentOutOfRangeException(nameof(value));
    }

    internal static void Array<T>(ImmutableArray<T> items)
    {
        if (items.IsDefault) throw new ArgumentException("Collections must be explicitly initialized.", nameof(items));
    }

    internal static void Party(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        PartyKey(party.Key);
        Text(party.DisplayName, 512, nameof(party.DisplayName), required: true);
        Text(party.Number, 128, nameof(party.Number));
        Text(party.Email, 320, nameof(party.Email));
        if (party.SourceUpdatedAtUtc is { } updated) Utc(updated);
        Array(party.Phones);
        Array(party.Addresses);
        foreach (var phone in party.Phones) Phone(phone);
        var keys = new HashSet<(AddressKind, string)>();
        foreach (var address in party.Addresses)
        {
            ArgumentNullException.ThrowIfNull(address);
            if (!Enum.IsDefined(address.Kind) || !keys.Add((address.Kind, address.ExternalId)))
                throw new ArgumentException("Address identities must be valid and unique within a party.");
            Text(address.ExternalId, 256, nameof(address.ExternalId), required: true);
            Text(address.CompanyName, 512, nameof(address.CompanyName));
            Text(address.FirstName, 256, nameof(address.FirstName));
            Text(address.LastName, 256, nameof(address.LastName));
            Text(address.Street, 512, nameof(address.Street));
            Text(address.HouseNumber, 64, nameof(address.HouseNumber));
            Text(address.AddressLine2, 512, nameof(address.AddressLine2));
            Text(address.PostalCode, 32, nameof(address.PostalCode));
            Text(address.City, 256, nameof(address.City));
            Text(address.CountryCode, 2, nameof(address.CountryCode));
            Text(address.Email, 320, nameof(address.Email));
            Array(address.Phones);
            foreach (var phone in address.Phones) Phone(phone);
        }
    }

    private static void Phone(PartyPhone phone)
    {
        ArgumentNullException.ThrowIfNull(phone);
        if (!Enum.IsDefined(phone.Kind)) throw new ArgumentOutOfRangeException(nameof(phone));
        Text(phone.OriginalValue, 2048, nameof(phone.OriginalValue), required: true);
        Source(phone.Source);
        E164(phone.E164);
        Text(phone.Extension, 32, nameof(phone.Extension));
    }

    internal static void Source(SourceField source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Text(source.RecordType, 64, nameof(source.RecordType), required: true);
        Text(source.RecordId, 256, nameof(source.RecordId), required: true);
        Text(source.FieldName, 128, nameof(source.FieldName), required: true);
    }

    private static void E164(string? value)
    {
        if (value is not null && (value.Length is < 3 or > 16 || value[0] != '+' || value[1] is < '1' or > '9'
            || value.AsSpan(1).ContainsAnyExceptInRange('0', '9')))
            throw new ArgumentException("An E.164 value must contain '+' and at most 15 ASCII digits.", nameof(value));
    }

    internal static void Call(CallSnapshot call, ImmutableArray<CallPartySnapshot> parties, ImmutableArray<CallEvent> events)
    {
        ArgumentNullException.ThrowIfNull(call);
        Utc(call.StartedAtUtc);
        Utc(call.StateChangedAtUtc);
        if (call.AnsweredAtUtc is { } answered) Utc(answered);
        if (call.EndedAtUtc is { } ended) Utc(ended);
        // Reuse the domain's persisted-snapshot rules rather than introducing another state validator.
        _ = new CallStateMachine(call);
        if (call.Company is not null) CompanyKey(call.Company);
        Address(call.LocalAddress);
        Address(call.RemoteAddress);
        Array(parties);
        Array(events);
        var keys = new HashSet<PartyKey>();
        var selected = 0;
        foreach (var party in parties)
        {
            ArgumentNullException.ThrowIfNull(party);
            PartyKey(party.Party);
            if (party.Party.Company != call.Company || !keys.Add(party.Party))
                throw new ArgumentException("Participants must be unique and belong to the call's assigned company.", nameof(parties));
            Text(party.DisplayName, 512, nameof(party.DisplayName), required: true);
            Text(party.Number, 2048, nameof(party.Number));
            if (party.IsSelected) selected++;
        }
        if (selected > 1) throw new ArgumentException("At most one participant may be selected.", nameof(parties));
        var ids = new HashSet<Guid>();
        long sequence = 0;
        var occurred = call.StartedAtUtc;
        foreach (var entry in events)
        {
            ArgumentNullException.ThrowIfNull(entry);
            _ = DatabaseId.Encode(entry.Id);
            Utc(entry.OccurredAtUtc);
            Code(entry.Code);
            if (entry.CallId != call.Id || !ids.Add(entry.Id) || entry.Sequence <= sequence
                || entry.OccurredAtUtc < occurred || !Enum.IsDefined(entry.State))
                throw new ArgumentException("Events must belong to the call and be uniquely ordered by sequence and UTC time.", nameof(events));
            sequence = entry.Sequence;
            occurred = entry.OccurredAtUtc;
        }
    }

    private static void Address(CallAddress address)
    {
        if (!Enum.IsDefined(address.Presentation)) throw new ArgumentOutOfRangeException(nameof(address));
        Text(address.OriginalValue, 2048, nameof(address.OriginalValue), required: address.Presentation == NumberPresentation.Available);
        E164(address.E164);
        Text(address.Extension, 32, nameof(address.Extension));
        if (address.Presentation == NumberPresentation.Withheld && (address.OriginalValue is not null || address.E164 is not null || address.Extension is not null))
            throw new ArgumentException("Withheld addresses must not retain a signalled number.", nameof(address));
    }

    internal static void Code(string code)
    {
        Text(code, 64, nameof(code), required: true);
        if (code.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
            throw new ArgumentException("Event/error codes must be stable ASCII identifiers.", nameof(code));
    }

    internal static void Recording(Recording recording)
    {
        ArgumentNullException.ThrowIfNull(recording);
        _ = DatabaseId.Encode(recording.Id);
        _ = DatabaseId.Encode(recording.CallId);
        if (!Enum.IsDefined(recording.Availability) || recording.Duration < TimeSpan.Zero)
            throw new ArgumentException("Recording availability and duration must be valid.", nameof(recording));
        Text(recording.Format, 32, nameof(recording.Format), required: true);
        Text(recording.RelativePath, 1024, nameof(recording.RelativePath), required: true);
        foreach (var part in recording.RelativePath.Split('/'))
        {
            if (part.Length is 0 or > 255 || part is "." or ".." || part.EndsWith(' ') || part.EndsWith('.')
                || part.IndexOfAny(['\\', ':', '<', '>', '"', '|', '?', '*']) >= 0)
                throw new ArgumentException("Recording paths must be canonical relative paths with forward slashes.", nameof(recording));
            var name = part.Split('.')[0].ToUpperInvariant();
            if (name is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
                || name.Length == 4 && (name.StartsWith("COM", StringComparison.Ordinal) || name.StartsWith("LPT", StringComparison.Ordinal))
                && name[3] is >= '1' and <= '9' or '¹' or '²' or '³')
                throw new ArgumentException("Recording paths must not contain device names.", nameof(recording));
        }
    }
}
