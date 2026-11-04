using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;
using Library = global::PhoneNumbers;

namespace WawiCallPro.Core.PhoneNumbers;

/// <summary>Offline interpretation with explicit context and presentation, independent of mutable company selection.</summary>
public sealed class PhoneNumberInterpreter(IPhoneNumberParser parser) : IPhoneNumberInterpreter
{
    private readonly Library.PhoneNumberUtil _metadata = Library.PhoneNumberUtil.GetInstance();

    public PhoneNumberInterpretation InterpretCompanyPhone(PartyPhone phone, CompanyPhoneNumberContext context,
        PhoneNumberInputKind inputKind = PhoneNumberInputKind.Automatic)
    {
        ArgumentNullException.ThrowIfNull(phone);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Company);
        return Interpret(new CallAddress(NumberPresentation.Available, phone.OriginalValue, Extension: phone.Extension),
            context.DefaultRegion, context.LocalAreaCode, context.TelephonySystemId, inputKind, phone.Source)
            with { CompanyContext = context, SourcePhoneKind = phone.Kind };
    }

    public PhoneNumberInterpretation InterpretIncoming(CallAddress address, PhoneLineContext? context = null,
        PhoneNumberInputKind inputKind = PhoneNumberInputKind.Automatic)
    {
        ArgumentNullException.ThrowIfNull(address);
        // Never carry a supplied raw number through withheld/unknown presentation or parse it as a caller.
        if (address.Presentation is NumberPresentation.Withheld or NumberPresentation.Unknown)
            return Interpret(address, null, null, null, inputKind, null);
        var result = Interpret(address, context?.DefaultRegion, context?.LocalAreaCode,
            context?.TelephonySystemId, inputKind, null) with { LineContext = context };
        if (context is not null && (string.IsNullOrWhiteSpace(context.TelephonySystemId) || string.IsNullOrWhiteSpace(context.LineId)))
            return Reject(result, "number.invalid-line-context");
        return result;
    }

    private PhoneNumberInterpretation Interpret(CallAddress address, string? region, string? areaCode,
        string? systemId, PhoneNumberInputKind inputKind, SourceField? source)
    {
        if (!Enum.IsDefined(inputKind)) throw new ArgumentOutOfRangeException(nameof(inputKind));
        if (!Enum.IsDefined(address.Presentation)) throw new ArgumentOutOfRangeException(nameof(address));
        var result = new PhoneNumberInterpretation
        {
            Kind = PhoneNumberAddressKind.Unresolved,
            Address = new CallAddress(address.Presentation, address.OriginalValue),
            Source = source,
            Version = parser.Version + ";interpretation/1"
        };
        if (address.Presentation is NumberPresentation.Withheld or NumberPresentation.Unknown)
            return result with
            {
                Kind = address.Presentation == NumberPresentation.Withheld ? PhoneNumberAddressKind.Withheld : PhoneNumberAddressKind.Unknown,
                Address = new CallAddress(address.Presentation),
                Code = address.Presentation == NumberPresentation.Withheld ? "number.withheld" : "number.unknown"
            };

        var raw = address.OriginalValue ?? string.Empty;
        var trimmed = raw.Trim();
        if (address.Extension is { } suppliedExtension &&
            (suppliedExtension.Length is < 1 or > 32 || !suppliedExtension.All(char.IsAsciiDigit)))
            return Reject(result, "number.invalid-extension");
        if (trimmed.StartsWith("sip:", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("sips:", StringComparison.OrdinalIgnoreCase))
            return result with { Kind = PhoneNumberAddressKind.SipUri, Code = "number.sip-address" };

        if (inputKind == PhoneNumberInputKind.Internal || trimmed.StartsWith("**", StringComparison.Ordinal))
        {
            var digits = trimmed.StartsWith("**", StringComparison.Ordinal) ? trimmed[2..] : trimmed;
            if (digits.Length == 0 || !digits.All(char.IsAsciiDigit)) return Reject(result, "number.invalid-internal");
            if (string.IsNullOrWhiteSpace(systemId)) return Reject(result, "number.internal-context-required");
            return result with
            {
                Kind = PhoneNumberAddressKind.Internal,
                Address = new CallAddress(address.Presentation, address.OriginalValue, Extension: address.Extension),
                InternalNumber = trimmed,
                TelephonySystemId = systemId,
                Code = "number.internal"
            };
        }
        if (trimmed.StartsWith('*') || trimmed.StartsWith('#')) return Reject(result, "number.non-public-code");

        var parsed = parser.Parse(raw, region, source);
        string? appliedArea = null;
        if (inputKind == PhoneNumberInputKind.LocalSubscriber ||
            (inputKind == PhoneNumberInputKind.Automatic && parsed.Status is not (PhoneNumberParseStatus.Valid or PhoneNumberParseStatus.UnsupportedLength) &&
             areaCode is not null && CouldBeLocal(trimmed, region)))
        {
            var local = UnwrapTel(trimmed);
            if (local.StartsWith('+') || local.StartsWith('＋')) return Reject(result, "number.input-kind-conflict");
            var normalizedRegion = region?.Trim().ToUpperInvariant();
            if (normalizedRegion is null || !_metadata.GetSupportedRegions().Contains(normalizedRegion))
                return Reject(result, "number.local-context-required");
            if (areaCode is null || areaCode.Length is < 1 or > 16 || !areaCode.All(char.IsAsciiDigit))
                return Reject(result, "number.invalid-area-code");
            var nationalPrefix = _metadata.GetNddPrefixForRegion(normalizedRegion, true);
            var canonicalArea = !string.IsNullOrEmpty(nationalPrefix) && areaCode.StartsWith(nationalPrefix, StringComparison.Ordinal) ?
                areaCode[nationalPrefix.Length..] : areaCode;
            if (canonicalArea.Length == 0) return Reject(result, "number.invalid-area-code");
            var countryCode = _metadata.GetCountryCodeForRegion(normalizedRegion);
            var candidate = parser.Parse($"+{countryCode}{canonicalArea} {local}", normalizedRegion, source);
            if (candidate.Status == PhoneNumberParseStatus.Valid &&
                (candidate.CountryCallingCode != countryCode || candidate.NationalDestinationCode != canonicalArea ||
                 candidate.NumberType is not (PhoneNumberType.FixedLine or PhoneNumberType.FixedLineOrMobile)))
                return Reject(result, "number.invalid-area-code");
            parsed = candidate with { OriginalValue = raw };
            appliedArea = areaCode;
        }

        if (address.Extension is { } extension)
        {
            if (parsed.Extension is not null && parsed.Extension != extension) return Reject(result, "number.extension-conflict");
            parsed = parsed with { Extension = extension };
        }
        var isPublic = parsed.Status == PhoneNumberParseStatus.Valid;
        return result with
        {
            Kind = isPublic ? PhoneNumberAddressKind.Public : PhoneNumberAddressKind.Unresolved,
            Address = new CallAddress(address.Presentation, address.OriginalValue, isPublic ? parsed.E164 : null, parsed.Extension),
            Parsed = parsed,
            AppliedAreaCode = appliedArea,
            Code = parsed.Code
        };
    }

    private bool CouldBeLocal(string value, string? region)
    {
        var local = UnwrapTel(value);
        if (local.Length == 0 || local.StartsWith('+') || local.StartsWith('＋')) return false;
        var digits = Library.PhoneNumberUtil.NormalizeDigitsOnly(local);
        // Explicit dialling prefixes are never repaired by prepending the configured local area.
        if (digits.Length == 0 || digits.StartsWith('0')) return false;
        var normalizedRegion = region?.Trim().ToUpperInvariant();
        if (normalizedRegion is null || !_metadata.GetSupportedRegions().Contains(normalizedRegion)) return false;
        var prefix = _metadata.GetNddPrefixForRegion(normalizedRegion, true);
        return (string.IsNullOrEmpty(prefix) || !digits.StartsWith(prefix, StringComparison.Ordinal)) &&
            (char.IsAsciiDigit(local[0]) || local[0] == '(');
    }

    private static string UnwrapTel(string value) =>
        value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ? value[4..].TrimStart() : value;

    private static PhoneNumberInterpretation Reject(PhoneNumberInterpretation result, string code) =>
        result with { Kind = PhoneNumberAddressKind.Unresolved, Address = new CallAddress(result.Address.Presentation, result.Address.OriginalValue),
            Parsed = null, InternalNumber = null, TelephonySystemId = null, Code = code };
}
