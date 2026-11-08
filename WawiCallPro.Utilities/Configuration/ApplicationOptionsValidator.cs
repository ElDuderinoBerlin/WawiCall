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
        if (options.User is null) issues.Add(new("user", "Benutzereinstellungen dürfen nicht null sein."));
        ValidateErp(options.Erp, issues);
        ValidateDevices(options.Devices, issues);
        ValidateDatabase(options.Database, issues);
        ValidateCompanies(options, issues);
        if (issues.Count != 0) throw new ConfigurationException(issues.ToImmutable());
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
        if (!IsValidUsername(account.Username))
            issues.Add(new("sipAccount.username", "Ein Benutzername ohne Leerraum und Steuerzeichen ist erforderlich."));
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

    private void ValidateErp(ErpConnectionSettings? erp, ImmutableArray<ConfigurationIssue>.Builder issues)
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
                issues.Add(new(field, clientKey ? "Ein ausdrücklich freigegebener Client-Key im GUID-Format mit geschweiften Klammern ist erforderlich." : "Das Kennwort darf nicht leer sein."));
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
        if (!IsValidDeviceId(devices.CaptureDeviceId) || !IsValidDeviceId(devices.PlaybackDeviceId) || !IsValidDeviceId(devices.RingDeviceId))
            issues.Add(new("devices", "Gerätekennungen müssen null oder nichtleere Kennungen ohne Steuerzeichen sein."));
        if (devices.PlaybackVolumePercent is < 0 or > 100)
            issues.Add(new("devices.playbackVolumePercent", "Die Lautstärke muss zwischen 0 und 100 liegen."));
        if (devices.RingVolumePercent is < 0 or > 100)
            issues.Add(new("devices.ringVolumePercent", "Die Lautstärke muss zwischen 0 und 100 liegen."));
    }

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
            if (fileName is not null && (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 240 ||
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
