using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Binds the explicit single-account company mapping to the exact saved account, excluding credentials.</summary>
public static class CallerLineConfiguration
{
    public static CallerLineAssignment Create(SipAccountOptions account, CompanyKey company) =>
        new(new(Identity(account), account.Username!), company, "SIP-Konto", true);

    public static CallerLineAssignment? Resolve(ApplicationOptions options)
    {
        if (options.CallerLineAssignment is not { } mapping) return null;
        var matches = options.SipAccount is { } account && mapping.Line.TelephonySystemId == Identity(account)
            && mapping.Line.LineId == account.Username;
        return mapping with { IsVerified = mapping.IsVerified && matches };
    }
    private static string Identity(SipAccountOptions account) =>
        "sip:" + account.Server?.ToLowerInvariant() + ":" + account.Port + ":" + account.Transport;
}
