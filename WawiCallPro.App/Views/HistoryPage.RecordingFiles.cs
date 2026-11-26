using System.IO;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.App.Views;

public partial class HistoryPage
{
    private void UpdateRecordingActions() => recordingActionsLayout.Visible =
        !_stopping && _recordingFiles is not null && view.GetFocusedRow() is HistoryRow row && _playable.Contains(row.Id);

    private async void ExportRecording_Click(object? sender, EventArgs e) => await RunRecordingActionAsync(async (files, callId, token) =>
    {
        var choices = await files.GetExportChoicesAsync(callId, token);
        if (choices.Count == 0) throw new HistoryRecordingFileException("Für dieses Gespräch ist keine bestätigte Aufnahme verfügbar.");
        using var dialog = new XtraSaveFileDialog
        {
            Title = "Gesprächsaufnahme exportieren",
            FileName = "Gesprächsaufnahme",
            Filter = string.Join("|", choices.Select(choice =>
            {
                var extension = choice.Format.Extension();
                return $"{extension.ToUpperInvariant()}-Aufnahme (*.{extension})|*.{extension}";
            })),
            FilterIndex = 1,
            AddExtension = true,
            OverwritePrompt = false,
            RestoreDirectory = true,
            StartPosition = FormStartPosition.CenterParent
        };
        token.ThrowIfCancellationRequested();
        var result = _state?.ShowSaveDialog(dialog, this, "HistoryRecordingExport") ?? dialog.ShowDialog(this);
        if (result != DialogResult.OK) return "Export abgebrochen.";
        var index = dialog.FilterIndex - 1;
        if ((uint)index >= choices.Count) throw new HistoryRecordingFileException("Das ausgewählte Exportformat ist ungültig.");
        var extension = "." + choices[index].Format.Extension();
        if (!string.Equals(Path.GetExtension(dialog.FileName), extension, StringComparison.OrdinalIgnoreCase))
            throw new HistoryRecordingFileException("Die Dateiendung muss zum gewählten Aufnahmeformat passen.");
        if (File.Exists(dialog.FileName)) throw new HistoryRecordingFileException("Die Zieldatei existiert bereits. Bitte einen neuen Namen wählen.");
        await files.ExportAsync(callId, choices[index], dialog.FileName, token);
        return "Aufnahme exportiert.";
    });

    private async void ConvertRecording_Click(object? sender, EventArgs e) => await RunRecordingActionAsync(async (files, callId, token) =>
    {
        var state = await files.QueueMp3Async(callId, token);
        return state switch
        {
            RecordingConversionState.Pending => "MP3-Erstellung vorgemerkt. Du kannst die fertige Datei über „Aufnahme exportieren“ auswählen.",
            RecordingConversionState.Running => "MP3-Erstellung läuft.",
            RecordingConversionState.Completed => "Die MP3-Aufnahme steht zum Export bereit.",
            RecordingConversionState.Failed => "Der vorhandene MP3-Auftrag ist fehlgeschlagen.",
            _ => "Der Status des MP3-Auftrags ist unbekannt."
        };
    });

    private async Task RunRecordingActionAsync(Func<HistoryRecordingFiles, Guid, CancellationToken, Task<string>> action)
    {
        if (_stopping || _operation is null || _operation.IsRunning || _recordingFiles is not { } files
            || view.GetFocusedRow() is not HistoryRow row || !_playable.Contains(row.Id)) return;
        exportRecordingButton.Enabled = convertRecordingButton.Enabled = false;
        try
        {
            await _operation.RunAsync(async token =>
            {
                try
                {
                    var message = await action(files, row.Id, token);
                    if (!_stopping) _log?.LogNormal(message);
                }
                catch (HistoryRecordingFileException error) { if (!_stopping) _log?.LogWarning(error.Message); }
                catch (Mp3ConversionException error) { if (!_stopping) _log?.LogWarning(error.Message); }
                catch (IOException) { if (!_stopping) _log?.LogWarning("Die Datei ist belegt oder der Speicherort ist nicht erreichbar."); }
                catch (UnauthorizedAccessException) { if (!_stopping) _log?.LogWarning("Der Dateizugriff wurde verweigert."); }
            });
        }
        finally
        {
            if (!_stopping) exportRecordingButton.Enabled = convertRecordingButton.Enabled = true;
        }
    }
}
