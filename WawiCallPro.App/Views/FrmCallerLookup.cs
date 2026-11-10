using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

public partial class FrmCallerLookup : XtraForm
{
    private bool _backgroundRun;
    private bool _closing;
    private bool _canClose;
    public FrmCallerLookup() { InitializeComponent(); AppIconHelper.Apply(this); }
    public FrmCallerLookup(AppWorkspace workspace, AppUiState? state) : this()
    { callerPage.Bind(workspace, state); state?.AttachWindow(this, "FrmCallerLookup"); }
    internal CallerLookupPage Page => callerPage;
    internal Task StopAsync() => callerPage.StopAsync();
    internal void PrepareForBackgroundRun() { _backgroundRun = true; ShowInTaskbar = false; Opacity = 0; }
    protected override bool ShowWithoutActivation => _backgroundRun || base.ShowWithoutActivation;
    protected override CreateParams CreateParams
    {
        get { var parameters = base.CreateParams; if (_backgroundRun) parameters.ExStyle |= 0x08000000; return parameters; }
    }
    private async void Caller_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        try { await StopAsync(); _canClose = true; Close(); }
        finally { _closing = false; }
    }
}
