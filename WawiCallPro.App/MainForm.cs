using DevExpress.XtraEditors;
using System.Reflection;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App;

public partial class MainForm : XtraForm
{
    private readonly ApplicationDescriptor? _application;
    private readonly ApplicationConfigurationService? _configuration;

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

    private void MainForm_Load(object? sender, EventArgs e)
    {
        var application = _application ?? ApplicationDescriptor.FromAssembly(Assembly.GetExecutingAssembly());
        Text = $"{application.Name} – {application.ProductLine}";
        connectionLabel.Text = _configuration?.Current.IsSipConfigured == true
            ? "Verbindung konfiguriert. Nicht verbunden."
            : "Keine Verbindung konfiguriert.";
    }
}
