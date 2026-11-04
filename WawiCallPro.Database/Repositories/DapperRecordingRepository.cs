using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperRecordingRepository(DatabaseRepositoryContext context) : IRecordingRepository
{
    public Task<StoredRecording?> GetAsync(Guid recordingId, CancellationToken cancellationToken = default)
    {
        var id = DatabaseId.Encode(recordingId);
        return context.RunAsync(false, async db =>
        {
            var row = await db.OneAsync($"SELECT * FROM {db.T("recordings")} WHERE id=@id", new { id }).ConfigureAwait(false);
            return row is null ? null : Read(row);
        }, cancellationToken);
    }

    public Task<StoredRecording> WriteAsync(RecordingWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        return context.RunAsync(true, async db =>
        {
            var item = write.Recording; var id = DatabaseId.Encode(item.Id); var call = DatabaseId.Encode(item.CallId);
            var row = await db.OneAsync($"SELECT * FROM {db.T("recordings")} WHERE id=@id", new { id }).ConfigureAwait(false);
            RepositorySession.Expected(row, write.ExpectedVersion);
            await DapperCallNoteRepository.RequireCallAsync(db, call).ConfigureAwait(false);
            if (row is not null && row.Text("callid") != call) throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
            if (row is not null) RepositoryData.Chronology(write.ChangedAtUtc, row.Utc("updatedatutc"));
            var created = row?.Utc("createdatutc") ?? write.ChangedAtUtc;
            var version = row is null ? 1 : RepositorySession.Next(row.Long("writeversion"));
            var values = new Dictionary<string, object?>
            {
                ["relativepath"] = item.RelativePath, ["format"] = item.Format, ["durationticks"] = item.Duration?.Ticks,
                ["availability"] = (int)item.Availability, ["writeversion"] = version, ["updatedatutc"] = DatabaseUtc.Encode(write.ChangedAtUtc)
            };
            if (row is null)
            {
                values.Add("id", id); values.Add("callid", call); values.Add("createdatutc", DatabaseUtc.Encode(created));
                await db.InsertAsync("recordings", values).ConfigureAwait(false);
            }
            else await db.UpdateAsync("recordings", "id", id, write.ExpectedVersion!.Value, values).ConfigureAwait(false);
            return new StoredRecording(item, version, created, write.ChangedAtUtc);
        }, cancellationToken);
    }

    internal static StoredRecording Read(RepositoryRow row)
    {
        var item = new Recording(row.Id("id"), row.Id("callid"), row.Text("relativepath"), row.Text("format"),
            row.OptionalLong("durationticks") is { } ticks ? TimeSpan.FromTicks(ticks) : null, (RecordingAvailability)checked((int)row.Long("availability")));
        StorageContract.Recording(item);
        return new(item, row.Long("writeversion"), row.Utc("createdatutc"), row.Utc("updatedatutc"));
    }
}
