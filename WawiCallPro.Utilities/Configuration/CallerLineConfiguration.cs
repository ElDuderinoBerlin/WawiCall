using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Binds the explicit single-account company mapping to the exact saved account, excluding credentials.</summary>
public static class CallerLineConfiguration
{
    public static CallerLineAssignment Create(SipAccountOptions account, CompanyKey company) =>
        new(ContextFor(account), company, "SIP-Konto", true);
    public static PhoneLineContext ContextFor(SipAccountOptions account) => new(Identity(account), account.Username!);

    public static CallerLineAssignment? Resolve(ApplicationOptions options)
    {
        if (options.CallerLineAssignment is not { } mapping) return null;
        var matches = options.SipAccount is { } account && mapping.Line.TelephonySystemId == Identity(account)
            && mapping.Line.LineId == account.Username;
        return mapping with { IsVerified = mapping.IsVerified && matches };
    }

    /// <summary>Uses the account bound to the call, even after another profile has been saved for the next login.</summary>
    public static CallerLineAssignment? ResolveForLine(ApplicationOptions options, PhoneLineContext? line)
    {
        if (line is null) return null;
        var profiles = SipProfileCatalog.Read(options).Where(profile => profile.Account is { } account
            && Identity(account) == line.TelephonySystemId && account.Username == line.LineId).ToArray();
        // Identical account addresses in several profiles cannot prove which company assignment was intended.
        if (profiles.Length != 1) return null;
        var profile = profiles[0];
        return Resolve(options with { SipAccount = profile.Account, CallerLineAssignment = profile.LineAssignment });
    }
    private static string Identity(SipAccountOptions account) =>
        "sip:" + account.Server?.ToLowerInvariant() + ":" + account.Port + ":" + account.Transport;
}
