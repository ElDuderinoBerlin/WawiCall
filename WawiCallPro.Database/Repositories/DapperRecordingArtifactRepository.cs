using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperRecordingArtifactRepository(DatabaseRepositoryContext context) : IRecordingArtifactRepository
{
    private static string Select(RepositorySession db) => $"SELECT a.*, r.callid FROM {db.T("recordingartifacts")} a JOIN {db.T("recordings")} r ON r.id=a.recordingid";
    public Task<StoredRecordingArtifact?> GetAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        var id = DatabaseId.Encode(artifactId);
        return context.RunAsync(false, async db =>
        { var row = await db.OneAsync(Select(db) + " WHERE a.id=@id", new { id }).ConfigureAwait(false); return row is null ? null : Read(row); }, cancellationToken);
    }
    public Task<IReadOnlyList<StoredRecordingArtifact>> GetForRecordingAsync(Guid recordingId, CancellationToken cancellationToken = default)
    {
        var recording = DatabaseId.Encode(recordingId);
        return context.RunAsync<IReadOnlyList<StoredRecordingArtifact>>(false, async db =>
            (await db.RowsAsync(Select(db) + " WHERE a.recordingid=@recording ORDER BY a.format", new { recording }).ConfigureAwait(false)).Select(Read).ToArray(), cancellationToken);
    }
    public Task<StoredRecordingArtifact> WriteAsync(RecordingArtifactWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        return context.RunAsync(true, async db =>
        {
            var item = write.Artifact; var id = DatabaseId.Encode(item.Id); var recording = DatabaseId.Encode(item.RecordingId);
            var parent = await db.OneAsync($"SELECT * FROM {db.T("recordings")} WHERE id=@recording", new { recording }).ConfigureAwait(false)
                ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
            if (parent.Id("callid") != item.CallId) throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
            var row = await db.OneAsync(Select(db) + " WHERE a.id=@id", new { id }).ConfigureAwait(false);
            var previous = row is null ? null : Read(row);
            if (previous is not null && previous.Artifact == item && previous.UpdatedAtUtc == write.ChangedAtUtc
                && previous.Version - 1 == (write.ExpectedVersion ?? 0)) return previous;
            RepositorySession.Expected(row, write.ExpectedVersion);
            if (previous is not null && (previous.Artifact.RecordingId != item.RecordingId || previous.Artifact.Format != item.Format || previous.Artifact.RelativePath != item.RelativePath))
                throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
            var duplicate = await db.OneAsync($"SELECT id FROM {db.T("recordingartifacts")} WHERE recordingid=@recording AND format=@format", new { recording, format = (int)item.Format }).ConfigureAwait(false);
            if (duplicate is not null && duplicate.Text("id") != id) throw new RepositoryConflictException(RepositoryConflictKind.AlreadyExists);
            if (previous is null)
            {
                if (item.Availability != RecordingAvailability.Pending) Conflict();
            }
            else
            {
                var old = previous.Artifact;
                if (old.ByteLength is not null && (old.ByteLength != item.ByteLength || old.Sha256 != item.Sha256)) Conflict();
                var allowed = old.Availability switch
                {
                    RecordingAvailability.Pending => item.Availability is RecordingAvailability.Pending or RecordingAvailability.Available or RecordingAvailability.Failed,
                    RecordingAvailability.Failed => item.Availability is RecordingAvailability.Failed or RecordingAvailability.Pending,
                    _ => item.Availability is RecordingAvailability.Available or RecordingAvailability.Missing
                };
                if (!allowed) Conflict();
                RepositoryData.Chronology(write.ChangedAtUtc, previous.UpdatedAtUtc);
            }
            var version = previous is null ? 1 : RepositorySession.Next(previous.Version);
            var created = previous?.CreatedAtUtc ?? write.ChangedAtUtc;
            var values = new Dictionary<string, object?>
            {
                ["recordingid"] = recording, ["format"] = (int)item.Format, ["relativepath"] = item.RelativePath,
                ["availability"] = (int)item.Availability, ["bytelength"] = item.ByteLength, ["sha256"] = item.Sha256,
                ["writeversion"] = version, ["createdatutc"] = DatabaseUtc.Encode(created), ["updatedatutc"] = DatabaseUtc.Encode(write.ChangedAtUtc)
            };
            if (previous is null) { values.Add("id", id); await db.InsertAsync("recordingartifacts", values).ConfigureAwait(false); }
            else await db.UpdateAsync("recordingartifacts", "id", id, write.ExpectedVersion!.Value, values).ConfigureAwait(false);
            return new StoredRecordingArtifact(item, version, created, write.ChangedAtUtc);
        }, cancellationToken);
    }
    private static StoredRecordingArtifact Read(RepositoryRow row) => new(new(row.Id("id"), row.Id("recordingid"), row.Id("callid"),
        (RecordingArtifactFormat)checked((int)row.Long("format")), row.Text("relativepath"), (RecordingAvailability)checked((int)row.Long("availability")),
        row.OptionalLong("bytelength"), row.OptionalText("sha256")), row.Long("writeversion"), row.Utc("createdatutc"), row.Utc("updatedatutc"));
    private static void Conflict() => throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
}
