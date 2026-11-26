using System.IO;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private static readonly (string Caption, string Value, bool Template)[] RecordingNameChoices =
    [
        ("Mandant, Datum und Zeitstempel (Standard)", RecordingFileName.DefaultPattern, true),
        ("Datum, Uhrzeit und Richtung", RecordingFileName.DescriptivePattern, true),
        ("Datum, Uhrzeit und Rufnummer", "{Datum}_{Timestamp}_{Rufnummer}", true),
        ("{Mandant}", "{Mandant}", false),
        ("{Timestamp}", "{Timestamp}", false),
        ("{Datum:yyyy-MM-dd}", "{Datum:yyyy-MM-dd}", false),
        ("{Datum:yyyyMMdd}", "{Datum:yyyyMMdd}", false),
        ("{Uhrzeit:HH-mm-ss}", "{Uhrzeit:HH-mm-ss}", false),
        ("{Uhrzeit:HHmmss}", "{Uhrzeit:HHmmss}", false),
        ("{Richtung}", "{Richtung}", false),
        ("{Rufnummer}", "{Rufnummer}", false)
    ];
    private static readonly Guid ExampleRecordingId = Guid.Parse("a31f0e03-3d61-4e74-8288-0abf04700102");
    private const string ExampleSipNumber = "03025776817";

    private void InitializeRecordingStorageEditor()
    {
        recordingPatternChoices.Properties.Items.Add("Platzhalter oder Vorlage …");
        recordingPatternChoices.Properties.Items.AddRange(RecordingNameChoices.Select(choice => (object)choice.Caption).ToArray());
        recordingPatternChoices.SelectedIndex = 0;
        recordingPatternEdit.Text = RecordingFileName.DefaultPattern;
    }

    private void ApplyRecordingStorage(RecordingStorageSettings settings)
    {
        recordingFolderEdit.Text = settings.PendingDirectory ?? settings.Directory ?? _workspace?.Paths.DefaultRecordingsDirectory ?? "";
        recordingPatternEdit.Text = settings.FileNamePattern;
        var hasPendingFolder = settings.PendingDirectory is not null && _workspace is not null;
        recordingFolderHint.Text = hasPendingFolder ? "Ordnerwechsel vorgemerkt. Aktiver Aufnahmeordner: " + _workspace!.Paths.RecordingsDirectory : "";
        recordingFolderHint.Visible = hasPendingFolder;
        UpdateRecordingPreview();
    }

    private RecordingStorageSettings ReadRecordingStorage(RecordingStorageSettings saved)
    {
        var defaultDirectory = _workspace?.Paths.DefaultRecordingsDirectory;
        if (defaultDirectory is null) return saved;
        var requested = string.IsNullOrWhiteSpace(recordingFolderEdit.Text)
            ? defaultDirectory : RecordingArchive.NormalizeDirectory(recordingFolderEdit.Text.Trim());
        var active = saved.Directory ?? defaultDirectory;
        return saved with
        {
            PendingDirectory = string.Equals(requested, active, StringComparison.OrdinalIgnoreCase) ? null : requested,
            FileNamePattern = recordingPatternEdit.Text
        };
    }

    private async Task ValidateRecordingDestinationAsync(RecordingStorageSettings draft, CancellationToken token)
    {
        if (_workspace is null || draft.PendingDirectory is not { } destination
            || draft.PendingDirectory == _snapshot?.Options.RecordingStorage.PendingDirectory) return;
        var source = _workspace.Paths.RecordingsDirectory;
        try { await Task.Run(() => RecordingArchive.ValidateDestination(source, destination), token); }
        catch (Exception error) when (error is FormatException or IOException or UnauthorizedAccessException)
        {
            recordingPreviewValue.Text = error is FormatException ? error.Message : "Der neue Aufnahmeordner ist nicht beschreibbar oder enthält eine Verknüpfung.";
            throw;
        }
    }

    private void RecordingStorage_Changed(object? sender, EventArgs e) => UpdateRecordingPreview();

    private void UpdateRecordingPreview()
    {
        if (recordingPatternEdit is null || recordingPreviewValue is null || recordingFormatEdit.SelectedIndex < 0) return;
        string preview;
        try
        {
            var stem = RecordingFileName.Format(recordingPatternEdit.Text, new DateTimeOffset(2026, 11, 25, 10, 44, 16, TimeSpan.Zero),
                CallDirection.Incoming, ExampleRecordingId, new(NumberPresentation.Available, ExampleSipNumber), "Musterfirma");
            var folder = string.IsNullOrWhiteSpace(recordingFolderEdit.Text)
                ? _workspace?.Paths.DefaultRecordingsDirectory ?? "recordings"
                : RecordingArchive.NormalizeDirectory(recordingFolderEdit.Text.Trim());
            var format = (RecordingArtifactFormat)(recordingFormatEdit.SelectedIndex + 1);
            preview = Path.Combine(folder, stem + "." + format.Extension());
        }
        catch (FormatException error) { preview = error.Message; }
        recordingPreviewValue.Text = preview;
        recordingPreviewValue.ToolTip = preview;
    }

    private void RecordingFolderBrowse_Click(object? sender, EventArgs e)
    {
        var owner = (Control?)FindForm() ?? this;
        using var waiting = new UiWaitCursor(owner);
        try
        {
            using var dialog = new XtraFolderBrowserDialog
            {
                Title = "Aufnahmeordner auswählen",
                SelectedPath = Directory.Exists(recordingFolderEdit.Text) ? recordingFolderEdit.Text : _workspace?.Paths.RecordingsDirectory ?? "",
                StartPosition = FormStartPosition.CenterParent
            };
            if (dialog.ShowDialog(owner) == DialogResult.OK) recordingFolderEdit.Text = dialog.SelectedPath;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        {
            recordingPreviewValue.Text = "Die Ordnerauswahl konnte nicht geöffnet werden.";
        }
    }

    private void RecordingFolderReset_Click(object? sender, EventArgs e) =>
        recordingFolderEdit.Text = _workspace?.Paths.DefaultRecordingsDirectory ?? "";

    private void RecordingPatternChoice_Changed(object? sender, EventArgs e)
    {
        var index = recordingPatternChoices.SelectedIndex - 1;
        if (index < 0 || index >= RecordingNameChoices.Length) return;
        var choice = RecordingNameChoices[index];
        if (choice.Template) recordingPatternEdit.Text = choice.Value;
        else recordingPatternEdit.SelectedText = choice.Value;
        recordingPatternChoices.SelectedIndex = 0;
        recordingPatternEdit.Focus();
    }
}
