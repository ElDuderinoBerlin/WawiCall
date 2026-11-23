namespace WawiCallPro.Utilities.Operations;

/// <summary>Fixed messages shared by the desktop window and configured log-file output.</summary>
public static class RecordingConversionMessages
{
    public static string? Get(OperationEvent operation) => operation switch
    {
        OperationEvent.RecordingConversionSchemaUpdateRequired =>
            "Die Prüfung früherer Aufnahmekonvertierungen wartet auf ein Datenbankupdate. Öffnen Sie im Ribbon „Datenbank → Datenbank einrichten“ und aktualisieren Sie das Schema der vorhandenen Kommunikationsdatenbank. Starten Sie WawiCallPro anschließend neu.",
        OperationEvent.RecordingConversionNewerSchema =>
            "Die Kommunikationsdatenbank verwendet ein neueres Schema. Aktualisieren Sie WawiCallPro, damit die Prüfung früherer Aufnahmekonvertierungen wieder möglich ist.",
        OperationEvent.RecordingConversionDatabaseUnavailable =>
            "Die Prüfung früherer Aufnahmekonvertierungen ist derzeit nicht möglich. Prüfen Sie die Kommunikationsdatenbank im Ribbon unter „Datenbank → Status prüfen“.",
        OperationEvent.RecordingConversionWorkstationMissing =>
            "Die Prüfung früherer Aufnahmekonvertierungen benötigt einen eingerichteten Arbeitsplatz. Prüfen Sie die Einrichtung unter „Datenbank → Datenbank einrichten“.",
        OperationEvent.RecordingConversionCheckFailed =>
            "Die Prüfung früherer Aufnahmekonvertierungen konnte nicht abgeschlossen werden. Prüfen Sie die Kommunikationsdatenbank und den Zugriff auf den lokalen Aufnahmeordner.",
        OperationEvent.RecordingConversionRecoveryIncomplete =>
            "Einige ausstehende Aufnahmekonvertierungen konnten nicht bestätigt werden. Die betroffenen Aufträge und Dateien bleiben für eine erneute Prüfung erhalten.",
        _ => null
    };
}
