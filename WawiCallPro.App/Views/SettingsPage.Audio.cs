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
    private IDisposable? _audioDeviceSubscription;
    private int _audioDevicesChanged;
    private long _retryAudioDevicesAfter;
    private AudioDeviceCatalog? _audioCatalog;
    private WindowsIncomingRingtone? _ringtones;
    private IAudioEndpointMeterFactory? _meterFactory;
    private EndpointMeterDisplay? _captureMeter, _playbackMeter, _ringMeter;
    private PageOperation? _ringtoneOperation;
    private CancellationTokenSource? _stopRingtonePreview;
    private AudioPlaybackVolume? _ringtonePreviewVolume;
    private IReadOnlyList<string> _windowsRingtones = [];
    private string? _customRingtonePath;
    private PcmWaveHeader? _customRingtoneFormat;
    private bool _audioStopped;

    internal void ConfigureAudioDevices(IAudioDeviceCatalog catalog)
    {
        _audioDevices = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _ringtones = (catalog as WindowsAudioDeviceCatalog)?.Ringtones;
        _meterFactory = catalog as IAudioEndpointMeterFactory;
        _captureMeter = new EndpointMeterDisplay(capturePeak, AudioEndpointDirection.Capture);
        _playbackMeter = new EndpointMeterDisplay(playbackPeak, AudioEndpointDirection.Playback);
        _ringMeter = new EndpointMeterDisplay(ringPeak, AudioEndpointDirection.Playback);
        StopAudioDeviceChanges();
        if (!_audioStopped && catalog is IAudioDeviceChanges changes)
        {
            _audioDeviceSubscription = changes.Subscribe(() => Interlocked.Exchange(ref _audioDevicesChanged, 1));
            audioDeviceChangesTimer.Start();
        }
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
        using var waiting = new UiWaitCursor(this);
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
        var volume = ringVolumeSlider.Value;
        var settings = new AudioDeviceSettings
        {
            WindowsRingtoneFile = SelectedWindowsRingtone, CustomRingtonePath = SelectedCustomRingtone,
            RingDeviceId = ReadDevice(ringDeviceEdit), RingVolumePercent = volume
        };
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        _stopRingtonePreview = stop;
        _ringtonePreviewVolume = new AudioPlaybackVolume(volume);
        browseRingtoneButton.Enabled = false;
        previewRingtoneButton.Text = "Stopp";
        audioStatus.Text = "";
        try
        {
            var selected = await player.PreviewAsync(settings, _ringtonePreviewVolume, stop.Token);
            if (!selected && !_audioStopped) audioStatus.Text = "Klingeldatei nicht verfügbar. Eingebauten Ton abgespielt.";
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception) { if (!_audioStopped) audioStatus.Text = "Klingelton konnte nicht abgespielt werden. Klingelausgabe und Windows-Lautstärke prüfen."; }
        finally
        {
            _stopRingtonePreview = null;
            _ringtonePreviewVolume = null;
            previewRingtoneButton.Text = "Abspielen";
            previewRingtoneButton.Enabled = browseRingtoneButton.Enabled = !_audioStopped && windowsRingtoneEdit.Enabled;
        }
    }) ?? Task.CompletedTask;

    private async void PreviewRingtone_Click(object? sender, EventArgs e)
    {
        if (_stopRingtonePreview is { } stop) await stop.CancelAsync();
        else await PreviewRingtoneAsync();
    }

    private void RingVolumeSlider_EditValueChanged(object? sender, EventArgs e)
    {
        if (_ringtonePreviewVolume is { } preview) preview.Percent = ringVolumeSlider.Value;
    }

    private void VolumeSlider_BeforeShowValueToolTip(object? sender, TrackBarValueToolTipEventArgs e)
    {
        if (sender is TrackBarControl slider) e.ShowArgs.ToolTip = $"{slider.Value} %";
        e.ShowArgs.ToolTipLocation = DevExpress.Utils.ToolTipLocation.TopCenter;
    }

    private static string? ReadDevice(LookUpEdit editor) => editor.EditValue is string { Length: > 0 } value ? value : null;

    private static void BindDevice(LookUpEdit editor, ImmutableArray<AudioDeviceInfo> devices, string? selected)
    {
        var rows = new List<AudioDeviceChoice> { new("", "Systemstandard") };
        rows.AddRange(devices.Select(device => new AudioDeviceChoice(device.Id, device.DisplayName)));
        var activeId = devices.FirstOrDefault(device => string.Equals(device.Id, selected, StringComparison.OrdinalIgnoreCase))?.Id;
        if (selected is not null && activeId is null)
            rows.Add(new(selected, "Gerät nicht verfügbar"));
        editor.Properties.DataSource = rows;
        editor.EditValue = activeId ?? selected ?? "";
    }

    private void Device_EditValueChanged(object? sender, EventArgs e)
    {
        if (sender is not LookUpEdit editor || editor.Properties.DataSource is not List<AudioDeviceChoice> rows) return;
        var name = rows.FirstOrDefault(row => string.Equals(row.Id, editor.EditValue as string ?? "", StringComparison.OrdinalIgnoreCase))?.Name ?? "Systemstandard";
        editor.ToolTip = name + (ReferenceEquals(editor, ringDeviceEdit)
            ? ". Änderungen gelten nach einem Neustart." : ". Die Gesprächsauswahl gilt nach dem Speichern.");
    }

    internal Task ReloadAudioDevicesAsync() => _operation?.RunAsync(LoadAudioDevicesAsync) ?? Task.CompletedTask;

    private async void AudioDeviceChangesTimer_Tick(object? sender, EventArgs e)
    {
        if (_audioStopped || !Visible || _operation?.IsRunning != false || _ringtoneOperation?.IsRunning != false) return;
        if (Interlocked.Exchange(ref _audioDevicesChanged, 0) != 0
            || _retryAudioDevicesAfter != 0 && Environment.TickCount64 >= _retryAudioDevicesAfter)
            await _ringtoneOperation.RunAsync(LoadAudioDevicesAsync);
    }

    private void StopAudioDeviceChanges()
    {
        audioDeviceChangesTimer.Stop();
        _audioDeviceSubscription?.Dispose();
        _audioDeviceSubscription = null;
        _retryAudioDevicesAfter = 0;
        Interlocked.Exchange(ref _audioDevicesChanged, 0);
    }

    private async Task LoadAudioDevicesAsync(CancellationToken token)
    {
        if (_audioStopped || _audioDevices is null) return;
        SetDeviceEditorsEnabled(false);
        audioStatus.Text = "Audiogeräte werden geladen …";
        try
        {
            var devices = await _audioDevices.ReadAsync(token);
            token.ThrowIfCancellationRequested();
            if (_audioStopped || IsDisposed) return;
            if (devices.Capture.IsDefault || devices.Playback.IsDefault || devices.Capture.Concat(devices.Playback).Any(device =>
                    device is null || string.IsNullOrWhiteSpace(device.Id) || string.IsNullOrWhiteSpace(device.DisplayName))
                || devices.Capture.Select(device => device.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != devices.Capture.Length
                || devices.Playback.Select(device => device.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != devices.Playback.Length)
                throw new AudioDeviceCatalogException();
            _audioCatalog = devices;
            _retryAudioDevicesAfter = 0;
            if (_ringtones is { } ringtones)
                _windowsRingtones = await Task.Run(ringtones.ReadWindowsRingtones, token);
            if (_customRingtonePath is { } customPath)
                _customRingtoneFormat = await Task.Run(() => WindowsIncomingRingtone.ReadCustomRingtoneFormat(customPath, token), token);
            token.ThrowIfCancellationRequested();
            if (_audioStopped || IsDisposed) return;
            var capture = ReadDevice(captureDeviceEdit);
            var playback = ReadDevice(playbackDeviceEdit);
            var ring = ReadDevice(ringDeviceEdit);
            BindDevice(captureDeviceEdit, devices.Capture, capture);
            BindDevice(playbackDeviceEdit, devices.Playback, playback);
            BindDevice(ringDeviceEdit, devices.Playback, ring);
            BindRingtones(ReadDevice(windowsRingtoneEdit));
            if (!devices.Suppressed && (capture is not null && !devices.Capture.Any(device => string.Equals(device.Id, capture, StringComparison.OrdinalIgnoreCase))
                || playback is not null && !devices.Playback.Any(device => string.Equals(device.Id, playback, StringComparison.OrdinalIgnoreCase))
                || ring is not null && !devices.Playback.Any(device => string.Equals(device.Id, ring, StringComparison.OrdinalIgnoreCase))))
                _retryAudioDevicesAfter = Environment.TickCount64 + 3000;
            SetDeviceEditorsEnabled(!devices.Suppressed);
            audioStatus.Text = devices.Suppressed ? "Geräteerkennung im Prüfmodus deaktiviert. Zuordnungen bleiben erhalten."
                : devices.Capture.IsEmpty && devices.Playback.IsEmpty ? "Keine aktiven Geräte gefunden. Zuordnungen bleiben erhalten."
                : "";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            if (!_audioStopped && !IsDisposed)
            {
                _retryAudioDevicesAfter = Environment.TickCount64 + 2000;
                audioStatus.Text = new AudioDeviceCatalogException().Message;
            }
        }
    }

    private void SetDeviceEditorsEnabled(bool enabled)
    {
        captureDeviceEdit.Enabled = playbackDeviceEdit.Enabled = ringDeviceEdit.Enabled = enabled && !_audioStopped;
        windowsRingtoneEdit.Enabled = previewRingtoneButton.Enabled = browseRingtoneButton.Enabled = ringDeviceEdit.Enabled && _ringtones is not null;
        AudioMeterView_Changed(this, EventArgs.Empty);
    }

    private void AudioMeterView_Changed(object? sender, EventArgs e)
    {
        if (audioMeterTimer is null || captureDeviceEdit is null) return;
        if (!_audioStopped && _meterFactory is not null && Visible && captureDeviceEdit.Enabled
            && tabs.SelectedTabPage == audioTab && audioPages.SelectedTabPage == audioDevicesTab)
        {
            AudioMeterTimer_Tick(this, EventArgs.Empty);
            audioMeterTimer.Start();
        }
        else StopAudioMeters();
    }

    private void AudioMeterTimer_Tick(object? sender, EventArgs e)
    {
        if (_meterFactory is not { } factory) return;
        _captureMeter?.Update(factory, ReadDevice(captureDeviceEdit));
        _playbackMeter?.Update(factory, ReadDevice(playbackDeviceEdit));
        _ringMeter?.Update(factory, ReadDevice(ringDeviceEdit));
    }

    private void StopAudioMeters()
    {
        audioMeterTimer.Stop();
        _captureMeter?.Dispose();
        _playbackMeter?.Dispose();
        _ringMeter?.Dispose();
    }
    private sealed record AudioDeviceChoice(string Id, string Name);
}
