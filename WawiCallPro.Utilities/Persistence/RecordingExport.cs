using System.Security.Cryptography;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

public interface IRecordingExport
{
    /// <summary>Copies one confirmed local original to a new absolute destination outside application storage. Never overwrites.</summary>
    Task ExportAsync(Guid artifactId, string destination, CancellationToken cancellationToken = default);
}

/// <summary>Explicit export under the recording lease and source read lock. No metadata changes or transcoding.</summary>
public sealed class RecordingExport(IRecordingArtifactRepository artifacts, ICallRepository calls, IWorkstationIdentity workstation,
    IRecordingJournalOutbox pending, IRecordingConversionOutbox conversions, IRecordingConversionRepository jobs,
    IRecordingArtifactFileInspector inspector, ApplicationPaths paths) : IRecordingExport
{
    public async Task ExportAsync(Guid artifactId, string destination, CancellationToken cancellationToken = default)
    {
        _ = DatabaseId.Encode(artifactId); cancellationToken.ThrowIfCancellationRequested();
        var saved = await artifacts.GetAsync(artifactId, cancellationToken).ConfigureAwait(false) ?? throw new RecordingOutboxException();
        var artifact = saved.Artifact;
        var target = ValidateDestination(destination, artifact.Format);
        await using var lease = pending.AcquireInspection(artifact.RecordingId);
        if (lease.HasPending) throw new RecordingOutboxException();
        await CheckOwnerAsync(artifact, cancellationToken).ConfigureAwait(false);
        if (artifact.Availability != RecordingAvailability.Available) throw new RecordingOutboxException();
        await CheckCompletionAsync(artifact, cancellationToken).ConfigureAwait(false);
        await using var observed = await inspector.InspectAsync(artifact, cancellationToken).ConfigureAwait(false);
        if (observed.State != RecordingArtifactFileState.Matches) throw new RecordingOutboxException();
        await using var source = new FileStream(paths.ResolveRecordingPath(artifact.RelativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        var temporary = Path.Combine(Path.GetDirectoryName(target)!, ".wcp-export-" + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536, true))
            {
                created = true;
                await source.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false); output.Flush(true);
                if (output.Length != artifact.ByteLength) throw new RecordingOutboxException();
                output.Position = 0;
                var digest = Convert.ToHexString(await SHA256.HashDataAsync(output, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
                if (digest != artifact.Sha256) throw new RecordingOutboxException();
            }
            if (await artifacts.GetAsync(artifactId, cancellationToken).ConfigureAwait(false) != saved) throw new RecordingOutboxException();
            await CheckOwnerAsync(artifact, cancellationToken).ConfigureAwait(false);
            await CheckCompletionAsync(artifact, cancellationToken).ConfigureAwait(false);
            if (lease.HasPending) throw new RecordingOutboxException();
            _ = ValidateDestination(target, artifact.Format);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, target, false); created = false;
        }
        finally { if (created) File.Delete(temporary); }
    }

    private async Task CheckOwnerAsync(RecordingArtifact artifact, CancellationToken token)
    {
        var local = await workstation.GetAsync(token).ConfigureAwait(false);
        var call = await calls.GetAsync(artifact.CallId, token).ConfigureAwait(false);
        if (local is null || call?.History.Call.Origin?.WorkstationId != local || call.History.Call.State is not (CallState.Ended or CallState.Failed))
            throw new RecordingOutboxException();
    }
    private async Task CheckCompletionAsync(RecordingArtifact artifact, CancellationToken token)
    {
        if (artifact.Format == RecordingArtifactFormat.Wave) return;
        foreach (var id in conversions.GetPending())
        {
            var job = await jobs.GetAsync(id, token).ConfigureAwait(false);
            if (job is null || job.Job.TargetArtifactId == artifact.Id) throw new RecordingOutboxException();
        }
    }
    private string ValidateDestination(string destination, RecordingArtifactFormat format)
    {
        if (!Path.IsPathFullyQualified(destination)) throw new RecordingOutboxException();
        var target = Path.GetFullPath(destination);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.RootDirectory));
        if (target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || target.Equals(root, StringComparison.OrdinalIgnoreCase))
            throw new RecordingOutboxException();
        var extension = "." + format.Extension();
        var name = Path.GetFileName(target);
        if (name.Contains(':') || name.EndsWith(' ') || name.EndsWith('.') || !Path.GetExtension(target).Equals(extension, StringComparison.OrdinalIgnoreCase))
            throw new RecordingOutboxException();
        try { _ = File.GetAttributes(target); throw new RecordingOutboxException(); }
        catch (FileNotFoundException) { }
        for (var directory = Path.GetDirectoryName(target); directory is not null; directory = Path.GetDirectoryName(directory))
        {
            var flags = File.GetAttributes(directory);
            if ((flags & FileAttributes.ReparsePoint) != 0 || (flags & FileAttributes.Directory) == 0) throw new RecordingOutboxException();
        }
        return target;
    }
}
