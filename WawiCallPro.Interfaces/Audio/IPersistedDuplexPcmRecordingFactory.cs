namespace WawiCallPro.Interfaces.Audio;

/// <summary>Call-owned recording with automatic metadata persistence. Requires RecordingEpochUtc and a WAV below the host recordings root.</summary>
public interface IPersistedDuplexPcmRecordingFactory : IDuplexPcmRecordingFactory { }

/// <summary>Audio state and database confirmation are distinct. Retrying never re-runs capture or WAV mixing.</summary>
public interface IRecordingPersistenceStatus
{
    bool HasPendingPersistence { get; }
    Task RetryPersistenceAsync(CancellationToken cancellationToken = default);
}

/// <summary>Safe diagnostic. A finalized WAV is retained when its database confirmation fails.</summary>
public sealed class RecordingPersistenceException(bool finalizedWavePreserved) : Exception(
    finalizedWavePreserved ? "Die WAV wurde fertiggestellt, ihre Metadaten konnten aber nicht bestätigt werden. Die Datei bleibt erhalten."
        : "Die Aufnahmemetadaten konnten nicht bestätigt werden. Der Vorgang benötigt eine erneute Prüfung.")
{
    public bool FinalizedWavePreserved { get; } = finalizedWavePreserved;
}
