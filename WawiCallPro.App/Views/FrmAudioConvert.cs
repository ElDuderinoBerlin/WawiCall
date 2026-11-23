using System.IO;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;

namespace WawiCallPro.App.Views;

public partial class FrmAudioConvert : XtraForm
{
    private readonly IAudioFileConversion? _audio;
    private readonly AppUiState? _state;
    private CancellationTokenSource? _request;
    private Task _running = Task.CompletedTask;
    private string? _result;
    private bool _closing;
    private bool _backgroundRun;

    public FrmAudioConvert()
    {
        InitializeComponent();
        AppIconHelper.Apply(this);
        WindowWorkArea.Attach(this);
        UiButtonSizing.Attach(this);
        LookAndFeel.StyleChanged += Appearance_Changed;
        SizeActions();
        UpdateActions();
        AdjustMinimum();
    }

    internal FrmAudioConvert(IAudioFileConversion audio, AppUiState? state) : this()
    {
        _audio = audio;
        _state = state;
        UpdateActions();
    }

    internal void PrepareForBackgroundRun() { _backgroundRun = true; ShowInTaskbar = false; Opacity = 0; }
    protected override bool ShowWithoutActivation => _backgroundRun || base.ShowWithoutActivation;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (_backgroundRun) parameters.ExStyle |= 0x08000000;
            return parameters;
        }
    }

    private void AdjustMinimum()
    {
        if (!IsHandleCreated) return;
        static int RequiredWidth(Control control) => control.PreferredSize.Width + control.Margin.Horizontal;
        var filesWidth = new[] { sourceLabel, targetLabel, rateLabel, channelLabel, volumeLabel }.Max(RequiredWidth)
            + Math.Max(sourceBrowse.Width, targetBrowse.Width) + sourceBrowse.Margin.Horizontal
            + Math.Max(sourcePlay.Width, resultPlay.Width) + sourcePlay.Margin.Horizontal + LogicalToDeviceUnits(210);
        var actionsWidth = convertButton.Width + cancelButton.Width + closeButton.Width + 3 * closeButton.Margin.Horizontal;
        var width = Math.Max(LogicalToDeviceUnits(610), Math.Max(filesWidth, actionsWidth) + layout.Padding.Horizontal);
        var body = filesLayout.GetPreferredSize(new Size(width, 0)).Height
            + actions.GetPreferredSize(new Size(width, 0)).Height + Font.Height * 4 + layout.Padding.Vertical;
        var area = WindowWorkArea.GetArea(this);
        MinimumSize = WindowWorkArea.LimitMinimum(new Size(width + Width - ClientSize.Width, body + Height - ClientSize.Height), area);
    }

    private void SizeActions()
    {
        UiButtonSizing.Apply(sourceBrowse, targetBrowse);
        UiButtonSizing.Apply(sourcePlay, resultPlay);
        UiButtonSizing.Apply(convertButton, cancelButton, closeButton);
    }

    private void Appearance_Changed(object? sender, EventArgs e)
    {
        SizeActions();
        AdjustMinimum();
    }
    private void Appearance_DpiChanged(object? sender, DpiChangedEventArgs e) => Appearance_Changed(sender, e);

    private void SourceBrowse_Click(object? sender, EventArgs e)
    {
        using var waiting = new UiWaitCursor(this);
        using var picker = new XtraOpenFileDialog
        {
            Title = "Audioquelle auswählen",
            Filter = "Audiodateien (*.wav;*.mp3;*.ogg;*.wma)|*.wav;*.mp3;*.ogg;*.wma",
            CheckFileExists = true, RestoreDirectory = true
        };
        if ((_state?.ShowOpenDialog(picker, this, "AudioFileConversionSource") ?? picker.ShowDialog(this)) != DialogResult.OK) return;
        sourceEdit.Text = picker.FileName;
        if (string.IsNullOrWhiteSpace(targetEdit.Text))
            targetEdit.Text = Path.Combine(Path.GetDirectoryName(picker.FileName)!,
                Path.GetFileNameWithoutExtension(picker.FileName) + "-konvertiert.wav");
    }

    private void TargetBrowse_Click(object? sender, EventArgs e)
    {
        using var waiting = new UiWaitCursor(this);
        using var picker = targetEdit.Text.Trim().Length == 0 ? new XtraSaveFileDialog() : new XtraSaveFileDialog { FileName = targetEdit.Text.Trim() };
        picker.Title = "WAV-Zieldatei auswählen";
        picker.Filter = "WAV-Datei (*.wav)|*.wav";
        picker.AddExtension = true;
        picker.OverwritePrompt = false;
        picker.RestoreDirectory = true;
        if ((_state?.ShowSaveDialog(picker, this, "AudioFileConversionTarget") ?? picker.ShowDialog(this)) == DialogResult.OK)
            targetEdit.Text = picker.FileName;
    }

    private void File_Changed(object? sender, EventArgs e)
    {
        _result = null;
        status.Text = "";
        progress.Position = 0;
        UpdateActions();
    }

    private async void Convert_Click(object? sender, EventArgs e) => await ConvertAsync();
    internal Task ConvertAsync()
    {
        if (!convertButton.Enabled) return Task.CompletedTask;
        var source = sourceEdit.Text.Trim();
        var destination = targetEdit.Text.Trim();
        var options = new AudioWaveOptions(int.Parse(rateEdit.Text, System.Globalization.CultureInfo.InvariantCulture),
            channelEdit.SelectedIndex + 1, (int)volumeEdit.Value);
        return StartAsync(async token =>
        {
            using var waiting = new UiWaitCursor(this);
            _result = null;
            progress.Position = 0;
            progress.Visible = true;
            status.Text = "Audiodatei wird konvertiert …";
            var updates = new Progress<int>(percent =>
            {
                if (!IsDisposed && !token.IsCancellationRequested && _request?.Token == token)
                    progress.Position = percent;
            });
            try { await _audio!.ConvertAsync(source, destination, options, updates, token); }
            finally { if (!IsDisposed) progress.Visible = false; }
            if (IsDisposed) return;
            _result = destination;
            progress.Position = 100;
            status.Text = "WAV-Datei gespeichert.";
        });
    }

    private async void SourcePlay_Click(object? sender, EventArgs e) => await PreviewAsync(result: false);
    private async void ResultPlay_Click(object? sender, EventArgs e) => await PreviewAsync(result: true);
    internal Task PreviewAsync(bool result)
    {
        if (!(result ? resultPlay.Enabled : sourcePlay.Enabled)) return Task.CompletedTask;
        var path = result ? _result! : sourceEdit.Text.Trim();
        return StartAsync(async token =>
        {
            status.Text = result ? "Ergebnis wird abgespielt …" : "Quelle wird abgespielt …";
            await _audio!.PreviewAsync(path, token);
            if (!IsDisposed) status.Text = "Wiedergabe beendet.";
        });
    }

    private Task StartAsync(Func<CancellationToken, Task> action)
    {
        if (_request is not null || _closing) return Task.CompletedTask;
        var request = _request = new CancellationTokenSource();
        UpdateActions();
        return _running = RunAsync(action, request);
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationTokenSource request)
    {
        try { await action(request.Token); }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            if (!IsDisposed) status.Text = "Vorgang abgebrochen.";
        }
        catch (Exception error)
        {
            if (!IsDisposed)
                status.Text = error switch
                {
                    AudioFileConversionException => error.Message,
                    FileNotFoundException => "Die Audiodatei wurde nicht gefunden.",
                    DirectoryNotFoundException => "Der gewählte Ordner wurde nicht gefunden.",
                    UnauthorizedAccessException => "Die gewählte Datei oder der Zielordner ist nicht zugänglich.",
                    IOException => "Die Audiodatei konnte nicht gelesen oder gespeichert werden. Dateiname und Schreibzugriff prüfen.",
                    _ => "Audio konnte nicht verarbeitet werden. Dateiformat und Wiedergabegerät prüfen."
                };
        }
        finally
        {
            _request = null;
            request.Dispose();
            if (!IsDisposed) UpdateActions();
        }
    }

    private void UpdateActions()
    {
        var available = _audio is not null && _request is null && !_closing;
        sourceEdit.Enabled = targetEdit.Enabled = sourceBrowse.Enabled = targetBrowse.Enabled = available;
        rateEdit.Enabled = channelEdit.Enabled = volumeEdit.Enabled = available;
        sourcePlay.Enabled = available && !string.IsNullOrWhiteSpace(sourceEdit.Text);
        resultPlay.Enabled = available && _result is not null;
        convertButton.Enabled = sourcePlay.Enabled && !string.IsNullOrWhiteSpace(targetEdit.Text);
        cancelButton.Enabled = _request is not null;
    }

    private void Cancel_Click(object? sender, EventArgs e) => _request?.Cancel();
    private void Close_Click(object? sender, EventArgs e) => Close();
    internal Task StopAsync()
    {
        _closing = true;
        _request?.Cancel();
        if (!IsDisposed) UpdateActions();
        return _running;
    }

    private async void AudioConvert_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_running.IsCompleted) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        await StopAsync();
        if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(Close));
    }

    private void DisposePage()
    {
        LookAndFeel.StyleChanged -= Appearance_Changed;
        _request?.Cancel();
    }
}
