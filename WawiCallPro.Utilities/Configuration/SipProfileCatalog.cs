using System.Collections.Immutable;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>A named account. The active row stores its account only in the existing root settings. LineAssignment remains readable for legacy files.</summary>
public sealed record SipAccountProfile(Guid Id, string Name, SipAccountOptions? Account = null,
    CallerLineAssignment? LineAssignment = null)
{
    public override string ToString() => nameof(SipAccountProfile);
}

/// <summary>Creates detached editor drafts and selects exactly one account for the existing SIP runtime.</summary>
public static class SipProfileCatalog
{
    public const int MaximumProfiles = 16;
    public static readonly Guid LegacyProfileId = new("cde2af0e-210c-4d5d-a962-369df250ab50");

    public static ImmutableArray<SipAccountProfile> Read(ApplicationOptions options)
    {
        if (options.SipProfiles.IsDefaultOrEmpty)
            return [new(LegacyProfileId, "Standard", options.SipAccount, options.CallerLineAssignment)];
        return options.SipProfiles.Select(profile => profile.Id == options.ActiveSipProfileId
            ? profile with { Account = options.SipAccount, LineAssignment = options.CallerLineAssignment } : profile).ToImmutableArray();
    }

    public static ApplicationOptions Select(ApplicationOptions options, ImmutableArray<SipAccountProfile> drafts, Guid selectedId)
    {
        var selected = drafts.Single(profile => profile.Id == selectedId);
        // Keep a fresh, untouched settings dialog unconfigured. Removing the last account also uses this state.
        if (drafts.Length == 1 && selected.Account is null && selected.Name == "Standard")
            return options with { SipProfiles = [], ActiveSipProfileId = null, SipAccount = null, CallerLineAssignment = null };
        return options with
        {
            SipAccount = selected.Account, CallerLineAssignment = null, ActiveSipProfileId = selectedId,
            SipProfiles = drafts.Select(profile => profile.Id == selectedId
                ? profile with { Account = null, LineAssignment = null } : profile with { LineAssignment = null }).ToImmutableArray()
        };
    }
}
