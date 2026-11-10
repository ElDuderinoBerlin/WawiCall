using System.Globalization;
using WawiCallPro.ConsoleApp.Presentation;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.ConsoleApp.Diagnostics;

internal static class DiagnosticTables
{
    internal static ConsoleTable Status(HostDiagnosticSnapshot status) => new("WawiCallPro – Einrichtungsstatus", "Merkmal", "Wert",
    [
        new("Anwendung", status.Application),
        new("Produktlinie", status.ProductLine),
        new("Host", status.HostStarted ? "Gestartet" : "Nicht gestartet"),
        new("Konfigurationsdatei", status.ConfigurationFound ? "Geladen" : "Nicht vorhanden; Vorgaben aktiv"),
        new("Dateiformat", status.ConfigurationFound ? status.SettingsFormatVersion.ToString(CultureInfo.InvariantCulture) : "Keine Datei"),
        new("Formatübernahme", status.SettingsUpgradeRequired ? "Beim nächsten ausdrücklichen Speichern" : "Nicht erforderlich"),
        new("SIP-Einrichtung", status.ConnectionConfigured ? "Vollständig geprüft" : "Nicht eingerichtet"),
        new("Wawi-Einrichtung", status.ProductLine == "FinOffice" ? "Automatische Installationserkennung; Quellzugriff nicht geprüft" : status.ErpConnectionConfigured ? "Konfiguration vorhanden; Quellzugriff nicht geprüft" : "Nicht eingerichtet; --erp-configure verwenden"),
        new("SIP-Verbindung", "Nicht verbunden"),
        new("Mandant", status.CompanySelected ? "Ausgewählt" : "Nicht ausgewählt"),
        new("Warenwirtschaftsplugin", status.PluginLoaded ? "Geladen und konfiguriert" : "Nicht geladen"),
        new("Datenbank", "Nicht geprüft; --db-status liest den tatsächlichen Schemastatus"),
        new("Dispatcher", status.Dispatcher),
        new("Protokoll", "Bereinigtes JSONL; --logs zeigt abgeschlossene Einträge"),
        new("Bedienung", "N nächste, P vorige, G Seite, Q Ende; jeweils Enter. ? bedeutet ungültige Eingabe.")
    ]);

    internal static ConsoleTable Paths(ApplicationPaths paths, ConfigurationLocation configuration) => new("WawiCallPro – Betriebspfade", "Bereich", "Pfad",
    [
        new("Stammverzeichnis", paths.RootDirectory),
        new("Konfiguration", configuration.FilePath),
        new("Protokolle", paths.LogsDirectory),
        new("Aufnahmen", paths.RecordingsDirectory),
        new("Backups", paths.BackupsDirectory)
    ]);

    internal static ConsoleTable Logs(LogSnapshot snapshot) => new(
        $"Betriebsprotokoll – {snapshot.Entries.Count} Einträge; {snapshot.IncompleteTails} unvollständige Dateiende(n) im Lesesnapshot",
        "UTC-Zeit", "Ereignis und Lauf", snapshot.Entries.Select(entry => new ConsoleRow(
            entry.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
            $"{entry.Level} | {entry.Event} | ID {entry.EventId}\nTyp: {entry.ExceptionType ?? "–"}\nPID {entry.ProcessId} | Lauf {entry.RunId:D}")).ToArray());

    internal static ConsoleTable Help() => new("WawiCallPro – Diagnosekonsole", "Aufruf", "Ausgabe",
    [
        new("--status (Vorgabe)", "Geprüfter Einrichtungsstatus; keine Netzwerkverbindung"),
        new("--paths", "Betriebspfade des aktuellen Benutzers und der Produktlinie"),
        new("--logs", "Snapshot aller abgeschlossenen JSONL-Einträge; offene Dateiende(n) werden gezählt"),
        new("--diagnostics", "Ein JSON-Dokument nach erfolgreichem Host-Stop; keine Zugangsdaten"),
        new("--help", "Diese Hilfe"),
        new("--db-status", "Kommunikationsdatenbank nur lesen; keine Anlage oder Migration"),
        new("--db-init", "Neue Kommunikationsdatenbank ausdrücklich anlegen, migrieren und Arbeitsplatz-GUID einrichten"),
        new("--db-migrate", "Vorhandene eigene Datenbank aktualisieren; vorhandene Sicherung vorher prüfen"),
        new("--db-approve-empty", "Vorhandenes leeres DBA-Ziel ausdrücklich freigeben und migrieren"),
        new("--db-backup <absoluter Pfad>", "SQLite-Sicherung in eine neue Datei; WAL wird konsistent berücksichtigt"),
        new("--db-restore <absoluter Pfad>", "SQLite-Sicherung in das konfigurierte, noch nicht existierende Ziel wiederherstellen"),
        new("--erp-configure", "Warenwirtschaft lokal einrichten; verdeckte Kennwort-/Client-Key-Eingabe, keine Umleitung; Neustart erforderlich"),
        new("--phone-rule <Installation> <Firma>", "Gespeicherte Stammnummernregel und Schreibversion einer bereits registrierten Cachefirma lesen"),
        new("--phone-rule <Installation> <Firma> <on|off> <N> <Version|new>", "Stammnummernabgleich mit 1–15 Anschlussziffern ausdrücklich ändern; Version schützt vor konkurrierenden Änderungen"),
        new("--phone-match <Installation> <Firma> <+Rufnummer>", "Lokalen Firmenindex aufbauen und alle Voll-/Stamm-/Blocktreffer anzeigen; keine automatische Auswahl"),
        new("--phone-search <+Rufnummer>", "Gemeinsamen Index aller gespeicherten, berechtigten Suchfirmen prüfen/aufbauen und alle Treffer ausgeben"),
        new("--workstation-init", "Lokale Arbeitsplatz-GUID einmal anlegen; bestehende Kennung erhalten"),
        new("N/P/G/Q + Enter", "Nächste/vorige Seite, Seitenzahl eingeben, Ausgabe beenden. ? = ungültige Eingabe."),
        new("Umleitung", "Bei umgeleiteter Ein- oder Ausgabe alle Zeilen ohne Tastaturabfrage; Ctrl+C bricht interaktiv ab.")
    ]);
}
