using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

public partial class FrmAudioSelfTest : XtraForm
{
    private readonly IAudioSelfTest? _audio;
    private readonly Func<AudioDeviceSettings, CancellationToken, Task>? _save;
    private readonly AudioDeviceSettings _initial = new();
    private readonly AudioPlaybackVolume _liveVolume = new(100);
    private AudioSelfTestSample? _sample;
    private CancellationTokenSource? _operation;
    private int _step;
    private bool _saving;
    private bool _closeRequested;
    private bool _tonePlayed;
    private bool _accepted;

    public FrmAudioSelfTest()
    {
        InitializeComponent();
        AppIconHelper.Apply(this);
        WindowWorkArea.Attach(this);
        UiButtonSizing.Attach(this);
        UpdateLayout();
        UpdateStep();
    }

    internal FrmAudioSelfTest(IAudioSelfTest audio, AudioDeviceSettings initial, string microphone, string headphones,
        Func<AudioDeviceSettings, CancellationToken, Task> save) : this()
    {
        _audio = audio;
        _initial = initial;
        _save = save;
        captureBufferEdit.Value = initial.CaptureBufferMilliseconds;
        playbackBufferEdit.Value = initial.PlaybackBufferMilliseconds;
        microphoneVolume.Value = initial.MicrophoneVolumePercent;
        headphonesVolume.Value = initial.PlaybackVolumePercent;
        microphoneName.Text = microphone;
        headphonesName.Text = headphones;
        UpdateActions();
    }

    internal AudioDeviceSettings Draft => _initial with
    {
        CaptureBufferMilliseconds = (int)captureBufferEdit.Value,
        PlaybackBufferMilliseconds = (int)playbackBufferEdit.Value,
        MicrophoneVolumePercent = microphoneVolume.Value,
        PlaybackVolumePercent = headphonesVolume.Value
    };

    private void UpdateStep()
    {
        layout.SuspendLayout();
        try
        {
            headphonesStep.Visible = _step == 0;
            microphoneStep.Visible = _step == 1;
            codecStep.Visible = _step == 2;
            stepTitle.Text = _step switch { 0 => "1 von 3 · Kopfhörer", 1 => "2 von 3 · Mikrofon", _ => "3 von 3 · Telefonie-Codecs" };
            statusLabel.Text = "";
            UpdateActions();
        }
        finally { layout.ResumeLayout(true); }
    }

    private void UpdateActions()
    {
        var ready = _operation is null && _audio is not null;
        backButton.Enabled = ready && _step > 0;
        nextButton.Enabled = ready;
        nextButton.Text = _step == 2 ? "Übernehmen" : "Weiter";
        toneButton.Enabled = ready;
        heardEdit.Enabled = ready && _tonePlayed;
        recordButton.Enabled = ready;
        sampleButton.Enabled = ready && _sample is not null;
        codecButton.Enabled = ready && _sample is not null;
        captureBufferEdit.Enabled = playbackBufferEdit.Enabled = microphoneVolume.Enabled = _operation is null;
        headphonesVolume.Enabled = !_saving;
        cancelButton.Enabled = !_saving;
        cancelButton.Text = _operation is not null && !_saving ? "Stopp" : "Abbrechen";
    }

    private void Back_Click(object? sender, EventArgs e) { _step--; UpdateStep(); }
    private async void Next_Click(object? sender, EventArgs e)
    {
        if (_step == 2) await ApplyAsync();
        else { _step++; UpdateStep(); }
    }
    private async void Tone_Click(object? sender, EventArgs e) => await PlayAsync(null);
    private async void Sample_Click(object? sender, EventArgs e) => await PlayAsync(_sample);
    private async void Record_Click(object? sender, EventArgs e) => await RecordAsync();
    private async void Codec_Click(object? sender, EventArgs e) => await CheckCodecsAsync();

    private Task PlayAsync(AudioSelfTestSample? sample)
    {
        _liveVolume.Percent = headphonesVolume.Value;
        if (sample is null)
        {
            _tonePlayed = false;
            heardEdit.Checked = false;
            headphoneDetails.Text = "";
        }
        var opened = new Progress<AudioSelfTestDevice>(info =>
        {
            if (!IsDisposed && _step == 0 && _operation is not null) headphoneDetails.Text = Describe(info);
        });
        return RunAsync("Wiedergabe läuft …", "Wiedergabe fehlgeschlagen. Ausgabegerät und Puffer prüfen.", async token =>
        {
            var info = await _audio!.PlayAsync(Draft, sample, _liveVolume, opened, token);
            if (sample is null)
            {
                headphoneDetails.Text = Describe(info);
                _tonePlayed = true;
            }
            statusLabel.Text = info.Muted || info.WindowsVolume == 0
                ? "Die Windows-Ausgabe ist stumm oder auf 0 % eingestellt."
                : "Wiedergabe beendet.";
        });
    }

