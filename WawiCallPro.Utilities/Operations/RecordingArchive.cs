using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.Utilities.Operations;

/// <summary>Coordinates an archive switch with cooperating hosts using the same profile.</summary>
public sealed class RecordingArchive(ApplicationPaths paths, ConfigurationStore store, ILogger<RecordingArchive> logger, StartupExecutionMode? mode = null)
    : IDisposable
{
    private FileStream? _lease;
    private ConfigurationSnapshot? _prepared;
    private string LockPath => Path.Combine(paths.RootDirectory, ".recording-archive.lock");

    internal async Task<ConfigurationSnapshot> OpenAsync(CancellationToken token)
    {
        if (_prepared is not null) return _prepared;
        Directory.CreateDirectory(paths.RootDirectory);
        try
        {
            _lease = OpenShared();
            var snapshot = await store.LoadAsync(token).ConfigureAwait(false);
            if (snapshot.Options.RecordingStorage.PendingDirectory is not null && mode is not { SuppressNetwork: true } and not { IsolatedProfile: true })
            {
                _lease.Dispose();
                _lease = null;
                try
                {
                    using var exclusive = new FileStream(LockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    snapshot = await store.LoadAsync(token).ConfigureAwait(false);
                    var settings = snapshot.Options.RecordingStorage;
                    if (settings.PendingDirectory is { } destination)
                    {
                        await CopyAsync(paths.GetRecordingDirectory(settings.Directory), destination, settings.Directory is not null, token).ConfigureAwait(false);
                        var directory = string.Equals(destination, paths.DefaultRecordingsDirectory, StringComparison.OrdinalIgnoreCase) ? null : destination;
                        await store.SaveAsync(snapshot.Options with { RecordingStorage = settings with { Directory = directory, PendingDirectory = null } },
                            snapshot, token).ConfigureAwait(false);
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ConfigurationException or FormatException)
                {
                    OperationEvents.Write(logger, OperationEvent.RecordingArchiveSwitchPending);
                }
                _lease = OpenShared();
                snapshot = await store.LoadAsync(token).ConfigureAwait(false);
            }
            paths.SetRecordingStorage(snapshot.Options.RecordingStorage);
            return _prepared = snapshot;
        }
        catch
        {
            _lease?.Dispose();
            _lease = null;
            throw;
        }
    }

    private FileStream OpenShared()
    {
        if (!File.Exists(LockPath))
        {
            try { using var created = new FileStream(LockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite); }
            catch (IOException) when (File.Exists(LockPath)) { }
        }
        try { return new FileStream(LockPath, FileMode.Open, FileAccess.Read, FileShare.Read); }
        catch (IOException) { throw ConfigurationException.FileError("Das Aufnahmeverzeichnis wird gerade gewechselt. WawiCallPro danach erneut starten."); }
    }

    /// <summary>Validates a regular local Windows directory path without accessing the filesystem.</summary>
    public static string NormalizeDirectory(string path)
    {
        try
        {
            if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)
                || path.Any(char.IsControl) || path.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0)
                throw new ArgumentException();
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            var root = Path.GetPathRoot(full)!;
            if (full.Length <= root.Length || full.AsSpan(root.Length).Contains(':'))
                throw new ArgumentException();
            return full;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new FormatException("Einen absoluten lokalen Aufnahmeordner unterhalb eines Laufwerks auswählen.");
        }
    }

    public static void ValidateDestination(string source, string destination)
    {
        source = NormalizeDirectory(source);
        destination = NormalizeDirectory(destination);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) return;
        if (IsBelow(source, destination) || IsBelow(destination, source))
            throw new FormatException("Alter und neuer Aufnahmeordner dürfen nicht ineinander liegen.");
        if (new DriveInfo(Path.GetPathRoot(destination)!).DriveType == DriveType.Network)
            throw new FormatException("Für Aufnahmen einen lokalen Ordner auswählen.");
        EnsureDirectory(destination, create: true);
        var probe = Path.Combine(destination, ".wawi-write-" + Guid.NewGuid().ToString("N") + ".tmp");
        using var writable = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
    }

    private static bool IsBelow(string path, string parent) =>
        path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool EnsureDirectory(string path, bool create)
    {
        // Check ancestors before creating descendants so a junction cannot redirect an archive operation.
        if (Path.GetDirectoryName(path) is { } parent && !EnsureDirectory(parent, create) && !create) return false;
        try
        {
            var flags = File.GetAttributes(path);
            if ((flags & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory)
                throw new IOException("Das Aufnahmeverzeichnis enthält eine Verknüpfung oder einen Dateipfad.");
            return true;
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            if (!create) return false;
            Directory.CreateDirectory(path);
            return EnsureDirectory(path, create: false);
        }
    }

    private static async Task CopyAsync(string source, string destination, bool requireExistingSource, CancellationToken token)
    {
        source = NormalizeDirectory(source);
        destination = NormalizeDirectory(destination);
        ValidateDestination(source, destination);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) return;
        if (!EnsureDirectory(source, create: false))
        {
            if (requireExistingSource) throw new IOException("Der bisherige Aufnahmeordner ist nicht erreichbar.");
            return;
        }
        var entries = new List<(string Source, string Destination, bool IsDirectory)>();
        var pending = new Stack<(string Source, string Destination)>();
        pending.Push((source, destination));
        while (pending.TryPop(out var pair))
        {
            token.ThrowIfCancellationRequested();
            EnsureDirectory(pair.Source, create: false);
            foreach (var entry in Directory.EnumerateFileSystemEntries(pair.Source))
            {
                token.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Verknüpfungen im Aufnahmeordner können nicht kopiert werden.");
                var target = Path.Combine(pair.Destination, Path.GetFileName(entry));
                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                entries.Add((entry, target, isDirectory));
                if (isDirectory) pending.Push((entry, target));
            }
        }
        // Finish enumerating before publication so no destination file can enter the source traversal.
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            if (entry.IsDirectory) EnsureDirectory(entry.Destination, create: true);
            else
            {
                EnsureDirectory(Path.GetDirectoryName(entry.Source)!, create: false);
                EnsureDirectory(Path.GetDirectoryName(entry.Destination)!, create: false);
                if ((File.GetAttributes(entry.Source) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Die Quelldatei ist eine Verknüpfung.");
                await CopyFileAsync(entry.Source, entry.Destination, token).ConfigureAwait(false);
            }
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken token)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        try
        {
            if ((File.GetAttributes(destination) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new IOException("Ein vorhandener Zielpfad verweist auf eine Verknüpfung oder einen Ordner.");
            await VerifyAsync(input, destination, token).ConfigureAwait(false);
            return;
        }
        catch (FileNotFoundException) { }

        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".wawi-archive-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await input.CopyToAsync(output, token).ConfigureAwait(false);
                await output.FlushAsync(token).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }
            await VerifyAsync(input, temporary, token).ConfigureAwait(false);
            File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static async Task VerifyAsync(Stream source, string destination, CancellationToken token)
    {
        source.Position = 0;
        await using var target = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        if (source.Length == target.Length)
        {
            var expected = await SHA256.HashDataAsync(source, token).ConfigureAwait(false);
            var actual = await SHA256.HashDataAsync(target, token).ConfigureAwait(false);
            if (expected.AsSpan().SequenceEqual(actual)) return;
        }
        throw new IOException("Eine Zieldatei hat einen abweichenden Inhalt.");
    }

    public void Dispose() => _lease?.Dispose();
}
