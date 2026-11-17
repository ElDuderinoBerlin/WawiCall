using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperInterruptedRecordingConversionQuery(DatabaseRepositoryContext context) : IInterruptedRecordingConversionQuery
{
    public Task<Page<StoredRecordingConversion>> GetPageAsync(Guid workstationId, Guid? afterJobId = null,
        int limit = 100, CancellationToken cancellationToken = default)
    {
        var workstation = DatabaseId.Encode(workstationId); var after = afterJobId is { } id ? DatabaseId.Encode(id) : "";
        var page = new PageRequest(0, limit);
        return context.RunAsync(false, async db =>
        {
            var select = $"SELECT j.* FROM {db.T("recordingconversions")} j JOIN {db.T("recordingartifacts")} a ON a.id=j.sourceartifactid " +
                $"JOIN {db.T("recordings")} r ON r.id=a.recordingid JOIN {db.T("phonecalls")} p ON p.id=r.callid " +
                "WHERE p.workstationid=@workstation AND p.state IN (@ended,@failed) AND j.state=@running AND j.id>@after";
            var rows = await db.RowsAsync(db.PageSql(select, "j.id ASC", page), new
            { workstation, after, ended = (int)CallState.Ended, failed = (int)CallState.Failed, running = (int)RecordingConversionState.Running }).ConfigureAwait(false);
            return new Page<StoredRecordingConversion>([.. rows.Take(limit).Select(DapperRecordingConversionRepository.Read)], rows.Count > limit);
        }, cancellationToken);
    }
}
