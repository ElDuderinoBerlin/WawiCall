using System.Collections.Immutable;
using CSCore.CoreAudioAPI;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.Audio;

/// <summary>Enumerates CSCore endpoint metadata on a worker; no audio client or stream is activated.</summary>
public sealed class WindowsAudioDeviceCatalog(StartupExecutionMode mode) : IAudioDeviceCatalog
{
    public Task<AudioDeviceCatalog> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mode.SuppressNetwork) return Task.FromResult(new AudioDeviceCatalog([], [], Suppressed: true));
        return Task.Run(() => Read(cancellationToken), cancellationToken);
    }

    private static AudioDeviceCatalog Read(CancellationToken token)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return new AudioDeviceCatalog(ReadFlow(enumerator, DataFlow.Capture, token), ReadFlow(enumerator, DataFlow.Render, token));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new AudioDeviceCatalogException(); }
    }

    private static ImmutableArray<AudioDeviceInfo> ReadFlow(MMDeviceEnumerator enumerator, DataFlow flow, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var devices = enumerator.EnumAudioEndpoints(flow, DeviceState.Active);
        var result = ImmutableArray.CreateBuilder<AudioDeviceInfo>();
        foreach (var item in devices)
        {
            using var device = item;
            token.ThrowIfCancellationRequested();
            result.Add(new(device.DeviceID, device.FriendlyName));
        }
        token.ThrowIfCancellationRequested();
        return result.OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase).ThenBy(device => device.Id, StringComparer.Ordinal).ToImmutableArray();
    }
}
