using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

public partial class FrmSettings : XtraForm
{
    private bool _canClose;
    private bool _closing;
    public FrmSettings() { InitializeComponent(); Load += Settings_Load; }
    public FrmSettings(AppWorkspace workspace) : this() => settingsPage.Bind(workspace);
    internal Task StopAsync() => settingsPage.StopAsync();
    private void Close_Click(object? sender, EventArgs e) => Close();
    private async void Settings_Load(object? sender, EventArgs e) => await settingsPage.ReloadAsync();
    private async void Settings_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        try { await StopAsync(); _canClose = true; Close(); }
        finally { _closing = false; }
    }
    private void DisposePage() { }
}
