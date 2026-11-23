namespace WawiCallPro.Utilities.Operations;

/// <summary>Allowlisted registration diagnostics without SIP headers or server response text.</summary>
public static class SipRegistrationMessages
{
    public static OperationEvent ForCode(string code) => code switch
    {
        "DatabaseNotReady" => OperationEvent.SipRegistrationDatabaseNotReady,
        "AccountMissing" => OperationEvent.SipRegistrationAccountMissing,
        "AccountInUse" => OperationEvent.SipRegistrationAccountInUse,
        "RegistrationRetry" => OperationEvent.SipRegistrationRetry,
        "RegistrationFailed" => OperationEvent.SipRegistrationFailed,
        _ => OperationEvent.SipRegistrationConnectionFailed
    };

    public static string? Get(OperationEvent operation) => operation switch
    {
        OperationEvent.SipRegistrationDatabaseNotReady =>
            "Die SIP-Anmeldung wurde nicht gestartet. Prüfen Sie die Kommunikationsdatenbank und den Arbeitsplatz unter „Datenbank → Datenbank einrichten“.",
        OperationEvent.SipRegistrationAccountMissing =>
            "Für die SIP-Anmeldung fehlt ein Konto. Wählen und speichern Sie unter „Einstellungen → SIP-Konto“ ein gültiges Profil.",
        OperationEvent.SipRegistrationAccountInUse =>
            "Ein anderer lokaler Prozess verwendet dieses SIP-Konto bereits. Melden Sie das Konto dort ab und versuchen Sie es erneut.",
        OperationEvent.SipRegistrationRetry =>
            "Der SIP-Server hat die Anmeldung noch nicht bestätigt. WawiCallPro versucht es erneut.",
        OperationEvent.SipRegistrationFailed =>
            "Der SIP-Server hat die Anmeldung nicht bestätigt. Prüfen Sie Serveradresse, Transport und Kontodaten.",
        OperationEvent.SipRegistrationConnectionFailed =>
            "Die SIP-Verbindung konnte nicht aufgebaut werden. Prüfen Sie Serveradresse, Port, Transport und die lokale Netzverbindung.",
        _ => null
    };
}
