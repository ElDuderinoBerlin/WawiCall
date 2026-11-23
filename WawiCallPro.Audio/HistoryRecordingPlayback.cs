using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.MediaFoundation;
using CSCore.SoundOut;
using WawiCallPro.Core.Audio;
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
}

public sealed class HistoryRecordingPlayback(RecordingPlaybackCatalog catalog, IWorkstationIdentity workstation,
    IRecordingJournalOutbox pending, IRecordingConversionOutbox conversions, IRecordingConversionRepository jobs,
    IRecordingArtifactFileInspector inspector, ApplicationConfigurationService configuration) : IHistoryRecordingPlayback
{
    private readonly SemaphoreSlim _playing = new(1, 1);

    public async Task<IReadOnlySet<Guid>> FindAsync(IReadOnlyCollection<Guid> callIds, CancellationToken token)
    {
        if (callIds.Count == 0 || await workstation.GetAsync(token).ConfigureAwait(false) is not { } id)
            return new HashSet<Guid>();
        return (await catalog.FindAsync(id, callIds, token).ConfigureAwait(false)).Keys.ToHashSet();
    }

    public async Task PlayAsync(Guid callId, CancellationToken token)
    {
        if (!await _playing.WaitAsync(0, token).ConfigureAwait(false))
            throw new InvalidOperationException("Eine Aufnahme wird bereits wiedergegeben.");
        try
        {
            if (await workstation.GetAsync(token).ConfigureAwait(false) is not { } id
                || !(await catalog.FindAsync(id, [callId], token).ConfigureAwait(false)).TryGetValue(callId, out var artifact))
                throw new InvalidOperationException("Für dieses Gespräch ist keine bestätigte lokale Aufnahme verfügbar.");

            await using var lease = pending.AcquireInspection(artifact.RecordingId);
            if (lease.HasPending) throw new RecordingOutboxException();
            if (artifact.Format == RecordingArtifactFormat.Mp3)
            {
                foreach (var jobId in conversions.GetPending())
                {
                    var job = await jobs.GetAsync(jobId, token).ConfigureAwait(false);
                    if (job is null || job.Job.TargetArtifactId == artifact.Id) throw new RecordingOutboxException();
                }
            }
            await using var observed = await inspector.InspectAsync(artifact, token).ConfigureAwait(false);
            if (observed.VerifiedStream is not { } stream) throw new RecordingOutboxException();
            var devices = configuration.Current.Options.Devices;
            await Task.Run(() => Play(stream, artifact.Format, devices, token), token).ConfigureAwait(false);
        }
        finally { _playing.Release(); }
    }

    private static void Play(Stream stream, RecordingArtifactFormat format, AudioDeviceSettings settings, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        stream.Position = 0;
        using IWaveSource source = format == RecordingArtifactFormat.Wave
            ? new VerifiedPcmSource(stream, PcmWaveHeader.Read(stream, token))
            : new MediaFoundationDecoder(stream);
        using var enumerator = new MMDeviceEnumerator();
        using var device = settings.PlaybackDeviceId is { } deviceId ? enumerator.GetDevice(deviceId)
            : MMDeviceEnumerator.DefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        using var output = new WasapiOut(true, AudioClientShareMode.Shared, 100) { Device = device };
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.Stopped += (_, args) =>
        {
            if (args.Exception is { } error) stopped.TrySetException(error);
            else stopped.TrySetResult();
        };
        output.Initialize(source);
        output.Volume = settings.PlaybackVolumePercent / 100F;
        token.ThrowIfCancellationRequested();
        output.Play();
        try { stopped.Task.WaitAsync(token).GetAwaiter().GetResult(); }
        finally { output.Stop(); }
    }
}
