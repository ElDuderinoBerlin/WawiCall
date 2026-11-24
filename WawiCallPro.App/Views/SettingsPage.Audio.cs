using System.Collections.Immutable;
using System.IO;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private const string CustomRingtone = "{custom-wav}";
    private IAudioDeviceCatalog? _audioDevices;
    private AudioDeviceCatalog? _audioCatalog;
    private WindowsIncomingRingtone? _ringtones;
    private PageOperation? _ringtoneOperation;
    private IReadOnlyList<string> _windowsRingtones = [];
    private string? _customRingtonePath;
    private PcmWaveHeader? _customRingtoneFormat;
    private bool _audioStopped;

    internal void ConfigureAudioDevices(IAudioDeviceCatalog catalog)
    {
        _audioDevices = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _ringtones = (catalog as WindowsAudioDeviceCatalog)?.Ringtones;
        refreshAudioButton.Enabled = !_audioStopped;
    }

    private void ApplyAudioDevices(AudioDeviceSettings settings)
    {
        BindDevice(captureDeviceEdit, _audioCatalog?.Capture ?? [], settings.CaptureDeviceId);
        BindDevice(playbackDeviceEdit, _audioCatalog?.Playback ?? [], settings.PlaybackDeviceId);
        BindDevice(ringDeviceEdit, _audioCatalog?.Playback ?? [], settings.RingDeviceId);
        _customRingtonePath = settings.CustomRingtonePath;
        _customRingtoneFormat = null;
        BindRingtones(_customRingtonePath is null ? settings.WindowsRingtoneFile : CustomRingtone);
    }

    private void BindRingtones(string? selected)
    {
        var rows = new List<AudioDeviceChoice> { new("", "Eingebauter Klingelton") };
        rows.AddRange(_windowsRingtones.Select(file => new AudioDeviceChoice(file, Path.GetFileNameWithoutExtension(file))));
        if (_customRingtonePath is { } customPath) rows.Add(new(CustomRingtone, Path.GetFileNameWithoutExtension(customPath)));
        var id = rows.FirstOrDefault(row => string.Equals(row.Id, selected, StringComparison.OrdinalIgnoreCase))?.Id;
        if (selected is not null && id is null) rows.Add(new(selected, "Windows-Klingelton nicht verfügbar"));
        windowsRingtoneEdit.Properties.DataSource = rows;
        windowsRingtoneEdit.EditValue = id ?? selected ?? "";
        UpdateCustomRingtoneInfo();
    }

    private void Ringtone_EditValueChanged(object? sender, EventArgs e) => UpdateCustomRingtoneInfo();

    private void UpdateCustomRingtoneInfo()
    {
        windowsRingtoneEdit.ToolTip = SelectedCustomRingtone;
        customRingtoneInfo.Text = SelectedCustomRingtone is null ? "" : _customRingtoneFormat is { } format
            ? $"{format.SampleRate:N0} Hz · {(format.Channels == 1 ? "Mono" : "Stereo")}"
            : "Eigene WAV-Datei nicht verfügbar";
    }

    private string? SelectedWindowsRingtone => ReadDevice(windowsRingtoneEdit) is { } selected && selected != CustomRingtone ? selected : null;
    private string? SelectedCustomRingtone => ReadDevice(windowsRingtoneEdit) == CustomRingtone ? _customRingtonePath : null;

    private async void BrowseRingtone_Click(object? sender, EventArgs e)
    {
        using var waiting = new UiWaitCursor(this);
        using var picker = new XtraOpenFileDialog
        {
            Title = "Eigene WAV-Datei als Klingelton auswählen", Filter = "WAV-Datei (*.wav)|*.wav",
            CheckFileExists = true, RestoreDirectory = true
        };
        var owner = (Control?)FindForm() ?? this;
        if ((_state?.ShowOpenDialog(picker, owner, "CustomRingtone") ?? picker.ShowDialog(owner)) == DialogResult.OK)
            await SelectCustomRingtoneAsync(picker.FileName);
    }

    internal Task SelectCustomRingtoneAsync(string fileName) => _ringtoneOperation?.RunAsync(async token =>
    {
        var format = await Task.Run(() => WindowsIncomingRingtone.ReadCustomRingtoneFormat(fileName, token), token);
        if (_audioStopped || IsDisposed) return;
        if (format is null)
        {
            audioStatus.Text = "Eine lokale PCM-WAV-Datei mit 16 Bit und Mono oder Stereo auswählen. Das Format lässt sich im WAV-Konverter ändern.";
            return;
        }
        _customRingtonePath = fileName;
        _customRingtoneFormat = format;
        BindRingtones(CustomRingtone);
        audioStatus.Text = "Eigene WAV-Datei gewählt.";
    }) ?? Task.CompletedTask;

    internal Task PreviewRingtoneAsync() => _ringtoneOperation?.RunAsync(async token =>
    {
        if (!previewRingtoneButton.Enabled || _ringtones is not { } player) return;
        var volume = (int)ringVolumeEdit.Value;
        if (volume == 0)
        {
            audioStatus.Text = "Die Klingellautstärke steht auf 0 %.";
            return;
        }
        var settings = new AudioDeviceSettings
        {
            WindowsRingtoneFile = SelectedWindowsRingtone, CustomRingtonePath = SelectedCustomRingtone,
            RingDeviceId = ReadDevice(ringDeviceEdit), RingVolumePercent = volume
        };
        previewRingtoneButton.Enabled = browseRingtoneButton.Enabled = false;
        audioStatus.Text = "Klingelton wird abgespielt …";
        try
        {
            var selected = await player.PreviewAsync(settings, token);
            if (!_audioStopped) audioStatus.Text = selected ? "Klingelton abgespielt." : "Klingeldatei nicht verfügbar. Eingebauten Ton abgespielt.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { if (!_audioStopped) audioStatus.Text = "Klingelton konnte nicht abgespielt werden. Klingelausgabe und Windows-Lautstärke prüfen."; }
        finally { previewRingtoneButton.Enabled = browseRingtoneButton.Enabled = !_audioStopped && windowsRingtoneEdit.Enabled; }
    }) ?? Task.CompletedTask;

    private async void PreviewRingtone_Click(object? sender, EventArgs e) => await PreviewRingtoneAsync();

    private void RingVolumeSlider_EditValueChanged(object? sender, EventArgs e) => ringVolumeEdit.Value = ringVolumeSlider.Value;

    private void RingVolumeEdit_EditValueChanged(object? sender, EventArgs e) => ringVolumeSlider.Value = (int)ringVolumeEdit.Value;

    private static string? ReadDevice(LookUpEdit editor) => editor.EditValue is string { Length: > 0 } value ? value : null;

    private static void BindDevice(LookUpEdit editor, ImmutableArray<AudioDeviceInfo> devices, string? selected)
    {
        var rows = new List<AudioDeviceChoice> { new("", "Systemstandard") };
        rows.AddRange(devices.Select(device => new AudioDeviceChoice(device.Id, device.DisplayName)));
        if (selected is not null && !devices.Any(device => device.Id == selected))
            rows.Add(new(selected, "Gerät nicht verfügbar"));
        editor.Properties.DataSource = rows;
        editor.EditValue = selected ?? "";
    }

    private void Device_EditValueChanged(object? sender, EventArgs e)
    {
        if (sender is not LookUpEdit editor || editor.Properties.DataSource is not List<AudioDeviceChoice> rows) return;
        var name = rows.FirstOrDefault(row => row.Id == (editor.EditValue as string ?? ""))?.Name ?? "Systemstandard";
        editor.ToolTip = name + (ReferenceEquals(editor, ringDeviceEdit)
            ? ". Änderungen gelten nach einem Neustart." : ". Die Gesprächsauswahl gilt nach dem Speichern.");
    }

    internal Task ReloadAudioDevicesAsync() => _operation?.RunAsync(LoadAudioDevicesAsync) ?? Task.CompletedTask;
    private async void RefreshAudio_Click(object? sender, EventArgs e) => await ReloadAudioDevicesAsync();

    private async Task LoadAudioDevicesAsync(CancellationToken token)
    {
        if (_audioStopped || _audioDevices is null) return;
        var capture = ReadDevice(captureDeviceEdit); var playback = ReadDevice(playbackDeviceEdit); var ring = ReadDevice(ringDeviceEdit);
        var ringtone = ReadDevice(windowsRingtoneEdit);
        SetDeviceEditorsEnabled(false); refreshAudioButton.Enabled = false;
        audioStatus.Text = "Audiogeräte werden geladen …";
        try
        {
            var devices = await _audioDevices.ReadAsync(token);
            token.ThrowIfCancellationRequested();
            if (_audioStopped || IsDisposed) return;
            if (devices.Capture.IsDefault || devices.Playback.IsDefault || devices.Capture.Concat(devices.Playback).Any(device =>
                    device is null || string.IsNullOrWhiteSpace(device.Id) || string.IsNullOrWhiteSpace(device.DisplayName))
                || devices.Capture.Select(device => device.Id).Distinct(StringComparer.Ordinal).Count() != devices.Capture.Length
                || devices.Playback.Select(device => device.Id).Distinct(StringComparer.Ordinal).Count() != devices.Playback.Length)
                throw new AudioDeviceCatalogException();
            _audioCatalog = devices;
            if (_ringtones is { } ringtones)
                _windowsRingtones = await Task.Run(ringtones.ReadWindowsRingtones, token);
            if (_customRingtonePath is { } customPath)
                _customRingtoneFormat = await Task.Run(() => WindowsIncomingRingtone.ReadCustomRingtoneFormat(customPath, token), token);
            token.ThrowIfCancellationRequested();
            if (_audioStopped || IsDisposed) return;
            BindDevice(captureDeviceEdit, devices.Capture, capture);
            BindDevice(playbackDeviceEdit, devices.Playback, playback);
            BindDevice(ringDeviceEdit, devices.Playback, ring);
            BindRingtones(ringtone);
            SetDeviceEditorsEnabled(!devices.Suppressed);
            audioStatus.Text = devices.Suppressed ? "Geräteerkennung im Prüfmodus deaktiviert. Zuordnungen bleiben erhalten."
                : devices.Capture.IsEmpty && devices.Playback.IsEmpty ? "Keine aktiven Geräte gefunden. Zuordnungen bleiben erhalten."
                : "Geräte geladen. Fehlende Zuordnungen bleiben erhalten.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            if (!_audioStopped && !IsDisposed) audioStatus.Text = new AudioDeviceCatalogException().Message;
        }
        finally { if (!_audioStopped && !IsDisposed) refreshAudioButton.Enabled = true; }
    }

    private void SetDeviceEditorsEnabled(bool enabled)
    {
        captureDeviceEdit.Enabled = playbackDeviceEdit.Enabled = ringDeviceEdit.Enabled = enabled && !_audioStopped;
        windowsRingtoneEdit.Enabled = previewRingtoneButton.Enabled = browseRingtoneButton.Enabled = ringDeviceEdit.Enabled && _ringtones is not null;
    }
    private sealed record AudioDeviceChoice(string Id, string Name);
}
