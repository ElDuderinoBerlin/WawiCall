using WawiCallPro.ConsoleApp.Presentation;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.ConsoleApp.Diagnostics;

/// <summary>Delegates an explicit export to the shared service without disclosing paths or raw failures.</summary>
internal static class RecordingExportCommands
{
    internal static async Task<ConsoleTable> ExecuteAsync(ConsoleCommand command, IRecordingExport exporter, CancellationToken token)
    {
        if (!command.UsesRecordingExport || command.RecordingArtifactId is not { } id || command.File is null)
            throw new DiagnosticException("Unvollständiger Exportaufruf. --help zeigt die erforderlichen Angaben.");
        try { await exporter.ExportAsync(id, command.File, token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            throw new DiagnosticException("Die Aufnahme konnte nicht exportiert werden. Lokale Zuordnung, bestätigte Originaldatei, offene Abschlussaufträge und ein neues beschreibbares Ziel mit passender Dateiendung prüfen. Vorhandene Dateien werden nicht überschrieben.");
        }
        // Publication may already have succeeded when cancellation arrives; retain that result.
        return new("Aufnahmeexport abgeschlossen", "Merkmal", "Ergebnis",
            [new("Export", "Bestätigtes Original als neue Datei veröffentlicht; keine Konvertierung und kein Überschreiben.")]);
    }
}
