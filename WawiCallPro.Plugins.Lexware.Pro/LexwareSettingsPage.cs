using DevExpress.XtraEditors;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.Plugins.Lexware.Pro;

/// <summary>Complete Pro-owned settings view: API access and a dialog-local authorized company selection.</summary>
public partial class LexwareSettingsPage : XtraUserControl
{
    [System.ComponentModel.Browsable(false), System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal IPluginConnectionDirectoryFactory? Directories { get; set; }
    private IBusinessDirectory? _preview;
    private IPluginSettingsPageContext? _context;
    private bool _aligningColumns;
    public LexwareSettingsPage() => InitializeComponent();
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_aligningColumns || companyPage is null || connectionPage is null) return;
        _aligningColumns = true;
        try
        {
            var labelWidth = Math.Max(companyPage.MeasureLabelColumn(), connectionPage.MeasureLabelColumn());
            var editor = companyPage.AlignLabelColumn(labelWidth);
            connectionPage.AlignColumns(companyPage.Left + editor.Left - connectionPage.Left, editor.Width);
        }
        finally { _aligningColumns = false; }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (layout is null) return base.GetPreferredSize(proposedSize);
        var width = Math.Max(1, proposedSize.Width > 0 ? proposedSize.Width : Width);
        return new(width, companyPage.GetPreferredSize(new(width, 0)).Height + companyPage.Margin.Vertical
            + connectionPage.GetPreferredSize(new(width, 0)).Height + connectionPage.Margin.Vertical + layout.Padding.Vertical);
    }
    internal bool HasChanges => connectionPage.HasChanges() || companyPage.HasUnsavedChanges;
    internal void Reload(IPluginConnectionEditContext context, IBusinessDirectory directory, Func<bool> connectionReady)
    {
        connectionPage.Reload(context);
        _context = context as IPluginSettingsPageContext;
        _preview = context is IPluginConnectionPreview preview && Directories is not null
            ? new ProSettingsDirectory(preview, connectionPage.ReadChanges, Directories) : directory;
        companyPage.Bind(_context, _preview, WawiCallPro.Interfaces.Companies.LexwareProductLine.Pro,
            () => true, describeFailure: DescribeFailure);
    }
    internal void ApplyChanges(IPluginConnectionEditContext context) => connectionPage.ApplyChanges(context);
    internal Task LoadAsync(CancellationToken token) => companyPage.LoadAsync(token);
    internal async Task ValidateAsync(CancellationToken token)
    {
        if (connectionPage.HasChanges()) await companyPage.ValidateCurrentScopeAsync(token);
        else await companyPage.ValidateAsync(token);
    }
    internal async Task<WawiCallPro.Interfaces.Companies.Company?> ResolveCompanyAsync(WawiCallPro.Interfaces.Companies.CompanyKey company, CancellationToken token)
    {
        if (_preview is null) throw new InvalidOperationException("Company source is unavailable.");
        return (await _preview.GetCompaniesAsync(token)).SingleOrDefault(item => item.Key == company);
    }
    internal Task StopAsync(CancellationToken token) => companyPage.StopAsync(token);
    private static string DescribeFailure(Exception error) => error is ProAdapterException failure ? failure.Failure switch
    {
        ProFailure.ConfigurationRequired => "Lexware-Pro-Zugang eingeben. Danach Mandanten laden.",
        ProFailure.AuthenticationFailed or ProFailure.InvalidToken => "Lexware-Anmeldung abgelehnt. API-Benutzer, Kennwort und Client-Key prüfen.",
        ProFailure.InstallationUnavailable => "Lexware-Pro-Installation oder API-Adresse prüfen.",
        ProFailure.ApiUnavailable => "Lexware-API nicht erreichbar. Dienst, Adresse und Verbindung prüfen.",
        ProFailure.PropertyStoreInvalid => "Lexware-Mandantennamen konnten nicht gelesen werden. Installation prüfen.",
        _ => "Die Lexware-Antwort enthält keinen gültigen Mandantenkatalog. Zugang und Installation prüfen."
    } : "Mandanten konnten nicht geladen werden. Lexware-Zugang und Verbindung prüfen.";
}
