namespace WawiCallPro.App;

public partial class MainForm
{
    private bool _companySelectorBound;
    private bool _sizingCompanyNavigation;
    private void CompanySelector_Layout(object? sender, LayoutEventArgs e)
    {
        if (_sizingCompanyNavigation || IsDisposed) return;
        _sizingCompanyNavigation = true;
        try
        {
            var width = companySelector.GetMinimumContentWidth() + Math.Max(0, navigationDock.Width - navigationContainer.ClientSize.Width);
            if (navigationDock.MinimumSize.Width != width) navigationDock.MinimumSize = new Size(width, 0);
            if (navigationDock.Width < width) navigationDock.Width = width;
        }
        finally { _sizingCompanyNavigation = false; }
    }
    internal Task ReloadCompaniesAsync()
    {
        if (_workspace is null || _closing) return Task.CompletedTask;
        if (!_companySelectorBound)
        {
            companySelector.Bind(_workspace.Directory, _workspace.Companies, _workspace.Plugins,
                _workspace.Application.ProductLine == "Pro" ? Interfaces.Companies.LexwareProductLine.Pro : Interfaces.Companies.LexwareProductLine.FinOffice);
            companySelector.LoadFailed += CompanyCatalog_LoadFailed;
            _companySelectorBound = true;
        }
        return companySelector.ReloadAsync();
    }
    private void CompanyCatalog_LoadFailed(object? sender, EventArgs e) =>
        AppendOutput("Mandanten konnten nicht geladen werden. Wawi-Zugang und aktives Plugin prüfen.");
    private async void CompanySettings_Closed(object? sender, FormClosedEventArgs e)
    {
        if (sender is Views.FrmSettings { RestartRequested: true }) return;
        await ReloadCompaniesAsync();
    }
}
