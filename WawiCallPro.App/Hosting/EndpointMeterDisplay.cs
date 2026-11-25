using Quantarix.DevExpressExtensions.Controls;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.App.Hosting;

internal sealed class EndpointMeterDisplay(AudioLevelMeter bar, AudioEndpointDirection direction) : IDisposable
{
    private IAudioEndpointMeter? _meter;
    private string? _deviceId;
    private long _reopenAfter;

    internal static string Describe(AudioEndpointReading? reading) => reading is null
        ? "Windows-Gerätestatus nicht verfügbar."
        : $"Windows: {reading.WindowsVolumePercent} %, {(reading.Muted ? "stumm" : "nicht stumm")}.";

    internal AudioEndpointReading? Update(IAudioEndpointMeterFactory factory, string? deviceId)
    {
        var now = Environment.TickCount64;
        if (_deviceId != deviceId || now >= _reopenAfter && _meter is null)
        {
            ReleaseMeter();
            if (_deviceId != deviceId) bar.Clear();
            _deviceId = deviceId;
            _reopenAfter = now + 1500;
            _meter = factory.Open(direction, deviceId);
        }

        var reading = _meter?.Read();
        if (reading is null && _meter is not null)
        {
            ReleaseMeter();
            _reopenAfter = now + 1500;
        }
        bar.Position = reading?.PeakPercent ?? 0;
        bar.SetSpectrum((reading?.Spectrum ?? AudioSpectrum.Silence).AsSpan());
        bar.ToolTip = Describe(reading);
        return reading;
    }

    public void Dispose()
    {
        ReleaseMeter();
        _reopenAfter = 0;
        if (!bar.IsDisposed)
        {
            bar.Clear();
            bar.ToolTip = "";
        }
    }

    private void ReleaseMeter()
    {
        _meter?.Dispose();
        _meter = null;
    }
}
