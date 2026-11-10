using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private CallerLineAssignment? _lineDraft;
    private void ApplyCallerLine(ApplicationOptions options)
    {
        _lineDraft = options.CallerLineAssignment;
        lineCompanyEdit.Properties.DataSource = options.CallerLineAssignment is { } mapping
            ? new[] { new LineCompany(mapping.Company, options.FindCompanySettings(mapping.Company)?.DisplayName ?? "Gespeicherte Firma") }
            : Array.Empty<LineCompany>();
        lineCompanyEdit.EditValue = options.CallerLineAssignment?.Company;
        ShowCallerLine();
    }
    private async void LoadLineCompanies_Click(object? sender, EventArgs e)
    {
        if (_operation is null || _workspace is null) return;
        await _operation.RunAsync(async token =>
        {
            var selected = lineCompanyEdit.EditValue;
            var companies = await _workspace.Directory.GetCompaniesAsync(token);
            lineCompanyEdit.Properties.DataSource = companies.Select(company => new LineCompany(company.Key, company.DisplayName)).ToArray();
            lineCompanyEdit.EditValue = selected;
            statusLabel.Text = "Firma auswählen und die Leitungszuordnung ausdrücklich bestätigen.";
        });
    }
    private void ConfirmLine_Click(object? sender, EventArgs e)
    {
        if (lineCompanyEdit.EditValue is not CompanyKey company || string.IsNullOrWhiteSpace(serverEdit.Text) || string.IsNullOrWhiteSpace(usernameEdit.Text))
        { statusLabel.Text = "Zuerst SIP-Server und Benutzername eintragen sowie eine verfügbare Firma auswählen."; return; }
        if (lineCompanyEdit.Properties.DataSource is not LineCompany[] companies || !companies.Any(item => item.Key == company)) return;
        _lineDraft = CallerLineConfiguration.Create(new() { Server = serverEdit.Text.Trim(), Port = (int)portEdit.Value,
            Transport = (SipTransport)(transportEdit.SelectedIndex + 1), Username = usernameEdit.Text.Trim() }, company);
        ShowCallerLine();
        statusLabel.Text = "Zuordnung bestätigt. Mit „Speichern“ dauerhaft übernehmen; die Suchfirmen bleiben unverändert.";
    }
    private void ClearLine_Click(object? sender, EventArgs e)
    { _lineDraft = null; lineCompanyEdit.EditValue = null; ShowCallerLine(); }
    private void LineCompany_Changed(object? sender, EventArgs e)
    { lineCompanyEdit.ToolTip = lineCompanyEdit.Text; ShowCallerLine(); }
    private void ShowCallerLine() => lineAssignmentStatus.Text = _lineDraft is null ? "Keine Leitungsfirma bestätigt. Auswahl mit „Bestätigen“ übernehmen."
        : lineCompanyEdit.EditValue as CompanyKey != _lineDraft.Company ? "Auswahl geändert. Erst bestätigen, um die gespeicherte Zuordnung zu ersetzen."
        : "Bestätigt. Nach Kontoänderungen erneut bestätigen.";
    private sealed record LineCompany(CompanyKey Key, string Name);
}
