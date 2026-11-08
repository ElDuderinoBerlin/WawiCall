using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>The version belongs to settings JSON, independently of the DPAPI envelope version.</summary>
public static class ConfigurationFormat
{
    public const int CurrentVersion = 3;

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
            new JsonStringEnumConverter<SipTransport>(allowIntegerValues: false),
            new JsonStringEnumConverter<CommunicationDatabaseProvider>(allowIntegerValues: false),
            new JsonStringEnumConverter<LexwareProductLine>(allowIntegerValues: false)
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
                throw ConfigurationException.FileError("Das Einstellungsformat wird nicht unterstützt. Unterstützt werden Version 1, 2 und 3.");
            var envelope = root.Deserialize<SettingsDocument>(JsonOptions)!;
            return new ConfigurationSnapshot(envelope.Settings, true) { FormatVersion = value, ContentHash = ComputeHash(bytes) };
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw ConfigurationException.FileError("Das JSON-Format, ein Feld oder ein geschützter Wert ist ungültig.");
        }
    }

    internal static byte[] Encode(ApplicationOptions options) =>
        JsonSerializer.SerializeToUtf8Bytes(new SettingsDocument { FormatVersion = CurrentVersion, Settings = options }, JsonOptions);

    internal static string ComputeHash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private sealed class SettingsDocument
    {
        public required int FormatVersion { get; init; }
        public required ApplicationOptions Settings { get; init; }
    }

    private sealed class LegacyConnectionOptions
    {
        public SipAccountOptions? SipAccount { get; init; }
    }
}
