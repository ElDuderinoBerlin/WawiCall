using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Interfaces.PhoneNumbers;

/// <summary>Explicit hints resolve ambiguous bare digits; length alone never proves an internal number.</summary>
public enum PhoneNumberInputKind
{
    Automatic = 0,
    National = 1,
    LocalSubscriber = 2,
    Internal = 3
}

public enum PhoneNumberAddressKind
{
    Unresolved = 0,
    Public = 1,
    Internal = 2,
    SipUri = 3,
    Withheld = 4,
    Unknown = 5
}

/// <summary>Detached company defaults; a PBX identity is required only for internal numbers.</summary>
public sealed record CompanyPhoneNumberContext(CompanyKey Company, string? DefaultRegion = null,
    string? LocalAreaCode = null, string? TelephonySystemId = null);

/// <summary>Explicit account/line defaults; never inferred from a caller's possible company matches.</summary>
public sealed record PhoneLineContext(string TelephonySystemId, string LineId,
    string? DefaultRegion = null, string? LocalAreaCode = null);

/// <summary>Context and evidence for a number; no business-party assignment or inferred PBX stem.</summary>
public sealed record PhoneNumberInterpretation
{
    public required PhoneNumberAddressKind Kind { get; init; }
    public required CallAddress Address { get; init; }
    public required string Version { get; init; }
    public SourceField? Source { get; init; }
    public PhoneKind? SourcePhoneKind { get; init; }
    public CompanyPhoneNumberContext? CompanyContext { get; init; }
    public PhoneLineContext? LineContext { get; init; }
    public PhoneNumberParseResult? Parsed { get; init; }
    public string? Code { get; init; }

    /// <summary>Applied configured area code, kept separately from the original input and explicit extension.</summary>
    public string? AppliedAreaCode { get; init; }

    /// <summary>Usable only together with TelephonySystemId; no global equality of internal numbers.</summary>
    public string? InternalNumber { get; init; }
    public string? TelephonySystemId { get; init; }

    public override string ToString() => $"{nameof(PhoneNumberInterpretation)}({Kind})";
}
