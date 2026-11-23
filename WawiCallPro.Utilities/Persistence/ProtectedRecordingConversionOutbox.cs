using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>DPAPI CurrentUser terminal MP3 pairs bound to the exact startup database and product line.</summary>
public sealed class ProtectedRecordingConversionOutbox(ApplicationPaths paths, ISecretProtector secrets,
    ApplicationConfigurationService configuration, ApplicationDescriptor application, IRecordingFileProbe probe) : IRecordingConversionOutbox
{
    private const string Purpose = "recording-conversion-v1";
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    private string Root => Path.Combine(paths.RootDirectory, "mp3-pending");
    public IReadOnlyList<Guid> GetPending()
    {
        try
        {
            try { CheckLink(Root); } catch (FileNotFoundException) { return []; } catch (DirectoryNotFoundException) { return []; }
            var files = Directory.EnumerateFiles(Root, "*.pending").Take(4097).Order(StringComparer.Ordinal).ToArray();
            if (files.Length > 4096) throw new RecordingConversionOutboxException();
            return files.Select(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (!Guid.TryParseExact(name, "N", out var id) || id == Guid.Empty || name != id.ToString("N")) throw new RecordingConversionOutboxException();
                return id;
            }).ToArray();
        }
        catch (Exception) { throw new RecordingConversionOutboxException(); }
    }
    public bool HasPending(Guid jobId)
    {
        _ = DatabaseId.Encode(jobId);
        try { CheckLink(Root); CheckLink(EntryPath(jobId)); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (Exception) { throw new RecordingConversionOutboxException(); }
    }
    public void StagePrepared(RecordingArtifactWrite target, RecordingConversionWrite conversion, string temporaryRelativePath,
        FileStream original, CancellationToken cancellationToken = default)
    {
        try
        {
            Validate(target, conversion);
            if (conversion.Job.State != RecordingConversionState.Completed) throw new RecordingConversionOutboxException();
            var entry = new Entry(2, Binding(), target, conversion, temporaryRelativePath);
            var temporary = PreparedPath(entry);
            if (!string.Equals(Path.GetFullPath(original.Name), temporary, StringComparison.OrdinalIgnoreCase) ||
                probe.IsAbsent(temporaryRelativePath) || !probe.IsAbsent(target.Artifact.RelativePath) ||
                original.Length is < 1 or > int.MaxValue || original.Length != target.Artifact.ByteLength)
                throw new RecordingConversionOutboxException();
            var position = original.Position;
            try
            {
                cancellationToken.ThrowIfCancellationRequested(); original.Flush(true); original.Position = 0;
                var digest = Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant();
                if (digest != target.Artifact.Sha256) throw new RecordingConversionOutboxException();
            }
            finally { original.Position = position; }
            Directory.CreateDirectory(Root); CheckLink(Root);
            using var lease = Lease(conversion.Job.Id);
            if (HasPending(conversion.Job.Id) || GetPending().Count >= 4096) throw new RecordingConversionOutboxException();
            var encrypted = secrets.Protect(JsonSerializer.Serialize(entry, Json), Purpose);
            var receipt = Path.Combine(Root, "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var file = new FileStream(receipt, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(file, encrypted, Json); file.Flush(true);
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(receipt, EntryPath(conversion.Job.Id), false);
            }
            finally { if (File.Exists(receipt)) File.Delete(receipt); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { throw new RecordingConversionOutboxException(); }
    }
    public async Task<IRecordingConversionDelivery> StageAsync(RecordingArtifactWrite target, RecordingConversionWrite conversion, CancellationToken cancellationToken = default)
    {
        Validate(target, conversion); FileStream? lease = null; FileStream? output = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); Directory.CreateDirectory(Root); CheckLink(Root);
            var id = conversion.Job.Id; lease = Lease(id); Entry entry;
            if (HasPending(id))
            {
                entry = await ReadAsync(id, cancellationToken).ConfigureAwait(false);
                if (entry.Target != target || entry.Conversion != conversion) throw new RecordingConversionOutboxException();
            }
            else
            {
                if (GetPending().Count >= 4096) throw new RecordingConversionOutboxException();
                entry = new(1, Binding(), target, conversion);
            }
            if (entry.PreparedRelativePath is not null && !probe.IsAbsent(entry.PreparedRelativePath)) throw new RecordingConversionOutboxException();
            output = await VerifyAsync(entry, cancellationToken).ConfigureAwait(false);
            if (!HasPending(id)) await PublishAsync(EntryPath(id), entry, cancellationToken).ConfigureAwait(false);
            return new Delivery(EntryPath(id), entry, lease, output);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { output?.Dispose(); lease?.Dispose(); throw; }
        catch (Exception) { output?.Dispose(); lease?.Dispose(); throw new RecordingConversionOutboxException(); }
    }
    public IAsyncDisposable AcquireEmpty(Guid jobId)
    {
        _ = DatabaseId.Encode(jobId); FileStream? lease = null;
        try
        {
            Directory.CreateDirectory(Root); CheckLink(Root); lease = Lease(jobId);
            if (HasPending(jobId) || GetPending().Count >= 4096) throw new RecordingConversionOutboxException();
            return lease;
        }
        catch (Exception) { lease?.Dispose(); throw new RecordingConversionOutboxException(); }
    }
    public async Task<IRecordingConversionDelivery> OpenAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _ = DatabaseId.Encode(jobId); FileStream? lease = null; FileStream? output = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); CheckLink(Root); lease = Lease(jobId);
            var entry = await ReadAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (entry.PreparedRelativePath is not null && !probe.IsAbsent(entry.PreparedRelativePath)) throw new RecordingConversionOutboxException();
            output = await VerifyAsync(entry, cancellationToken).ConfigureAwait(false);
            return new Delivery(EntryPath(jobId), entry, lease, output);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { output?.Dispose(); lease?.Dispose(); throw; }
        catch (Exception) { output?.Dispose(); lease?.Dispose(); throw new RecordingConversionOutboxException(); }
    }
    public async Task<IRecordingConversionDelivery> ResumeAsync(StoredRecordingArtifact target, StoredRecordingConversion conversion, CancellationToken cancellationToken = default)
    {
        var jobId = conversion.Job.Id; FileStream? lease = null; FileStream? output = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); CheckLink(Root); lease = Lease(jobId);
            var entry = await ReadAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (entry.Target.Artifact.Id != target.Artifact.Id || entry.Target.Artifact.RecordingId != target.Artifact.RecordingId ||
                entry.Target.Artifact.CallId != target.Artifact.CallId || entry.Target.Artifact.RelativePath != target.Artifact.RelativePath ||
                entry.Conversion.Job.SourceArtifactId != conversion.Job.SourceArtifactId || entry.Conversion.Job.TargetArtifactId != conversion.Job.TargetArtifactId ||
                entry.Conversion.Job.Format != conversion.Job.Format || entry.Conversion.Job.AttemptId != conversion.Job.AttemptId || entry.Conversion.Job.Attempts != conversion.Job.Attempts)
                throw new RecordingConversionOutboxException();
            if (entry.PreparedRelativePath is not null && !probe.IsAbsent(entry.PreparedRelativePath))
            {
                if (target.Artifact.Availability != RecordingAvailability.Pending || conversion.Job.State != RecordingConversionState.Running ||
                    target.Version != entry.Target.ExpectedVersion || conversion.Version != entry.Conversion.ExpectedVersion)
                    throw new RecordingConversionOutboxException();
                if (!probe.IsAbsent(entry.Target.Artifact.RelativePath)) throw new RecordingConversionOutboxException();
                // Only the protected original may be promoted; never overwrite an existing destination.
                await using (var prepared = new FileStream(PreparedPath(entry), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true))
                {
                    if (probe.IsAbsent(entry.PreparedRelativePath) || prepared.Length is < 1 or > int.MaxValue || prepared.Length != entry.Target.Artifact.ByteLength ||
                        Convert.ToHexString(await SHA256.HashDataAsync(prepared, cancellationToken).ConfigureAwait(false)).ToLowerInvariant() != entry.Target.Artifact.Sha256)
                        throw new RecordingConversionOutboxException();
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(PreparedPath(entry), paths.ResolveRecordingPath(entry.Target.Artifact.RelativePath), false);
            }
            output = await VerifyAsync(entry, cancellationToken).ConfigureAwait(false);
            return new Delivery(EntryPath(jobId), entry, lease, output);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { output?.Dispose(); lease?.Dispose(); throw; }
        catch (Exception) { output?.Dispose(); lease?.Dispose(); throw new RecordingConversionOutboxException(); }
    }
    private async Task<Entry> ReadAsync(Guid id, CancellationToken token)
    {
        var path = EntryPath(id); CheckLink(path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        if (file.Length is < 1 or > 65536) throw new RecordingConversionOutboxException();
        var encrypted = await JsonSerializer.DeserializeAsync<ProtectedSecret>(file, Json, token).ConfigureAwait(false) ?? throw new RecordingConversionOutboxException();
        using var plain = secrets.Unprotect(encrypted, Purpose);
        var entry = JsonSerializer.Deserialize<Entry>(plain.Reveal(), Json) ?? throw new RecordingConversionOutboxException();
        Validate(entry.Target, entry.Conversion);
        if (entry.Binding != Binding() || entry.Conversion.Job.Id != id) throw new RecordingConversionOutboxException();
        if (entry.Version == 2 && entry.PreparedRelativePath is not null && entry.Conversion.Job.State == RecordingConversionState.Completed)
            _ = PreparedPath(entry);
        else if (entry.Version != 1 || entry.PreparedRelativePath is not null) throw new RecordingConversionOutboxException();
        return entry;
    }
    private string PreparedPath(Entry entry)
    {
        var relative = entry.PreparedRelativePath;
        if (relative is null || relative.Length > 1024 || relative.Contains('\\') || relative.Contains(':') || relative.Any(char.IsControl) ||
            relative.Split('/').Any(part => part is "" or "." or "..") ||
            Path.GetDirectoryName(relative) != Path.GetDirectoryName(entry.Target.Artifact.RelativePath)) throw new RecordingConversionOutboxException();
        var name = Path.GetFileName(relative);
        if (name.Length != 46 || !name.StartsWith(".wawi-mp3-", StringComparison.Ordinal) || !name.EndsWith(".tmp", StringComparison.Ordinal) ||
            !Guid.TryParseExact(name.Substring(10, 32), "N", out var id) || id == Guid.Empty || name.Substring(10, 32) != id.ToString("N"))
            throw new RecordingConversionOutboxException();
        return paths.ResolveRecordingPath(relative);
    }
    private async Task<FileStream?> VerifyAsync(Entry entry, CancellationToken token)
    {
        var artifact = entry.Target.Artifact;
        if (artifact.Availability != RecordingAvailability.Available) return null;
        if (probe.IsAbsent(artifact.RelativePath)) throw new RecordingConversionOutboxException();
        var output = new FileStream(paths.ResolveRecordingPath(artifact.RelativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        try
        {
            if (probe.IsAbsent(artifact.RelativePath) || output.Length is < 1 or > int.MaxValue || output.Length != artifact.ByteLength) throw new RecordingConversionOutboxException();
            var digest = Convert.ToHexString(await SHA256.HashDataAsync(output, token).ConfigureAwait(false)).ToLowerInvariant();
            if (digest != artifact.Sha256) throw new RecordingConversionOutboxException();
            output.Position = 0; return output;
        }
        catch { await output.DisposeAsync().ConfigureAwait(false); throw; }
    }
    private async Task PublishAsync(string path, Entry entry, CancellationToken token)
    {
        var temporary = Path.Combine(Root, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var encrypted = secrets.Protect(JsonSerializer.Serialize(entry, Json), Purpose);
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await JsonSerializer.SerializeAsync(file, encrypted, Json, token).ConfigureAwait(false);
                await file.FlushAsync(token).ConfigureAwait(false); file.Flush(true);
            }
            token.ThrowIfCancellationRequested(); File.Move(temporary, path, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private string Binding()
    {
        var database = configuration.Current.Options.Database; string target;
        if (database.Provider == CommunicationDatabaseProvider.Sqlite)
            target = Path.GetFullPath(Path.Combine(paths.RootDirectory, database.SqliteFileName ?? "communications.db")).ToUpperInvariant();
        else
        {
            using var connection = secrets.Unprotect(database.ConnectionString ?? throw new RecordingConversionOutboxException(), CommunicationDatabaseSettings.ConnectionStringPurpose);
            target = connection.Reveal();
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(application.ProductLine + "\n" + database.Provider + "\n" + target)));
    }
    private string EntryPath(Guid id) => Path.Combine(Root, id.ToString("N") + ".pending");
    private FileStream Lease(Guid id)
    {
        var path = Path.Combine(Root, id.ToString("N") + ".lock"); if (File.Exists(path)) CheckLink(path);
        return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
    }
    private static void CheckLink(string path) { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new RecordingConversionOutboxException(); }
    private static void Validate(RecordingArtifactWrite target, RecordingConversionWrite conversion)
    {
        ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(conversion);
        var artifact = target.Artifact; var job = conversion.Job;
        if (artifact.Id != job.TargetArtifactId || artifact.Format == RecordingArtifactFormat.Wave || target.ChangedAtUtc != conversion.ChangedAtUtc ||
            target.ExpectedVersion is null || conversion.ExpectedVersion is null || job.State is not (RecordingConversionState.Completed or RecordingConversionState.Failed) ||
            artifact.Availability != (job.State == RecordingConversionState.Completed ? RecordingAvailability.Available : RecordingAvailability.Failed))
            throw new RecordingConversionOutboxException();
    }
    private sealed record Entry(int Version, string Binding, RecordingArtifactWrite Target, RecordingConversionWrite Conversion, string? PreparedRelativePath = null);
    private sealed class Delivery(string path, Entry entry, FileStream lease, FileStream? output) : IRecordingConversionDelivery
    {
        public RecordingArtifactWrite Target => entry.Target;
        public RecordingConversionWrite Conversion => entry.Conversion;
        public void Acknowledge() { try { CheckLink(path); File.Delete(path); } catch (Exception) { throw new RecordingConversionOutboxException(); } }
        public async ValueTask DisposeAsync() { try { if (output is not null) await output.DisposeAsync().ConfigureAwait(false); } finally { await lease.DisposeAsync().ConfigureAwait(false); } }
    }
}
