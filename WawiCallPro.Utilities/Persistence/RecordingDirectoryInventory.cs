using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Persistence;

public sealed record RecordingDirectoryEntry(string RelativePath, bool IsLink, bool AwaitingConfirmation = false)
{ public override string ToString() => nameof(RecordingDirectoryEntry); }
public interface IRecordingDirectoryEntries
{
    IEnumerable<RecordingDirectoryEntry> Read(CancellationToken cancellationToken = default);
}

/// <summary>Enumerates metadata only. Never follows known links, opens file content or changes the directory.</summary>
public sealed class LocalRecordingDirectoryEntries(ApplicationPaths paths) : IRecordingDirectoryEntries
{
    public IEnumerable<RecordingDirectoryEntry> Read(CancellationToken cancellationToken = default)
    {
        var pending = new Stack<(string Path, int Depth)>(); pending.Push((paths.RecordingsDirectory, 0)); var visited = 0;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested(); var current = pending.Pop();
            FileAttributes attributes;
            try { attributes = File.GetAttributes(current.Path); }
            catch (DirectoryNotFoundException) when (current.Depth == 0) { yield break; }
            catch (FileNotFoundException) when (current.Depth == 0) { yield break; }
            if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) == 0 || current.Depth > 32)
                throw new RecordingOutboxException();
            foreach (var entry in Directory.EnumerateFileSystemEntries(current.Path))
            {
                cancellationToken.ThrowIfCancellationRequested(); if (++visited > 100000) throw new RecordingOutboxException();
                var relative = Path.GetRelativePath(paths.RecordingsDirectory, entry).Replace('\\', '/');
                _ = paths.ResolveRecordingPath(relative); var flags = File.GetAttributes(entry);
                if ((flags & FileAttributes.ReparsePoint) != 0) { yield return new(relative, true); continue; }
                if ((flags & FileAttributes.Directory) != 0) pending.Push((entry, current.Depth + 1));
                else yield return new(relative, false);
            }
        }
    }
}

public sealed record RecordingDirectoryInventoryResult(int Registered, int WithoutLocalReference, int TemporaryWithoutLocalReference, int LinksSkipped,
    ImmutableArray<RecordingDirectoryEntry> Review);

public interface IRecordingDirectoryInventory
{
    Task<RecordingDirectoryInventoryResult> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reports paths absent from the observed local catalog, not proven orphan files. No adoption or deletion.</summary>
public sealed class RecordingDirectoryInventory(IRecordingFileCatalogQuery catalog, IWorkstationIdentity workstation,
    IRecordingDirectoryEntries directory, ApplicationPaths paths, IInterruptedRecordingQuery? unfinished = null) : IRecordingDirectoryInventory
{
    public async Task<RecordingDirectoryInventoryResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        var local = await workstation.GetAsync(cancellationToken).ConfigureAwait(false) ?? throw new RecordingOutboxException();
        var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase); Guid? after = null; var count = 0;
        while (true)
        {
            var page = await catalog.GetPageAsync(local, after, 100, cancellationToken).ConfigureAwait(false);
            if (page.Items.IsDefault || page.Items.Length > 100 || page.HasMore && page.Items.IsEmpty) throw new RecordingOutboxException();
            foreach (var row in page.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++count > 100000 || row.RelativePaths.IsDefault || row.RelativePaths.Length > 3) throw new RecordingOutboxException();
                foreach (var relative in row.RelativePaths) { _ = paths.ResolveRecordingPath(relative); registered.Add(Normalize(relative)); }
            }
            if (!page.HasMore) break;
            var next = page.Items[^1].RecordingId;
            if (after is { } old && StringComparer.Ordinal.Compare(DatabaseId.Encode(next), DatabaseId.Encode(old)) <= 0) throw new RecordingOutboxException();
            after = next;
        }
        var unconfirmed = await ReadUnconfirmedPathsAsync(local, cancellationToken).ConfigureAwait(false);
        var known = 0; var unregistered = 0; var temporary = 0; var links = 0; var files = 0;
        var review = ImmutableArray.CreateBuilder<RecordingDirectoryEntry>();
        foreach (var entry in directory.Read(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); if (++files > 100000) throw new RecordingOutboxException();
            _ = paths.ResolveRecordingPath(entry.RelativePath);
            if (entry.IsLink) { links++; review.Add(entry); }
            else if (registered.Contains(Normalize(entry.RelativePath)))
            {
                known++;
                if (unconfirmed.Contains(Normalize(entry.RelativePath))) review.Add(entry with { AwaitingConfirmation = true });
            }
            else { if (Path.GetExtension(entry.RelativePath).Equals(".tmp", StringComparison.OrdinalIgnoreCase)) temporary++; else unregistered++; review.Add(entry); }
        }
        return new(known, unregistered, temporary, links, review.ToImmutable());
    }

    private async Task<HashSet<string>> ReadUnconfirmedPathsAsync(Guid local, CancellationToken token)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (unfinished is null) return result;
        Guid? after = null;
        var count = 0;
        while (true)
        {
            var page = await unfinished.GetPageAsync(local, after, 100, token).ConfigureAwait(false);
            if (page.Items.IsDefault || page.Items.Length > 100 || page.HasMore && page.Items.IsEmpty) throw new RecordingOutboxException();
            foreach (var row in page.Items)
            {
                token.ThrowIfCancellationRequested();
                if (++count > 100000) throw new RecordingOutboxException();
                result.Add(Normalize(row.Journal.Recording.Recording.RelativePath));
            }
            if (!page.HasMore) return result;
            var next = page.Items[^1].Journal.Recording.Recording.Id;
            if (after is { } old && StringComparer.Ordinal.Compare(DatabaseId.Encode(next), DatabaseId.Encode(old)) <= 0) throw new RecordingOutboxException();
            after = next;
        }
    }
    private static string Normalize(string relative) => relative.Replace('\\', '/');
}
