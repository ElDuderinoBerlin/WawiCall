using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

public partial class FrmSettings : XtraForm
{
    private bool _canClose;
    private bool _closing;
    private bool _backgroundRun;
    private bool _sizingMinimum;
    private Task? _stopTask;
    internal void PrepareForBackgroundRun()
    {
        _backgroundRun = true;
        Opacity = 0;
        ShowInTaskbar = false;
    }
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
    public FrmSettings()
    {
        InitializeComponent(); UiButtonSizing.Attach(this); AppIconHelper.Apply(this);
        Opacity = 0;
        WindowWorkArea.Attach(this);
        settingsPage.CloseRequested += Settings_CloseRequested;
        settingsPage.SaveCompleted += Settings_SaveCompleted;
        settingsPage.RestartRequested += Settings_RestartRequested; settingsPage.ContentSizeChanged += ContentSize_Changed;
        SizeChanged += ContentSize_Changed; FontChanged += ContentSize_Changed; DpiChanged += ContentSize_DpiChanged; Shown += Settings_Shown;
    }
    private void ContentSize_Changed(object? sender, EventArgs e)
    {
        if (_sizingMinimum || !IsHandleCreated || IsDisposed || settingsPage.Width < LogicalToDeviceUnits(300)) return;
        _sizingMinimum = true;
        try
        {
            var height = Math.Min(settingsPage.GetRequiredGeneralHeight(), settingsPage.GetMinimumWindowHeight()) + layout.Padding.Vertical + settingsPage.Margin.Vertical
                + Height - ClientSize.Height;
            var area = WindowWorkArea.GetArea(this);
            MinimumSize = WindowWorkArea.LimitMinimum(new Size(LogicalToDeviceUnits(680), height), area);
            WindowWorkArea.Constrain(this, area);
            settingsPage.FitSipViewport(Height >= area.Height - LogicalToDeviceUnits(2));
        }
        finally { _sizingMinimum = false; }
    }
    internal bool RestartRequested { get; private set; }
    private void ContentSize_DpiChanged(object? sender, DpiChangedEventArgs e) => ContentSize_Changed(sender, e);
    private void Settings_RestartRequested(object? sender, EventArgs e) { RestartRequested = true; Close(); }
    public FrmSettings(AppWorkspace workspace, Func<Task>? beforePluginSwitch = null, Func<Task>? afterPluginSwitch = null) : this()
    {
        settingsPage.Bind(workspace, deferInitialBinding: true);
        settingsPage.ConfigurePluginSwitch(beforePluginSwitch, afterPluginSwitch);
    }
    internal void SelectPluginsTab() => settingsPage.SelectPluginsTab();
    internal void SelectMailTab() => settingsPage.SelectMailTab();
    internal void SelectAutomationTab(string id) => settingsPage.SelectAutomationTab(id);
    internal void ConfigureAudioDevices(WawiCallPro.Interfaces.Audio.IAudioDeviceCatalog catalog) => settingsPage.ConfigureAudioDevices(catalog);
    internal DevExpress.XtraTab.XtraTabControl SettingsTabs => settingsPage.SettingsTabs;
    internal async Task<bool> PrepareAsync(CancellationToken token = default)
    {
        await settingsPage.ReloadAsync().WaitAsync(token);
        return settingsPage.HasLoadedSettings;
    }
    internal Task StopAsync() => _stopTask ??= settingsPage.StopAsync();
    internal void ConfigureLayoutPersistence(AppUiState? state)
    {
        settingsPage.ConfigureLayoutPersistence(state);
        state?.AttachWindow(this, "FrmSettings");
    }
    private void Settings_CloseRequested(object? sender, EventArgs e) { DialogResult = DialogResult.Cancel; Close(); }
    private void Settings_SaveCompleted(object? sender, EventArgs e) { DialogResult = DialogResult.OK; Close(); }
    private void Settings_Shown(object? sender, EventArgs e)
    {
        // The native tab handles and restored window placement are available only once the modal is shown.
        try
        {
            ContentSize_Changed(this, EventArgs.Empty);
            PerformLayout();
            settingsPage.PerformLayout();
            Refresh();
        }
        finally
        {
            if (!_backgroundRun && !IsDisposed) Opacity = 1;
        }
    }
    private async void Settings_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        try
        {
            await StopAsync();
            _canClose = true;
            // A modal dialog ignores a nested Close while the original FormClosing is still running.
            if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(Close));
        }
        finally { _closing = false; }
    }
    private void DisposePage() { }
}
