using DevExpress.XtraEditors;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.Plugins.Lexware.Pro;

/// <summary>Complete Pro-owned settings view: API access and a dialog-local authorized company selection.</summary>
public partial class LexwareSettingsPage : XtraUserControl
{
    public LexwareSettingsPage() => InitializeComponent();
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
        companyPage.Bind(context as IPluginSettingsPageContext, directory, WawiCallPro.Interfaces.Companies.LexwareProductLine.Pro,
            connectionReady, connectionPage.HasChanges, DescribeFailure);
    }
    internal void ApplyChanges(IPluginConnectionEditContext context)
    {
        if (connectionPage.HasChanges() && companyPage.HasUnsavedChanges)
            throw new PluginSettingsValidationException(PluginSettingsIssue.ConnectionRestartRequired);
        connectionPage.ApplyChanges(context);
    }
    internal Task LoadAsync(CancellationToken token) => companyPage.LoadAsync(token);
    internal Task ValidateAsync(CancellationToken token)
    {
        if (connectionPage.HasChanges() && companyPage.HasUnsavedChanges)
            throw new PluginSettingsValidationException(PluginSettingsIssue.ConnectionRestartRequired);
        return companyPage.ValidateAsync(token);
    }
    internal Task StopAsync(CancellationToken token) => companyPage.StopAsync(token);
    private static string DescribeFailure(Exception error) => error is ProAdapterException failure ? failure.Failure switch
    {
        ProFailure.ConfigurationRequired => "Lexware-Pro-Zugang einrichten, speichern und neu starten. Danach Mandanten laden.",
        ProFailure.AuthenticationFailed or ProFailure.InvalidToken => "Lexware-Anmeldung abgelehnt. API-Benutzer, Kennwort und Client-Key prüfen.",
        ProFailure.InstallationUnavailable => "Lexware-Pro-Installation oder API-Adresse prüfen.",
        ProFailure.ApiUnavailable => "Lexware-API nicht erreichbar. Dienst, Adresse und Verbindung prüfen.",
        ProFailure.PropertyStoreInvalid => "Lexware-Mandantennamen konnten nicht gelesen werden. Installation prüfen.",
        _ => "Die Lexware-Antwort enthält keinen gültigen Mandantenkatalog. Zugang und Installation prüfen."
    } : "Mandanten konnten nicht geladen werden. Lexware-Zugang und Verbindung prüfen.";
}
