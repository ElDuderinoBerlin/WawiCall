using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace WawiCallPro.Database;

public enum DatabaseErrorCode
{
    InvalidTarget,
    AlreadyExists,
    MissingDatabase,
    MissingOrInaccessible,
    AccessDenied,
    ConnectionFailed,
    ProvisioningFailed,
    ForeignDatabase,
    InvalidSchema,
    NewerSchema,
    OwnershipUnverified,
    MigrationDefinitionsMissing,
    MigrationBusy,
    MigrationFailed,
    MigrationVerificationFailed,
    ProgressFailed
}

/// <summary>Fixed operational errors contain neither connection strings nor inner provider exceptions.</summary>
public sealed class DatabaseOperationException : Exception
{
    public DatabaseOperationException(DatabaseErrorCode code) : base(code switch
    {
        DatabaseErrorCode.InvalidTarget => "Die Kommunikationsdatenbank-Konfiguration ist ungültig.",
        DatabaseErrorCode.AlreadyExists => "Die Datenbank besteht bereits und wurde nicht ersetzt.",
        DatabaseErrorCode.MissingDatabase => "Die Kommunikationsdatenbank fehlt. Zuerst ausdrücklich anlegen.",
        DatabaseErrorCode.MissingOrInaccessible => "Die Datenbank fehlt oder ist mit diesen Rechten nicht sichtbar.",
        DatabaseErrorCode.AccessDenied => "Die benötigten Datenbank- oder Dateirechte fehlen.",
        DatabaseErrorCode.ConnectionFailed => "Die Kommunikationsdatenbank konnte nicht gelesen werden.",
        DatabaseErrorCode.ProvisioningFailed => "Die Kommunikationsdatenbank konnte nicht angelegt werden.",
        DatabaseErrorCode.ForeignDatabase => "Das Ziel ist keine ausgewiesene WawiCallPro-Kommunikationsdatenbank.",
        DatabaseErrorCode.InvalidSchema => "Die Datenbank-Metadaten sind ungültig oder widersprüchlich.",
        DatabaseErrorCode.NewerSchema => "Das Datenbankschema ist neuer als die verfügbaren Migrationen.",
        DatabaseErrorCode.OwnershipUnverified => "Die leere Serverdatenbank benötigt einen Nachweis ihrer ausdrücklichen Neuanlage.",
        DatabaseErrorCode.MigrationDefinitionsMissing => "Es sind noch keine Kommunikationsdatenbank-Migrationen eingebunden.",
        DatabaseErrorCode.MigrationBusy => "Dieser Migrationsdienst führt bereits eine Aktualisierung aus.",
        DatabaseErrorCode.MigrationFailed => "Die Datenbankmigration ist fehlgeschlagen. Den Schemastatus erneut prüfen.",
        DatabaseErrorCode.MigrationVerificationFailed => "Die Migration wurde nicht im erwarteten Schemastand bestätigt.",
        DatabaseErrorCode.ProgressFailed => "Der Datenbankfortschritt konnte nicht gemeldet werden.",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    }) => Code = code;

    public DatabaseErrorCode Code { get; }

    internal static DatabaseErrorCode Classify(Exception exception) => exception switch
    {
        UnauthorizedAccessException => DatabaseErrorCode.AccessDenied,
        SqliteException { SqliteErrorCode: 11 or 26 } => DatabaseErrorCode.InvalidSchema,
        SqliteException { SqliteErrorCode: 23 } => DatabaseErrorCode.AccessDenied,
        SqlException { Number: 229 or 262 or 916 } => DatabaseErrorCode.AccessDenied,
        PostgresException { SqlState: "42501" } => DatabaseErrorCode.AccessDenied,
        MySqlException { Number: 1044 or 1045 or 1142 } => DatabaseErrorCode.AccessDenied,
        _ => DatabaseErrorCode.ConnectionFailed
    };
}
