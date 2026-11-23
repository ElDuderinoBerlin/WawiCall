using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.App.Hosting;

internal sealed record RecordingExportChoice(Guid ArtifactId, RecordingArtifactFormat Format);

public sealed class HistoryRecordingFiles(RecordingPlaybackCatalog catalog, IWorkstationIdentity workstation,
    IRecordingArtifactRepository artifacts, IRecordingCaptureRepository captures, IRecordingConversionRepository jobs,
    IRecordingConversionRequests conversions, IRecordingExport exporter, IMp3Converter encoder)
{
    internal async Task<IReadOnlyList<RecordingExportChoice>> GetExportChoicesAsync(Guid callId, CancellationToken token)
    {
        var candidate = await FindAsync(callId, token).ConfigureAwait(false);
        if (candidate is null) return [];
        var files = await artifacts.GetForRecordingAsync(candidate.RecordingId, token).ConfigureAwait(false);
        return files.Where(file => file.Artifact.CallId == callId &&
                file.Artifact.Availability == RecordingAvailability.Available)
            .Select(file => new RecordingExportChoice(file.Artifact.Id, file.Artifact.Format))
            .OrderBy(choice => choice.Format).ToArray();
    }

    internal async Task ExportAsync(Guid callId, RecordingExportChoice choice, string destination, CancellationToken token)
    {
        if (!(await GetExportChoicesAsync(callId, token).ConfigureAwait(false)).Contains(choice))
            throw new HistoryRecordingFileException("Die gewählte Aufnahme ist nicht mehr verfügbar.");
        await exporter.ExportAsync(choice.ArtifactId, destination, token).ConfigureAwait(false);
    }

    internal async Task<RecordingConversionState> QueueMp3Async(Guid callId, CancellationToken token)
    {
        var source = await FindAsync(callId, token).ConfigureAwait(false);
        if (source is null) throw new HistoryRecordingFileException("Für dieses Gespräch ist keine bestätigte Aufnahme verfügbar.");
        if (source.Format != RecordingArtifactFormat.Wave)
        {
            if ((await GetExportChoicesAsync(callId, token).ConfigureAwait(false)).Any(choice => choice.Format == RecordingArtifactFormat.Mp3))
                return RecordingConversionState.Completed;
            throw new HistoryRecordingFileException("Für die MP3-Erstellung ist die bestätigte WAV-Aufnahme erforderlich.");
        }

        var existing = await jobs.GetForSourceAsync(source.Id, token).ConfigureAwait(false);
        var format = existing?.Job.Format;
        if (format is null)
        {
            var capture = await captures.GetAsync(source.RecordingId, token).ConfigureAwait(false);
            if (capture is null) throw new HistoryRecordingFileException("Für diese WAV-Datei fehlen die Aufnahmemetadaten.");
            var formats = await encoder.GetSupportedFormatsAsync(token).ConfigureAwait(false);
            var sampleRate = capture.Capture.SampleRate;
            var channels = capture.Capture.Channels;
            format = formats.Where(item => item.SampleRate == sampleRate && item.Channels == channels)
                .OrderBy(item => Math.Abs(item.BitRate - 128000)).ThenBy(item => item.BitRate).FirstOrDefault()
                ?? throw new HistoryRecordingFileException("Der MP3-Encoder unterstützt das Format dieser Aufnahme nicht.");
        }

        var queued = await conversions.EnqueueAsync(source.RecordingId, format, token).ConfigureAwait(false);
        if (queued.Conversion.Job.State == RecordingConversionState.Completed
            && queued.Target.Artifact.Availability != RecordingAvailability.Available)
            throw new HistoryRecordingFileException("Der MP3-Auftrag ist abgeschlossen, aber die Datei ist derzeit nicht verfügbar.");
        return queued.Conversion.Job.State;
    }

    private async Task<RecordingArtifact?> FindAsync(Guid callId, CancellationToken token)
    {
        if (await workstation.GetAsync(token).ConfigureAwait(false) is not { } local) return null;
        var recordings = await catalog.FindAsync(local, [callId], token).ConfigureAwait(false);
        return recordings.GetValueOrDefault(callId);
    }
}

internal sealed class HistoryRecordingFileException(string message) : Exception(message);
