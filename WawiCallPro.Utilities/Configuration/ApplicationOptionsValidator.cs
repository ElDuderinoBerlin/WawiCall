using System.Collections.Immutable;
using WawiCallPro.Utilities.Security;
using System.Globalization;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Database;

namespace WawiCallPro.Utilities.Configuration;

public sealed class ApplicationOptionsValidator(ISecretProtector secrets)
{
    public void Validate(ApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var issues = ImmutableArray.CreateBuilder<ConfigurationIssue>();
        if (options.SipAccount is { } account) ValidateSip(account, issues);
        ValidateSipProfiles(options, issues);
        if (options.User is null) issues.Add(new("user", "Benutzereinstellungen dürfen nicht null sein."));
        if (options.User is { } user)
        {
            if (string.IsNullOrWhiteSpace(user.FontFamily) || user.FontFamily.Length > 128 || user.FontFamily.Any(char.IsControl)
                || !float.IsFinite(user.FontSize) || user.FontSize is < 6 or > 48)
                issues.Add(new("user.font", "Schriftfamilie und Schriftgröße (6 bis 48 Punkt) prüfen."));
            if (!Enum.IsDefined(user.LogLevel)) issues.Add(new("user.logLevel", "Ein gültiges Log-Level auswählen."));
            if (user.LogDirectory is { } directory && !IsValidLogDirectory(directory))
                issues.Add(new("user.logDirectory", "Für das Log-Verzeichnis einen absoluten lokalen Ordnerpfad angeben."));
            if (!GlobalHotkeySettings.IsSupported(user.ShowWindowHotkey) || !GlobalHotkeySettings.IsSupported(user.DialHotkey))
                issues.Add(new("user.hotkeys", "Erlaubt sind deaktivierte Tastenkürzel oder Strg+Alt+F1 bis Strg+Alt+F11."));
            if (user.ShowWindowHotkey is not null && user.ShowWindowHotkey == user.DialHotkey)
                issues.Add(new("user.hotkeys", "Für die beiden Aktionen unterschiedliche Tastenkürzel auswählen."));
        }
        if (options.User?.ActivePluginId is { } pluginId && (pluginId.Length is 0 or > 160
            || pluginId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('.' or '-' or '_'))
            || pluginId is "." or ".."))
            issues.Add(new("user.activePluginId", "Die Plugin-Kennung ist ungültig."));
        ValidateErp(options.Erp, issues, options.ProductClientKeyAvailable || options.ProductClientKeyEdit is not null);
        ValidateDevices(options.Devices, issues);
        if (options.RecordingOutput is { } output &&
            (!Enum.IsDefined(output.Format) || (output.Format == Database.Contracts.RecordingArtifactFormat.Wave) != (output.SinceUtc is null) ||
             output.SinceUtc is { Offset: var offset } && offset != TimeSpan.Zero))
            issues.Add(new("recordingOutput", "Das Aufnahmeformat oder der Startzeitpunkt ist ungültig."));
        if (options.AutomaticMp3 is null || options.AutomaticMp3.Enabled && options.AutomaticMp3.SinceUtc is null ||
            options.AutomaticMp3?.SinceUtc is { } since && since.Offset != TimeSpan.Zero)
            issues.Add(new("automaticMp3", "Automatische MP3-Konvertierung benötigt einen gültigen Aktivierungszeitpunkt in UTC."));
        try
        {
            if (options.RecordingStorage.Directory is { } directory) Operations.RecordingArchive.NormalizeDirectory(directory);
            if (options.RecordingStorage.PendingDirectory is { } pending) Operations.RecordingArchive.NormalizeDirectory(pending);
            RecordingFileName.Format(options.RecordingStorage.FileNamePattern, DateTimeOffset.UnixEpoch, Interfaces.Telephony.CallDirection.Outgoing, Guid.Empty,
                new(Interfaces.Telephony.NumberPresentation.Available, new string('9', 40)));
        }
        catch (FormatException error) { issues.Add(new("recordingStorage", error.Message)); }
        ValidateDatabase(options.Database, issues);
        ValidateCompanies(options, issues);
        ValidateMail(options.MailAccounts, issues);
        if (options.MailCache is null || options.MailCache.Days is < 1 or > 36500)
            issues.Add(new("mailCache.days", "Für den Mailcache 1 bis 36500 Tage auswählen."));
        if (options.CallerLineAssignment is { } mapping && (mapping.Company is null || mapping.Line is null
            || !SafeLineText(mapping.DisplayName) || !SafeLineText(mapping.Line.TelephonySystemId) || !SafeLineText(mapping.Line.LineId)
            || mapping.Line.DefaultRegion is not null && !IsValidRegion(mapping.Line.DefaultRegion)
            || mapping.Line.LocalAreaCode is { } area && (area.Length is < 1 or > 16 || !area.All(char.IsAsciiDigit))))
            issues.Add(new("callerLineAssignment", "Die Leitungszuordnung muss eine Firma und einen vollständigen, gültigen Leitungskontext enthalten."));
        if (issues.Count != 0) throw new ConfigurationException(issues.ToImmutable());
    }

    private static bool SafeLineText(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 512 && !value.Any(char.IsControl);

    private void ValidateSipProfiles(ApplicationOptions options, ImmutableArray<ConfigurationIssue>.Builder issues)
    {
        var profiles = options.SipProfiles;
        if (profiles.IsDefault || profiles.Length > SipProfileCatalog.MaximumProfiles || profiles.Any(profile => profile is null))
        { issues.Add(new("sipProfiles", "Höchstens 16 gültige SIP-Profile speichern.")); return; }
        if (profiles.IsEmpty)
        {
            if (options.ActiveSipProfileId is not null) issues.Add(new("sipProfiles", "Das ausgewählte SIP-Profil fehlt."));
            return;
        }
        if (profiles.Select(profile => profile.Id).Distinct().Count() != profiles.Length
            || profiles.Any(profile => profile.Id == Guid.Empty || string.IsNullOrWhiteSpace(profile.Name)
                || profile.Name != profile.Name.Trim() || profile.Name.Length > 80 || profile.Name.Any(char.IsControl))
            || profiles.Select(profile => profile.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != profiles.Length)
            issues.Add(new("sipProfiles", "Unterschiedliche Profilnamen mit 1 bis 80 Zeichen und eindeutige Kennungen verwenden."));
        var active = profiles.FirstOrDefault(profile => profile.Id == options.ActiveSipProfileId);
        if (active is null || active.Account is not null || active.LineAssignment is not null || options.SipAccount is null)
            issues.Add(new("sipProfiles", "Ein eingerichtetes SIP-Profil auswählen. Das aktive Konto wird ausschließlich im gemeinsamen Kontofeld gespeichert."));
        foreach (var profile in profiles.Where(profile => profile.Id != options.ActiveSipProfileId))
        {
            if (profile.Account is null) issues.Add(new("sipProfiles", "Alle gespeicherten SIP-Profile müssen vollständig eingerichtet sein."));
            else ValidateSip(profile.Account, issues);
            if (profile.LineAssignment is { } mapping && (mapping.Company is null || mapping.Line is null
                || !SafeLineText(mapping.DisplayName) || !SafeLineText(mapping.Line.TelephonySystemId) || !SafeLineText(mapping.Line.LineId)
                || mapping.Line.DefaultRegion is not null && !IsValidRegion(mapping.Line.DefaultRegion)
                || mapping.Line.LocalAreaCode is { } area && (area.Length is < 1 or > 16 || !area.All(char.IsAsciiDigit))))
                issues.Add(new("sipProfiles", "Die Leitungszuordnung eines SIP-Profils ist ungültig."));
        }
    }

    private void ValidateMail(ImmutableArray<MailAccountSettings> accounts, ImmutableArray<ConfigurationIssue>.Builder issues)
    {
        if (accounts.IsDefault || accounts.Length > 32 || accounts.Any(account => account is null) || accounts.Select(account => account.Id).Distinct().Count() != accounts.Length)
        { issues.Add(new("mailAccounts", "Höchstens 32 unterschiedliche Postfächer konfigurieren.")); return; }
        foreach (var account in accounts)
        {
            if (account.Id == Guid.Empty || !Enum.IsDefined(account.Provider) || !SafeLineText(account.DisplayName) || account.ImportDays is < 0 or > 36500 ||
                account.MaximumMessages is < 1 or > 100000 || account.FolderIds.IsDefault || account.FolderIds.Length > 1024 ||
                account.FolderIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 4096 || id.Any(char.IsControl)) || account.FolderIds.Distinct().Count() != account.FolderIds.Length ||
                account.RemoteIdentity is { } identity && (identity.Length > 1024 || identity.Any(char.IsControl)) ||
                account.OutlookStoreId is { } store && (store.Length > 4096 || store.Any(char.IsControl)) ||
                account.FolderNames is null || account.FolderNames.Count > 1024 || account.FolderNames.Any(pair => !account.FolderIds.Contains(pair.Key) || !SafeLineText(pair.Value)))
                issues.Add(new("mailAccounts", "Postfachname, Kennungen, Zeitraum oder Mengenbegrenzung prüfen."));
            if (!account.Enabled || account.Provider == WawiCallPro.Interfaces.Office.MailProviderKind.OutlookDesktop) continue;
            if (account.Provider == WawiCallPro.Interfaces.Office.MailProviderKind.Microsoft365 &&
                (!Guid.TryParse(account.ClientId, out var client) || client == Guid.Empty ||
                 account.Tenant is not ("common" or "organizations" or "consumers") && !Guid.TryParse(account.Tenant, out _)))
                issues.Add(new("mailAccounts", "Für Microsoft 365 eine gültige Client-ID und einen Mandanten (GUID/common/organizations/consumers) eintragen."));
            if (account.Provider == WawiCallPro.Interfaces.Office.MailProviderKind.Microsoft365 && account.ApplicationAuthentication)
            {
                if (!Guid.TryParse(account.Tenant, out _) || !System.Net.Mail.MailAddress.TryCreate(account.Mailbox, out var mailbox) || mailbox.Address != account.Mailbox)
                    issues.Add(new("mailAccounts", "Für die Microsoft-App-Anmeldung eine Mandanten-GUID und die genaue Postfachadresse eintragen."));
                try { using var value = secrets.Unprotect(account.MicrosoftClientSecret ?? throw new SecretProtectionException(), MailAccountSettings.MicrosoftSecretPurpose); if (value.IsEmpty) throw new SecretProtectionException(); }
                catch (SecretProtectionException) { issues.Add(new("mailAccounts", "Das geschützte Microsoft-App-Secret fehlt oder ist beschädigt.")); }
            }
            if (account.Provider == WawiCallPro.Interfaces.Office.MailProviderKind.Gmail)
            {
                if (string.IsNullOrWhiteSpace(account.ClientId) || !account.ClientId.EndsWith(".apps.googleusercontent.com", StringComparison.Ordinal) || account.ClientId.Any(char.IsWhiteSpace))
                    issues.Add(new("mailAccounts", "Für Gmail die Client-ID einer Google-Desktop-App eintragen."));
                try { using var secret = secrets.Unprotect(account.GoogleClientSecret ?? throw new SecretProtectionException(), MailAccountSettings.GoogleSecretPurpose); if (secret.IsEmpty) throw new SecretProtectionException(); }
                catch (SecretProtectionException) { issues.Add(new("mailAccounts", "Die Google-Desktop-Clientdaten fehlen oder können nicht entschlüsselt werden.")); }
            }
        }
    }

    private static bool IsValidLogDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || directory != directory.Trim() || directory.Length > 1024
            || directory.Any(char.IsControl) || directory.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || directory.IndexOfAny(['*', '?']) >= 0
            || !Path.IsPathFullyQualified(directory) || directory.StartsWith(@"\\", StringComparison.Ordinal)
            || directory.AsSpan(2).Contains(':')) return false;
        try { return Path.GetPathRoot(directory)?.Length == 3 && Path.GetFullPath(directory).Length > 3; }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    private void ValidateSip(SipAccountOptions account, ImmutableArray<ConfigurationIssue>.Builder issues)
    {
        if (string.IsNullOrWhiteSpace(account.Server) || account.Server != account.Server.Trim() ||
            Uri.CheckHostName(account.Server) == UriHostNameType.Unknown)
            issues.Add(new("sipAccount.server", "Ein Hostname oder eine IP-Adresse ohne URI und Zugangsdaten ist erforderlich."));
        if (account.Port is < 1 or > 65535)
            issues.Add(new("sipAccount.port", "Der Port muss zwischen 1 und 65535 liegen."));
        if (!Enum.IsDefined(account.Transport))
            issues.Add(new("sipAccount.transport", "Erlaubt sind Udp, Tcp und Tls."));
        var connection = account.Connection;
        if (connection is null || !System.Net.IPAddress.TryParse(connection.BindAddress, out var bind)
            || bind.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
            || connection.LocalPort is < 1 or > 65535
            || connection.RegistrationExpirySeconds is < 60 or > 86400
            || connection.RegistrationTimeoutSeconds is < 1 or > 120
            || connection.RetryIntervalSeconds is < 1 or > 3600
            || connection.RingTimeoutSeconds is < 5 or > 600)
            issues.Add(new("sipAccount.connection", "Erweiterte SIP-Einstellungen prüfen: IPv4-Bind-Adresse, lokaler Port 1–65535, Registrierung 60–86400 s, Timeout 1–120 s, Wiederholung 1–3600 s und Klingelzeit 5–600 s."));
        if (!IsValidUsername(account.Username))
            issues.Add(new("sipAccount.username", "Ein Benutzername ohne Leerraum und Steuerzeichen ist erforderlich."));
        if (connection?.KeepAliveIntervalSeconds is < 0 or > 3600)
            issues.Add(new("sipAccount.connection.keepAliveIntervalSeconds", "Das SIP-Keep-Alive-Intervall muss zwischen 0 und 3600 Sekunden liegen (0 = aus)."));
        if (connection?.CodecOrder is { } codecOrder && !SipCodecOrders.Choices.Contains(codecOrder))
            issues.Add(new("sipAccount.connection.codecOrder", "Eine gültige Reihenfolge aus PCMA, PCMU und G.722 auswählen."));
        if (connection is not null && !Enum.IsDefined(connection.DtmfMethod))
            issues.Add(new("sipAccount.connection.dtmfMethod", "Als DTMF-Methode RTP oder SIP INFO auswählen."));
        if (account.AuthenticationUsername is not null && !IsValidUsername(account.AuthenticationUsername))
            issues.Add(new("sipAccount.authenticationUsername", "Der optionale Authentifizierungsname ist ungültig."));
        if (account.Password is null)
            issues.Add(new("sipAccount.password", "Ein geschütztes Kennwort ist erforderlich."));
        else
        {
            try
            {
                using var password = secrets.Unprotect(account.Password, SipAccountOptions.PasswordPurpose);
                if (password.IsEmpty) issues.Add(new("sipAccount.password", "Das geschützte Kennwort ist leer."));
            }
            catch (SecretProtectionException)
            {
                issues.Add(new("sipAccount.password", "Das geschützte Kennwort ist für dieses Benutzerkonto nicht verfügbar oder beschädigt."));
            }
        }
    }

    private void ValidateErp(ErpConnectionSettings? erp, ImmutableArray<ConfigurationIssue>.Builder issues,
        bool externalClientKey)
    {
        if (erp is null) { issues.Add(new("erp", "Warenwirtschaftseinstellungen dürfen nicht null sein.")); return; }
        foreach (var id in new[] { erp.FinOfficeInstallationId, erp.Pro?.InstallationId })
            if (id is not null && (string.IsNullOrWhiteSpace(id) || id != id.Trim() || id.Length > 200 || id.Any(char.IsControl)))
                issues.Add(new("erp.installationId", "Die optionale Installationskennung ist ungültig."));
        if (erp.Pro is not { } pro) return;
        if (pro.ApiUrl is { } url && (!url.IsAbsoluteUri || url.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment)))
            issues.Add(new("erp.pro.apiUrl", "Eine absolute HTTP-/HTTPS-Adresse ohne Zugangsdaten, Query oder Fragment ist erforderlich."));
        if (!IsValidUsername(pro.Username)) issues.Add(new("erp.pro.username", "Ein gültiger API-Benutzername ist erforderlich."));
        if (pro.TimeoutSeconds is < 1 or > 300 || pro.TokenTimeoutSeconds is < 1 or > 300)
            issues.Add(new("erp.pro.timeout", "Zeitlimits müssen zwischen 1 und 300 Sekunden liegen."));
        ValidateErpSecret(pro.Password, ProErpSettings.PasswordPurpose, false, issues);
        if (pro.ClientKey is not null || !externalClientKey && LexwareClientDefaults.GetProClientKey() is null)
            ValidateErpSecret(pro.ClientKey, ProErpSettings.ClientKeyPurpose, true, issues);
    }

    private void ValidateErpSecret(ProtectedSecret? secret, string purpose, bool clientKey, ImmutableArray<ConfigurationIssue>.Builder issues)
    {
        var field = clientKey ? "erp.pro.clientKey" : "erp.pro.password";
        if (secret is null) { issues.Add(new(field, "Ein geschützter Wert ist erforderlich.")); return; }
        try
        {
            using var value = secrets.Unprotect(secret, purpose);
            if (value.IsEmpty || (clientKey && (!Guid.TryParseExact(value.Reveal(), "B", out var id) || id == Guid.Empty)))
                issues.Add(new(field, clientKey ? "Ein gültiger Client-Key im GUID-Format mit geschweiften Klammern ist erforderlich." : "Das Kennwort darf nicht leer sein."));
        }
        catch (SecretProtectionException) { issues.Add(new(field, "Der geschützte Wert ist für dieses Benutzerkonto nicht verfügbar oder beschädigt.")); }
    }

    private static void ValidateDevices(AudioDeviceSettings? devices, ImmutableArray<ConfigurationIssue>.Builder issues)
    {
        if (devices is null)
        {
            issues.Add(new("devices", "Geräteeinstellungen dürfen nicht null sein."));
            return;
        }
        if (!IsValidDeviceId(devices.CaptureDeviceName) || !IsValidDeviceId(devices.PlaybackDeviceName) || !IsValidDeviceId(devices.RingDeviceName))
            issues.Add(new("devices.names", "Mindestens ein Audiogerätename ist ungültig."));
        if (!IsValidDeviceId(devices.CaptureDeviceId) || !IsValidDeviceId(devices.PlaybackDeviceId) || !IsValidDeviceId(devices.RingDeviceId))
            issues.Add(new("devices", "Gerätekennungen müssen null oder nichtleere Kennungen ohne Steuerzeichen sein."));
        if (devices.WindowsRingtoneFile is { } ringtone && !IsWindowsRingtoneFileName(ringtone))
            issues.Add(new("devices.windowsRingtoneFile", "Der Windows-Klingelton muss ein WAV-Dateiname ohne Pfad sein."));
        if (devices.CustomRingtonePath is { } custom && (!IsLocalRingtonePath(custom) || devices.WindowsRingtoneFile is not null))
            issues.Add(new("devices.customRingtonePath", "Eine lokale WAV-, MP3-, Ogg/Vorbis- oder WMA-Datei oder einen Windows-Klingelton auswählen."));
        if (devices.MicrophoneVolumePercent is < 0 or > 100)
            issues.Add(new("devices.microphoneVolumePercent", "Die Lautstärke muss zwischen 0 und 100 liegen."));
        if (devices.PlaybackVolumePercent is < 0 or > 100)
            issues.Add(new("devices.playbackVolumePercent", "Die Lautstärke muss zwischen 0 und 100 liegen."));
        if (devices.RingVolumePercent is < 0 or > 100)
            issues.Add(new("devices.ringVolumePercent", "Die Lautstärke muss zwischen 0 und 100 liegen."));
        if (devices.CaptureBufferMilliseconds is < 0 or > 500)
            issues.Add(new("devices.captureBufferMilliseconds", "Der Mikrofonpuffer muss zwischen 0 und 500 ms liegen."));
        if (devices.PlaybackBufferMilliseconds is < 0 or > 500)
            issues.Add(new("devices.playbackBufferMilliseconds", "Der Ausgabepuffer muss zwischen 0 und 500 ms liegen."));
        if (!Enum.IsDefined(devices.RecordingReceiveSource))
            issues.Add(new("devices.recordingReceiveSource", "Eine gültige Aufnahmequelle auswählen."));
        if (devices.Announcements is not { } announcements || !IsValidDeviceId(announcements.VoiceId)
            || !IsValidDeviceId(announcements.CaptureDeviceId) || !IsValidDeviceId(announcements.PlaybackDeviceId))
            issues.Add(new("devices.announcements", "Für Ansagen sind nur gültige Stimmen- und Gerätekennungen zulässig."));
        else if (!Enum.IsDefined(announcements.SpeechProvider))
            issues.Add(new("devices.announcements.speechProvider", "Einen gültigen Sprachanbieter für Ansagen auswählen."));
        else
        {
            var cloud = announcements.Cloud;
            if (cloud is null || !ValidSpeechIdentifier(cloud.AzureRegion, 2, 40)
                || (cloud.AzureKey is not null && cloud.AzureRegion is null)
                || !ValidSpeechIdentifier(cloud.GoogleProjectId, 6, 30)
                || (cloud.GoogleCredential is not null && cloud.GoogleProjectId is null))
                issues.Add(new("devices.announcements.cloud", "Für die Cloud-Ansagen eine gültige Azure-Region bzw. Google-Projekt-ID angeben."));
            else if (cloud.AzureMonthlyAllowance is < 0 or > AnnouncementCloudSettings.AzureFreeCharacters
                || cloud.GoogleNeural2MonthlyAllowance is < 0 or > AnnouncementCloudSettings.GoogleFreeCharactersPerVoiceFamily
                || cloud.GoogleChirpMonthlyAllowance is < 0 or > AnnouncementCloudSettings.GoogleFreeCharactersPerVoiceFamily)
                issues.Add(new("devices.announcements.cloud", "Das Monatsbudget für Cloud-Ansagen liegt außerhalb des Freikontingents."));
        }
    }

    private static bool ValidSpeechIdentifier(string? value, int minimum, int maximum) =>
        value is null || (value.Length >= minimum && value.Length <= maximum
            && char.IsAsciiLetterLower(value[0]) && char.IsAsciiLetterOrDigit(value[^1])
            && value.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-'));

    private void ValidateDatabase(CommunicationDatabaseSettings? database, ImmutableArray<ConfigurationIssue>.Builder issues)
    {
        if (database is null || !Enum.IsDefined(database.Provider))
        {
            issues.Add(new("database.provider", "Ein unterstützter Kommunikationsdatenbank-Provider ist erforderlich."));
            return;
        }
        if (database.Provider == CommunicationDatabaseProvider.Sqlite)
        {
            var fileName = database.SqliteFileName;
            var absolute = fileName is not null && Path.IsPathFullyQualified(fileName);
            if (absolute)
            {
                try
                {
                    _ = DatabaseTarget.Sqlite(fileName!);
                    var name = Path.GetFileName(fileName!);
                    if (string.IsNullOrWhiteSpace(name) || name.Length > 240 || name.EndsWith('.') || name.EndsWith(' ') || IsReservedDeviceName(name))
                        issues.Add(new("database.sqliteFileName", "Ein gültiger absoluter SQLite-Dateipfad ist erforderlich."));
                }
                catch (DatabaseOperationException) { issues.Add(new("database.sqliteFileName", "Ein gültiger absoluter SQLite-Dateipfad ist erforderlich.")); }
            }
            if (!absolute && fileName is not null && (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 240 ||
                fileName is "." or ".." || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(':') || fileName.EndsWith('.') || fileName.EndsWith(' ') ||
                IsReservedDeviceName(fileName)))
                issues.Add(new("database.sqliteFileName", "Ein gültiger lokaler Dateiname ohne Verzeichnis ist erforderlich."));
            if (database.ConnectionString is not null)
                issues.Add(new("database.connectionString", "SQLite verwendet einen Dateinamen statt einer Server-Verbindungszeichenfolge."));
            return;
        }
        if (database.SqliteFileName is not null)
            issues.Add(new("database.sqliteFileName", "Ein Server-Provider verwendet keinen SQLite-Dateinamen."));
        if (database.ConnectionString is null)
        {
            issues.Add(new("database.connectionString", "Eine geschützte Verbindungszeichenfolge ist erforderlich."));
            return;
        }
        try
        {
            using var connection = secrets.Unprotect(database.ConnectionString, CommunicationDatabaseSettings.ConnectionStringPurpose);
            if (connection.IsEmpty) issues.Add(new("database.connectionString", "Die geschützte Verbindungszeichenfolge ist leer."));
            else
            {
                var type = database.Provider switch
                {
                    CommunicationDatabaseProvider.SqlServer => DatabaseType.SqlServer,
                    CommunicationDatabaseProvider.MySql => DatabaseType.MySql,
                    _ => DatabaseType.PostgreSql
                };
                _ = DatabaseTarget.Server(type, connection.Reveal());
            }
        }
        catch (SecretProtectionException)
        {
            issues.Add(new("database.connectionString", "Die geschützte Verbindungszeichenfolge ist nicht verfügbar oder beschädigt."));
        }
        catch (DatabaseOperationException)
        {
            issues.Add(new("database.connectionString", "Die Verbindungszeichenfolge passt nicht zum Provider oder bezeichnet kein zulässiges eigenes Datenbankziel."));
        }
    }

    private static void ValidateCompanies(ApplicationOptions options, ImmutableArray<ConfigurationIssue>.Builder issues)
    {
        if (options.Companies.IsDefault || options.Companies.Length > 256)
        {
            issues.Add(new("companies", "Eine initialisierte Liste mit höchstens 256 Mandanteneinstellungen ist erforderlich."));
            return;
        }
        var companies = new HashSet<CompanyKey>();
        foreach (var settings in options.Companies)
        {
            if (settings?.Company is null || !companies.Add(settings.Company))
            {
                issues.Add(new("companies", "Mandantenbezüge müssen vollständig und eindeutig sein."));
                continue;
            }
            if (settings.DisplayName is { } name && (string.IsNullOrWhiteSpace(name) || name.Length > 512 || name.Any(char.IsControl)))
                issues.Add(new("companies.displayName", "Der optionale Mandantenname muss lesbar sein und darf höchstens 512 Zeichen enthalten."));
            if (!IsValidRegion(settings.DefaultPhoneRegion))
                issues.Add(new("companies.defaultPhoneRegion", "Ein gültiger zweistelliger ISO-Ländercode in Großbuchstaben ist erforderlich."));
            if (settings.LocalAreaCode is { } areaCode && (areaCode.Length is < 1 or > 16 || !areaCode.All(char.IsAsciiDigit)))
                issues.Add(new("companies.localAreaCode", "Die Ortsvorwahl muss aus 1 bis 16 Ziffern bestehen."));
        }
    }

    private static bool IsValidRegion(string? region)
    {
        if (region is null || region.Length != 2 || !region.All(character => character is >= 'A' and <= 'Z')) return false;
        try { return new RegionInfo(region).TwoLetterISORegionName == region; }
        catch (ArgumentException) { return false; }
    }

    private static bool IsValidDeviceId(string? id) =>
        id is null || (!string.IsNullOrWhiteSpace(id) && id.Length <= 1024 && !id.Any(char.IsControl));

    private static bool IsWindowsRingtoneFileName(string name) => name.Length is > 4 and <= 255
        && name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
        && !name.Any(char.IsControl) && name.IndexOfAny(['/', '\\', ':']) < 0
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>Accepts fully qualified drive paths to supported local ringtone files.</summary>
    public static bool IsLocalRingtonePath(string path) => path.Length is > 4 and <= 32767
        && Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal)
        && Path.GetExtension(path).ToLowerInvariant() is ".wav" or ".mp3" or ".ogg" or ".wma"
        && !path.Any(char.IsControl) && path.IndexOf(':', 2) < 0;

    private static bool IsReservedDeviceName(string fileName)
    {
        var stem = fileName.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" ||
               (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                stem[3] is >= '1' and <= '9');
    }

    private static bool IsValidUsername(string? username) =>
        !string.IsNullOrWhiteSpace(username) && username.Length <= 256 && !username.Any(character => char.IsWhiteSpace(character) || char.IsControl(character));
}
