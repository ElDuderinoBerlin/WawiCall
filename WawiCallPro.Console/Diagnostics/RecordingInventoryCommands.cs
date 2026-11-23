using System.Globalization;
using WawiCallPro.ConsoleApp.Presentation;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.ConsoleApp.Diagnostics;

/// <summary>Explicit read-only diagnostics. Relative review paths require a separate user opt-in.</summary>
internal static class RecordingInventoryCommands
{
    internal static async Task<ConsoleTable> CopyWaveAsync(string relative, string destination, IRecordingReviewExport export, CancellationToken token)
    {
        try { await export.CopyWaveAsync(relative, destination, token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            throw new DiagnosticException("Die WAV-Datei konnte nicht gerettet werden. Bitte einen relativen Prüfpfad aus der Aufnahmeinventur und einen neuen absoluten WAV-Zielpfad außerhalb des App-Ordners wählen.");
        }
        return new("Aufnahme gerettet", "Merkmal", "Ergebnis", [new("WAV", "Eine separate Kopie wurde erstellt. Die Quelldatei und ihre Zuordnung wurden nicht verändert.")]);
    }

    internal static async Task<ConsoleTable> ExecuteAsync(ConsoleCommand command, IRecordingDirectoryInventory inventory, CancellationToken token)
    {
        RecordingDirectoryInventoryResult result;
        try { result = await inventory.ReadAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            throw new DiagnosticException("Die Aufnahmeinventur konnte nicht vollständig durchgeführt werden. Vorhandene Kommunikationsdatenbank, Arbeitsplatzkennung und Ordnerzugriff prüfen. Dateien wurden nicht verändert.");
        }
        token.ThrowIfCancellationRequested();
        var rows = new List<ConsoleRow>
        {
            new("Registrierte Dateien", result.Registered.ToString(CultureInfo.InvariantCulture)),
            new("Ohne lokale Referenz", result.WithoutLocalReference.ToString(CultureInfo.InvariantCulture)),
            new("Temporär ohne Referenz", result.TemporaryWithoutLocalReference.ToString(CultureInfo.InvariantCulture)),
            new("Links übersprungen", result.LinksSkipped.ToString(CultureInfo.InvariantCulture)),
            new("Einordnung", "Beobachteter Zustand, kein Verwaisungsnachweis. Dateien können zu laufenden Aufnahmen oder anderen Arbeitsplätzen gehören. Keine Übernahme oder Löschung."),
            new("Prüfpfade", command.ShowRecordingPaths ? "Relative Pfade auf ausdrücklichen Aufruf; keine automatische Protokollierung." : "Ausgeblendet; --recordings-inventory --details zeigt relative Pfade.")
        };
        if (command.ShowRecordingPaths)
        {
            foreach (var entry in result.Review.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                var category = entry.IsLink ? "Link übersprungen" : entry.AwaitingConfirmation ? "WAV-Abschluss ausstehend" :
                    Path.GetExtension(entry.RelativePath).Equals(".tmp", StringComparison.OrdinalIgnoreCase)
                        ? "Temporär ohne Referenz" : "Ohne lokale Referenz";
                rows.Add(new(category, entry.RelativePath));
            }
            if (result.Review.IsEmpty) rows.Add(new("Prüfliste", "Keine Einträge im beobachteten Bestand."));
        }
        return new("Aufnahmeinventur – nur lesend", "Merkmal / Prüfgrund", "Anzahl / Einordnung / relativer Pfad", rows);
    }
}
