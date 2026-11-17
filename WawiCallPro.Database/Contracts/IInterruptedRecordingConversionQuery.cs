using WawiCallPro.Interfaces.Common;

namespace WawiCallPro.Database.Contracts;

/// <summary>Reads running MP3 attempts of local terminal calls. The exclusive ID cursor remains stable
/// when recovered attempts leave the result set; owning the recording lease is still required.</summary>
public interface IInterruptedRecordingConversionQuery
{
    Task<Page<StoredRecordingConversion>> GetPageAsync(Guid workstationId, Guid? afterJobId = null,
        int limit = 100, CancellationToken cancellationToken = default);
}
