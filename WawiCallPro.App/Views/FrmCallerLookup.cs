using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App.Views;

public partial class FrmCallerLookup : XtraForm
{
    private bool _backgroundRun;
    private bool _closing;
    private bool _canClose;
    private bool _passive;
    private bool _ended;
    private AppWorkspace? _workspace;
    private AppUiState? _state;
    private WawiCallPro.Interfaces.Telephony.ITelephonyService? _telephony;
    private string _callStatus = "";
    public FrmCallerLookup()
    {
        InitializeComponent();
        callerPage.StatusChanged += CallerPage_StatusChanged;
        Caller_FontChanged(this, EventArgs.Empty);
        UpdateStatusBar();
        UiButtonSizing.Attach(this);
        AppIconHelper.Apply(this);
    }
    public FrmCallerLookup(AppWorkspace workspace, AppUiState? state) : this()
    {
        _workspace = workspace;
        _state = state;
        _telephony = workspace.Telephony;
        callerPage.Bind(workspace, state);
        state?.AttachWindow(this, "FrmCallerLookup");
    }
    internal CallerLookupPage Page => callerPage;
    internal Guid? CallId { get; private set; }
    internal bool CanReuse => _ended && Visible && !_closing && !IsDisposed && _workspace is not null;

    internal void BindCall(WawiCallPro.Interfaces.Telephony.CallSnapshot call, Func<WawiCallPro.Interfaces.PhoneNumbers.CallerMatchDecision, CancellationToken, Task> accept)
    {
        CallId = call.Id;
        _ended = false;
        callerPage.BindCall(call, accept);
        callControls.UseStatusBar(DisplayCallStatus);
        callControls.Bind(_telephony, call);
        callControls.Visible = true;
        UpdateCaption(call);
    }

    internal void PrepareForReuse()
    {
        if (!CanReuse) throw new InvalidOperationException("Das Anruffenster gehört noch zu einem laufenden Gespräch.");
        _ended = false;
        CallId = null;
        Hide();
        layout.SuspendLayout();
        try
        {
            layout.Controls.Remove(callerPage);
            layout.Controls.Remove(callControls);
            callControls.Dispose();
            callerPage.StatusChanged -= CallerPage_StatusChanged;
            callerPage.Dispose();
            callControls = new CallControlPanel { Dock = DockStyle.Fill, Visible = false };
            callerPage = new CallerLookupPage { Dock = DockStyle.Fill };
            callerPage.Bind(_workspace!, _state);
            callerPage.StatusChanged += CallerPage_StatusChanged;
            _callStatus = "";
            UpdateStatusBar();
            layout.Controls.Add(callControls, 0, 0);
            layout.Controls.Add(callerPage, 0, 1);
        }
        finally { layout.ResumeLayout(); }
    }

    internal void ObserveCall(WawiCallPro.Interfaces.Telephony.CallSnapshot call)
    {
        if (CallId != call.Id) return;
        callerPage.ObserveCallAddress(call);
        callControls.Observe(call);
        UpdateCaption(call);
    }

    internal async Task CompleteCallAsync(Guid id)
    {
        if (CallId != id || _ended) return;
        await Task.WhenAll(callerPage.EndCallAsync(), callControls.StopAsync());
        if (CallId == id && !IsDisposed) _ended = true;
    }

    private void CallerPage_StatusChanged(object? sender, EventArgs e) => UpdateStatusBar();

    private void Caller_FontChanged(object? sender, EventArgs e)
    {
        statusText.ItemAppearance.Normal.Font = Font;
        statusText.ItemAppearance.Normal.Options.UseFont = true;
    }

    private void DisplayCallStatus(string status)
    {
        _callStatus = status;
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        var lookupStatus = callerPage.StatusText;
        statusText.Caption = _callStatus.Length == 0 ? lookupStatus :
            lookupStatus.Length == 0 ? _callStatus : _callStatus + "\n" + lookupStatus;
        statusText.Hint = statusText.Caption;
    }

    private void UpdateCaption(CallSnapshot call)
    {
        var address = call.RemoteAddress;
        var number = address.Presentation switch
        {
            NumberPresentation.Withheld => "Rufnummer unterdrückt",
            NumberPresentation.Available => !string.IsNullOrWhiteSpace(address.OriginalValue) ? address.OriginalValue : address.E164,
            _ => null
        };
        var display = number is null ? "" : string.Concat(number.Where(character => !char.IsControl(character))).Trim();
        if (display.Length == 0) display = "Rufnummer unbekannt";
        else if (call.Direction == CallDirection.Incoming && address.Presentation == NumberPresentation.Available &&
            callerPage.DescribeIncomingRegion(address) is { Length: > 0 } region)
            display += " (" + region + ")";
        Text = "Anrufzuordnung · " + display + " · " + call.StartedAtUtc.LocalDateTime.ToString("T");
    }
    internal void BindNotes(CallNotesSession notes)
    {
        if (CallId != notes.CallId) throw new InvalidOperationException("Call notes must belong to this caller window.");
        callerPage.BindNotes(notes);
    }
    internal void ShowForIncoming(IWin32Window owner)
    {
        _passive = true;
        try
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Show(owner);
        }
        finally { _passive = false; }
    }
    internal Task StopAsync() => Task.WhenAll(callerPage.StopAsync(), callControls.StopAsync());
    internal void PrepareForBackgroundRun() { _backgroundRun = true; ShowInTaskbar = false; Opacity = 0; }
    protected override bool ShowWithoutActivation => _backgroundRun || _passive || base.ShowWithoutActivation;
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
        try { await StopAsync(); _canClose = true; if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(Close)); }
        finally { _closing = false; }
    }
}
