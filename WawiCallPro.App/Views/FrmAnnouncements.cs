using System.IO;
using System.ComponentModel;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

public partial class FrmAnnouncements : XtraForm
{
    private static readonly ProviderChoice[] SpeechProviders =
    [
        new(AnnouncementSpeechProvider.Windows, "Windows"),
        new(AnnouncementSpeechProvider.AzureSpeech, "Azure Speech"),
        new(AnnouncementSpeechProvider.GoogleCloud, "Google Cloud")
    ];

    private readonly IAnnouncementAudio? _audio;
    private readonly IAnnouncementSpeech? _speech;
    private readonly Dictionary<AnnouncementSpeechProvider, string?> _selectedVoices = [];
    private IAnnouncementSpeech? _selectedSpeech;
    private AnnouncementSpeechProvider _selectedProvider;
    private readonly IAudioFileConversion? _conversion;
    private readonly IAudioDeviceCatalog? _catalog;
    private readonly IAudioEndpointMeterFactory? _meterFactory;
    private readonly EndpointMeterDisplay? _captureMeter;
    private readonly EndpointMeterDisplay? _playbackMeter;
    private readonly ConfigurationStore? _settings;
    private readonly AppUiState? _state;
    private CancellationTokenSource? _operation;
    private CancellationTokenSource? _finishRecording;
    private Task _pending = Task.CompletedTask;
    private string? _draft;
    private bool _loaded;
    private bool _catalogSuppressed;
    private bool _speechAvailable;
    private bool _closing;
    private bool _background;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<AnnouncementSpeechProvider, IAnnouncementSpeech?>? ResolveCloudSpeech { private get; init; }

    public FrmAnnouncements()
    {
        InitializeComponent();
        providerEdit.Properties.DataSource = SpeechProviders;
        providerEdit.EditValue = AnnouncementSpeechProvider.Windows;
        AppIconHelper.Apply(this);
        WindowWorkArea.Attach(this);
        UiButtonSizing.Attach(this);
        LookAndFeel.StyleChanged += Font_Changed;
        UpdateActions();
    }

    internal FrmAnnouncements(IAnnouncementAudio audio, IAnnouncementSpeech speech, IAudioFileConversion conversion,
        IAudioDeviceCatalog catalog, ConfigurationStore settings, AppUiState? state) : this()
    {
        _audio = audio;
        _speech = speech;
        _conversion = conversion;
        _catalog = catalog;
        _meterFactory = catalog as IAudioEndpointMeterFactory;
        _captureMeter = new EndpointMeterDisplay(capturePeak, AudioEndpointDirection.Capture);
        _playbackMeter = new EndpointMeterDisplay(playbackPeak, AudioEndpointDirection.Playback);
        _settings = settings;
        _state = state;
    }

    internal void PrepareForBackgroundRun()
    {
        _background = true;
        ShowInTaskbar = false;
        Opacity = 0;
    }

