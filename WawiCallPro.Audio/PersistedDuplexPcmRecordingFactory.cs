using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Audio;

public sealed class PersistedDuplexPcmRecordingFactory(IDuplexPcmRecordingFactory recordings, IRecordingJournal journal,
    ApplicationPaths paths, TimeProvider clock, IPreparedWaveOutbox? preparedWaves = null) : IPersistedDuplexPcmRecordingFactory
{
    public async Task<IDuplexPcmRecording> StartAsync(IConversationPcmFailureSource source, DuplexPcmRecordingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Recording); ArgumentNullException.ThrowIfNull(request.Sent); ArgumentNullException.ThrowIfNull(request.Received);
        var item = request.Recording; var now = clock.GetUtcNow();
        if (request.RecordingEpochUtc is not { } epoch || epoch.Offset != TimeSpan.Zero || epoch > now)
            throw new ArgumentException("Eine explizite UTC-Aufnahmeepoche ist erforderlich und darf nicht in der Zukunft liegen.");
        if (!Enum.IsDefined(request.TimelineBasis) || request.TimelineBasis == RecordingTimelineBasis.LocalCodecObservation
            && (source is PlaybackRecordingSource loopback ? loopback.Sent : source) is not LiveRecordingPreparation)
            throw new ArgumentException("Die lokale Medienzeitachse benötigt ihre eigene vorbereitete Aufnahmequelle.");
        if (request.TimelineBasis == RecordingTimelineBasis.LocalCodecObservation)
        {
            var prepared = (LiveRecordingPreparation)(source is PlaybackRecordingSource selected ? selected.Sent : source);
            var expected = prepared.CreateRequest(item);
            if (expected.RecordingEpochUtc != epoch || expected.Sent != request.Sent || expected.Received != request.Received)
                throw new ArgumentException("Die Aufnahmezeitreferenzen gehören nicht zu dieser lokalen Medienvorbereitung.");
        }
        ConversationWaveMixValidation.Validate(new(item.CallId, item.RecordingId, [], item.WavePath, item.SampleRate, TimeSpan.FromMilliseconds(1), item.Layout));
        if (item.QueueCapacity is < 1 or > 256 || !Valid(request.Sent.RtpClockRate, request.Sent.RecordingOffset) || !Valid(request.Received.RtpClockRate, request.Received.RecordingOffset))
            throw new ArgumentException("Gültige Aufnahmeanker und eine begrenzte Audiowarteschlange sind erforderlich.");
        var relative = RelativePath(item.WavePath);
        var capture = new RecordingCapture(item.RecordingId, item.CallId, epoch, null, PcmRecordingState.Capturing,
            item.SampleRate, item.Layout == ConversationWaveLayout.SeparatedStereo ? 2 : 1);
        var file = new Recording(item.RecordingId, item.CallId, relative, "wav", null, RecordingAvailability.Pending);
        cancellationToken.ThrowIfCancellationRequested();
        StoredRecordingJournal initial;
        try { initial = await journal.BeginAsync(new(file, null, now), new(capture, null, now), cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is RepositoryConflictException or WawiCallPro.Database.DatabaseOperationException) { throw; }
        catch (Exception) { throw new RecordingPersistenceException(false); }
        var writer = new RecordingJournalWriter(journal, clock, initial);
        IDuplexPcmRecording? inner = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { Directory.CreateDirectory(Path.GetDirectoryName(item.WavePath)!); _ = RelativePath(item.WavePath); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            { throw new PcmRecordingException(PcmRecordingFailure.FileFailure); }
            var persistedRequest = request with
            {
                Recording = item with
                {
                    StageWaveAsync = preparedWaves is null ? null : (path, wave, token) =>
                        writer.PrepareWaveAsync(preparedWaves, path, wave, token)
                }
            };
            inner = await recordings.StartAsync(source, persistedRequest, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new PersistedDuplexPcmRecording(inner, writer, item, epoch, clock)
            { ReleaseCompletedSegments = request.TimelineBasis == RecordingTimelineBasis.LocalCodecObservation };
        }
        catch (Exception error)
        {
            Exception? cleanup = null;
            try { if (inner is not null) await inner.DisposeAsync().ConfigureAwait(false); } catch (Exception failed) { cleanup = failed; }
            var canceled = error is OperationCanceledException && cleanup is null;
            var cause = cleanup ?? error;
            var failure = canceled ? RecordingCaptureFailure.Cancelled : cause is PcmRecordingException pcm ? (RecordingCaptureFailure)(int)pcm.Failure : RecordingCaptureFailure.StartupFailed;
            await writer.SaveAsync(new(item.RecordingId, item.CallId, epoch, null, canceled ? PcmRecordingState.Aborted : PcmRecordingState.Failed,
                capture.SampleRate, capture.Channels, failure: failure)).ConfigureAwait(false);
            if (cleanup is null && (error is OperationCanceledException or PcmRecordingException)) throw;
            throw new PcmRecordingException(PcmRecordingFailure.FileFailure);
        }
    }

    private string RelativePath(string wavePath)
    {
        try
        {
            var relative = Path.GetRelativePath(paths.RecordingsDirectory, Path.GetFullPath(wavePath)).Replace('\\', '/');
            if (!string.Equals(paths.ResolveRecordingPath(relative), Path.GetFullPath(wavePath), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException();
            var current = paths.RecordingsDirectory;
            CheckLink(current);
            foreach (var part in relative.Split('/')) { current = Path.Combine(current, part); CheckLink(current); }
            return relative;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or OperationException)
        { throw new ArgumentException("Das WAV-Ziel muss innerhalb des lokalen Aufnahmeverzeichnisses ohne Verknüpfungen liegen."); }
    }
    private static void CheckLink(string path)
    {
        try { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException(); }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }
    private static bool Valid(int rate, TimeSpan offset) => rate is >= 1 and <= 192000 && offset >= TimeSpan.FromSeconds(-2) && offset < TimeSpan.FromHours(8);
}
