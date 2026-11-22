namespace WawiCallPro.App;

public partial class MainForm
{
    internal Task ReloadCompaniesAsync()
    {
        if (_workspace is null || _closing) return Task.CompletedTask;
        return !_companyRestore.IsCompleted ? _companyRestore : _companyRestore = RestoreCompanyAsync();
    }
    private CancellationTokenSource _companyRestoreStopping = new();
    private Task _companyRestore = Task.CompletedTask;
    private bool _companyRestoreDisposed;
    private async Task RestoreCompanyAsync()
    {
        var token = _companyRestoreStopping.Token;
        try
        {
            var options = (await _workspace!.Settings.LoadAsync(token)).Options;
            var key = _workspace.Companies.Current.Company?.Key ?? options.User.PreferredCompany;
            if (key is null) return;
            var pluginId = (_workspace.Plugins as WawiCallPro.Interfaces.Plugins.IProductPluginSelection)?.ActivePluginId;
            if (!_workspace.CanUseBusinessDirectory) { await _workspace.Companies.SelectAsync(null); return; }
            var configured = await _workspace.Plugins.UseAsync((plugin, _) => Task.FromResult(
                plugin is not WawiCallPro.Interfaces.Directory.IBusinessDirectoryConnectionState connection || connection.IsConnectionConfigured), token);
            if (!configured) { await _workspace.Companies.SelectAsync(null); return; }
            var companies = await _workspace.Directory.GetCompaniesAsync(token);
            token.ThrowIfCancellationRequested();
            if (_closing || pluginId != (_workspace.Plugins as WawiCallPro.Interfaces.Plugins.IProductPluginSelection)?.ActivePluginId) return;
            var selected = companies.SingleOrDefault(company => company.Key == key);
            if (_workspace.Companies.Current.Company != selected) await _workspace.Companies.SelectAsync(selected);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch { if (!_closing) AppendOutput("Der gespeicherte Mandant ist nicht verfügbar. Bitte unter Einstellungen → Lexware prüfen."); }
    }
    private async Task StopCompanyRestoreAsync()
    {
        if (_companyRestoreDisposed) return;
        await _companyRestoreStopping.CancelAsync();
        await _companyRestore;
        if (!_closing) { _companyRestoreStopping.Dispose(); _companyRestoreStopping = new(); }
    }
    private void DisposeCompanyRestore()
    {
        if (_companyRestoreDisposed) return;
        _companyRestoreDisposed = true; _closing = true;
        _companyRestoreStopping.Cancel(); _companyRestoreStopping.Dispose();
    }

    private async void CompanySettings_Closed(object? sender, FormClosedEventArgs e)
    {
        if (sender is Views.FrmSettings { RestartRequested: true }) return;
        await ReloadCompaniesAsync();
    }
}