    internal Task RecordAsync()
    {
        ClearSample();
        var meter = new Progress<System.Collections.Immutable.ImmutableArray<int>>(bands =>
        {
            if (!IsDisposed && _operation is not null && _step == 1) spectrum.SetSpectrum(bands.AsSpan());
        });
        return RunAsync("Aufnahme läuft. Bitte jetzt sprechen …", "Mikrofonprobe fehlgeschlagen. Mikrofon und Puffer prüfen.", async token =>
        {
            _sample = await _audio!.RecordAsync(Draft, meter, token);
            var peak = _sample.Peak == 0 ? "kein Signal" : $"{20 * Math.Log10(_sample.Peak / 32768d):0.#} dBFS";
            microphoneDetails.Text = $"{Describe(_sample.Device)} · Spitze: {peak}";
            statusLabel.Text = _sample.Peak < 33 ? "Kaum Mikrofonsignal. Stummschaltung und Pegel prüfen." : "Die Mikrofonprobe ist abspielbereit.";
        });
    }

    internal Task CheckCodecsAsync()
    {
        var sample = _sample;
        if (sample is null) return Task.CompletedTask;
        return RunAsync("Codecs werden geprüft …", "Der Codec-Test konnte nicht abgeschlossen werden.", async token =>
        {
            var results = await _audio!.CheckCodecsAsync(sample, token);
            codecDetails.Text = string.Join(Environment.NewLine, results.Select(result => result.Passed
                ? $"{result.Name} · {result.Packets} Blöcke kodiert und dekodiert"
                : $"{result.Name} · Fehler nach {result.Packets} Blöcken"));
            statusLabel.Text = sample.Peak < 33 ? "Die Probe enthält kaum Signal." : "";
        });
    }

    internal Task ApplyAsync() => RunAsync("Einstellungen werden übernommen …", "Die Audioeinstellungen konnten nicht gespeichert werden.", async token =>
    {
        _saving = true;
        UpdateActions();
        using (new UiWaitCursor(this)) await _save!(Draft, token);
        _accepted = true;
    });

    private async Task RunAsync(string working, string failure, Func<CancellationToken, Task> action)
    {
        if (_operation is not null || _audio is null) return;
        using var operation = new CancellationTokenSource();
        _operation = operation;
        statusLabel.Text = working;
        UpdateActions();
        try { await action(operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { statusLabel.Text = "Test gestoppt."; }
        catch (ConfigurationException error) { statusLabel.Text = error.Message; }
        catch (Exception) { statusLabel.Text = failure; }
        finally
        {
            _operation = null;
            _saving = false;
            spectrum.SetSpectrum(AudioSpectrum.Silence.AsSpan());
            UpdateActions();
        }
        if (_accepted) DialogResult = DialogResult.OK;
        if (_closeRequested || _accepted) Close();
    }

    private void Cancel_Click(object? sender, EventArgs e)
    {
        if (_operation is { } action) action.Cancel();
        else { DialogResult = DialogResult.Cancel; Close(); }
    }

    private void Wizard_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_operation is not null)
        {
            e.Cancel = true;
            if (!_saving)
            {
                _closeRequested = true;
                _operation.Cancel();
            }
        }
        else ClearSample();
    }

    private void ClearSample()
    {
        if (_sample is { } previous) Array.Clear(previous.Samples);
        _sample = null;
        microphoneDetails.Text = "";
        codecDetails.Text = "";
    }

    private void HeadphonesVolume_Changed(object? sender, EventArgs e) => _liveVolume.Percent = headphonesVolume.Value;

    private void Volume_ToolTip(object? sender, TrackBarValueToolTipEventArgs e)
    {
        if (sender is TrackBarControl slider) e.ShowArgs.ToolTip = $"{slider.Value} %";
        e.ShowArgs.ToolTipLocation = DevExpress.Utils.ToolTipLocation.TopCenter;
    }

    private void Wizard_SizeChanged(object? sender, EventArgs e) => UpdateLayout();
    private void UpdateLayout()
    {
        var width = Math.Max(1, ClientSize.Width - layout.Padding.Horizontal - LogicalToDeviceUnits(16));
        foreach (var label in new[] { headphoneDetails, microphoneDetails, codecDetails, statusLabel, headphoneHint, microphoneHint, codecHint, headphonesName, microphoneName })
        {
            if (label.MaximumSize.Width != width) label.MaximumSize = new Size(width, 0);
        }
    }

    private static string Describe(AudioSelfTestDevice device) =>
        $"{device.Name} · {device.SampleRate:N0} Hz · {device.Channels} {(device.Channels == 1 ? "Kanal" : "Kanäle")} · {device.Bits} Bit · Puffer {device.BufferMs:0.#} ms · Windows {device.WindowsVolume} %{(device.Muted ? " (stumm)" : "")}";
}
