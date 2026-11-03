using DevExpress.XtraEditors;
using System.Reflection;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.App;

public partial class MainForm : XtraForm
{
    private readonly ApplicationDescriptor? _application;

    public MainForm()
    {
        InitializeComponent();
    }

    public MainForm(ApplicationDescriptor application) : this()
    {
        ArgumentNullException.ThrowIfNull(application);
        _application = application;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        var application = _application ?? ApplicationDescriptor.FromAssembly(Assembly.GetExecutingAssembly());
        Text = $"{application.Name} – {application.ProductLine}";
    }
}
