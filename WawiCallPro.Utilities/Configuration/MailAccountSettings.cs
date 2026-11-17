using System.Collections.Immutable;
using WawiCallPro.Interfaces.Office;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Local connection settings. OAuth tokens live in a separate protected cache and are never exported.</summary>
public sealed record MailAccountSettings
{
    public const string GoogleSecretPurpose = "mail-google-desktop-client";
    public const string MicrosoftSecretPurpose = "mail-microsoft-application-client";
    public Guid Id { get; init; }
    public MailProviderKind Provider { get; init; } = MailProviderKind.OutlookDesktop;
    public string DisplayName { get; init; } = "Postfach";
    public bool Enabled { get; init; } = true;
    public string? ClientId { get; init; }
    public string Tenant { get; init; } = "common";
    public ProtectedSecret? GoogleClientSecret { get; init; }
    public bool ApplicationAuthentication { get; init; }
    public ProtectedSecret? MicrosoftClientSecret { get; init; }
    public string? Mailbox { get; init; }
    public string? RemoteIdentity { get; init; }
    public string? OutlookStoreId { get; init; }
    public ImmutableArray<string> FolderIds { get; init; } = [];
    public ImmutableDictionary<string, string> FolderNames { get; init; } = ImmutableDictionary<string, string>.Empty;
    public WawiCallPro.Interfaces.Companies.CompanyKey? Company { get; init; }
    public int ImportDays { get; init; } = 365;
    public int MaximumMessages { get; init; } = 10000;
    public bool SynchronizeOnStartup { get; init; }
    public override string ToString() => nameof(MailAccountSettings);
}
