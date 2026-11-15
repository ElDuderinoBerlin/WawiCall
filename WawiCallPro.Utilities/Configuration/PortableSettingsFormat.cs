using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Allowlisted portable preferences. Credentials, connections and local identities have no JSON fields.</summary>
internal static class PortableSettingsFormat
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 8,
        Converters = { new JsonStringEnumConverter<LogLevel>(allowIntegerValues: false) }
    };

    internal static byte[] Encode(ApplicationOptions options) => JsonSerializer.SerializeToUtf8Bytes(new Document(1, "WawiCallPro",
        new(options.User.LogLevel, options.User.LogDirectory, options.User.ShowCallNotifications, options.User.StartMinimized,
            options.User.UseDirectXRendering, options.User.ShowWindowHotkey, options.User.DialHotkey),
        new(options.Devices.PlaybackVolumePercent, options.Devices.RingVolumePercent, options.Devices.MicrophoneMuted)), JsonOptions);

    internal static ApplicationOptions Decode(ReadOnlyMemory<byte> bytes, ApplicationOptions local)
    {
        try
        {
            if (bytes.Span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) bytes = bytes[3..];
            var document = JsonSerializer.Deserialize<Document>(bytes.Span, JsonOptions);
            if (document is null || document.FormatVersion != 1 || document.Application != "WawiCallPro" || document.User is null || document.Audio is null)
                throw new JsonException();
            var user = document.User; var audio = document.Audio;
            return local with
            {
                User = local.User with { LogLevel = user.LogLevel, LogDirectory = user.LogDirectory,
                    ShowCallNotifications = user.ShowCallNotifications, StartMinimized = user.StartMinimized,
                    UseDirectXRendering = user.UseDirectXRendering, ShowWindowHotkey = user.ShowWindowHotkey, DialHotkey = user.DialHotkey },
                Devices = local.Devices with { PlaybackVolumePercent = audio.PlaybackVolumePercent, RingVolumePercent = audio.RingVolumePercent, MicrophoneMuted = audio.MicrophoneMuted }
            };
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        { throw ConfigurationException.FileError("Dies ist keine gültige WawiCallPro-Einstellungsdatei für Import/Export (Version 1)."); }
    }

    private sealed record Document(int FormatVersion, string Application, UserPreferences User, AudioPreferences Audio);
    private sealed record UserPreferences(LogLevel LogLevel, string? LogDirectory, bool ShowCallNotifications,
        bool StartMinimized, bool UseDirectXRendering, string? ShowWindowHotkey, string? DialHotkey);
    private sealed record AudioPreferences(int PlaybackVolumePercent, int RingVolumePercent, bool MicrophoneMuted);
}