    protected override bool ShowWithoutActivation => _background || base.ShowWithoutActivation;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (_background) parameters.ExStyle |= 0x08000000;
            return parameters;
        }
    }

    internal Task LoadAsync() => _settings is null || _loaded ? Task.CompletedTask : RunAsync(async token =>
    {
        using var waiting = new UiWaitCursor(this);
        var saved = (await _settings.LoadAsync(token)).Options.Devices.Announcements;
        var devices = await _catalog!.ReadAsync(token);
        _catalogSuppressed = devices.Suppressed;
        Bind(captureEdit, devices.Capture.Select(device => new Choice(device.Id, device.DisplayName)), saved.CaptureDeviceId);
        Bind(playbackEdit, devices.Playback.Select(device => new Choice(device.Id, device.DisplayName)), saved.PlaybackDeviceId);
        providerEdit.EditValue = saved.SpeechProvider;
        _selectedVoices[saved.SpeechProvider] = saved.VoiceId;
        await LoadSpeechVoicesAsync(saved.SpeechProvider, token);
        _loaded = true;
        MeterView_Changed(this, EventArgs.Empty);
        statusLabel.Text = devices.Suppressed ? "Audiogeräte sind im Prüfmodus deaktiviert." : "";
    });

    private async Task LoadSpeechVoicesAsync(AnnouncementSpeechProvider provider, CancellationToken token)
    {
        _selectedProvider = provider;
        _selectedSpeech = null;
        _speechAvailable = false;
        var windows = provider == AnnouncementSpeechProvider.Windows;
        var defaultVoiceLabel = windows ? "Systemstandard" : "Standardstimme";
        var savedVoice = _selectedVoices.GetValueOrDefault(provider);
        Bind(voiceEdit, [], savedVoice, defaultVoiceLabel);
        providerHint.Text = "";
        providerHint.Visible = false;
        var hint = "Beim Klick auf „Text vertonen“ wird der Ansagetext an den gewählten Anbieter gesendet.";
        IReadOnlyList<AnnouncementVoice> available = [];
        try
        {
            _selectedSpeech = windows ? _speech : await Task.Run(() => ResolveCloudSpeech?.Invoke(provider), token);
            token.ThrowIfCancellationRequested();
            available = _selectedSpeech?.GetVoices() ?? [];
            if (!windows && _selectedSpeech is null)
                hint = provider == AnnouncementSpeechProvider.AzureSpeech
                    ? "Azure Speech ist nicht eingerichtet. Die Umgebungsvariable WAWICALLPRO_AZURE_SPEECH_ENDPOINT prüfen."
                    : "Google Cloud ist nicht eingerichtet. Die Umgebungsvariable WAWICALLPRO_GOOGLE_TTS_PROJECT prüfen.";
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            _selectedSpeech = null;
            hint = "Der Sprachanbieter konnte nicht geladen werden. Lokale Anmeldung und Einrichtung prüfen.";
        }
        token.ThrowIfCancellationRequested();
        _speechAvailable = available.Count > 0;
        if (!windows && !_speechAvailable && _selectedSpeech is not null)
            hint = "Für den gewählten Sprachanbieter sind keine Stimmen verfügbar.";
        var choices = available.Select(voice => new Choice(voice.Id, $"{voice.DisplayName} ({voice.Language})"));
        Bind(voiceEdit, choices, savedVoice, defaultVoiceLabel);
        providerHint.Text = windows ? "" : hint;
        providerHint.Visible = !windows;
        RefreshMinimum();
    }

    private static void Bind(LookUpEdit editor, IEnumerable<Choice> available, string? saved) =>
        Bind(editor, available, saved, "Systemstandard");

    private static void Bind(LookUpEdit editor, IEnumerable<Choice> available, string? saved, string defaultLabel)
    {
        var choices = new List<Choice> { new("", defaultLabel) };
        choices.AddRange(available);
        if (saved is not null && choices.All(item => item.Id != saved))
            choices.Add(new(saved, "Nicht verfügbar (gespeichert)"));
        editor.Properties.DisplayMember = nameof(Choice.Name);
        editor.Properties.ValueMember = nameof(Choice.Id);
        editor.Properties.DataSource = choices;
        editor.Properties.Columns.Clear();
        editor.Properties.Columns.Add(new LookUpColumnInfo(nameof(Choice.Name)));
        editor.EditValue = saved ?? "";
    }

    private static string? SelectedId(LookUpEdit editor) => editor.EditValue is string { Length: > 0 } id ? id : null;

    internal Task RecordAsync()
    {
        if (!recordButton.Enabled) return Task.CompletedTask;
        var device = SelectedId(captureEdit);
        return RunAsync(async token =>
        {
            using var finish = new CancellationTokenSource();
            _finishRecording = finish;
            stopButton.Text = "Aufnahme stoppen";
            statusLabel.Text = "Mikrofonaufnahme läuft …";
            try { await CreateDraftAsync(path => _audio!.RecordAsync(device, path, finish.Token, token)); }
            finally { _finishRecording = null; }
        });
    }

    internal void StopAction()
    {
        if (_finishRecording is { } finish) finish.Cancel();
        else _operation?.Cancel();
    }

    internal Task SynthesizeAsync()
    {
        if (!speechButton.Enabled || _selectedSpeech is not { } speech) return Task.CompletedTask;
        var text = textEdit.Text;
        var voice = SelectedId(voiceEdit);
        return RunAsync(token => CreateDraftAsync(path => speech.WriteWaveAsync(text, voice, path, token)));
    }

    private async Task CreateDraftAsync(Func<string, Task> create)
    {
        var path = Path.Combine(Path.GetTempPath(), "WawiCallPro-announcement-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            await create(path);
            if (IsDisposed || _closing) return;
            DeleteDraft();
            _draft = path;
            statusLabel.Text = "Ansage ist zum Abspielen und Speichern bereit.";
        }
        finally
        {
            if (_draft != path) File.Delete(path);
        }
    }

    internal Task PreviewAsync()
    {
        if (!previewButton.Enabled) return Task.CompletedTask;
        var device = SelectedId(playbackEdit);
        var path = _draft!;
        return RunAsync(async token =>
        {
            var deviceStatus = _meterFactory is { } factory ? " " + EndpointMeterDisplay.Describe(_playbackMeter?.Update(factory, device)) : "";
            statusLabel.Text = "Ansage wird abgespielt …" + deviceStatus;
            var options = (await _settings!.LoadAsync(token)).Options.Devices;
            await _audio!.PreviewAsync(path, options with { PlaybackDeviceId = device }, token);
            statusLabel.Text = "Wiedergabe beendet." + deviceStatus;
        });
    }

    private void MeterView_Changed(object? sender, EventArgs e)
    {
        if (meterTimer is null) return;
        if (_loaded && !_closing && !_catalogSuppressed && Visible && _meterFactory is not null)
        {
            MeterTimer_Tick(this, EventArgs.Empty);
            meterTimer.Start();
        }
        else StopMeters();
    }

    private void MeterTimer_Tick(object? sender, EventArgs e)
    {
        if (_meterFactory is not { } factory) return;
        _captureMeter?.Update(factory, SelectedId(captureEdit));
        _playbackMeter?.Update(factory, SelectedId(playbackEdit));
    }

    private void StopMeters()
    {
        meterTimer.Stop();
        _captureMeter?.Dispose();
        _playbackMeter?.Dispose();
    }

    internal Task SavePreferencesAsync()
    {
        if (!preferencesButton.Enabled) return Task.CompletedTask;
        var selections = new AnnouncementAudioSettings
        {
            CaptureDeviceId = SelectedId(captureEdit),
            PlaybackDeviceId = SelectedId(playbackEdit),
            VoiceId = SelectedId(voiceEdit),
            SpeechProvider = _selectedProvider
        };
        return RunAsync(async token =>
        {
            using var waiting = new UiWaitCursor(this);
            var current = await _settings!.LoadAsync(token);
            await _settings.SaveAsync(current.Options with { Devices = current.Options.Devices with { Announcements = selections } }, current, token);
            statusLabel.Text = "Sprachanbieter, Stimme und Audiogeräte gespeichert.";
        });
    }

    internal Task ExportAsync(string destination)
    {
        if (!exportButton.Enabled) return Task.CompletedTask;
        var source = _draft!;
        return RunAsync(async token =>
        {
            using var waiting = new UiWaitCursor(this);
            await _conversion!.ConvertAsync(source, destination, new AudioWaveOptions(null, 1, 100), null, token);
            statusLabel.Text = "WAV-Datei gespeichert.";
        });
    }

    private Task RunAsync(Func<CancellationToken, Task> work)
    {
        if (_closing || _operation is not null) return Task.CompletedTask;
        var operation = _operation = new CancellationTokenSource();
        stopButton.Text = "Vorgang stoppen";
        UpdateActions();
        return _pending = CompleteAsync(work, operation);
    }

    private async Task CompleteAsync(Func<CancellationToken, Task> work, CancellationTokenSource operation)
    {
        try { await work(operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        { if (!IsDisposed) statusLabel.Text = "Vorgang abgebrochen."; }
        catch (Exception error)
        {
            if (!IsDisposed) statusLabel.Text = error is AudioFileConversionException or AnnouncementSpeechException or ConfigurationException
                ? error.Message : "Ansagenvorgang fehlgeschlagen. Audiogerät oder Dateizugriff prüfen.";
        }
        finally
        {
            _operation = null;
            operation.Dispose();
            if (!IsDisposed) UpdateActions();
        }
    }

    private void UpdateActions()
    {
        var ready = _loaded && !_closing && _operation is null;
        captureEdit.Enabled = playbackEdit.Enabled = providerEdit.Enabled = textEdit.Enabled = preferencesButton.Enabled = ready;
        voiceEdit.Enabled = ready && _speechAvailable;
        recordButton.Enabled = ready && !_catalogSuppressed;
        speechButton.Enabled = ready && _speechAvailable && !string.IsNullOrWhiteSpace(textEdit.Text);
        previewButton.Enabled = ready && !_catalogSuppressed && _draft is not null;
        exportButton.Enabled = ready && _draft is not null;
        stopButton.Enabled = _operation is not null && !_closing;
    }

    private void RefreshMinimum()
    {
        if (!IsHandleCreated) return;
        var width = Math.Max(LogicalToDeviceUnits(540), actions.GetPreferredSize(Size.Empty).Width + layout.Padding.Horizontal);
        var contentWidth = width - layout.Padding.Horizontal;
        var height = PreferredRowHeight(choicesLayout) + PreferredRowHeight(textLabel)
            + textEdit.Font.Height * 3 + textEdit.Margin.Vertical
            + PreferredRowHeight(statusLabel) + PreferredRowHeight(actions) + layout.Padding.Vertical;
        MinimumSize = WindowWorkArea.LimitMinimum(new Size(width + Width - ClientSize.Width, height + Height - ClientSize.Height),
            WindowWorkArea.GetArea(this));

        int PreferredRowHeight(Control control) => control.GetPreferredSize(
            new Size(Math.Max(1, contentWidth - control.Margin.Horizontal), 0)).Height + control.Margin.Vertical;
    }

    private async void Window_Shown(object? sender, EventArgs e) { RefreshMinimum(); await LoadAsync(); }
    private void Font_Changed(object? sender, EventArgs e) => RefreshMinimum();
    private void Dpi_Changed(object? sender, DpiChangedEventArgs e) => RefreshMinimum();
    private void Text_Changed(object? sender, EventArgs e) => UpdateActions();
    private async void Provider_Changed(object? sender, EventArgs e)
    {
        if (!_loaded || _operation is not null || providerEdit.EditValue is not AnnouncementSpeechProvider provider
            || provider == _selectedProvider) return;
        _selectedVoices[_selectedProvider] = SelectedId(voiceEdit);
        await RunAsync(token => LoadSpeechVoicesAsync(provider, token));
    }
    private async void Record_Click(object? sender, EventArgs e) => await RecordAsync();
    private void Stop_Click(object? sender, EventArgs e) => StopAction();
    private async void Speech_Click(object? sender, EventArgs e) => await SynthesizeAsync();
    private async void Preview_Click(object? sender, EventArgs e) => await PreviewAsync();
    private async void Preferences_Click(object? sender, EventArgs e) => await SavePreferencesAsync();

    private async void Export_Click(object? sender, EventArgs e)
    {
        using var waiting = new UiWaitCursor(this);
        using var picker = new XtraSaveFileDialog
        {
            Title = "Ansage als WAV speichern", Filter = "WAV-Datei (*.wav)|*.wav",
            AddExtension = true, OverwritePrompt = false, RestoreDirectory = true
        };
        if ((_state?.ShowSaveDialog(picker, this, "AnnouncementExport") ?? picker.ShowDialog(this)) == DialogResult.OK)
            await ExportAsync(picker.FileName);
    }

    internal Task StopAsync()
    {
        _closing = true;
        StopMeters();
        _operation?.Cancel();
        UpdateActions();
        return _pending;
    }

    private async void Window_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_pending.IsCompleted) return;
        e.Cancel = true;
        if (_closing) return;
        await StopAsync();
        if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(Close));
    }

    private void DisposePage()
    {
        LookAndFeel.StyleChanged -= Font_Changed;
        StopMeters();
        _operation?.Cancel();
        DeleteDraft();
    }

    private void DeleteDraft()
    {
        if (_draft is null) return;
        try { File.Delete(_draft); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        _draft = null;
    }

    private sealed record Choice(string Id, string Name);
    private sealed record ProviderChoice(AnnouncementSpeechProvider Id, string Name);
}
