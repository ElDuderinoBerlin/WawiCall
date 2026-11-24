using System.Collections.Immutable;
using System.IO;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private IAudioDeviceCatalog? _audioDevices;
    private AudioDeviceCatalog? _audioCatalog;
    private WindowsIncomingRingtone? _ringtones;
    private PageOperation? _ringtoneOperation;
    private IReadOnlyList<string> _windowsRingtones = [];
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
        BindRingtones(settings.WindowsRingtoneFile);
    }

    private void BindRingtones(string? selected)
    {
        var rows = new List<AudioDeviceChoice> { new("", "Eingebauter Klingelton") };
        rows.AddRange(_windowsRingtones.Select(file => new AudioDeviceChoice(file, Path.GetFileNameWithoutExtension(file))));
        var id = rows.FirstOrDefault(row => string.Equals(row.Id, selected, StringComparison.OrdinalIgnoreCase))?.Id;
        if (selected is not null && id is null) rows.Add(new(selected, "Windows-Klingelton nicht verfügbar"));
        windowsRingtoneEdit.Properties.DataSource = rows;
        windowsRingtoneEdit.EditValue = id ?? selected ?? "";
    }

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
            WindowsRingtoneFile = ReadDevice(windowsRingtoneEdit), RingDeviceId = ReadDevice(ringDeviceEdit), RingVolumePercent = volume
        };
        previewRingtoneButton.Enabled = false;
        audioStatus.Text = "Klingelton wird abgespielt …";
        try
        {
            var selected = await player.PreviewAsync(settings, token);
            if (!_audioStopped) audioStatus.Text = selected ? "Klingelton abgespielt." : "Windows-Klingelton nicht verfügbar. Eingebauten Ton abgespielt.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { if (!_audioStopped) audioStatus.Text = "Klingelton konnte nicht abgespielt werden. Klingelausgabe und Windows-Lautstärke prüfen."; }
        finally { previewRingtoneButton.Enabled = !_audioStopped && windowsRingtoneEdit.Enabled; }
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
        windowsRingtoneEdit.Enabled = previewRingtoneButton.Enabled = ringDeviceEdit.Enabled && _ringtones is not null;
    }
    private sealed record AudioDeviceChoice(string Id, string Name);
}
