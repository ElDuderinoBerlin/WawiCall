using CSCore;
using CSCore.Codecs.WAV;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Audio;

/// <summary>Local microphone capture and draft playback without a telephony session.</summary>
public interface IAnnouncementAudio
{
    Task RecordAsync(string? deviceId, string destination, CancellationToken finish, CancellationToken abort);
    Task PreviewAsync(string path, AudioDeviceSettings settings, CancellationToken token);
}

internal sealed class WindowsAnnouncementAudio(IAudioFileConversion conversion, IHistoryRecordingOutput output) : IAnnouncementAudio
{
    public async Task RecordAsync(string? deviceId, string destination, CancellationToken finish, CancellationToken abort)
    {
        var raw = Path.Combine(Path.GetTempPath(), "WawiCallPro-microphone-" + Guid.NewGuid().ToString("N") + ".wav");
        var ownsFile = false;
        try
        {
            await Task.Run(async () =>
            {
                using var file = new FileStream(raw, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                ownsFile = true;
                await CaptureAsync(deviceId, file, finish, abort).ConfigureAwait(false);
            }, abort).ConfigureAwait(false);
            await conversion.ConvertAsync(raw, destination, new AudioWaveOptions(null, 1, 100), null, abort)
                .ConfigureAwait(false);
        }
        finally
        {
            if (ownsFile) File.Delete(raw);
        }
    }

    private static async Task CaptureAsync(string? deviceId, Stream file, CancellationToken finish, CancellationToken abort)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = deviceId is null ? MMDeviceEnumerator.DefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia)
            : enumerator.GetDevice(deviceId);
        using var capture = new WasapiCapture(true, AudioClientShareMode.Shared, 100, new WaveFormat(16000, 16, 1))
            { Device = device };
        capture.Initialize();
        using var writer = new WaveWriter(file, capture.WaveFormat);
        using var ending = CancellationTokenSource.CreateLinkedTokenSource(finish, abort);
        var stopped = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        long written = 0;
        capture.Stopped += (_, e) => stopped.TrySetResult(e.Exception);
        capture.DataAvailable += (_, e) =>
        {
            if (stopped.Task.IsCompleted) return;
            try
            {
                writer.Write(e.Data, e.Offset, e.ByteCount);
                written += e.ByteCount;
            }
            catch (Exception error) { stopped.TrySetResult(error); }
        };
        abort.ThrowIfCancellationRequested();
        capture.Start();
        var limit = Task.Delay(TimeSpan.FromMinutes(5), ending.Token);
        try
        {
            var completed = await Task.WhenAny(stopped.Task, limit).ConfigureAwait(false);
            abort.ThrowIfCancellationRequested();
            if (completed == stopped.Task)
                throw new AudioFileConversionException("Die Mikrofonaufnahme wurde vom Audiogerät unterbrochen.");
        }
        finally
        {
            capture.Stop();
            ending.Cancel();
        }
        if (stopped.Task.IsCompletedSuccessfully && await stopped.Task.ConfigureAwait(false) is not null)
            throw new AudioFileConversionException("Die Mikrofonaufnahme konnte nicht fertiggestellt werden.");
        if (written == 0) throw new AudioFileConversionException("Das Mikrofon hat noch keine Audiodaten geliefert.");
    }

    public Task PreviewAsync(string path, AudioDeviceSettings settings, CancellationToken token) => Task.Run(() =>
    {
        using var source = new WaveFileReader(path);
        output.Play(source, settings, token);
    }, token);
}
