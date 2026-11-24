using System.Collections.Immutable;
using CSCore.CoreAudioAPI;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.Audio;

/// <summary>Enumerates CSCore endpoint metadata on a worker; no audio client or stream is activated.</summary>
public sealed class WindowsAudioDeviceCatalog(StartupExecutionMode mode) : IAudioDeviceCatalog, IAudioEndpointMeterFactory, IAudioDeviceChanges
{
    public WindowsAudioDeviceCatalog(StartupExecutionMode mode, WindowsIncomingRingtone ringtones) : this(mode) => Ringtones = ringtones;

    public WindowsIncomingRingtone? Ringtones { get; }

    public IAudioEndpointMeter? Open(AudioEndpointDirection direction, string? deviceId) =>
        mode.SuppressNetwork ? null : WindowsAudioEndpointMeter.Open(direction, deviceId);

    public IDisposable? Subscribe(Action onChanged)
    {
        if (mode.SuppressNetwork) return null;
        try { return new EndpointSubscription(onChanged); }
        catch (CoreAudioAPIException) { return null; }
        catch (System.Runtime.InteropServices.COMException) { return null; }
    }

    private sealed class EndpointSubscription : IDisposable
    {
        private readonly MMDeviceEnumerator _enumerator = new();
        private readonly Action _changed;

        internal EndpointSubscription(Action changed)
        {
            _changed = changed;
            _enumerator.DeviceAdded += Notify;
            _enumerator.DeviceRemoved += Notify;
            _enumerator.DeviceStateChanged += Notify;
            _enumerator.DefaultDeviceChanged += Notify;
        }

        private void Notify(object? sender, EventArgs e) => _changed();
        public void Dispose() => _enumerator.Dispose();
    }

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
