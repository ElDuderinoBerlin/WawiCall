using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Telephony.Sip;

/// <summary>CurrentUser-DPAPI records, atomically published before database delivery; App and Console share the lock.</summary>
public sealed class ProtectedSipCallOutbox(ApplicationPaths paths, ISecretProtector secrets, ApplicationDescriptor application) : ISipCallOutbox
{
    private const string Purpose = "sip-call-outbox-v1";
    private readonly Queue<(string Path, CallSnapshot Call)> _entries = new();
    private string Root => Path.Combine(paths.RootDirectory, "sip-pending");
    private FileStream? _lease;
    private string? _binding;
    private long _sequence;

    public async Task<IReadOnlyList<CallSnapshot>> OpenAsync(CommunicationDatabaseSettings database, CancellationToken cancellationToken = default)
    {
        var binding = DatabaseBinding(database);
        if (_lease is not null)
        {
            if (_binding != binding) throw Unavailable();
            return _entries.Select(entry => entry.Call).ToArray();
        }
        try
        {
            Directory.CreateDirectory(Root);
            _lease = new FileStream(Path.Combine(Root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var loaded = new List<(string Path, CallSnapshot Call)>();
            var keys = new HashSet<(Guid, long)>();
            _sequence = 0;
            foreach (var file in Directory.EnumerateFiles(Root, "*.pending").Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Length != 20 || !long.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
                    || sequence <= _sequence) throw Unavailable();
                await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                if (stream.Length is < 1 or > 65536) throw Unavailable();
                var encrypted = await JsonSerializer.DeserializeAsync<ProtectedSecret>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
                    ?? throw Unavailable();
                using var plaintext = secrets.Unprotect(encrypted, Purpose);
                var entry = JsonSerializer.Deserialize<Record>(plaintext.Reveal()) ?? throw Unavailable();
                if (entry.Version != 1 || entry.Binding != binding || entry.Sequence != sequence || entry.Call?.Origin is null
                    || !keys.Add((entry.Call.Id, entry.Call.Revision))) throw Unavailable();
                _ = new CallStateMachine(entry.Call);
                loaded.Add((file, entry.Call));
                _sequence = sequence;
            }
            // Do not expose a valid prefix if any later record is damaged or belongs to a different target.
            foreach (var entry in loaded) _entries.Enqueue(entry);
            _binding = binding;
            return loaded.Select(entry => entry.Call).ToArray();
        }
        catch (OperationCanceledException) { Close(); throw; }
        catch { Close(); throw Unavailable(); }
    }

    public async Task AppendAsync(CallSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (_lease is null || snapshot.Origin is null) throw Unavailable();
        _ = new CallStateMachine(snapshot);
        var sequence = checked(_sequence + 1);
        var destination = Path.Combine(Root, sequence.ToString("D20", CultureInfo.InvariantCulture) + ".pending");
        var temporary = Path.Combine(Root, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            var encrypted = secrets.Protect(JsonSerializer.Serialize(new Record(1, _binding!, sequence, snapshot)), Purpose);
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(file, encrypted, cancellationToken: cancellationToken).ConfigureAwait(false);
                await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                file.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: false);
            _entries.Enqueue((destination, snapshot));
            _sequence = sequence;
        }
        catch (OperationCanceledException) { throw; }
        catch { throw Unavailable(); }
        finally
        {
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    public void Acknowledge(CallSnapshot snapshot)
    {
        if (_lease is null || !_entries.TryPeek(out var entry) || entry.Call.Id != snapshot.Id || entry.Call.Revision != snapshot.Revision)
            throw Unavailable();
        try { File.Delete(entry.Path); }
        catch { throw Unavailable(); }
        _entries.Dequeue();
    }

    private string DatabaseBinding(CommunicationDatabaseSettings database)
    {
        string target;
        if (database.Provider == CommunicationDatabaseProvider.Sqlite)
            target = Path.GetFullPath(Path.Combine(paths.RootDirectory, database.SqliteFileName ?? "communications.db")).ToUpperInvariant();
        else
        {
            if (database.ConnectionString is null) throw Unavailable();
            using var connection = secrets.Unprotect(database.ConnectionString, CommunicationDatabaseSettings.ConnectionStringPurpose);
            target = connection.Reveal();
        }
        // The exact configured server connection also binds credentials: changing it requires restoring the
        // old configuration and delivering outstanding records first. No connection value is written in cleartext.
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(application.ProductLine + "\n" + database.Provider + "\n" + target)));
    }

    public void Close()
    {
        _entries.Clear(); _binding = null;
        _lease?.Dispose(); _lease = null;
    }

    public void Dispose() => Close();
    private static IOException Unavailable() => new("Die geschützte Gesprächswarteschlange ist belegt, beschädigt oder gehört zu einer anderen Datenbank. Sie wurde nicht verworfen.");
    private sealed record Record(int Version, string Binding, long Sequence, CallSnapshot Call);
}
