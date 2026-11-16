using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>DPAPI CurrentUser delivery records bound to the exact startup database and product line.</summary>
public sealed class ProtectedRecordingJournalOutbox(ApplicationPaths paths, ISecretProtector secrets,
    ApplicationConfigurationService configuration, ApplicationDescriptor application) : IRecordingJournalOutbox
{
    private const string Purpose = "recording-journal-v1";
    private static readonly JsonSerializerOptions Json = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    private string Root => Path.Combine(paths.RootDirectory, "recording-pending");

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
        Validate(recording, capture); var id = recording.Recording.Id; FileStream? lease = null; FileStream? wave = null;
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
                    wave = OpenWave(recording.Recording.RelativePath);
                    digest = Convert.ToHexString(await SHA256.HashDataAsync(wave, cancellationToken).ConfigureAwait(false)); wave.Position = 0;
                }
                entry = new(1, Binding(), recording, capture, digest);
                await PublishAsync(path, entry, cancellationToken).ConfigureAwait(false);
            }
            return new Delivery(path, entry, lease, wave);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { wave?.Dispose(); lease?.Dispose(); throw; }
        catch (Exception) { wave?.Dispose(); lease?.Dispose(); throw new RecordingOutboxException(); }
    }

    public async Task<IRecordingJournalDelivery> OpenAsync(Guid recordingId, CancellationToken cancellationToken = default)
    {
        _ = DatabaseId.Encode(recordingId); FileStream? lease = null; FileStream? wave = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); CheckLink(Root); lease = Lease(recordingId);
            var entry = await ReadAsync(recordingId, cancellationToken).ConfigureAwait(false);
            wave = await VerifyAsync(entry, cancellationToken).ConfigureAwait(false);
            return new Delivery(EntryPath(recordingId), entry, lease, wave);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { wave?.Dispose(); lease?.Dispose(); throw; }
        catch (Exception) { wave?.Dispose(); lease?.Dispose(); throw new RecordingOutboxException(); }
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
        if (entry.Version != 1 || entry.Binding != Binding() || entry.Recording.Recording.Id != id
            || (entry.Capture.Capture.State == PcmRecordingState.Completed ? entry.Digest is null || entry.Digest.Length != 64 || entry.Digest.Any(ch => !Uri.IsHexDigit(ch)) : entry.Digest is not null))
            throw new RecordingOutboxException();
        return entry;
    }

    private async Task<FileStream?> VerifyAsync(Entry entry, CancellationToken token)
    {
        if (entry.Digest is null) return null;
        var wave = OpenWave(entry.Recording.Recording.RelativePath);
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

    private FileStream OpenWave(string relative)
    {
        var path = paths.ResolveRecordingPath(relative); var current = paths.RecordingsDirectory; CheckLink(current);
        foreach (var part in relative.Split('/')) { current = Path.Combine(current, part); CheckLink(current); }
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        if (stream.Length is < 44 or > int.MaxValue) { stream.Dispose(); throw new RecordingOutboxException(); }
        return stream;
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
    private sealed record Entry(int Version, string Binding, RecordingWrite Recording, RecordingCaptureWrite Capture, string? Digest);
    private sealed class Delivery(string path, Entry entry, FileStream lease, FileStream? wave) : IRecordingJournalDelivery
    {
        public RecordingWrite Recording => entry.Recording;
        public RecordingCaptureWrite Capture => entry.Capture;
        public Stream? Wave => wave;
        public void Acknowledge() { try { CheckLink(path); File.Delete(path); } catch (Exception) { throw new RecordingOutboxException(); } }
        public async ValueTask DisposeAsync() { try { if (wave is not null) await wave.DisposeAsync().ConfigureAwait(false); } finally { await lease.DisposeAsync().ConfigureAwait(false); } }
    }
}
