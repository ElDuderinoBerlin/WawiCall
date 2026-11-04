using System.Reflection;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using Library = global::PhoneNumbers;
using NumberType = WawiCallPro.Interfaces.PhoneNumbers.PhoneNumberType;

namespace WawiCallPro.Core.PhoneNumbers;

/// <summary>Local libphonenumber adapter. No database, network, UI or company-context access.</summary>
public sealed class LibPhoneNumberParser : IPhoneNumberParser
{
    private static readonly string ParserVersion = CreateVersion();
    private readonly Library.PhoneNumberUtil _numbers = Library.PhoneNumberUtil.GetInstance();
    private readonly Library.PhoneNumberOfflineGeocoder _geocoder = Library.PhoneNumberOfflineGeocoder.GetInstance();

    public string Version => ParserVersion;

    public PhoneNumberParseResult Parse(string originalValue, string? defaultRegion = null, SourceField? source = null)
    {
        ArgumentNullException.ThrowIfNull(originalValue);
        var result = new PhoneNumberParseResult
        {
            OriginalValue = originalValue,
            Source = source,
            ParserVersion = Version,
            Status = PhoneNumberParseStatus.Unparseable
        };
        if (string.IsNullOrWhiteSpace(originalValue))
            return result with { Status = PhoneNumberParseStatus.Empty, Code = "number.empty" };

        var region = string.IsNullOrWhiteSpace(defaultRegion) ? null : defaultRegion.Trim().ToUpperInvariant();
        if (region is not null && !_numbers.GetSupportedRegions().Contains(region))
            return result with { Code = "number.unsupported-region" };

        var trimmed = originalValue.TrimStart();
        if (trimmed.StartsWith("sip:", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("sips:", StringComparison.OrdinalIgnoreCase))
            return result with { Code = "number.unsupported-address" };

        Library.PhoneNumber number;
        try
        {
            number = _numbers.Parse(originalValue, region ?? "ZZ");
        }
        catch (Library.NumberParseException exception)
        {
            return result with { Code = ParseCode(exception.ErrorType) };
        }

        var valid = _numbers.IsValidNumber(number);
        var possible = _numbers.IsPossibleNumber(number);
        var e164 = valid ? _numbers.Format(number, Library.PhoneNumberFormat.E164) : null;
        var status = valid ? PhoneNumberParseStatus.Valid :
            possible ? PhoneNumberParseStatus.Possible : PhoneNumberParseStatus.Invalid;
        var code = valid ? null : possible ? "number.possible-only" : "number.invalid";
        // Some metadata accepts longer national numbers; the communication schema deliberately uses E.164.
        if (e164 is { Length: > 16 })
        {
            status = PhoneNumberParseStatus.UnsupportedLength;
            code = "number.e164-length";
            e164 = null;
        }

        return result with
        {
            Status = status,
            Code = code,
            E164 = e164,
            Extension = string.IsNullOrEmpty(number.Extension) ? null : number.Extension,
            CountryCallingCode = number.CountryCode,
            RegionCode = _numbers.GetRegionCodeForNumber(number),
            NationalNumber = _numbers.GetNationalSignificantNumber(number),
            NumberType = MapType(_numbers.GetNumberType(number)),
            RegionDescription = status == PhoneNumberParseStatus.Valid ?
                NullIfEmpty(_geocoder.GetDescriptionForNumber(number, Library.Locale.German)) : null
        };
    }

    private static string ParseCode(Library.ErrorType error) => error switch
    {
        Library.ErrorType.INVALID_COUNTRY_CODE => "number.invalid-country-or-region",
        Library.ErrorType.NOT_A_NUMBER => "number.not-a-number",
        Library.ErrorType.TOO_SHORT_AFTER_IDD => "number.too-short",
        Library.ErrorType.TOO_SHORT_NSN => "number.too-short",
        Library.ErrorType.TOO_LONG => "number.too-long",
        _ => "number.parse-failed"
    };

    private static NumberType MapType(Library.PhoneNumberType type) => type switch
    {
        Library.PhoneNumberType.FIXED_LINE => NumberType.FixedLine,
        Library.PhoneNumberType.MOBILE => NumberType.Mobile,
        Library.PhoneNumberType.FIXED_LINE_OR_MOBILE => NumberType.FixedLineOrMobile,
        Library.PhoneNumberType.TOLL_FREE => NumberType.TollFree,
        Library.PhoneNumberType.PREMIUM_RATE => NumberType.PremiumRate,
        Library.PhoneNumberType.SHARED_COST => NumberType.SharedCost,
        Library.PhoneNumberType.VOIP => NumberType.Voip,
        Library.PhoneNumberType.PERSONAL_NUMBER => NumberType.PersonalNumber,
        Library.PhoneNumberType.PAGER => NumberType.Pager,
        Library.PhoneNumberType.UAN => NumberType.UniversalAccess,
        Library.PhoneNumberType.VOICEMAIL => NumberType.Voicemail,
        _ => NumberType.Unknown
    };

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string CreateVersion()
    {
        var assembly = typeof(Library.PhoneNumberUtil).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? throw new InvalidOperationException("Phone number metadata version is unavailable.");
        return $"libphonenumber-csharp/{version};normalization/1";
    }
}
