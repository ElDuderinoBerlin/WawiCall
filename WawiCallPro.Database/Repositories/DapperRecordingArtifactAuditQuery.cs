using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperRecordingArtifactAuditQuery(DatabaseRepositoryContext context) : IRecordingArtifactAuditQuery
{
    public Task<Page<StoredRecordingArtifact>> GetPageAsync(Guid workstationId, Guid? afterArtifactId = null,
        int limit = 100, CancellationToken cancellationToken = default)
    {
        var workstation = DatabaseId.Encode(workstationId); var after = afterArtifactId is { } id ? DatabaseId.Encode(id) : "";
        var page = new PageRequest(0, limit);
        return context.RunAsync(false, async db =>
        {
            var select = $"SELECT a.*, r.callid FROM {db.T("recordingartifacts")} a JOIN {db.T("recordings")} r ON r.id=a.recordingid " +
                $"JOIN {db.T("phonecalls")} p ON p.id=r.callid WHERE p.workstationid=@workstation AND p.state IN (@ended,@failed) " +
                "AND a.format IN (@wave,@mp3) AND a.availability IN (@available,@missing) AND a.id>@after";
            var rows = await db.RowsAsync(db.PageSql(select, "a.id ASC", page), new
            {
                workstation, after, ended = (int)CallState.Ended, failed = (int)CallState.Failed, wave = (int)RecordingArtifactFormat.Wave,
                mp3 = (int)RecordingArtifactFormat.Mp3, available = (int)RecordingAvailability.Available, missing = (int)RecordingAvailability.Missing
            }).ConfigureAwait(false);
            return new Page<StoredRecordingArtifact>([.. rows.Take(limit).Select(DapperRecordingArtifactRepository.Read)], rows.Count > limit);
        }, cancellationToken);
    }
}
