using System.Runtime.InteropServices;
using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.SoundOut;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Audio;

/// <summary>Looks up playable conversations and plays a confirmed local recording until stopped or finished.</summary>
public interface IHistoryRecordingPlayback
{
    Task<IReadOnlySet<Guid>> FindAsync(IReadOnlyCollection<Guid> callIds, CancellationToken token);
    Task PlayAsync(Guid callId, CancellationToken token);
    Task PlayAsync(Guid callId, HistoryPlaybackTransport transport, CancellationToken token) => PlayAsync(callId, token);
}

/// <summary>Consumes decoded PCM on the configured audio endpoint.</summary>
public interface IHistoryRecordingOutput
{
    void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token);

    void Play(IWaveSource source, AudioDeviceSettings settings, AudioPlaybackVolume volume, CancellationToken token) =>
        Play(source, settings with { PlaybackVolumePercent = volume.Percent }, token);

    void Play(IWaveSource source, AudioDeviceSettings settings, HistoryPlaybackTransport transport, CancellationToken token) =>
        Play(source, settings, token);
}

/// <summary>Volume shared between the UI and one playback operation.</summary>
public sealed class AudioPlaybackVolume(int percent)
{
    private int _percent = Math.Clamp(percent, 0, 100);
    public int Percent
    {
        get => Volatile.Read(ref _percent);
        set => Volatile.Write(ref _percent, Math.Clamp(value, 0, 100));
    }
}

public sealed class HistoryRecordingPlayback(RecordingPlaybackCatalog catalog, IWorkstationIdentity workstation,
    IRecordingJournalOutbox pending, IRecordingConversionOutbox conversions, IRecordingConversionRepository jobs,
    IRecordingArtifactFileInspector inspector, ApplicationConfigurationService configuration, ConfigurationStore settings, IHistoryRecordingOutput output) : IHistoryRecordingPlayback
{
    private readonly SemaphoreSlim _playing = new(1, 1);

    public async Task<IReadOnlySet<Guid>> FindAsync(IReadOnlyCollection<Guid> callIds, CancellationToken token)
    {
        if (callIds.Count == 0 || await workstation.GetAsync(token).ConfigureAwait(false) is not { } id)
            return new HashSet<Guid>();
        return (await catalog.FindAsync(id, callIds, token).ConfigureAwait(false)).Keys.ToHashSet();
    }

    public Task PlayAsync(Guid callId, CancellationToken token) => PlayCoreAsync(callId, null, token);

    public Task PlayAsync(Guid callId, HistoryPlaybackTransport transport, CancellationToken token) => PlayCoreAsync(callId, transport, token);

    private async Task PlayCoreAsync(Guid callId, HistoryPlaybackTransport? transport, CancellationToken token)
    {
        if (!await _playing.WaitAsync(0, token).ConfigureAwait(false))
            throw new InvalidOperationException("Eine Aufnahme wird bereits wiedergegeben.");
        try
        {
            var preferred = (await settings.LoadAsync(token).ConfigureAwait(false)).Options.GetRecordingOutput().Format;
            if (await workstation.GetAsync(token).ConfigureAwait(false) is not { } id
                || !(await catalog.FindAsync(id, [callId], token, preferred).ConfigureAwait(false)).TryGetValue(callId, out var artifact))
                throw new InvalidOperationException("Für dieses Gespräch ist keine bestätigte lokale Aufnahme verfügbar.");

            await using var lease = pending.AcquireInspection(artifact.RecordingId);
            if (lease.HasPending) throw new RecordingOutboxException();
            if (artifact.Format != RecordingArtifactFormat.Wave)
            {
                foreach (var jobId in conversions.GetPending())
                {
                    var job = await jobs.GetAsync(jobId, token).ConfigureAwait(false);
                    if (job is null || job.Job.TargetArtifactId == artifact.Id) throw new RecordingOutboxException();
                }
            }
            await using var observed = await inspector.InspectAsync(artifact, token).ConfigureAwait(false);
            if (observed.VerifiedStream is not { } stream) throw new RecordingOutboxException();
            var initial = configuration.Current.Options.Devices;
            var committed = configuration.ConversationDevices;
            var devices = initial with
            {
                PlaybackDeviceId = committed.PlaybackId,
                PlaybackDeviceName = committed.PlaybackName,
                PlaybackVolumePercent = committed.PlaybackVolumePercent ?? initial.PlaybackVolumePercent,
                PlaybackBufferMilliseconds = committed.PlaybackBufferMilliseconds ?? initial.PlaybackBufferMilliseconds
            };
            await Task.Run(() =>
            {
                using var source = RecordingFormatConverters.OpenPlayback(stream, artifact.Format, token);
                if (transport is null) output.Play(source, devices, token);
                else
                {
                    var format = source.WaveFormat;
                    transport.SetMedia(new(artifact.Format, format.SampleRate, format.Channels, format.BitsPerSample,
                        source.CanSeek ? source.GetLength() : TimeSpan.Zero, source.CanSeek, (stream as FileStream)?.Name));
                    output.Play(source, devices, transport, token);
                }
            }, token).ConfigureAwait(false);
        }
        finally
        {
            transport?.ClearSpectrum();
            _playing.Release();
        }
    }

}

