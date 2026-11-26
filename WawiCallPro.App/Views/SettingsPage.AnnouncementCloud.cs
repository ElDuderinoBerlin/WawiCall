using System.IO;
using System.Text.Json;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private const int MaxGoogleCredentialBytes = 16_384;
    private ProtectedSecret? _importedGoogleSpeechCredential;
    private Func<AnnouncementSpeechProvider, IAnnouncementSpeech?>? _resolveAnnouncementSpeech;
    private CancellationTokenSource? _announcementPreviewStop;
    private bool _announcementVoiceBusy;

    private sealed record AnnouncementProviderChoice(AnnouncementSpeechProvider Id, string Name);
    private sealed record AnnouncementVoiceChoice(string Id, string Name);

    internal void ConfigureAnnouncementSpeech(Func<AnnouncementSpeechProvider, IAnnouncementSpeech?>? resolve) =>
        _resolveAnnouncementSpeech = resolve;

    private AnnouncementSpeechProvider StandardAnnouncementProvider =>
        standardSpeechProviderEdit.EditValue is AnnouncementSpeechProvider provider ? provider : AnnouncementSpeechProvider.Windows;

    private string? StandardAnnouncementVoice => standardSpeechVoiceEdit.EditValue is string { Length: > 0 } voice ? voice : null;

    private void ApplyAnnouncementVoice(AnnouncementAudioSettings settings)
    {
        if (standardSpeechProviderEdit.Properties.DataSource is null)
        {
            standardSpeechProviderEdit.Properties.DataSource = new AnnouncementProviderChoice[]
            {
                new(AnnouncementSpeechProvider.Windows, "Windows"),
                new(AnnouncementSpeechProvider.AzureSpeech, "Azure Speech"),
                new(AnnouncementSpeechProvider.GoogleCloud, "Google Cloud")
            };
        }
        standardSpeechProviderEdit.EditValue = settings.SpeechProvider;
        BindStandardAnnouncementVoices(PresetAnnouncementVoices(), settings.VoiceId);
        UpdateAnnouncementVoiceActions();
    }

    private IReadOnlyList<AnnouncementVoice> PresetAnnouncementVoices()
    {
        var provider = StandardAnnouncementProvider;
        if (provider != AnnouncementSpeechProvider.Windows) return CloudAnnouncementSpeechServices.GetPresetVoices(provider);
        try { return _workspace?.AnnouncementSpeech?.GetVoices() ?? []; }
        catch (Exception) { return []; }
    }

    private void BindStandardAnnouncementVoices(IReadOnlyList<AnnouncementVoice> voices, string? selected)
    {
        var choices = new List<AnnouncementVoiceChoice>
        {
            new("", StandardAnnouncementProvider == AnnouncementSpeechProvider.Windows ? "Systemstandard" : "Standardstimme des Anbieters")
        };
        choices.AddRange(voices.Select(voice => new AnnouncementVoiceChoice(voice.Id, $"{voice.DisplayName} ({voice.Language})")));
        if (selected is not null && choices.All(voice => voice.Id != selected))
            choices.Add(new(selected, $"{selected} (gespeichert)"));
        standardSpeechVoiceEdit.Properties.DataSource = choices;
        standardSpeechVoiceEdit.EditValue = selected ?? "";
    }

    private void StandardSpeechProvider_Changed(object? sender, EventArgs e)
    {
        BindStandardAnnouncementVoices(PresetAnnouncementVoices(), null);
        speechTestStatus.Text = "";
        UpdateAnnouncementVoiceActions();
    }

    private void SpeechTestText_Changed(object? sender, EventArgs e) => UpdateAnnouncementVoiceActions();

    private void UpdateAnnouncementVoiceActions()
    {
        var ready = _workspace is not null && !_stoppingTabs && !_announcementVoiceBusy;
        standardSpeechProviderEdit.Enabled = standardSpeechVoiceEdit.Enabled = speechTestEdit.Enabled = ready;
        refreshSpeechVoicesButton.Enabled = ready && _resolveAnnouncementSpeech is not null
            && StandardAnnouncementProvider != AnnouncementSpeechProvider.Windows;
        speechTestButton.Enabled = (_announcementPreviewStop is not null && !_stoppingTabs)
            || ready && _workspace?.AnnouncementAudio is not null && !string.IsNullOrWhiteSpace(speechTestEdit.Text);
    }

    internal Task RefreshAnnouncementVoicesAsync() => RunAnnouncementVoiceActionAsync(async token =>
    {
        var voice = StandardAnnouncementVoice;
        var speech = await ResolveAnnouncementSpeechAsync(StandardAnnouncementProvider, token);
        BindStandardAnnouncementVoices(await speech.LoadVoicesAsync(token), voice);
        speechTestStatus.Text = "Stimmenliste geladen.";
    });

    internal Task PreviewAnnouncementVoiceAsync() => RunAnnouncementVoiceActionAsync(async token =>
    {
        if (_workspace?.AnnouncementAudio is not { } audio || string.IsNullOrWhiteSpace(speechTestEdit.Text)) return;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        _announcementPreviewStop = stop;
        speechTestButton.Text = "Stopp";
        UpdateAnnouncementVoiceActions();
        var path = Path.Combine(Path.GetTempPath(), $"WawiCallPro-voice-preview-{Guid.NewGuid():N}.wav");
        try
        {
            var devices = (await _workspace.Settings.LoadAsync(stop.Token)).Options.Devices;
            var speech = await ResolveAnnouncementSpeechAsync(StandardAnnouncementProvider, stop.Token);
            await speech.WriteWaveAsync(speechTestEdit.Text, StandardAnnouncementVoice, path, stop.Token);
            await audio.PreviewAsync(path, devices with { PlaybackDeviceId = devices.Announcements.PlaybackDeviceId }, stop.Token);
            speechTestStatus.Text = "Wiedergabe beendet.";
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            speechTestStatus.Text = "Vorschau abgebrochen.";
        }
        finally
        {
            _announcementPreviewStop = null;
            speechTestButton.Text = "Vertonen und abspielen";
            try { File.Delete(path); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            UpdateAnnouncementUsage();
        }
    });

    private Task RunAnnouncementVoiceActionAsync(Func<CancellationToken, Task> action) => _operation?.RunAsync(async token =>
    {
        _announcementVoiceBusy = true;
        UpdateAnnouncementVoiceActions();
        speechTestStatus.Text = "Bitte warten …";
        try { await action(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (AnnouncementSpeechException error) { speechTestStatus.Text = error.Message; }
        catch (Exception) { speechTestStatus.Text = "Stimmenprobe fehlgeschlagen. Zugang und Audiogerät prüfen."; }
        finally
        {
            _announcementVoiceBusy = false;
            UpdateAnnouncementVoiceActions();
        }
    }) ?? Task.CompletedTask;

    private async Task<IAnnouncementSpeech> ResolveAnnouncementSpeechAsync(AnnouncementSpeechProvider provider, CancellationToken token)
    {
        var speech = provider == AnnouncementSpeechProvider.Windows ? _workspace?.AnnouncementSpeech
            : await Task.Run(() => _resolveAnnouncementSpeech?.Invoke(provider), token);
        return speech ?? throw new AnnouncementSpeechException("Für den gewählten Anbieter zuerst den Zugang speichern.");
    }

    private async void RefreshSpeechVoices_Click(object? sender, EventArgs e) => await RefreshAnnouncementVoicesAsync();

    private async void SpeechTest_Click(object? sender, EventArgs e)
    {
        if (_announcementPreviewStop is { } stop) await stop.CancelAsync();
        else await PreviewAnnouncementVoiceAsync();
    }

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
        speechDeepLKeyEdit.Text = "";
        speechDeepLKeyEdit.Properties.NullValuePrompt = settings.DeepLKey is null ? "" : "Gespeichert";
        speechDeepLClear.Checked = false;
        speechDeepLClear.Enabled = settings.DeepLKey is not null;
        quotaAzureEdit.Value = settings.AzureMonthlyAllowance;
        quotaNeural2Edit.Value = settings.GoogleNeural2MonthlyAllowance;
        quotaChirpEdit.Value = settings.GoogleChirpMonthlyAllowance;
        UpdateAnnouncementUsage();
    }

    private AnnouncementCloudSettings ReadAnnouncementCloud(AnnouncementCloudSettings existing)
    {
        var enteredKey = speechAzureKeyEdit.Text.Trim();
        var azureRegion = speechAzureRegionEdit.Text.Trim().ToLowerInvariant();
        var googleProject = speechGoogleProjectEdit.Text.Trim();
        var enteredDeepLKey = speechDeepLKeyEdit.Text.Trim();
        var azureKey = speechAzureClear.Checked ? null : enteredKey.Length == 0 ? existing.AzureKey
            : _workspace!.Secrets.Protect(enteredKey, AnnouncementCloudSettings.AzureKeyPurpose);
        return new()
        {
            AzureRegion = azureRegion.Length == 0 ? null : azureRegion,
            AzureKey = azureKey,
            GoogleProjectId = googleProject.Length == 0 ? null : googleProject,
            GoogleCredential = speechGoogleClear.Checked ? null : _importedGoogleSpeechCredential ?? existing.GoogleCredential,
            DeepLKey = speechDeepLClear.Checked ? null : enteredDeepLKey.Length == 0 ? existing.DeepLKey
                : _workspace!.Secrets.Protect(enteredDeepLKey, AnnouncementCloudSettings.DeepLKeyPurpose),
            AzureMonthlyAllowance = (int)quotaAzureEdit.Value,
            GoogleNeural2MonthlyAllowance = (int)quotaNeural2Edit.Value,
            GoogleChirpMonthlyAllowance = (int)quotaChirpEdit.Value
        };
    }

    private void UpdateAnnouncementUsage()
    {
        (int Azure, int Neural2, int Chirp)? usage = null;
        try { usage = _workspace?.AnnouncementQuota?.Read(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
        quotaAzureUsed.Text = UsageText(AnnouncementCloudSettings.AzureFreeCharacters, usage?.Azure);
        quotaNeural2Used.Text = UsageText(AnnouncementCloudSettings.GoogleFreeCharactersPerVoiceFamily, usage?.Neural2);
        quotaChirpUsed.Text = UsageText(AnnouncementCloudSettings.GoogleFreeCharactersPerVoiceFamily, usage?.Chirp);

        static string UsageText(int free, int? used) => used is { } count
            ? $"Frei: {free:N0} · Diesen Monat lokal reserviert: {count:N0}"
            : $"Frei: {free:N0} · Lokaler Monatsverbrauch nicht verfügbar";
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
