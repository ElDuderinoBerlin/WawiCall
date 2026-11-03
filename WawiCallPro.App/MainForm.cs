using DevExpress.XtraEditors;
using System.Reflection;
using ApplicationIdentity = WawiCallPro.Utilities.ApplicationIdentity;

namespace WawiCallPro.App;

public partial class MainForm : XtraForm
{
    public MainForm()
    {
        InitializeComponent();
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        var productLine = ApplicationIdentity.GetProductLine(Assembly.GetExecutingAssembly());
        Text = $"{ApplicationIdentity.Name} – {productLine}";
    }
}
