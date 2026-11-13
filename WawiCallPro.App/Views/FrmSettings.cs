using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

public partial class FrmSettings : XtraForm
{
    private bool _canClose;
    private bool _closing;
    private bool _backgroundRun;
    private bool _sizingMinimum;
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
        InitializeComponent(); UiButtonSizing.Attach(this); settingsPage.ConfigureCommandButtons(closeButton);
        settingsPage.RestartRequested += Settings_RestartRequested; settingsPage.ContentSizeChanged += ContentSize_Changed;
        SizeChanged += ContentSize_Changed; FontChanged += ContentSize_Changed; Load += Settings_Load;
    }
    private void ContentSize_Changed(object? sender, EventArgs e)
    {
        if (_sizingMinimum || !IsHandleCreated || IsDisposed || settingsPage.Width < LogicalToDeviceUnits(300)) return;
        _sizingMinimum = true;
        try
        {
            var height = settingsPage.GetRequiredGeneralHeight() + Height - settingsPage.Height;
            MinimumSize = new(MinimumSize.Width, height);
        }
        finally { _sizingMinimum = false; }
    }
    internal bool RestartRequested { get; private set; }
    private void Settings_RestartRequested(object? sender, EventArgs e) { RestartRequested = true; Close(); }
    public FrmSettings(AppWorkspace workspace, Func<Task>? beforePluginSwitch = null) : this()
    {
        settingsPage.Bind(workspace);
        settingsPage.ConfigurePluginSwitch(beforePluginSwitch);
    }
    internal void SelectPluginsTab() => settingsPage.SelectPluginsTab();
    internal DevExpress.XtraTab.XtraTabControl SettingsTabs => settingsPage.SettingsTabs;
    internal Task StopAsync() => settingsPage.StopAsync();
    internal void ConfigureLayoutPersistence(AppUiState? state)
    {
        settingsPage.ConfigureLayoutPersistence(state);
        state?.AttachWindow(this, "FrmSettings");
    }
    private void Close_Click(object? sender, EventArgs e) => Close();
    private async void Settings_Load(object? sender, EventArgs e) { await settingsPage.ReloadAsync(); ContentSize_Changed(this, EventArgs.Empty); }
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
