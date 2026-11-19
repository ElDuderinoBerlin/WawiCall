using System.Collections.Immutable;
using WawiCallPro.Interfaces.Common;

namespace WawiCallPro.Database.Contracts;

public sealed record RecordingFileReference(Guid RecordingId, ImmutableArray<string> RelativePaths);

/// <summary>All states of recordings owned by one workstation, including legacy paths and pending artifacts.</summary>
public interface IRecordingFileCatalogQuery
{
    Task<Page<RecordingFileReference>> GetPageAsync(Guid workstationId, Guid? afterRecordingId = null,
        int limit = 100, CancellationToken cancellationToken = default);
}
