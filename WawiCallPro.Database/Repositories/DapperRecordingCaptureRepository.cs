using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperRecordingCaptureRepository(DatabaseRepositoryContext context) : IRecordingCaptureRepository
{
    private static string Select(RepositorySession db) => $"SELECT c.*, r.callid FROM {db.T("recordingcaptures")} c JOIN {db.T("recordings")} r ON c.id=r.id";
    public Task<StoredRecordingCapture?> GetAsync(Guid recordingId, CancellationToken cancellationToken = default)
    {
        var id = DatabaseId.Encode(recordingId);
        return context.RunAsync(false, async db =>
        { var row = await db.OneAsync(Select(db) + " WHERE c.id=@id", new { id }).ConfigureAwait(false); return row is null ? null : Read(row); }, cancellationToken);
    }

    public Task<StoredRecordingCapture> WriteAsync(RecordingCaptureWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        return context.RunAsync(true, async db => { return await WriteCoreAsync(write, db); }, cancellationToken);
    }

    internal static async Task<StoredRecordingCapture> WriteCoreAsync(RecordingCaptureWrite write, RepositorySession db)
    {
        var capture = write.Capture; var id = DatabaseId.Encode(capture.RecordingId);
        var parent = await db.OneAsync($"SELECT * FROM {db.T("recordings")} WHERE id=@id", new { id }).ConfigureAwait(false)
                     ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
        if (parent.Id("callid") != capture.CallId) throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
        var row = await db.OneAsync(Select(db) + " WHERE c.id=@id", new { id }).ConfigureAwait(false);
        var previous = row is null ? null : Read(row);
        if (previous is not null && previous.Capture == capture && previous.UpdatedAtUtc == write.ChangedAtUtc
            && previous.Version - 1 == (write.ExpectedVersion ?? 0)) return previous;
        RepositorySession.Expected(row, write.ExpectedVersion);
        if (previous is null)
        {
            if (capture.State != PcmRecordingState.Capturing) Conflict();
        }
        else
        {
            var old = previous.Capture;
            if (old.StartedAtUtc != capture.StartedAtUtc || old.SampleRate != capture.SampleRate || old.Channels != capture.Channels
                || old.State is PcmRecordingState.Completed or PcmRecordingState.Failed or PcmRecordingState.Aborted
                || old.State == PcmRecordingState.Stopping && capture.State == PcmRecordingState.Capturing) Conflict();
            RepositoryData.Chronology(write.ChangedAtUtc, previous.UpdatedAtUtc);
        }
        var version = previous is null ? 1 : RepositorySession.Next(previous.Version);
        var values = Values(capture, version, write.ChangedAtUtc);
        if (previous is null) { values.Add("id", id); await db.InsertAsync("recordingcaptures", values).ConfigureAwait(false); }
        else await db.UpdateAsync("recordingcaptures", "id", id, write.ExpectedVersion!.Value, values).ConfigureAwait(false);
        return new StoredRecordingCapture(capture, version, write.ChangedAtUtc);
    }

    public Task<int> RecoverEndedCallAsync(Guid callId, DateTimeOffset recoveredAtUtc, CancellationToken cancellationToken = default)
    {
        var call = DatabaseId.Encode(callId); StorageContract.Utc(recoveredAtUtc);
        return context.RunAsync(true, async db =>
        {
            var parent = await db.OneAsync($"SELECT * FROM {db.T("phonecalls")} WHERE id=@call", new { call }).ConfigureAwait(false)
                ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
            if ((CallState)parent.Long("state") is not (CallState.Ended or CallState.Failed)) Conflict();
            RepositoryData.Chronology(recoveredAtUtc, parent.Utc("statechangedatutc"));
            var rows = await db.RowsAsync(Select(db) + " WHERE r.callid=@call AND c.state IN (0,1) ORDER BY c.id", new { call }).ConfigureAwait(false);
            foreach (var row in rows)
            {
                var old = Read(row); RepositoryData.Chronology(recoveredAtUtc, old.UpdatedAtUtc);
                var item = old.Capture;
                var interrupted = new RecordingCapture(item.RecordingId, item.CallId, item.StartedAtUtc, null,
                    PcmRecordingState.Aborted, item.SampleRate, item.Channels, failure: RecordingCaptureFailure.Interrupted);
                await db.UpdateAsync("recordingcaptures", "id", DatabaseId.Encode(item.RecordingId), old.Version,
                    Values(interrupted, RepositorySession.Next(old.Version), recoveredAtUtc)).ConfigureAwait(false);
            }
            return rows.Count;
        }, cancellationToken);
    }

    private static Dictionary<string, object?> Values(RecordingCapture capture, long version, DateTimeOffset changed) => new()
    {
        ["startedatutc"] = DatabaseUtc.Encode(capture.StartedAtUtc), ["endedatutc"] = capture.EndedAtUtc is { } end ? DatabaseUtc.Encode(end) : null,
        ["state"] = (int)capture.State, ["samplerate"] = capture.SampleRate, ["channels"] = capture.Channels,
        ["framecount"] = capture.FrameCount, ["failure"] = capture.Failure is { } failure ? (int)failure : null,
        ["writeversion"] = version, ["updatedatutc"] = DatabaseUtc.Encode(changed)
    };
    internal static StoredRecordingCapture Read(RepositoryRow row) => new(new(row.Id("id"), row.Id("callid"), row.Utc("startedatutc"), row.OptionalUtc("endedatutc"),
        (PcmRecordingState)checked((int)row.Long("state")), checked((int)row.Long("samplerate")), checked((int)row.Long("channels")), row.OptionalLong("framecount"),
        row.OptionalLong("failure") is { } failure ? (RecordingCaptureFailure)checked((int)failure) : null), row.Long("writeversion"), row.Utc("updatedatutc"));
    private static void Conflict() => throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
}
