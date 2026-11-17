using WawiCallPro.Database.Contracts;

namespace WawiCallPro.Utilities.Persistence;

public interface IPersistedMp3ConversionFactory
{
    /// <summary>Validates an explicit Pending job of this workstation's ended call, locks its source and
    /// recording lease, then claims one attempt. Does not encode until RunAsync is called. Caller owns disposal.</summary>
    Task<IPersistedMp3Conversion> StartAsync(Guid jobId, CancellationToken cancellationToken = default);
}

/// <summary>Owns one attempt, source/output read locks and recording lease. No automatic restart or file deletion.</summary>
public interface IPersistedMp3Conversion : IAsyncDisposable
{
    /// <summary>Runs once; concurrent calls share the same task. Returns Completed/Failed metadata.
    /// Cancellation confirms Failed/Cancelled before propagating. Published output is retained on DB failures.</summary>
    Task<StoredRecordingConversionJournal> RunAsync(CancellationToken cancellationToken = default);
    /// <summary>Retries only the prepared exact metadata pair after a DB failure; never re-encodes.
    /// Available after RunAsync prepares its result, while this owner remains alive.</summary>
    Task<StoredRecordingConversionJournal> ConfirmAsync(CancellationToken cancellationToken = default);
}
