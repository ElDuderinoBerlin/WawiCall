using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;

namespace WawiCallPro.App.Views;

/// <summary>A compact notification surface for application-provided text and actions.</summary>
public partial class FrmToast : XtraForm
{
    private readonly PageOperation _operation;
    private bool _backgroundRun;
    private Screen? _screen;

    public FrmToast()
    {
        InitializeComponent();
        _operation = new PageOperation(message => statusLabel.Text = message, this, showWaiting: false);
        Disposed += (_, _) => _operation.Dispose();
        SizeChanged += (_, _) => PlaceAtScreenEdge();
        UiButtonSizing.Attach(this);
    }

    internal void SetContent(string title, string message, string detail)
    {
        Text = titleLabel.Text = title;
        messageLabel.Text = message;
        detailLabel.Text = detail;
    }

    internal void BindActions(IReadOnlyList<AppToastAction> actions)
    {
        foreach (var action in actions)
        {
            var button = new SimpleButton { Text = action.Caption, AutoSize = true, Margin = new Padding(3) };
            button.Click += async (_, _) =>
            {
                if (_operation.IsRunning) return;
                actionsLayout.Enabled = false;
                statusLabel.Text = "";
                try { await _operation.RunAsync(action.ExecuteAsync); }
                finally { if (!IsDisposed) actionsLayout.Enabled = true; }
            };
            actionsLayout.Controls.Add(button);
        }
    }

    internal void ShowAt(Form anchor, bool backgroundRun)
    {
        _backgroundRun = backgroundRun;
        if (backgroundRun)
        {
            Opacity = 0;
            TopMost = false;
        }
        _screen = Screen.FromHandle(anchor.Handle);
        PlaceAtScreenEdge();
        // Showing an owned window would hide it together with its minimized owner.
        Show();
        PlaceAtScreenEdge();
    }

    private void PlaceAtScreenEdge()
    {
        if (_screen is null) return;
        var area = _screen.WorkingArea;
        var inset = LogicalToDeviceUnits(16);
        Location = new Point(Math.Max(area.Left, area.Right - Width - inset), Math.Max(area.Top, area.Bottom - Height - inset));
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (_backgroundRun) parameters.ExStyle |= 0x08000000;
            return parameters;
        }
    }

    private void Close_Click(object? sender, EventArgs e) => Close();
}
