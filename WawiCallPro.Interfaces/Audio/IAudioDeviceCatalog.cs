using System.Collections.Immutable;

namespace WawiCallPro.Interfaces.Audio;

/// <summary>A detached active Windows endpoint. Persist Id, never the display name or list position.</summary>
public sealed record AudioDeviceInfo(string Id, string DisplayName);

/// <summary>Device discovery does not open capture/playback streams. Suppressed distinguishes audits from an empty device list.</summary>
public sealed record AudioDeviceCatalog(ImmutableArray<AudioDeviceInfo> Capture, ImmutableArray<AudioDeviceInfo> Playback, bool Suppressed = false);

public interface IAudioDeviceCatalog
{
    Task<AudioDeviceCatalog> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>A safe display message; native exception details and endpoint IDs stay out of UI diagnostics.</summary>
public sealed class AudioDeviceCatalogException() : Exception("Geräteliste nicht verfügbar. Bitte erneut laden. Zuordnungen bleiben erhalten.");
