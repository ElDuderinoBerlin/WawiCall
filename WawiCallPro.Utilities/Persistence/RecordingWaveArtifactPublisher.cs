using System.Security.Cryptography;
using WawiCallPro.Core.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Publishes only an explicitly completed, read-locked WAV. Never scans or adopts unrelated files.</summary>
public sealed class RecordingWaveArtifactPublisher(IRecordingArtifactRepository artifacts) : IRecordingCompletionPublisher
{
    public void Validate(IRecordingJournalDelivery delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery); cancellationToken.ThrowIfCancellationRequested();
        var capture = delivery.Capture.Capture;
        if (capture.State != PcmRecordingState.Completed) return;
        var file = delivery.Recording.Recording;
        if (file.Id != capture.RecordingId || file.CallId != capture.CallId || file.Format != "wav"
            || file.Availability != RecordingAvailability.Available || file.Duration != capture.Duration
            || delivery.WaveSha256 is not { Length: 64 } protectedDigest || protectedDigest.Any(ch => !Uri.IsHexDigit(ch))) throw new RecordingOutboxException();
        var wave = delivery.Wave ?? throw new RecordingOutboxException();
        if (!wave.CanRead || !wave.CanSeek || wave.Length is < 44 or > int.MaxValue) throw new RecordingOutboxException();
        wave.Position = 0;
        var header = PcmWaveHeader.Read(wave, cancellationToken);
        if (header.SampleRate != capture.SampleRate || header.Channels != capture.Channels || header.DataLength / (header.Channels * 2) != capture.FrameCount)
            throw new RecordingOutboxException();
    }
    public async Task PublishAsync(IRecordingJournalDelivery delivery, CancellationToken cancellationToken = default)
    {
        Validate(delivery, cancellationToken);
        if (delivery.Capture.Capture.State != PcmRecordingState.Completed) return;
        var file = delivery.Recording.Recording;
        var wave = delivery.Wave ?? throw new RecordingOutboxException();
        string digest;
        try { digest = Convert.ToHexString(await SHA256.HashDataAsync(wave, cancellationToken).ConfigureAwait(false)).ToLowerInvariant(); }
        finally { wave.Position = 0; }
        if (!string.Equals(digest, delivery.WaveSha256, StringComparison.OrdinalIgnoreCase)) throw new RecordingOutboxException();
        var expected = new RecordingArtifact(file.Id, file.Id, file.CallId, RecordingArtifactFormat.Wave,
            file.RelativePath, RecordingAvailability.Available, wave.Length, digest);
        var saved = (await artifacts.GetForRecordingAsync(file.Id, cancellationToken).ConfigureAwait(false))
            .SingleOrDefault(item => item.Artifact.Format == RecordingArtifactFormat.Wave);
        var changed = delivery.Recording.ChangedAtUtc;
        if (saved is null)
        {
            var pending = new RecordingArtifact(file.Id, file.Id, file.CallId, RecordingArtifactFormat.Wave, file.RelativePath, RecordingAvailability.Pending);
            saved = await artifacts.WriteAsync(new(pending, null, changed), cancellationToken).ConfigureAwait(false);
        }
        var old = saved.Artifact;
        if (old.Id != expected.Id || old.RecordingId != expected.RecordingId || old.CallId != expected.CallId || old.RelativePath != expected.RelativePath)
            throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
        if (old.ByteLength is not null && (old.ByteLength != expected.ByteLength || old.Sha256 != expected.Sha256))
            throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
        if (old.Availability == RecordingAvailability.Available) return;
        if (old.Availability is not (RecordingAvailability.Pending or RecordingAvailability.Missing))
            throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
        if (changed < saved.UpdatedAtUtc) changed = saved.UpdatedAtUtc;
        await artifacts.WriteAsync(new(expected, saved.Version, changed), cancellationToken).ConfigureAwait(false);
    }
}
