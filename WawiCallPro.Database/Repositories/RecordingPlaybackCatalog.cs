using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Repositories;

/// <summary>Returns confirmed local recording candidates for the requested history rows.</summary>
public sealed class RecordingPlaybackCatalog(DatabaseRepositoryContext context)
{
    public Task<IReadOnlyDictionary<Guid, RecordingArtifact>> FindAsync(Guid workstationId, IReadOnlyCollection<Guid> callIds,
        CancellationToken cancellationToken = default)
    {
        var workstation = DatabaseId.Encode(workstationId);
        var ids = callIds.Distinct().Select(DatabaseId.Encode).ToArray();
        if (ids.Length == 0) return Task.FromResult<IReadOnlyDictionary<Guid, RecordingArtifact>>(new Dictionary<Guid, RecordingArtifact>());

        return context.RunAsync<IReadOnlyDictionary<Guid, RecordingArtifact>>(false, async db =>
        {
            var result = new Dictionary<Guid, RecordingArtifact>();
            foreach (var batch in ids.Chunk(200))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rows = await db.RowsAsync(
                    $"SELECT a.*, r.callid FROM {db.T("recordingartifacts")} a JOIN {db.T("recordings")} r ON r.id=a.recordingid " +
                    $"JOIN {db.T("phonecalls")} c ON c.id=r.callid WHERE c.workstationid=@workstation AND {db.In("r.callid", "@ids")} " +
                    "AND c.state IN (@ended,@failed) AND a.availability=@available AND a.format IN (@wave,@mp3) ORDER BY a.format, a.id",
                    new { workstation, ids = batch, ended = (int)CallState.Ended, failed = (int)CallState.Failed,
                        available = (int)RecordingAvailability.Available, wave = (int)RecordingArtifactFormat.Wave,
                        mp3 = (int)RecordingArtifactFormat.Mp3 }).ConfigureAwait(false);
                foreach (var row in rows)
                {
                    var artifact = DapperRecordingArtifactRepository.Read(row).Artifact;
                    result.TryAdd(artifact.CallId, artifact);
                }
            }
            return result;
        }, cancellationToken);
    }
}
