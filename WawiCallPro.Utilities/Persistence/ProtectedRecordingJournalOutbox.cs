using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WawiCallPro.Core.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>DPAPI CurrentUser delivery records bound to the exact startup database and product line.</summary>
public sealed class ProtectedRecordingJournalOutbox(ApplicationPaths paths, ISecretProtector secrets,
    ApplicationConfigurationService configuration, ApplicationDescriptor application) : IRecordingJournalOutbox, IPreparedWaveOutbox
{
    private const string Purpose = "recording-journal-v1";
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    private ApplicationPaths Paths => paths;
    private string Root => Path.Combine(paths.RootDirectory, "recording-pending");
    private readonly LocalRecordingFileProbe _files = new(paths);

    public IReadOnlyList<Guid> GetPending()
    {
        try
        {
            try { CheckLink(Root); }
            catch (DirectoryNotFoundException) { return []; }
            catch (FileNotFoundException) { return []; }
            var result = new List<Guid>();
            var files = Directory.EnumerateFiles(Root, "*.pending").Take(4097).ToArray();
            if (files.Length > 4096) throw new RecordingOutboxException();
            foreach (var file in files.Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (result.Count >= 4096 || !Guid.TryParseExact(name, "N", out var id) || id == Guid.Empty || name != id.ToString("N")) throw new RecordingOutboxException();
                result.Add(id);
            }
            return result;
        }
        catch (Exception) { throw new RecordingOutboxException(); }
    }

    public async Task<IRecordingJournalDelivery> StageAsync(RecordingWrite recording, RecordingCaptureWrite capture, CancellationToken cancellationToken = default)
    {
        Validate(recording, capture); var id = recording.Recording.Id; FileStream? lease = null; VerifiedWave? wave = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); Directory.CreateDirectory(Root); CheckLink(Root);
            lease = Lease(id); var path = EntryPath(id); Entry entry;
            if (File.Exists(path))
            {
                entry = await ReadAsync(id, cancellationToken).ConfigureAwait(false);
                if (entry.Recording != recording || entry.Capture != capture) throw new RecordingOutboxException();
                wave = await VerifyAsync(entry, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                string? digest = null;
                if (capture.Capture.State == PcmRecordingState.Completed)
                {
                    wave = new(OpenWave(recording.Recording.RelativePath), false);
                    digest = Convert.ToHexString(await SHA256.HashDataAsync(wave.Stream, cancellationToken).ConfigureAwait(false)); wave.Stream.Position = 0;
                }
                entry = new(1, Binding(), recording, capture, digest);
                await PublishAsync(path, entry, cancellationToken).ConfigureAwait(false);
            }
            return new Delivery(this, path, entry, lease, wave);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { wave?.Stream.Dispose(); lease?.Dispose(); throw; }
        catch (Exception) { wave?.Stream.Dispose(); lease?.Dispose(); throw new RecordingOutboxException(); }
    }

    public async Task StagePreparedWaveAsync(RecordingWrite recording, RecordingCaptureWrite capture, string temporaryPath,
        CancellationToken cancellationToken = default)
    {
        Validate(recording, capture);
        if (capture.Capture.State != PcmRecordingState.Completed) throw new RecordingOutboxException();
        var id = recording.Recording.Id;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Root);
            CheckLink(Root);
            await using var lease = Lease(id);
            var relative = Path.GetRelativePath(paths.RecordingsDirectory, temporaryPath).Replace('\\', '/');
            var entry = new Entry(2, Binding(), recording, capture, null, relative);
            _ = PreparedRelativePath(entry);
            if (!_files.IsAbsent(recording.Recording.RelativePath)) throw new RecordingOutboxException();
            await using var wave = OpenWave(relative);
            ValidateWave(wave, capture.Capture, cancellationToken);
            wave.Position = 0;
            var digest = Convert.ToHexString(await SHA256.HashDataAsync(wave, cancellationToken).ConfigureAwait(false));
            await PublishAsync(EntryPath(id), entry with { Digest = digest }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { throw new RecordingOutboxException(); }
    }

    public async Task<IRecordingJournalDelivery> OpenAsync(Guid recordingId, CancellationToken cancellationToken = default)
    {
        _ = DatabaseId.Encode(recordingId); FileStream? lease = null; VerifiedWave? wave = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); CheckLink(Root); lease = Lease(recordingId);
            var entry = await ReadAsync(recordingId, cancellationToken).ConfigureAwait(false);
            wave = await VerifyAsync(entry, cancellationToken).ConfigureAwait(false);
            return new Delivery(this, EntryPath(recordingId), entry, lease, wave);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { wave?.Stream.Dispose(); lease?.Dispose(); throw; }
        catch (Exception) { wave?.Stream.Dispose(); lease?.Dispose(); throw new RecordingOutboxException(); }
    }

    private async Task<Entry> ReadAsync(Guid id, CancellationToken token)
    {
        var path = EntryPath(id); CheckLink(path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        if (file.Length is < 1 or > 65536) throw new RecordingOutboxException();
        var encrypted = await JsonSerializer.DeserializeAsync<ProtectedSecret>(file, Json, token).ConfigureAwait(false) ?? throw new RecordingOutboxException();
        using var plain = secrets.Unprotect(encrypted, Purpose);
        var entry = JsonSerializer.Deserialize<Entry>(plain.Reveal(), Json) ?? throw new RecordingOutboxException();
        Validate(entry.Recording, entry.Capture);
        if (entry.Binding != Binding() || entry.Recording.Recording.Id != id
            || (entry.Capture.Capture.State == PcmRecordingState.Completed ? entry.Digest is null || entry.Digest.Length != 64 || entry.Digest.Any(ch => !Uri.IsHexDigit(ch)) : entry.Digest is not null))
            throw new RecordingOutboxException();
        if (entry.Version == 2 && entry.Digest is not null) _ = PreparedRelativePath(entry);
        else if (entry.Version != 1 || entry.PreparedPath is not null) throw new RecordingOutboxException();
        return entry;
    }

    private string PreparedRelativePath(Entry entry)
    {
        var path = entry.PreparedPath ?? throw new RecordingOutboxException();
        var name = Path.GetFileName(path);
        const string prefix = ".wawi-mix-";
        if (path.Length > 1024 || path.Any(char.IsControl) || path.Contains('\\') ||
            path.Split('/').Any(part => part is "" or "." or "..") ||
            !string.Equals(Path.GetDirectoryName(path), Path.GetDirectoryName(entry.Recording.Recording.RelativePath), StringComparison.OrdinalIgnoreCase) ||
            name.Length != prefix.Length + 32 + 4 ||
            !name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".tmp", StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[prefix.Length..^4], "N", out var id) || id == Guid.Empty)
            throw new RecordingOutboxException();
        _ = paths.ResolveRecordingPath(path);
        return path;
    }

    private async Task<VerifiedWave?> VerifyAsync(Entry entry, CancellationToken token)
    {
        if (entry.Digest is null) return null;
        var relative = entry.Recording.Recording.RelativePath;
        var prepared = entry.PreparedPath is not null && !_files.IsAbsent(entry.PreparedPath);
        if (prepared && !_files.IsAbsent(relative)) throw new RecordingOutboxException();
        return new(await OpenVerifiedWaveAsync(entry, prepared ? entry.PreparedPath! : relative, token, prepared).ConfigureAwait(false), prepared);
    }

    private async Task<FileStream> OpenVerifiedWaveAsync(Entry entry, string relativePath, CancellationToken token, bool prepared = false)
    {
        var wave = OpenWave(relativePath, prepared ? FileShare.Read | FileShare.Delete : FileShare.Read);
        try
        {
            var digest = Convert.ToHexString(await SHA256.HashDataAsync(wave, token).ConfigureAwait(false));
            if (!string.Equals(digest, entry.Digest, StringComparison.Ordinal)) throw new RecordingOutboxException();
            wave.Position = 0; return wave;
        }
        catch { wave.Dispose(); throw; }
    }

    private async Task PublishAsync(string path, Entry entry, CancellationToken token)
    {
        var temporary = Path.Combine(Root, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var encrypted = secrets.Protect(JsonSerializer.Serialize(entry, Json), Purpose);
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(file, encrypted, Json, token).ConfigureAwait(false);
                await file.FlushAsync(token).ConfigureAwait(false); file.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested(); File.Move(temporary, path, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private FileStream OpenWave(string relative, FileShare share = FileShare.Read)
    {
        var path = paths.ResolveRecordingPath(relative); var current = paths.RecordingsDirectory; CheckLink(current);
        foreach (var part in relative.Split('/')) { current = Path.Combine(current, part); CheckLink(current); }
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, 65536, useAsync: true);
        if (stream.Length is < 44 or > int.MaxValue) { stream.Dispose(); throw new RecordingOutboxException(); }
        return stream;
    }

    private static void ValidateWave(Stream wave, RecordingCapture capture, CancellationToken token)
    {
        var header = PcmWaveHeader.Read(wave, token);
        if (header.SampleRate != capture.SampleRate || header.Channels != capture.Channels ||
            header.DataLength / (capture.Channels * 2) != capture.FrameCount)
            throw new RecordingOutboxException();
    }
    private string Binding()
    {
        var database = configuration.Current.Options.Database;
        string target;
        if (database.Provider == CommunicationDatabaseProvider.Sqlite)
            target = Path.GetFullPath(Path.Combine(paths.RootDirectory, database.SqliteFileName ?? "communications.db")).ToUpperInvariant();
        else
        {
            using var connection = secrets.Unprotect(database.ConnectionString ?? throw new RecordingOutboxException(), CommunicationDatabaseSettings.ConnectionStringPurpose);
            target = connection.Reveal();
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(application.ProductLine + "\n" + database.Provider + "\n" + target)));
    }
    private FileStream Lease(Guid id)
    {
        var path = Path.Combine(Root, id.ToString("N") + ".lock");
        if (File.Exists(path)) CheckLink(path);
        return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
    }
    public IRecordingArtifactAuditLease AcquireInspection(Guid recordingId)
    {
        _ = DatabaseId.Encode(recordingId); FileStream? lease = null;
        try
        {
            Directory.CreateDirectory(Root); CheckLink(Root); lease = Lease(recordingId);
            var pending = false;
            try { CheckLink(EntryPath(recordingId)); pending = true; }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            return new InspectionLease(lease, pending);
        }
        catch (Exception) { lease?.Dispose(); throw new RecordingOutboxException(); }
    }
    private sealed class InspectionLease(FileStream lease, bool pending) : IRecordingArtifactAuditLease
    {
        public bool HasPending => pending;
        public ValueTask DisposeAsync() => lease.DisposeAsync();
    }
    private string EntryPath(Guid id) => Path.Combine(Root, id.ToString("N") + ".pending");
    private static void CheckLink(string path) { if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new RecordingOutboxException(); }
    private static void Validate(RecordingWrite recording, RecordingCaptureWrite capture)
    {
        ArgumentNullException.ThrowIfNull(recording); ArgumentNullException.ThrowIfNull(capture);
        var item = capture.Capture; var file = recording.Recording;
        if (recording.ExpectedVersion is null || capture.ExpectedVersion is null || file.Id != item.RecordingId || file.CallId != item.CallId
            || recording.ChangedAtUtc != capture.ChangedAtUtc || file.Format != "wav" || file.Duration != item.Duration
            || item.State is not (PcmRecordingState.Completed or PcmRecordingState.Failed or PcmRecordingState.Aborted)
            || file.Availability != (item.State == PcmRecordingState.Completed ? WawiCallPro.Interfaces.History.RecordingAvailability.Available : WawiCallPro.Interfaces.History.RecordingAvailability.Failed))
            throw new RecordingOutboxException();
    }
    private sealed record Entry(int Version, string Binding, RecordingWrite Recording, RecordingCaptureWrite Capture, string? Digest,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PreparedPath = null);
    private sealed record VerifiedWave(FileStream Stream, bool Prepared);

    private sealed class Delivery(ProtectedRecordingJournalOutbox owner, string path, Entry entry, FileStream lease, VerifiedWave? wave) : IRecordingJournalDelivery
    {
        private VerifiedWave? _wave = wave;
        public RecordingWrite Recording => entry.Recording;
        public RecordingCaptureWrite Capture => entry.Capture;
        public Stream? Wave => _wave?.Stream;
        public string? WaveSha256 => entry.Digest;
        public bool NeedsWavePublication => _wave?.Prepared == true;
        public async Task PublishPreparedWaveAsync(CancellationToken cancellationToken = default)
        {
            if (_wave is not { Prepared: true } prepared) return;
            var source = owner.PreparedRelativePath(entry);
            var destination = entry.Recording.Recording.RelativePath;
            if (owner._files.IsAbsent(source) || !owner._files.IsAbsent(destination)) throw new RecordingOutboxException();
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(owner.Paths.ResolveRecordingPath(source), owner.Paths.ResolveRecordingPath(destination), overwrite: false);
            await prepared.Stream.DisposeAsync().ConfigureAwait(false);
            _wave = new(await owner.OpenVerifiedWaveAsync(entry, destination, cancellationToken).ConfigureAwait(false), false);
        }
        public void Acknowledge()
        {
            if (NeedsWavePublication) throw new RecordingOutboxException();
            try { CheckLink(path); File.Delete(path); }
            catch (Exception) { throw new RecordingOutboxException(); }
        }
        public async ValueTask DisposeAsync() { try { if (_wave is not null) await _wave.Stream.DisposeAsync().ConfigureAwait(false); } finally { await lease.DisposeAsync().ConfigureAwait(false); } }
    }
}
