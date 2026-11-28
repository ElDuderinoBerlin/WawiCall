using SIPSorcery.SIP;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Telephony.Sip;

internal static class SipCallerIdentity
{
    internal static CallAddress Read(SIPRequest request, SIPEndPoint? registeredPeer)
    {
        var header = request.Header;
        if (header.UnknownHeaders.Any(WithholdsIdentity)) return new(NumberPresentation.Withheld);

        var from = GetIdentity(header.From.FromURI);
        var anonymous = string.Equals(from, "anonymous", StringComparison.OrdinalIgnoreCase);
        if (IsPhoneNumber(from)) return new(NumberPresentation.Available, from);

        // Asserted identities are meaningful only from the proxy that accepted our registration.
        if (IsRegisteredPeer(request.RemoteSIPEndPoint, registeredPeer))
        {
            var asserted = header.PassertedIdentity.Select(identity => GetIdentity(identity.URI)).FirstOrDefault(IsPhoneNumber);
            if (asserted is not null) return new(NumberPresentation.Available, asserted);
        }

        if (anonymous) return new(NumberPresentation.Withheld);
        return string.IsNullOrEmpty(from) ? new(NumberPresentation.Unknown) : new(NumberPresentation.Available, from);
    }

    /// <summary>True only for the exact transport endpoint that answered the current registration.</summary>
    internal static bool IsRegisteredPeer(SIPEndPoint? source, SIPEndPoint? registeredPeer) =>
        registeredPeer is not null && source is not null
        && source.Protocol == registeredPeer.Protocol && source.Port == registeredPeer.Port
        && source.Address.Equals(registeredPeer.Address);

    private static string? GetIdentity(SIPURI uri) => uri.Scheme == SIPSchemesEnum.tel ? uri.User ?? uri.Host : uri.User;

    private static bool IsPhoneNumber(string? value) => value is { Length: > 0 and <= 32 }
        && value.AsSpan(value[0] == '+' ? 1 : 0) is { IsEmpty: false } digits
        && digits.IndexOfAnyExceptInRange('0', '9') < 0;

    private static bool WithholdsIdentity(string header)
    {
        var separator = header.IndexOf(':');
        if (separator < 0 || !header.AsSpan(0, separator).Trim().Equals("Privacy", StringComparison.OrdinalIgnoreCase))
            return false;
        foreach (var token in header.AsSpan(separator + 1).Split(';'))
        {
            var value = header.AsSpan(separator + 1)[token].Trim();
            if (value.Equals("id", StringComparison.OrdinalIgnoreCase) || value.Equals("user", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
