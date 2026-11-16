using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperInterruptedRecordingQuery(DatabaseRepositoryContext context) : IInterruptedRecordingQuery
{
    public Task<Page<InterruptedRecordingCandidate>> GetPageAsync(Guid workstationId, Guid? afterRecordingId = null,
        int limit = 100, CancellationToken cancellationToken = default)
    {
        var workstation = DatabaseId.Encode(workstationId);
        var after = afterRecordingId is { } id ? DatabaseId.Encode(id) : "";
        var page = new PageRequest(0, limit);
        return context.RunAsync(false, async db =>
        {
            var select = $"SELECT r.*, p.statechangedatutc AS callchangedatutc FROM {db.T("recordings")} r " +
                $"JOIN {db.T("recordingcaptures")} c ON c.id=r.id JOIN {db.T("phonecalls")} p ON p.id=r.callid " +
                "WHERE p.workstationid=@workstation AND p.state IN (@ended,@failed) AND r.id>@after " +
                "AND r.format=@format AND r.availability=@pending AND r.durationticks IS NULL AND c.state IN (@capturing,@stopping)";
            var rows = await db.RowsAsync(db.PageSql(select, "r.id ASC", page), new
            {
                workstation, after, ended = (int)CallState.Ended, failed = (int)CallState.Failed,
                format = "wav", pending = (int)RecordingAvailability.Pending,
                capturing = (int)PcmRecordingState.Capturing, stopping = (int)PcmRecordingState.Stopping
            }).ConfigureAwait(false);
            var result = new List<InterruptedRecordingCandidate>();
            foreach (var row in rows.Take(limit))
            {
                var capture = await db.OneAsync($"SELECT c.*, r.callid FROM {db.T("recordingcaptures")} c " +
                    $"JOIN {db.T("recordings")} r ON r.id=c.id WHERE c.id=@id", new { id = row.Text("id") }).ConfigureAwait(false)
                    ?? throw new DatabaseOperationException(DatabaseErrorCode.InvalidSchema);
                result.Add(new(new(DapperRecordingRepository.Read(row), DapperRecordingCaptureRepository.Read(capture)), row.Utc("callchangedatutc")));
            }
            return new Page<InterruptedRecordingCandidate>([.. result], rows.Count > limit);
        }, cancellationToken);
    }
}
