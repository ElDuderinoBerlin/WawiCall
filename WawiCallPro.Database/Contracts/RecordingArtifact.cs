using WawiCallPro.Interfaces.History;

namespace WawiCallPro.Database.Contracts;

public enum RecordingArtifactFormat { Wave = 1, Mp3 = 2, OggVorbis = 3, Wma = 4 }

public static class RecordingArtifactFormats
{
    public static string Extension(this RecordingArtifactFormat format) => format switch
    {
        RecordingArtifactFormat.Wave => "wav",
        RecordingArtifactFormat.Mp3 => "mp3",
        RecordingArtifactFormat.OggVorbis => "ogg",
        RecordingArtifactFormat.Wma => "wma",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}

/// <summary>One canonical file per recording/format. Fingerprint metadata describes a verified file;
/// constructing or persisting this value never reads or creates that file.</summary>
public sealed record RecordingArtifact
{
    public RecordingArtifact(Guid id, Guid recordingId, Guid callId, RecordingArtifactFormat format,
        string relativePath, RecordingAvailability availability, long? byteLength = null, string? sha256 = null)
    {
        _ = DatabaseId.Encode(recordingId);
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        var extension = format.Extension();
        StorageContract.Recording(new(id, callId, relativePath, extension, null, availability));
        if (!relativePath.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Artifact format and extension must agree.");
        if (availability is RecordingAvailability.Available or RecordingAvailability.Missing)
        {
            if (byteLength is not > 0 || sha256 is null || sha256.Length != 64 || sha256.Any(ch => !(ch is >= '0' and <= '9' or >= 'a' and <= 'f')))
                throw new ArgumentException("Verified artifacts require a positive byte length and canonical SHA-256.");
        }
        else if (byteLength is not null || sha256 is not null) throw new ArgumentException("Unverified artifacts must not claim a fingerprint.");
        Id = id; RecordingId = recordingId; CallId = callId; Format = format; RelativePath = relativePath;
        Availability = availability; ByteLength = byteLength; Sha256 = sha256;
    }
    public Guid Id { get; }
    public Guid RecordingId { get; }
    public Guid CallId { get; }
    public RecordingArtifactFormat Format { get; }
    public string RelativePath { get; }
    public RecordingAvailability Availability { get; }
    public long? ByteLength { get; }
    public string? Sha256 { get; }
    public override string ToString() => nameof(RecordingArtifact);
}

public sealed record RecordingArtifactWrite
{
    public RecordingArtifactWrite(RecordingArtifact artifact, long? expectedVersion, DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(artifact); StorageContract.ExpectedVersion(expectedVersion); StorageContract.Utc(changedAtUtc);
        Artifact = artifact; ExpectedVersion = expectedVersion; ChangedAtUtc = changedAtUtc;
    }
    public RecordingArtifact Artifact { get; }
    public long? ExpectedVersion { get; }
    public DateTimeOffset ChangedAtUtc { get; }
    public override string ToString() => nameof(RecordingArtifactWrite);
}

public sealed record StoredRecordingArtifact(RecordingArtifact Artifact, long Version, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

/// <summary>Metadata only. Identity/path and a confirmed fingerprint are immutable; retries reuse the same artifact ID.</summary>
public interface IRecordingArtifactRepository
{
    Task<StoredRecordingArtifact?> GetAsync(Guid artifactId, CancellationToken cancellationToken = default);
    /// <summary>At most one artifact per supported format; no audio contents are loaded.</summary>
    Task<IReadOnlyList<StoredRecordingArtifact>> GetForRecordingAsync(Guid recordingId, CancellationToken cancellationToken = default);
    Task<StoredRecordingArtifact> WriteAsync(RecordingArtifactWrite write, CancellationToken cancellationToken = default);
}
