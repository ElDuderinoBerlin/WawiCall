using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Local user's preferences. PreferredCompany is a preference, not proof of a selectable/active company.</summary>
public sealed record UserSettings
{
    public string? ActivePluginId { get; init; }
    public CompanyKey? PreferredCompany { get; init; }
    public bool ShowCallNotifications { get; init; } = true;
    public bool StartMinimized { get; init; }
    public bool UseDirectXRendering { get; init; }
    public override string ToString() => nameof(UserSettings);
}

/// <summary>Local CSCore/MMDevice endpoint IDs, never enumeration indexes. Null selects the system default later.</summary>
public sealed record AudioDeviceSettings
{
    public string? CaptureDeviceId { get; init; }
    public string? PlaybackDeviceId { get; init; }
    public string? RingDeviceId { get; init; }
    public int PlaybackVolumePercent { get; init; } = 100;
    public int RingVolumePercent { get; init; } = 100;
    public bool MicrophoneMuted { get; init; }
    public override string ToString() => nameof(AudioDeviceSettings);
}

/// <summary>Number interpretation defaults for exactly one external company. No fallback to another company's values.</summary>
public sealed record CompanySettings
{
    public required CompanyKey Company { get; init; }
    public bool IncludeInPhoneCache { get; init; }
    public string? DisplayName { get; init; }
    public string DefaultPhoneRegion { get; init; } = "DE";
    public string? LocalAreaCode { get; init; }
    public override string ToString() => nameof(CompanySettings);
}

/// <summary>Configured provider of the application's owned communications database.</summary>
public enum CommunicationDatabaseProvider
{
    Sqlite = 1,
    SqlServer = 2,
    MySql = 3,
    PostgreSql = 4
}

/// <summary>The application's communication database, shared across companies; never a Lexware source database.</summary>
public sealed record CommunicationDatabaseSettings
{
    public const string ConnectionStringPurpose = "communications-database-connection-string";
    public CommunicationDatabaseProvider Provider { get; init; } = CommunicationDatabaseProvider.Sqlite;
    // Null uses communications.db under the shared App/Console directory; a file name or an explicit absolute path is accepted.
    public string? SqliteFileName { get; init; }
    public ProtectedSecret? ConnectionString { get; init; }
    public override string ToString() => nameof(CommunicationDatabaseSettings);
}
