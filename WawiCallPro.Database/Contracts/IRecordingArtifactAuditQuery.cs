using WawiCallPro.Interfaces.Common;

namespace WawiCallPro.Database.Contracts;

/// <summary>Bounded, exclusive-ID pages of confirmed recording artifacts from terminal calls of one workstation.</summary>
public interface IRecordingArtifactAuditQuery
{
    Task<Page<StoredRecordingArtifact>> GetPageAsync(Guid workstationId, Guid? afterArtifactId = null,
        int limit = 100, CancellationToken cancellationToken = default);
}
