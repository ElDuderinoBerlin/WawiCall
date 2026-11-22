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
        _ => null
    };
}
