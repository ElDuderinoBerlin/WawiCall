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
    private const string CustomRingtone = "{custom-wav}";
    private IAudioDeviceCatalog? _audioDevices;
    private IDisposable? _audioDeviceSubscription;
    private int _audioDevicesChanged;
    private int _audioDeviceReadVersion;
    private readonly CancellationTokenSource _audioDeviceLifetime = new();
    private Task _audioDeviceRefresh = Task.CompletedTask;
    private AudioDeviceCatalog? _audioCatalog;
    private AudioDeviceCatalog? _deferredAudioCatalog;
    private WindowsIncomingRingtone? _ringtones;
    private IAudioEndpointMeterFactory? _meterFactory;
    private EndpointMeterDisplay? _captureMeter, _playbackMeter, _ringMeter;
    private PageOperation? _ringtoneOperation;
    private CancellationTokenSource? _stopRingtonePreview;
    private AudioPlaybackVolume? _ringtonePreviewVolume;
    private IReadOnlyList<string> _windowsRingtones = [];
    private string? _customRingtonePath;
    private CSCore.WaveFormat? _customRingtoneFormat;
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
            _audioDeviceSubscription = changes.Subscribe(QueueAudioDeviceChange);
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
            : "Eigene Klingeltondatei nicht verfügbar";
    }

    private string? SelectedWindowsRingtone => ReadDevice(windowsRingtoneEdit) is { } selected && selected != CustomRingtone ? selected : null;
    private string? SelectedCustomRingtone => ReadDevice(windowsRingtoneEdit) == CustomRingtone ? _customRingtonePath : null;

    private async void BrowseRingtone_Click(object? sender, EventArgs e)
    {
        using var waiting = new UiWaitCursor(this);
        using var picker = new XtraOpenFileDialog
        {
            Title = "Eigene Klingeltondatei auswählen",
            Filter = "Audiodateien (*.wav;*.mp3;*.ogg;*.wma)|*.wav;*.mp3;*.ogg;*.wma|WAV-Dateien (*.wav)|*.wav|MP3-Dateien (*.mp3)|*.mp3|Ogg/Vorbis-Dateien (*.ogg)|*.ogg|WMA-Dateien (*.wma)|*.wma",
            CheckFileExists = true, RestoreDirectory = true
        };
        var owner = (Control?)FindForm() ?? this;
        if ((_state?.ShowOpenDialog(picker, owner, "CustomRingtone") ?? picker.ShowDialog(owner)) == DialogResult.OK)
            await SelectCustomRingtoneAsync(picker.FileName);
    }

    internal Task SelectCustomRingtoneAsync(string fileName) => _ringtoneOperation?.RunAsync(async token =>
    {
        using var waiting = new UiWaitCursor(this);
        var format = await Task.Run(() => WindowsIncomingRingtone.ProbeCustomRingtone(fileName, token), token);
        if (_audioStopped || IsDisposed) return;
        if (format is null)
        {
            audioStatus.Text = "Eine abspielbare lokale WAV-, MP3-, Ogg/Vorbis- oder WMA-Datei in Mono oder Stereo auswählen.";
            return;
        }
        _customRingtonePath = fileName;
        _customRingtoneFormat = format;
        BindRingtones(CustomRingtone);
        audioStatus.Text = "";
    }) ?? Task.CompletedTask;

    internal Task PreviewRingtoneAsync() => _ringtoneOperation?.RunAsync(async token =>
    {
        if (!previewRingtoneButton.Enabled || _ringtones is not { } player) return;
        var volume = ringVolumeSlider.Value;
        var settings = new AudioDeviceSettings
        {
            WindowsRingtoneFile = SelectedWindowsRingtone, CustomRingtonePath = SelectedCustomRingtone,
            RingDeviceId = ReadDevice(ringDeviceEdit), RingVolumePercent = volume,
            PlaybackBufferMilliseconds = decimal.ToInt32(playbackBufferEdit.Value)
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

    private void QueueAudioDeviceChange()
    {
        if (!IsHandleCreated || IsDisposed || Interlocked.Exchange(ref _audioDevicesChanged, 1) != 0) return;
        try { BeginInvoke((Action)AudioDevicesChangedOnUiThread); }
        catch (InvalidOperationException) { Interlocked.Exchange(ref _audioDevicesChanged, 0); }
    }

    private void AudioDevicesChangedOnUiThread()
    {
        if (!_audioStopped && _audioDeviceRefresh.IsCompleted)
            _audioDeviceRefresh = ReadChangedAudioDevicesAsync(_audioDeviceLifetime.Token);
    }

    private async Task ReadChangedAudioDevicesAsync(CancellationToken token)
    {
        while (!_audioStopped && Interlocked.Exchange(ref _audioDevicesChanged, 0) != 0)
        {
            var catalog = _audioDevices;
            if (catalog is null) return;
            var version = ++_audioDeviceReadVersion;
            try
            {
                var devices = await catalog.ReadAsync(token);
                token.ThrowIfCancellationRequested();
                if (!_audioStopped && version == _audioDeviceReadVersion && ReferenceEquals(catalog, _audioDevices))
                    ApplyAudioDeviceCatalog(devices);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception)
            {
                if (!_audioStopped && !IsDisposed)
                    audioStatus.Text = new AudioDeviceCatalogException().Message;
            }
        }
    }

    private void DevicePopup_Closed(object? sender, DevExpress.XtraEditors.Controls.ClosedEventArgs e)
    {
        if (_deferredAudioCatalog is not null && !IsDisposed && IsHandleCreated)
            BeginInvoke((Action)ApplyDeferredAudioDeviceCatalog);
    }

    private void ApplyDeferredAudioDeviceCatalog()
    {
        if (!_audioStopped && _deferredAudioCatalog is { } devices)
            ApplyAudioDeviceCatalog(devices);
    }

    private void ApplyAudioDeviceCatalog(AudioDeviceCatalog devices)
    {
        static bool Valid(ImmutableArray<AudioDeviceInfo> entries) => !entries.IsDefault
            && entries.All(device => device is not null && !string.IsNullOrWhiteSpace(device.Id) && !string.IsNullOrWhiteSpace(device.DisplayName))
            && entries.Select(device => device.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == entries.Length;

        if (!Valid(devices.Capture) || !Valid(devices.Playback)) throw new AudioDeviceCatalogException();
        var previous = _audioCatalog;
        var captureChanged = previous is null || !previous.Capture.SequenceEqual(devices.Capture);
        var playbackChanged = previous is null || !previous.Playback.SequenceEqual(devices.Playback);
        if (captureChanged && captureDeviceEdit.IsPopupOpen ||
            playbackChanged && (playbackDeviceEdit.IsPopupOpen || ringDeviceEdit.IsPopupOpen))
        {
            _deferredAudioCatalog = devices;
            return;
        }
        _audioCatalog = devices;
        _deferredAudioCatalog = null;
        if (captureChanged) BindDevice(captureDeviceEdit, devices.Capture, ReadDevice(captureDeviceEdit));
        if (playbackChanged)
        {
            BindDevice(playbackDeviceEdit, devices.Playback, ReadDevice(playbackDeviceEdit));
            BindDevice(ringDeviceEdit, devices.Playback, ReadDevice(ringDeviceEdit));
        }
    }

    private void StopAudioDeviceChanges()
    {
        _audioDeviceSubscription?.Dispose();
        _audioDeviceSubscription = null;
        _deferredAudioCatalog = null;
        Interlocked.Exchange(ref _audioDevicesChanged, 0);
        if (_audioStopped && !_audioDeviceLifetime.IsCancellationRequested) _audioDeviceLifetime.Cancel();
    }

    private async Task LoadAudioDevicesAsync(CancellationToken token)
    {
        if (_audioStopped || _audioDevices is null) return;
        SetDeviceEditorsEnabled(false);
        audioStatus.Text = "Audiogeräte werden geladen …";
        var version = ++_audioDeviceReadVersion;
        try
        {
            var devices = await _audioDevices.ReadAsync(token);
            token.ThrowIfCancellationRequested();
            if (_audioStopped || IsDisposed) return;
            if (version == _audioDeviceReadVersion) ApplyAudioDeviceCatalog(devices);
            if (_ringtones is { } ringtones)
                _windowsRingtones = await Task.Run(ringtones.ReadWindowsRingtones, token);
            if (_customRingtonePath is { } customPath)
                _customRingtoneFormat = await Task.Run(() => WindowsIncomingRingtone.ProbeCustomRingtone(customPath, token), token);
            token.ThrowIfCancellationRequested();
            if (_audioStopped || IsDisposed) return;
            BindRingtones(ReadDevice(windowsRingtoneEdit));
            devices = _audioCatalog ?? devices;
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
                audioStatus.Text = new AudioDeviceCatalogException().Message;
            }
        }
    }

    private void AudioSelfTest_Click(object? sender, EventArgs e)
    {
        if (_workspace?.AudioSelfTest is not { } audio || _snapshot is null || FindForm() is not { } owner
            || _operation?.IsRunning == true || _ringtoneOperation?.IsRunning == true) return;
        var draft = _snapshot.Options.Devices with
        {
            CaptureDeviceId = ReadDevice(captureDeviceEdit),
            PlaybackDeviceId = ReadDevice(playbackDeviceEdit),
            CaptureBufferMilliseconds = (int)captureBufferEdit.Value,
            PlaybackBufferMilliseconds = (int)playbackBufferEdit.Value,
            MicrophoneVolumePercent = micVolumeSlider.Value,
            PlaybackVolumePercent = playVolumeSlider.Value,
            MicrophoneMuted = mutedEdit.Checked
        };
        using var wizard = new FrmAudioSelfTest(audio, draft, captureDeviceEdit.Text, playbackDeviceEdit.Text, SaveAudioSelfTestAsync);
        _state?.AttachWindow(wizard, "FrmAudioSelfTest.v1");
        StopAudioMeters();
        try { wizard.ShowDialog(owner); }
        finally { AudioMeterView_Changed(this, EventArgs.Empty); }
    }

    internal async Task SaveAudioSelfTestAsync(AudioDeviceSettings draft, CancellationToken token)
    {
        var snapshot = _snapshot!;
        var devices = snapshot.Options.Devices with
        {
            CaptureDeviceId = draft.CaptureDeviceId,
            PlaybackDeviceId = draft.PlaybackDeviceId,
            CaptureBufferMilliseconds = draft.CaptureBufferMilliseconds,
            PlaybackBufferMilliseconds = draft.PlaybackBufferMilliseconds,
            MicrophoneVolumePercent = draft.MicrophoneVolumePercent,
            PlaybackVolumePercent = draft.PlaybackVolumePercent
        };
        await _workspace!.Settings.SaveAsync(snapshot.Options with { Devices = devices }, snapshot, token);
        _snapshot = await _workspace.Settings.LoadAsync(token);
        BindDevice(captureDeviceEdit, _audioCatalog?.Capture ?? [], devices.CaptureDeviceId);
        BindDevice(playbackDeviceEdit, _audioCatalog?.Playback ?? [], devices.PlaybackDeviceId);
        captureBufferEdit.Value = devices.CaptureBufferMilliseconds;
        playbackBufferEdit.Value = devices.PlaybackBufferMilliseconds;
        micVolumeSlider.Value = devices.MicrophoneVolumePercent;
        playVolumeSlider.Value = devices.PlaybackVolumePercent;
    }

    private void SetDeviceEditorsEnabled(bool enabled)
    {
        captureDeviceEdit.Enabled = playbackDeviceEdit.Enabled = ringDeviceEdit.Enabled = enabled && !_audioStopped;
        audioSelfTestButton.Enabled = captureDeviceEdit.Enabled && _workspace?.AudioSelfTest is not null;
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