/// <summary>The requested Windows render endpoint could not be opened or was disconnected.</summary>
public sealed class RecordingPlaybackDeviceUnavailableException : Exception
{
    internal RecordingPlaybackDeviceUnavailableException(bool explicitDevice, Exception? cause = null)
        : base(explicitDevice
            ? "Die gewählte Gesprächsausgabe ist nicht verfügbar. Gerät verbinden oder unter Einstellungen → Audio → Geräte eine verfügbare Gesprächsausgabe wählen."
            : "Die Windows-Standardausgabe ist nicht verfügbar. Windows-Audioausgabe prüfen.", cause) { }
}

internal sealed class WasapiHistoryRecordingOutput : IHistoryRecordingOutput
{
    public void Play(IWaveSource source, AudioDeviceSettings settings, CancellationToken token) => PlayCore(source, settings, null, token);

    public void Play(IWaveSource source, AudioDeviceSettings settings, AudioPlaybackVolume volume, CancellationToken token) =>
        PlayCore(source, settings, volume, token);

    public void Play(IWaveSource source, AudioDeviceSettings settings, HistoryPlaybackTransport transport, CancellationToken token)
    {
        using var measured = new MeteredSource(source, transport) { DisposeBaseSource = false };
        transport.SetGain(settings.PlaybackVolumePercent);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var ticks = transport.TakeSeek();
            if (ticks >= 0)
            {
                try
                {
                    source.SetPosition(TimeSpan.FromTicks(ticks));
                    measured.ResetPosition(source.Position);
                    transport.ClearSpectrum();
                }
                finally { transport.CompleteSeek(); }
            }
            if (transport.Paused)
            {
                Task.Delay(40, token).GetAwaiter().GetResult();
                continue;
            }
            if (measured.CanSeek && measured.Position >= measured.Length) return;
            if (PlayWithDeviceRecovery(settings.PlaybackDeviceId, deviceId =>
            {
                using var device = OpenDevice(WindowsAudioDeviceNames.ResolveId(deviceId, settings.PlaybackDeviceName, DataFlow.Render));
                return PlaySegment(device, measured, settings.PlaybackVolumePercent, null, transport, token, settings.PlaybackBufferMilliseconds);
            }, token)) return;
        }
    }

    private static bool PlaySegment(MMDevice device, IWaveSource source, int volume, AudioPlaybackVolume? liveVolume,
        HistoryPlaybackTransport? transport, CancellationToken token, int bufferMs)
    {
        using var output = new WasapiOut(bufferMs == 0, AudioClientShareMode.Shared, bufferMs == 0 ? 100 : bufferMs) { Device = device };
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.Stopped += (_, args) =>
        {
            if (args.Exception is { } error) stopped.TrySetException(error);
            else stopped.TrySetResult();
        };
        try { output.Initialize(source); }
        catch (CoreAudioAPIException error) when (error.ErrorCode == DeviceInvalidated)
        {
            throw new PlaybackDeviceActivationException(error);
        }
        output.Volume = (liveVolume?.Percent ?? volume) / 100F;
        token.ThrowIfCancellationRequested();
        output.Play();
        try
        {
            while ((transport is not null || liveVolume is not null)
                && !stopped.Task.IsCompleted && transport?.SeekPending != true)
            {
                token.ThrowIfCancellationRequested();
                if (transport is not null)
                {
                    if (transport.Paused) output.Pause();
                    else output.Resume();
                }
                if (liveVolume is not null) output.Volume = liveVolume.Percent / 100F;
                Task.WhenAny(stopped.Task, Task.Delay(40, token)).GetAwaiter().GetResult();
            }
            if (transport?.SeekPending != true)
            {
                stopped.Task.WaitAsync(token).GetAwaiter().GetResult();
                return transport?.SeekPending != true;
            }
        }
        finally { output.Stop(); }
        stopped.Task.WaitAsync(token).GetAwaiter().GetResult();
        return false;
    }

    private sealed class MeteredSource(IWaveSource source, HistoryPlaybackTransport transport) : WaveAggregatorBase(source)
    {
        private long _position;

        internal void ResetPosition(long position)
        {
            _position = position;
            transport.SetPosition(this.GetTime(position));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = BaseSource.Read(buffer, offset, count);
            ResetPosition(_position + read);
            var format = WaveFormat;
            if (read > 0 && format.BitsPerSample == 16)
                transport.AddSamples(format.SampleRate, format.Channels, MemoryMarshal.Cast<byte, short>(buffer.AsSpan(offset, read)));
            return read;
        }
    }

    private static void PlayCore(IWaveSource source, AudioDeviceSettings settings, AudioPlaybackVolume? volume, CancellationToken token)
    {
        PlayWithDeviceRecovery(settings.PlaybackDeviceId, deviceId =>
        {
            using var device = OpenDevice(WindowsAudioDeviceNames.ResolveId(deviceId, settings.PlaybackDeviceName, DataFlow.Render));
            return PlaySegment(device, source, settings.PlaybackVolumePercent, volume, null, token, settings.PlaybackBufferMilliseconds);
        }, token);
    }

    private const int DeviceInvalidated = unchecked((int)0x88890004);
    private const int DeviceNotFound = unchecked((int)0x80070490);

    private static MMDevice OpenDevice(string? deviceId)
    {
        using var enumerator = new MMDeviceEnumerator();
        MMDevice device;
        try
        {
            device = deviceId is not null ? enumerator.GetDevice(deviceId)
                : enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (CoreAudioAPIException error) when (error.ErrorCode is DeviceInvalidated or DeviceNotFound)
        {
            throw new PlaybackDeviceActivationException(error);
        }

        try
        {
            if (device.DeviceState == DeviceState.Active) return device;
            throw new PlaybackDeviceActivationException();
        }
        catch (CoreAudioAPIException error) when (error.ErrorCode == DeviceInvalidated)
        {
            device.Dispose();
            throw new PlaybackDeviceActivationException(error);
        }
        catch
        {
            device.Dispose();
            throw;
        }
    }

    internal static bool PlayWithDeviceRecovery(string? deviceId, Func<string?, bool> play, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { return play(deviceId); }
        catch (PlaybackDeviceActivationException) { }
        catch (CoreAudioAPIException error) when (error.ErrorCode == DeviceInvalidated)
        {
            throw new RecordingPlaybackDeviceUnavailableException(deviceId is not null, error);
        }

        // Resolve the same endpoint again. For a system selection, use the current default endpoint.
        token.ThrowIfCancellationRequested();
        try { return play(deviceId); }
        catch (Exception error) when (error is PlaybackDeviceActivationException or CoreAudioAPIException { ErrorCode: DeviceInvalidated })
        {
            throw new RecordingPlaybackDeviceUnavailableException(deviceId is not null, error.InnerException ?? error);
        }
    }

    internal sealed class PlaybackDeviceActivationException(Exception? cause = null)
        : Exception("The Windows playback endpoint could not be activated.", cause);
}
