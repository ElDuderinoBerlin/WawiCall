using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Local user's preferences. PreferredCompany is a preference, not proof of a selectable/active company.</summary>
public sealed record UserSettings
{
    public string FontFamily { get; init; } = "Segoe UI";
    public float FontSize { get; init; } = 9;
    public bool ShowStartPage { get; init; } = true;
    public bool LoggingEnabled { get; init; } = true;
    public Microsoft.Extensions.Logging.LogLevel LogLevel { get; init; } = Microsoft.Extensions.Logging.LogLevel.Information;
    public string? LogDirectory { get; init; }
    public string? ActivePluginId { get; init; }
    public CompanyKey? PreferredCompany { get; init; }
    public bool ShowCallNotifications { get; init; } = true;
    public bool ShowIncomingCallToasts { get; init; }
    public bool ReuseCallDialog { get; init; }
    public bool StartMinimized { get; init; }
    [System.Text.Json.Serialization.JsonInclude]
    [System.Text.Json.Serialization.JsonPropertyName("useDirectXRendering")]
    internal bool LegacyDirectXRendering { init { } }
    public string? ShowWindowHotkey { get; init; }
    public string? DialHotkey { get; init; }
    public override string ToString() => nameof(UserSettings);
}

/// <summary>Local opt-in boundary; portable preferences do not grant automatic file processing.</summary>
public sealed record AutomaticMp3Settings
{
    public bool Enabled { get; init; }
    public DateTimeOffset? SinceUtc { get; init; }
}

/// <summary>Local format preference and the starting boundary for automatic conversion.</summary>
public sealed record RecordingOutputSettings(Database.Contracts.RecordingArtifactFormat Format, DateTimeOffset? SinceUtc);

public enum RecordingReceiveSource { Rtp, PlaybackLoopback }

public enum AnnouncementSpeechProvider { Windows, AzureSpeech, GoogleCloud }

public sealed record AnnouncementAudioSettings
{
    public AnnouncementSpeechProvider SpeechProvider { get; init; } = AnnouncementSpeechProvider.Windows;
    public string? VoiceId { get; init; }
    public string? CaptureDeviceId { get; init; }
    public string? PlaybackDeviceId { get; init; }
    public AnnouncementCloudSettings Cloud { get; init; } = new();
}

/// <summary>Speech credentials scoped to the Windows user and omitted from portable preferences.</summary>
public sealed record AnnouncementCloudSettings
{
    public const string AzureKeyPurpose = "announcement.azure.key";
    public const string GoogleCredentialPurpose = "announcement.google.credential";
    public const string DeepLKeyPurpose = "announcement.deepl.key";
    public const int AzureFreeCharacters = 500_000;
    public const int GoogleFreeCharactersPerVoiceFamily = 1_000_000;

    public string? AzureRegion { get; init; }
    public ProtectedSecret? AzureKey { get; init; }
    public string? GoogleProjectId { get; init; }
    public ProtectedSecret? GoogleCredential { get; init; }
    public ProtectedSecret? DeepLKey { get; init; }
    public int AzureMonthlyAllowance { get; init; }
    public int GoogleNeural2MonthlyAllowance { get; init; }
    public int GoogleChirpMonthlyAllowance { get; init; }
    public override string ToString() => nameof(AnnouncementCloudSettings);
}

/// <summary>Local CSCore/MMDevice endpoint IDs, never enumeration indexes. Null selects the system default later.</summary>
public sealed record AudioDeviceSettings
{
    public string? CaptureDeviceId { get; init; }
    public string? PlaybackDeviceId { get; init; }
    public string? RingDeviceId { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CaptureDeviceName { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PlaybackDeviceName { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RingDeviceName { get; init; }
    public string? WindowsRingtoneFile { get; init; }
    public string? CustomRingtonePath { get; init; }
    public int MicrophoneVolumePercent { get; init; } = 100;
    public int PlaybackVolumePercent { get; init; } = 100;
    public int RingVolumePercent { get; init; } = 100;
    public int CaptureBufferMilliseconds { get; init; }
    public int PlaybackBufferMilliseconds { get; init; }
    public bool MicrophoneMuted { get; init; }
    public bool RecordConnectedCallsAutomatically { get; init; }
    public RecordingReceiveSource RecordingReceiveSource { get; init; } = RecordingReceiveSource.Rtp;
    public AnnouncementAudioSettings Announcements { get; init; } = new();
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
