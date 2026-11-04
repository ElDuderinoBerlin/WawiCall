using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperCallNoteRepository(DatabaseRepositoryContext context) : ICallNoteRepository
{
    public Task<StoredCallNote?> GetAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var id = DatabaseId.Encode(noteId);
        return context.RunAsync(false, async db =>
        {
            var row = await db.OneAsync($"SELECT * FROM {db.T("callnotes")} WHERE id=@id", new { id }).ConfigureAwait(false);
            return row is null ? null : Read(row);
        }, cancellationToken);
    }

    public Task<StoredCallNote> WriteAsync(CallNoteWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        return context.RunAsync(true, async db =>
        {
            var id = DatabaseId.Encode(write.NoteId); var call = DatabaseId.Encode(write.CallId);
            var row = await db.OneAsync($"SELECT * FROM {db.T("callnotes")} WHERE id=@id", new { id }).ConfigureAwait(false);
            RepositorySession.Expected(row, write.ExpectedVersion);
            await RequireCallAsync(db, call).ConfigureAwait(false);
            if (row is not null && row.Text("callid") != call) throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
            if (row is not null) RepositoryData.Chronology(write.ChangedAtUtc, row.Utc("updatedatutc"));
            var created = row?.Utc("createdatutc") ?? write.ChangedAtUtc;
            var version = row is null ? 1 : RepositorySession.Next(row.Long("writeversion"));
            var values = new Dictionary<string, object?> { ["text"] = write.Text, ["writeversion"] = version, ["updatedatutc"] = DatabaseUtc.Encode(write.ChangedAtUtc) };
            if (row is null)
            {
                values.Add("id", id); values.Add("callid", call); values.Add("createdatutc", DatabaseUtc.Encode(created));
                await db.InsertAsync("callnotes", values).ConfigureAwait(false);
            }
            else await db.UpdateAsync("callnotes", "id", id, write.ExpectedVersion!.Value, values).ConfigureAwait(false);
            return new StoredCallNote(new(write.NoteId, write.CallId, write.Text, created, write.ChangedAtUtc), version);
        }, cancellationToken);
    }

    internal static StoredCallNote Read(RepositoryRow row) => new(new(row.Id("id"), row.Id("callid"), row.Text("text"), row.Utc("createdatutc"), row.Utc("updatedatutc")), row.Long("writeversion"));
    internal static async Task RequireCallAsync(RepositorySession db, string call)
    {
        if (await db.ScalarAsync<long>($"SELECT COUNT(*) FROM {db.T("phonecalls")} WHERE id=@call", new { call }).ConfigureAwait(false) != 1)
            throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
    }
}
