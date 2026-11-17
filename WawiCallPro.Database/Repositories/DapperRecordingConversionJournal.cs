using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.Database.Repositories;

public sealed class DapperRecordingConversionJournal(DatabaseRepositoryContext context) : IRecordingConversionJournal
{
    public Task<StoredRecordingConversionJournal> BeginAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken cancellationToken = default) =>
        SaveAsync(target, conversion, true, cancellationToken);
    public Task<StoredRecordingConversionJournal> WriteAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken cancellationToken = default) =>
        SaveAsync(target, conversion, false, cancellationToken);

    private Task<StoredRecordingConversionJournal> SaveAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, bool begin, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(conversion);
        var artifact = target.Artifact; var job = conversion.Job;
        var availability = job.State switch
        {
            RecordingConversionState.Pending => RecordingAvailability.Pending,
            RecordingConversionState.Completed => RecordingAvailability.Available,
            RecordingConversionState.Failed => RecordingAvailability.Failed,
            _ => throw new ArgumentException("Ein laufender Versuch wird separat über die Auftragsversion gestartet.")
        };
        if (artifact.Format != RecordingArtifactFormat.Mp3 || artifact.Id != job.TargetArtifactId || artifact.Availability != availability ||
            target.ChangedAtUtc != conversion.ChangedAtUtc ||
            begin && (target.ExpectedVersion is not null || conversion.ExpectedVersion is not null || job.State != RecordingConversionState.Pending || job.Attempts != 0) ||
            !begin && (target.ExpectedVersion is null || conversion.ExpectedVersion is null))
            throw new ArgumentException("MP3-Artefakt und Auftrag müssen denselben Vorgang und Status beschreiben.");
        return context.RunAsync(true, async db =>
        {
            var artifactId = DatabaseId.Encode(artifact.Id); var jobId = DatabaseId.Encode(job.Id);
            var targetRow = await db.OneAsync($"SELECT a.*, r.callid FROM {db.T("recordingartifacts")} a JOIN {db.T("recordings")} r ON r.id=a.recordingid WHERE a.id=@artifactId", new { artifactId }).ConfigureAwait(false);
            var jobRow = await db.OneAsync($"SELECT * FROM {db.T("recordingconversions")} WHERE id=@jobId", new { jobId }).ConfigureAwait(false);
            if (targetRow is not null && jobRow is not null)
            {
                var oldTarget = DapperRecordingArtifactRepository.Read(targetRow); var oldJob = DapperRecordingConversionRepository.Read(jobRow);
                if (oldTarget.Artifact == artifact && oldJob.Job == job && oldTarget.UpdatedAtUtc == target.ChangedAtUtc && oldJob.UpdatedAtUtc == conversion.ChangedAtUtc &&
                    oldTarget.Version - 1 == (target.ExpectedVersion ?? 0) && oldJob.Version - 1 == (conversion.ExpectedVersion ?? 0))
                    return new StoredRecordingConversionJournal(oldTarget, oldJob);
            }
            // Reject a single-row replay: neither a legacy target nor an independent write proves the paired operation.
            RepositorySession.Expected(targetRow, target.ExpectedVersion);
            RepositorySession.Expected(jobRow, conversion.ExpectedVersion);
            var savedTarget = await DapperRecordingArtifactRepository.WriteCoreAsync(target, db).ConfigureAwait(false);
            var savedJob = await DapperRecordingConversionRepository.WriteCoreAsync(conversion, db).ConfigureAwait(false);
            return new StoredRecordingConversionJournal(savedTarget, savedJob);
        }, token);
    }
}
