namespace WawiCallPro.Interfaces.Telephony;

/// <summary>Explicit account lifecycle, separate from saving declarative settings.</summary>
public interface ITelephonyConnection
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    string? LastFailureCode { get; }
}

public interface IDtmfReceiver
{
    event EventHandler<DtmfReceivedEventArgs>? DtmfReceived;
}

/// <summary>One completed RTP telephone event, scoped to its stable application call ID.</summary>
public sealed class DtmfReceivedEventArgs(Guid callId, char digit, int duration) : EventArgs
{
    public Guid CallId { get; } = callId;
    public char Digit { get; } = digit;
    public int Duration { get; } = duration;
}

/// <summary>Stable safe failure code; no SIP headers, server responses or credentials.</summary>
public sealed class TelephonyException(string code) : Exception(code switch
{
    "DatabaseNotReady" => "Zuerst die Kommunikationsdatenbank und den Arbeitsplatz einrichten.",
    "AccountMissing" => "Zuerst das SIP-Konto einrichten und speichern.",
    "AccountInUse" => "Dieses SIP-Konto wird bereits von einem anderen lokalen Host verwendet.",
    "AudioUnavailable" or "AudioDeviceUnavailable" => "Der Audio-Endpunkt ist nicht verfügbar. Audiogeräte und Einstellungen prüfen.",
    "NotRegistered" => "Das SIP-Konto ist nicht angemeldet.",
    "Busy" => "Es ist bereits ein Gespräch aktiv.",
    "CallNotActive" => "Dieses Gespräch ist nicht mehr aktiv.",
    "HoldRejected" => "Die Gegenstelle hat Halten/Fortsetzen abgelehnt.",
    "HoldTimedOut" => "Halten/Fortsetzen wurde von der Gegenstelle nicht bestätigt.",
    "TransferRejected" => "Die Gegenstelle hat die Weiterleitung nicht angenommen.",
    "InvalidDtmf" => "Erlaubte DTMF-Zeichen sind 0–9, *, # und A–D.",
    "InvalidDestination" => "Eine gültige Rufnummer oder SIP-Adresse eingeben.",
    _ => "Die Telefonie-Aktion ist fehlgeschlagen. Konto und Verbindung prüfen."
})
{
    public string Code { get; } = code;
}
