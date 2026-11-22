using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Database.Contracts;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private PhoneMatchingProfile? _matchingProfile;
    private bool _bindingMatchingCompanies;
    private bool _matchingBusy;

    private void BindMatchingCompanies()
    {
        var selected = matchingCompaniesEdit.EditValue as CompanyKey;
        var companies = _cacheCompanies.Where(company => company.FromSource).ToArray();
        _bindingMatchingCompanies = true;
        try
        {
            matchingCompaniesEdit.Properties.DataSource = companies;
            matchingCompaniesEdit.EditValue = companies.Any(company => company.Key == selected) ? selected : null;
        }
        finally { _bindingMatchingCompanies = false; }
        if (matchingCompaniesEdit.EditValue as CompanyKey != selected) ClearMatchingProfile();
        SetMatchingBusy(false);
    }
    private void MatchingCompany_Changed(object? sender, EventArgs e)
    {
        matchingCompaniesEdit.ToolTip = matchingCompaniesEdit.Text;
        if (!_bindingMatchingCompanies) ClearMatchingProfile();
    }
    private void ClearMatchingProfile()
    {
        _matchingProfile = null;
        matchingEnabledEdit.Checked = false; matchingWidthEdit.Value = 5;
        ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsSelectionChanged);
        SetMatchingBusy(false);
    }
    private void SetMatchingBusy(bool busy)
    {
        _matchingBusy = busy;
        matchingCompaniesEdit.Enabled = loadMatchingCompaniesButton.Enabled = !busy;
        loadMatchingRuleButton.Enabled = !busy && matchingCompaniesEdit.EditValue is CompanyKey;
        matchingEnabledEdit.Enabled = matchingWidthEdit.Enabled = saveMatchingRuleButton.Enabled = !busy && _matchingProfile is not null;
    }
    private async void LoadMatchingRule_Click(object? sender, EventArgs e)
    {
        if (_workspace is null || _operation is null || _operation.IsRunning || _matchingBusy
            || matchingCompaniesEdit.EditValue is not CompanyKey company) return;
        await _operation.RunAsync(async token =>
        {
            SetMatchingBusy(true);
            try
            {
                var profile = await _workspace.PhoneMatching.GetProfileAsync(company, token);
                token.ThrowIfCancellationRequested();
                _matchingProfile = profile;
                matchingEnabledEdit.Checked = profile.Settings.StemMatchingEnabled;
                matchingWidthEdit.Value = profile.Settings.StartWidth;
                ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsRuleLoaded);
            }
            catch (RepositoryConflictException exception) when (exception.Kind == RepositoryConflictKind.MissingRecord)
            {
                _matchingProfile = null;
                ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsSelectionChanged);
            }
            finally { SetMatchingBusy(false); }
        });
    }
    private async void SaveMatchingRule_Click(object? sender, EventArgs e)
    {
        if (_workspace is null || _operation is null || _operation.IsRunning || _matchingBusy || _matchingProfile is not { } profile
            || matchingCompaniesEdit.EditValue as CompanyKey != profile.Settings.Company) return;
        var settings = profile.Settings with { StemMatchingEnabled = matchingEnabledEdit.Checked, StartWidth = (int)matchingWidthEdit.Value };
        await _operation.RunAsync(async token =>
        {
            SetMatchingBusy(true);
            try
            {
                _matchingProfile = await _workspace.PhoneMatching.SaveProfileAsync(settings, profile.Version, token);
                ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsRuleSaved);
            }
            catch (RepositoryConflictException exception) when (exception.Kind is RepositoryConflictKind.VersionMismatch or RepositoryConflictKind.AlreadyExists)
            {
                ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsValidationFailed);
            }
            finally { SetMatchingBusy(false); }
        });
    }
}
