using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperRecordingFileCatalogQuery(DatabaseRepositoryContext context) : IRecordingFileCatalogQuery
{
    public Task<Page<RecordingFileReference>> GetPageAsync(Guid workstationId, Guid? afterRecordingId = null,
        int limit = 100, CancellationToken cancellationToken = default)
    {
        var workstation = DatabaseId.Encode(workstationId); var after = afterRecordingId is { } id ? DatabaseId.Encode(id) : "";
        var page = new PageRequest(0, limit);
        return context.RunAsync(false, async db =>
        {
            var select = $"SELECT r.id,r.relativepath FROM {db.T("recordings")} r JOIN {db.T("phonecalls")} p ON p.id=r.callid " +
                "WHERE p.workstationid=@workstation AND r.id>@after";
            var rows = await db.RowsAsync(db.PageSql(select, "r.id ASC", page), new { workstation, after }).ConfigureAwait(false);
            var selected = rows.Take(limit).ToArray();
            if (selected.Length == 0) return new Page<RecordingFileReference>([], false);
            var ids = selected.Select(row => row.Text("id")).ToArray();
            var artifacts = await db.RowsAsync($"SELECT recordingid,relativepath FROM {db.T("recordingartifacts")} WHERE {db.In("recordingid", "@ids")}", new { ids }).ConfigureAwait(false);
            var paths = artifacts.ToLookup(row => row.Text("recordingid"), row => row.Text("relativepath"));
            return new Page<RecordingFileReference>([.. selected.Select(row => new RecordingFileReference(row.Id("id"),
                [.. paths[row.Text("id")].Prepend(row.Text("relativepath")).Distinct(StringComparer.OrdinalIgnoreCase)]))], rows.Count > limit);
        }, cancellationToken);
    }
}
