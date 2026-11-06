using DevExpress.XtraEditors;
using System.Reflection;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App;

public partial class MainForm : XtraForm
{
    private readonly ApplicationDescriptor? _application;
    private readonly ApplicationConfigurationService? _configuration;
    private bool _backgroundRun;

    public MainForm()
    {
        InitializeComponent();
    }

    public MainForm(ApplicationDescriptor application) : this()
    {
        ArgumentNullException.ThrowIfNull(application);
        _application = application;
    }

    public MainForm(ApplicationDescriptor application, ApplicationConfigurationService configuration) : this(application)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    /// <summary>Background diagnostics retain the real message loop without activating an invisible window.</summary>
    internal void PrepareForBackgroundRun()
    {
        _backgroundRun = true;
        ShowInTaskbar = false;
        Opacity = 0;
    }

    protected override bool ShowWithoutActivation => _backgroundRun || base.ShowWithoutActivation;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (_backgroundRun) parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE also prevents activation by mouse input.
            return parameters;
        }
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        var application = _application ?? ApplicationDescriptor.FromAssembly(Assembly.GetExecutingAssembly());
        Text = $"{application.Name} – {application.ProductLine}";
        connectionLabel.Text = _configuration?.Current.IsSipConfigured == true
            ? "Verbindung konfiguriert. Nicht verbunden."
            : "Keine Verbindung konfiguriert.";
    }
}
