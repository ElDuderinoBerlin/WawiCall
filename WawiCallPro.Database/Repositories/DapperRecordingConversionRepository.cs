using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperRecordingConversionRepository(DatabaseRepositoryContext context) : IRecordingConversionRepository
{
    public Task<StoredRecordingConversion?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var id = DatabaseId.Encode(jobId);
        return context.RunAsync(false, async db =>
        {
            var row = await db.OneAsync($"SELECT * FROM {db.T("recordingconversions")} WHERE id=@id", new { id }).ConfigureAwait(false);
            return row is null ? null : Read(row);
        }, cancellationToken);
    }

    public Task<StoredRecordingConversion?> GetForSourceAsync(Guid sourceArtifactId, CancellationToken cancellationToken = default)
    {
        var source = DatabaseId.Encode(sourceArtifactId);
        return context.RunAsync(false, async db =>
        {
            var row = await db.OneAsync($"SELECT * FROM {db.T("recordingconversions")} WHERE sourceartifactid=@source", new { source }).ConfigureAwait(false);
            return row is null ? null : Read(row);
        }, cancellationToken);
    }

    public Task<Page<StoredRecordingConversion>> GetPageAsync(Guid workstationId, Guid? afterJobId = null,
        int limit = 100, CancellationToken cancellationToken = default)
    {
        var workstation = DatabaseId.Encode(workstationId); var after = afterJobId is { } id ? DatabaseId.Encode(id) : "";
        var page = new PageRequest(0, limit);
        return context.RunAsync(false, async db =>
        {
            var select = $"SELECT j.* FROM {db.T("recordingconversions")} j JOIN {db.T("recordingartifacts")} a ON a.id=j.sourceartifactid " +
                $"JOIN {db.T("recordings")} r ON r.id=a.recordingid JOIN {db.T("phonecalls")} p ON p.id=r.callid " +
                "WHERE p.workstationid=@workstation AND j.id>@after";
            var rows = await db.RowsAsync(db.PageSql(select, "j.id ASC", page), new { workstation, after }).ConfigureAwait(false);
            return new Page<StoredRecordingConversion>([.. rows.Take(limit).Select(Read)], rows.Count > limit);
        }, cancellationToken);
    }

    public Task<StoredRecordingConversion> WriteAsync(RecordingConversionWrite write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        return context.RunAsync(true, async db => { return await WriteCoreAsync(write, db); }, cancellationToken);
    }

    internal static async Task<StoredRecordingConversion> WriteCoreAsync(RecordingConversionWrite write, RepositorySession db)
    {
        var item = write.Job; var id = DatabaseId.Encode(item.Id);
        var row = await db.OneAsync($"SELECT * FROM {db.T("recordingconversions")} WHERE id=@id", new { id }).ConfigureAwait(false);
        var previous = row is null ? null : Read(row);
        if (previous is not null && previous.Job == item && previous.UpdatedAtUtc == write.ChangedAtUtc &&
            previous.Version - 1 == (write.ExpectedVersion ?? 0)) return previous;
        RepositorySession.Expected(row, write.ExpectedVersion);
        if (previous is null)
        {
            if (item.State != RecordingConversionState.Pending || item.Attempts != 0) Conflict();
        }
        else
        {
            var old = previous.Job;
            if (old.SourceArtifactId != item.SourceArtifactId || old.TargetArtifactId != item.TargetArtifactId || old.Format != item.Format)
                throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
            var allowed = old.State switch
            {
                RecordingConversionState.Pending => item.State == RecordingConversionState.Running && old.Attempts < int.MaxValue && item.Attempts == old.Attempts + 1,
                RecordingConversionState.Running => (item.State is RecordingConversionState.Completed or RecordingConversionState.Failed) && item.Attempts == old.Attempts && item.AttemptId == old.AttemptId,
                RecordingConversionState.Failed => item.State == RecordingConversionState.Pending && item.Attempts == old.Attempts,
                _ => false
            };
            if (!allowed) Conflict();
            RepositoryData.Chronology(write.ChangedAtUtc, previous.UpdatedAtUtc);
        }
        var source = DatabaseId.Encode(item.SourceArtifactId); var target = DatabaseId.Encode(item.TargetArtifactId);
        var duplicates = await db.RowsAsync($"SELECT id FROM {db.T("recordingconversions")} WHERE sourceartifactid=@source OR targetartifactid=@target", new { source, target }).ConfigureAwait(false);
        if (duplicates.Any(duplicate => duplicate.Id("id") != item.Id)) throw new RepositoryConflictException(RepositoryConflictKind.AlreadyExists);
        var sourceRow = await db.OneAsync($"SELECT a.*, r.callid FROM {db.T("recordingartifacts")} a JOIN {db.T("recordings")} r ON r.id=a.recordingid WHERE a.id=@source", new { source }).ConfigureAwait(false)
                        ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
        var targetRow = await db.OneAsync($"SELECT a.*, r.callid FROM {db.T("recordingartifacts")} a JOIN {db.T("recordings")} r ON r.id=a.recordingid WHERE a.id=@target", new { target }).ConfigureAwait(false)
                        ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
        var wave = DapperRecordingArtifactRepository.Read(sourceRow); var mp3 = DapperRecordingArtifactRepository.Read(targetRow);
        if (wave.Artifact.RecordingId != mp3.Artifact.RecordingId || wave.Artifact.Format != RecordingArtifactFormat.Wave || mp3.Artifact.Format != RecordingArtifactFormat.Mp3)
            throw new RepositoryConflictException(RepositoryConflictKind.OwnershipMismatch);
        var recording = DatabaseId.Encode(wave.Artifact.RecordingId);
        var capture = await db.OneAsync($"SELECT * FROM {db.T("recordingcaptures")} WHERE id=@recording", new { recording }).ConfigureAwait(false)
                      ?? throw new RepositoryConflictException(RepositoryConflictKind.MissingRecord);
        if (capture.Long("state") != (int)PcmRecordingState.Completed || capture.Long("samplerate") != item.Format.SampleRate || capture.Long("channels") != item.Format.Channels) Conflict();
        if (item.State is RecordingConversionState.Pending or RecordingConversionState.Running && wave.Artifact.Availability != RecordingAvailability.Available) Conflict();
        if (item.State == RecordingConversionState.Completed ? mp3.Artifact.Availability != RecordingAvailability.Available :
                mp3.Artifact.Availability != RecordingAvailability.Pending && !(item.State == RecordingConversionState.Failed && mp3.Artifact.Availability == RecordingAvailability.Failed)) Conflict();
        RepositoryData.Chronology(write.ChangedAtUtc, wave.CreatedAtUtc);
        RepositoryData.Chronology(write.ChangedAtUtc, mp3.CreatedAtUtc);
        RepositoryData.Chronology(write.ChangedAtUtc, capture.Utc("updatedatutc"));
        if (item.State == RecordingConversionState.Completed) RepositoryData.Chronology(write.ChangedAtUtc, mp3.UpdatedAtUtc);
        var version = previous is null ? 1 : RepositorySession.Next(previous.Version);
        var created = previous?.CreatedAtUtc ?? write.ChangedAtUtc;
        var values = new Dictionary<string, object?>
        {
            ["sourceartifactid"] = source, ["targetartifactid"] = target,
            ["samplerate"] = item.Format.SampleRate, ["channels"] = item.Format.Channels, ["bitrate"] = item.Format.BitRate,
            ["state"] = (int)item.State, ["attempts"] = item.Attempts, ["attemptid"] = item.AttemptId is { } attempt ? DatabaseId.Encode(attempt) : null,
            ["failure"] = item.Failure is { } failure ? (int)failure : null, ["writeversion"] = version,
            ["createdatutc"] = DatabaseUtc.Encode(created), ["updatedatutc"] = DatabaseUtc.Encode(write.ChangedAtUtc)
        };
        if (previous is null) { values.Add("id", id); await db.InsertAsync("recordingconversions", values).ConfigureAwait(false); }
        else await db.UpdateAsync("recordingconversions", "id", id, write.ExpectedVersion!.Value, values).ConfigureAwait(false);
        return new StoredRecordingConversion(item, version, created, write.ChangedAtUtc);
    }

    internal static StoredRecordingConversion Read(RepositoryRow row) => new(new(row.Id("id"), row.Id("sourceartifactid"), row.Id("targetartifactid"),
        new(checked((int)row.Long("samplerate")), checked((int)row.Long("channels")), checked((int)row.Long("bitrate"))),
        (RecordingConversionState)checked((int)row.Long("state")), checked((int)row.Long("attempts")),
        row.OptionalText("attemptid") is { } attempt ? DatabaseId.Decode(attempt) : null,
        row.OptionalLong("failure") is { } failure ? (RecordingConversionFailure)checked((int)failure) : null),
        row.Long("writeversion"), row.Utc("createdatutc"), row.Utc("updatedatutc"));
    private static void Conflict() => throw new RepositoryConflictException(RepositoryConflictKind.SnapshotMismatch);
}
