using System.IO;
using System.Text.Json;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private const int MaxGoogleCredentialBytes = 16_384;
    private ProtectedSecret? _importedGoogleSpeechCredential;

    private void ApplyAnnouncementCloud(AnnouncementCloudSettings settings)
    {
        speechAzureRegionEdit.Text = settings.AzureRegion ?? "";
        speechAzureKeyEdit.Text = "";
        speechAzureKeyEdit.Properties.NullValuePrompt = settings.AzureKey is null ? "" : "Gespeichert";
        speechAzureClear.Checked = false;
        speechAzureClear.Enabled = settings.AzureKey is not null;
        speechGoogleProjectEdit.Text = settings.GoogleProjectId ?? "";
        _importedGoogleSpeechCredential = null;
        speechGoogleCredentialEdit.Text = settings.GoogleCredential is null ? "Keine JSON gespeichert" : "Gespeichert";
        speechGoogleClear.Checked = false;
        speechGoogleClear.Enabled = settings.GoogleCredential is not null;
    }

    private AnnouncementCloudSettings ReadAnnouncementCloud(AnnouncementCloudSettings existing)
    {
        var enteredKey = speechAzureKeyEdit.Text.Trim();
        var azureRegion = speechAzureRegionEdit.Text.Trim().ToLowerInvariant();
        var googleProject = speechGoogleProjectEdit.Text.Trim();
        var azureKey = speechAzureClear.Checked ? null : enteredKey.Length == 0 ? existing.AzureKey
            : _workspace!.Secrets.Protect(enteredKey, AnnouncementCloudSettings.AzureKeyPurpose);
        return new()
        {
            AzureRegion = azureRegion.Length == 0 ? null : azureRegion,
            AzureKey = azureKey,
            GoogleProjectId = googleProject.Length == 0 ? null : googleProject,
            GoogleCredential = speechGoogleClear.Checked ? null : _importedGoogleSpeechCredential ?? existing.GoogleCredential
        };
    }

    private async void SpeechGoogleBrowse_Click(object? sender, EventArgs e)
    {
        if (_workspace is null || _operation is null) return;
        using var waiting = new UiWaitCursor(this);
        using var picker = new XtraOpenFileDialog
        {
            Title = "Google-Dienstkonto-JSON auswählen",
            Filter = "Google-Dienstkonto (*.json)|*.json",
            CheckFileExists = true,
            RestoreDirectory = true
        };
        var owner = FindForm() ?? (Control)this;
        if ((_state?.ShowOpenDialog(picker, owner, "SpeechGoogleCredential") ?? picker.ShowDialog(owner)) != DialogResult.OK) return;
        var file = picker.FileName;
        await _operation.RunAsync(async token =>
        {
            try
            {
                await using var stream = File.OpenRead(file);
                if (stream.Length is 0 or > MaxGoogleCredentialBytes) throw new FormatException();
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync(token);
                SetGoogleSpeechCredential(json);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or FormatException
                or JsonException or System.Security.Cryptography.CryptographicException or InvalidOperationException)
            {
                speechGoogleCredentialEdit.Text = "Import fehlgeschlagen. Google-Dienstkonto-JSON prüfen.";
            }
        });
    }

    internal void SetGoogleSpeechCredential(string json)
    {
        _ = CloudAnnouncementSpeechServices.ReadGoogleServiceAccount(json);
        _importedGoogleSpeechCredential = _workspace!.Secrets.Protect(json, AnnouncementCloudSettings.GoogleCredentialPurpose);
        speechGoogleClear.Checked = false;
        speechGoogleClear.Enabled = true;
        speechGoogleCredentialEdit.Text = "Importiert. Mit „Speichern“ übernehmen.";
    }
}
