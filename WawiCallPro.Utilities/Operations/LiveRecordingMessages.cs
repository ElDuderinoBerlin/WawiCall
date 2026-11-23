using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Utilities.Operations;

public static class LiveRecordingMessages
{
    public static string Failure(LiveRecordingFailure? failure) => failure switch
    {
        LiveRecordingFailure.Cancelled => "Aufnahmevorbereitung abgebrochen.",
        LiveRecordingFailure.SignalMissing => "Aufnahme konnte nicht starten. Mindestens ein Audiosignal fehlt.",
        LiveRecordingFailure.InputFailed => "Aufnahme wegen eines Audiofehlers beendet. Aufnahmequelle und Audiogeräte prüfen.",
        LiveRecordingFailure.StorageFailed => "Aufnahme konnte nicht gespeichert oder abgeschlossen werden. Aufnahmeordner und Kommunikationsdatenbank prüfen.",
        LiveRecordingFailure.Unavailable => "Die Aufnahmequelle für dieses Gespräch ist nicht verfügbar.",
        _ => "Aufnahme fehlgeschlagen. Audiosignal, Geräte und Speicher prüfen."
    };
}
