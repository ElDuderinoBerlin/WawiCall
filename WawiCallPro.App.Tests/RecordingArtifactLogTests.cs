using Microsoft.Extensions.Logging;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class RecordingArtifactLogTests
{
    [TestMethod]
    [DataRow(OperationEvent.RecordingArtifactUnavailable, "bestätigte Aufnahmedatei")]
    [DataRow(OperationEvent.RecordingArtifactAuditIncomplete, "Abgleich")]
    [DataRow(OperationEvent.RecordingDirectoryReviewRequired, "ohne Zuordnung")]
    [DataRow(OperationEvent.RecordingDirectoryInventoryIncomplete, "nicht vollständig")]
    [DataRow(OperationEvent.RecordingConversionRecoveryIncomplete, "ausstehende Aufnahmekonvertierungen")]
    [DataRow(OperationEvent.RecordingConversionSchemaUpdateRequired, "Datenbank → Datenbank einrichten")]
    [DataRow(OperationEvent.RecordingConversionNewerSchema, "Aktualisieren Sie WawiCallPro")]
    [DataRow(OperationEvent.RecordingConversionDatabaseUnavailable, "Datenbank → Status prüfen")]
    [DataRow(OperationEvent.RecordingConversionWorkstationMissing, "eingerichteten Arbeitsplatz")]
    [DataRow(OperationEvent.RecordingConversionCheckFailed, "Zugriff auf den lokalen Aufnahmeordner")]
    [DataRow(OperationEvent.RecordingConversionInterrupted, "Unterbrochene Aufnahmekonvertierungen")]
    [DataRow(OperationEvent.RecordingConversionQueueIncomplete, "Konvertierungswarteschlange")]
    [DataRow(OperationEvent.SipRegistrationDatabaseNotReady, "Datenbank → Datenbank einrichten")]
    [DataRow(OperationEvent.SipRegistrationFailed, "SIP-Server")]
    [DataRow(OperationEvent.SipRegistrationRetry, "versucht es erneut")]
    [DataRow(OperationEvent.LiveRecordingSignalMissing, "Audiosignal fehlt")]
    [DataRow(OperationEvent.LiveRecordingInputFailed, "Audiofehler")]
    [DataRow(OperationEvent.LiveRecordingStorageFailed, "Kommunikationsdatenbank")]
    [DataRow(OperationEvent.LiveRecordingUnavailable, "Aufnahmequelle")]
    public void AuditWarningsAreUnderstandableInTheExistingLogWindow(OperationEvent operation, string expected)
    {
        var logs = new LogWindowService(); using var provider = new UiLogProvider(logs);
        OperationEvents.Write(provider.CreateLogger("RecordingArtifactAudit"), operation);
        var entry = logs.Drain().Single(); Assert.AreEqual(LogLevel.Warning, entry.Level); StringAssert.Contains(entry.Message, expected);
        Assert.IsFalse(entry.Message.Contains(operation.ToString(), StringComparison.Ordinal));
    }
}
