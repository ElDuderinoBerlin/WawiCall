using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Interfaces.PhoneNumbers;

/// <summary>Offline number-plan parsing. A valid number is not proof of a caller's identity or location.</summary>
public interface IPhoneNumberParser
{
    /// <summary>Identifies the pinned number metadata and our normalization rules for future cache rebuilds.</summary>
    string Version { get; }

    /// <summary>
    /// Preserves the original value and source. National numbers require an explicit ISO region;
    /// a null region allows international numbers only. Expected input errors are returned as fixed codes.
    /// </summary>
    PhoneNumberParseResult Parse(string originalValue, string? defaultRegion = null, SourceField? source = null);
}
