using System.Security.Cryptography;
using WawiCallPro.Core.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

public enum RecordingArtifactFileState { Missing, Matches, Changed }

/// <summary>Owns the observed file's read lock through metadata confirmation; missing observations have no file handle.</summary>
public interface IRecordingArtifactObservation : IAsyncDisposable
{
    RecordingArtifactFileState State { get; }
}
/// <summary>Observes one confirmed artifact; caller owns the observation until its metadata update has completed.</summary>
public interface IRecordingArtifactFileInspector
{
    Task<IRecordingArtifactObservation> InspectAsync(RecordingArtifact artifact, CancellationToken cancellationToken = default);
}

/// <summary>Checks registered WAV/MP3 original bytes under a read lock. WAVs also require a valid PCM header; no decoder is needed for MP3 identity.</summary>
public sealed class RecordingArtifactFileInspector(ApplicationPaths paths, IRecordingFileProbe probe) : IRecordingArtifactFileInspector
{
    public async Task<IRecordingArtifactObservation> InspectAsync(RecordingArtifact artifact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact); cancellationToken.ThrowIfCancellationRequested();
        if (artifact.Format is not (RecordingArtifactFormat.Wave or RecordingArtifactFormat.Mp3) || artifact.Availability is not (RecordingAvailability.Available or RecordingAvailability.Missing))
            throw new ArgumentException("Only confirmed WAV/MP3 artifacts can be inspected.");
        if (probe.IsAbsent(artifact.RelativePath)) return new Observation(RecordingArtifactFileState.Missing, null);
        FileStream? stream = null;
        try
        {
            try { stream = new(paths.ResolveRecordingPath(artifact.RelativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true); }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                if (probe.IsAbsent(artifact.RelativePath)) return new Observation(RecordingArtifactFileState.Missing, null);
                throw;
            }
            if (probe.IsAbsent(artifact.RelativePath)) throw new RecordingOutboxException();
            var state = RecordingArtifactFileState.Changed;
            if (stream.Length is > 0 and <= int.MaxValue && stream.Length == artifact.ByteLength)
            {
                var valid = true;
                try { if (artifact.Format == RecordingArtifactFormat.Wave) _ = PcmWaveHeader.Read(stream, cancellationToken); }
                catch (Mp3ConversionException) { valid = false; }
                if (valid)
                {
                    var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
                    if (digest == artifact.Sha256) state = RecordingArtifactFileState.Matches;
                }
            }
            return new Observation(state, stream);
        }
        catch { if (stream is not null) await stream.DisposeAsync().ConfigureAwait(false); throw; }
    }
    private sealed class Observation(RecordingArtifactFileState state, FileStream? stream) : IRecordingArtifactObservation
    {
        public RecordingArtifactFileState State => state;
        public ValueTask DisposeAsync() => stream?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
