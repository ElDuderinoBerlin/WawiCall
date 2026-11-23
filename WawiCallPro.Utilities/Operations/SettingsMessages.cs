namespace WawiCallPro.Utilities.Operations;

/// <summary>Closed, display-safe settings notices shared by UI and file logging.</summary>
public static class SettingsMessages
{
    public static string? Get(OperationEvent value) => value switch
    {
        OperationEvent.SettingsWaiting => "Einstellungen werden bearbeitet …",
        OperationEvent.SettingsLoaded => "Gespeicherte Einstellungen geladen.",
        OperationEvent.SettingsDefaultsLoaded => "Vorgaben geladen. Es wurden noch keine Einstellungen gespeichert.",
        OperationEvent.SettingsSaved => "Einstellungen gespeichert.",
        OperationEvent.SettingsCancelled => "Einstellungsvorgang abgebrochen. Gespeicherte Werte bleiben erhalten.",
        OperationEvent.SettingsFailed => "Die Einstellungsaktion ist fehlgeschlagen. Einstellungen und Erreichbarkeit prüfen.",
        OperationEvent.SettingsValidationFailed => "Einstellungen konnten nicht übernommen werden. Eingaben und Plugin-Zugang prüfen.",
        OperationEvent.SettingsCompaniesLoading => "Mandanten werden asynchron geladen …",
        OperationEvent.SettingsCompaniesLoaded => "Mandanten geladen. Mehrere Mandanten für Telefon- und Mailzuordnung auswählen und speichern.",
        OperationEvent.SettingsCompaniesEmpty => "Keine freigegebenen Mandanten verfügbar.",
        OperationEvent.SettingsCompaniesUnavailable => "Mindestens ein gewählter Mandant ist nicht mehr verfügbar. Zugang und Auswahl prüfen.",
        OperationEvent.SettingsCompaniesFailed => "Mandanten konnten nicht geladen werden. Lexware-Zugang und Verbindung prüfen.",
        OperationEvent.SettingsSelectionChanged => "Mandantenauswahl im Entwurf geändert. Mit Speichern übernehmen.",
        OperationEvent.SettingsCacheUpdating => "Telefon-Cache wird aktualisiert …",
        OperationEvent.SettingsCacheUpdated => "Telefon-Cacheaktualisierung abgeschlossen.",
        OperationEvent.SettingsCacheCancelled => "Cacheaktualisierung abgebrochen. Bereits abgeschlossene Mandanten bleiben gespeichert.",
        OperationEvent.SettingsImported => "Einstellungen importiert und gespeichert. Lokale Verbindungen bleiben erhalten.",
        OperationEvent.SettingsExported => "Gespeicherte Einstellungen als JSON exportiert. Zugangsdaten sind nicht enthalten.",
        OperationEvent.SettingsDatabaseConfigured => "Datenbankverbindung gespeichert. Die Anwendung für die Übernahme neu starten.",
        OperationEvent.SettingsRestartRequired => "Einstellungen gespeichert. Die geänderten Startoptionen werden nach einem Neustart wirksam.",
        OperationEvent.SettingsRuleLoaded => "Mandantenbezogene Abgleichregel geladen.",
        OperationEvent.SettingsRuleSaved => "Mandantenbezogene Abgleichregel gespeichert.",
        OperationEvent.SettingsCompaniesConfigurationRequired => "Mandantenladung fehlgeschlagen: Zugang fehlt oder ist ungültig. API-Benutzer, Kennwort und Client-Key prüfen.",
        OperationEvent.SettingsCompaniesSourceAccess => "Mandantenladung fehlgeschlagen: Plugin-Sitzung oder Installationsfreigabe ist nicht mehr gültig. Aktives Plugin prüfen.",
        OperationEvent.SettingsCompaniesInstallationUnavailable => "Mandantenladung fehlgeschlagen: Lexware-Pro-Installation konnte nicht ermittelt werden.",
        OperationEvent.SettingsCompaniesAuthenticationFailed => "Mandantenladung fehlgeschlagen: API-Anmeldung wurde abgelehnt. Benutzer und Kennwort prüfen.",
        OperationEvent.SettingsCompaniesClientKeyMissing => "Mandantenladung fehlgeschlagen: API meldet: Client-Key fehlt (2001).",
        OperationEvent.SettingsCompaniesClientKeyUnknown => "Mandantenladung fehlgeschlagen: API meldet: unbekannter Client-Key (2002). Schreibweise und äußere Leerzeichen des Schlüssels prüfen.",
        OperationEvent.SettingsCompaniesClientKeyUnauthorized => "Mandantenladung fehlgeschlagen: API meldet: Client-Key ist für diesen Zugriff nicht autorisiert (2003/3003).",
        OperationEvent.SettingsCompaniesPermissionDenied => "Mandantenladung fehlgeschlagen: API meldet: Berechtigung für den Zugriff fehlt (1010/2004).",
        OperationEvent.SettingsCompaniesClientConfigurationExpired => "Mandantenladung fehlgeschlagen: API meldet: Client-Key-Konfiguration ist abgelaufen (2005).",
        OperationEvent.SettingsCompaniesClientConfigurationMissing => "Mandantenladung fehlgeschlagen: API meldet: serverseitige Client-Key-Konfigurationsdatei fehlt (2006).",
        OperationEvent.SettingsCompaniesClientConfigurationInvalid => "Mandantenladung fehlgeschlagen: API meldet: serverseitige Client-Key-Konfiguration ist nicht lesbar (2007).",
        OperationEvent.SettingsCompaniesApiUnavailable => "Mandantenladung fehlgeschlagen: API-Dienst ist nicht erreichbar oder meldet einen HTTP-/Dienstfehler.",
        OperationEvent.SettingsCompaniesTimeout => "Mandantenladung fehlgeschlagen: Zeitlimit beim API-Aufruf überschritten.",
        OperationEvent.SettingsCompaniesInvalidResponse => "Mandantenladung fehlgeschlagen: API-Antwort oder Mandantenkatalog hat ein ungültiges Format oder enthält einen nicht zugeordneten API-Fehler.",
        OperationEvent.SettingsCompaniesCompanyNames => "Mandantenladung fehlgeschlagen: Lokale Lexware-Mandantennamen konnten nicht gelesen werden.",
        _ => null
    };
}
