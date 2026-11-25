using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>The version belongs to settings JSON, independently of the DPAPI envelope version.</summary>
public static class ConfigurationFormat
{
    public const int CurrentVersion = 21;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
        MaxDepth = 16,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter<Microsoft.Extensions.Logging.LogLevel>(allowIntegerValues: false),
            new JsonStringEnumConverter<SipTransport>(allowIntegerValues: false),
            new JsonStringEnumConverter<RecordingReceiveSource>(allowIntegerValues: false),
            new JsonStringEnumConverter<Database.Contracts.RecordingArtifactFormat>(allowIntegerValues: false),
            new JsonStringEnumConverter<CommunicationDatabaseProvider>(allowIntegerValues: false),
            new JsonStringEnumConverter<LexwareProductLine>(allowIntegerValues: false),
            new JsonStringEnumConverter<WawiCallPro.Interfaces.Office.MailProviderKind>(allowIntegerValues: false)
        }
    };

    internal static ConfigurationSnapshot Decode(byte[] bytes)
    {
        try
        {
            ReadOnlyMemory<byte> json = bytes;
            if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) json = json[3..];
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowDuplicateProperties = false, MaxDepth = 16 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
            if (!root.TryGetProperty("formatVersion", out var version))
            {
                // Only the actual unversioned schema is accepted; new fields require the versioned envelope.
                var legacy = root.Deserialize<LegacyConnectionOptions>(JsonOptions)!;
                return new ConfigurationSnapshot(new ApplicationOptions { SipAccount = legacy.SipAccount }, true)
                {
                    FormatVersion = 0,
                    ContentHash = ComputeHash(bytes)
                };
            }
            if (!version.TryGetInt32(out var value) || value is < 1 or > CurrentVersion)
                throw ConfigurationException.FileError($"Das Einstellungsformat wird nicht unterstützt. Unterstützt werden Version 1 bis {CurrentVersion}.");
            var envelope = root.Deserialize<SettingsDocument>(JsonOptions)!;
            return new ConfigurationSnapshot(envelope.Settings with { User = ApplyLogging(root.TryGetProperty("Logging", out var logging) ? logging : default, envelope.Settings.User) }, true) { FormatVersion = value, ContentHash = ComputeHash(bytes) };
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw ConfigurationException.FileError("Das JSON-Format, ein Feld oder ein geschützter Wert ist ungültig.");
        }
    }

    internal static byte[] Encode(ApplicationOptions options) =>
        JsonSerializer.SerializeToUtf8Bytes(new SettingsDocument { FormatVersion = CurrentVersion, Settings = options, Logging = new() { Enabled = options.User.LoggingEnabled, LogLevel = options.User.LogLevel, LogDirectory = options.User.LogDirectory } }, JsonOptions);

    internal static string ComputeHash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    internal static UserSettings ApplyLogging(JsonElement section, UserSettings user)
    {
        if (section.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return user;
        try
        {
            var settings = section.Deserialize<LoggingDocument>(JsonOptions) ?? throw new JsonException();
            return user with { LoggingEnabled = settings.Enabled ?? user.LoggingEnabled, LogLevel = settings.LogLevel ?? user.LogLevel,
                LogDirectory = section.TryGetProperty("LogDirectory", out _) ? settings.LogDirectory : user.LogDirectory };
        }
        catch (JsonException) { throw ConfigurationException.FileError("Die Logging-Einstellungen sind ungültig."); }
    }
    private sealed class LoggingDocument
    {
        [JsonPropertyName("Enabled")] public bool? Enabled { get; init; }
        [JsonPropertyName("LogLevel")] public Microsoft.Extensions.Logging.LogLevel? LogLevel { get; init; }
        [JsonPropertyName("LogDirectory")] public string? LogDirectory { get; init; }
    }

    private sealed class SettingsDocument
    {
        public required int FormatVersion { get; init; }
        public required ApplicationOptions Settings { get; init; }
        [JsonPropertyName("Logging")] public LoggingDocument? Logging { get; init; }
    }

    private sealed class LegacyConnectionOptions
    {
        public SipAccountOptions? SipAccount { get; init; }
    }
}
