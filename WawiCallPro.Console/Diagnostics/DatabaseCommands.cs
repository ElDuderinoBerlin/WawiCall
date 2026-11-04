using System.Globalization;
using WawiCallPro.ConsoleApp.Presentation;
using WawiCallPro.Database;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.ConsoleApp.Diagnostics;

internal static class DatabaseCommands
{
    internal static async Task<ConsoleTable> ExecuteAsync(ConsoleCommand command, ICommunicationDatabase database, CancellationToken token)
    {
        var progress = new DatabaseConsoleProgress();
        DatabaseMigrationStatus status;
        switch (command.Name)
        {
            case "--db-status": status = await database.GetStatusAsync(token); break;
            case "--db-init": status = await database.CreateAndMigrateAsync(progress, token); break;
            case "--db-migrate": status = await database.MigrateAsync(progress: progress, cancellationToken: token); break;
            case "--db-approve-empty": status = await database.MigrateAsync(approveEmpty: true, progress: progress, cancellationToken: token); break;
            case "--db-backup":
                await database.BackupSqliteAsync(command.File!, token);
                return new("SQLite-Sicherung", "Merkmal", "Wert", [new("Sicherung", "Geprüft und als neue Datei veröffentlicht; Aufnahmen separat sichern.")]);
            case "--db-restore":
                await database.RestoreSqliteNewAsync(command.File!, token);
                status = await database.GetStatusAsync(token); break;
            default: throw new DiagnosticException("Unbekannter Datenbankbefehl.");
        }
        return Status(status);
    }

    internal static ConsoleTable Status(DatabaseMigrationStatus status) => new("Kommunikationsdatenbank", "Merkmal", "Wert",
    [
        new("Provider", status.Type.ToString()), new("Schemastatus", status.State.ToString()),
        new("Verfügbare Version", status.LatestAvailableVersion?.ToString(CultureInfo.InvariantCulture) ?? "–"),
        new("Angewendet", string.Join(", ", status.AppliedVersions)), new("Ausstehend", string.Join(", ", status.PendingVersions)),
        new("Fehlercode", status.Error?.ToString() ?? "–"),
        new("Lexware", "Unabhängige Kommunikationsdatenbank; keine Anmeldung an der Warenwirtschaft erforderlich.")
    ]);

    private sealed class DatabaseConsoleProgress : IProgress<DatabaseMigrationProgress>
    {
        public void Report(DatabaseMigrationProgress value) => Console.Error.WriteLine(FormattableString.Invariant(
            $"Datenbank: {value.Phase}; Version {value.Version?.ToString(CultureInfo.InvariantCulture) ?? "–"}; Schritte {value.CompletedSteps}/{value.TotalSteps}"));
    }
}
