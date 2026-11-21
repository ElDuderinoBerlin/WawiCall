using System.Text.Json;
using DevExpress.XtraBars.Docking2010.Views;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Automation;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App;

public partial class MainForm
{
    internal object AutomationDiagnostics() => new
    {
        Documents = _documents.Count + _pluginPages.Count,
        PartyViews = _automationParties.Count(item => _pluginPages.ContainsKey(item.Value)),
        GlobalCompanySelected = _workspace?.Companies.Current.Company is not null,
        OwnedViews = OwnedForms.Where(form => !form.IsDisposed).Select(form => form.GetType().Name).ToArray(),
        ModalViews = OwnedForms.Where(form => form.Modal && form.Visible).Select(form => form.GetType().Name).ToArray()
    };
    private readonly Dictionary<(PartyKey Party, string View), BaseDocument> _automationParties = [];
    private readonly Dictionary<FrmDial, (WawiCallPro.Interfaces.Companies.CompanyKey Company, string Number)> _automationDials = [];
    internal async Task PresentAutomationAsync(PresentationRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_closing || _workspace is null) throw new AutomationFault(AutomationFailure.Stopping);
        if (request.ActivateOnly) { RestoreFromTray(); return; }
        var id = request.ViewId;
        var settings = id is "settings" or "plugin" or "number-diagnostics" || id.StartsWith("settings-", StringComparison.Ordinal);
        var modal = FindModalWindow();
        if (modal is not null)
        {
            if (settings && modal is FrmSettings existingSettings) { existingSettings.SelectAutomationTab(id); existingSettings.Activate(); return; }
            if (id == "database" && modal is FrmDatabaseSetup) { modal.Activate(); return; }
            if (id == "home") { modal.Activate(); return; }
            throw new AutomationFault(AutomationFailure.Busy);
        }
        if (request.Party is { } party)
        {
            if (!_workspace.CanUseBusinessDirectory) throw new AutomationFault(AutomationFailure.AccessDenied);
            await OpenAutomationPartyAsync(party, request.Number ?? "", id.StartsWith("plugin:", StringComparison.Ordinal) ? id[7..] : null, token);
            return;
        }
        if (id.StartsWith("plugin:", StringComparison.Ordinal) || id is "customer" or "supplier") throw new AutomationFault(AutomationFailure.InvalidArgument);
        if (settings)
        {
            await ShowAutomationModalAsync(() => { var dialog = CreateSettingsDialog(); dialog.SelectAutomationTab(id); return dialog; }, dialog =>
            {
                if (dialog is FrmSettings { RestartRequested: true }) { RestartRequested = true; Close(); }
                UpdateBusinessActions(); UpdateDatabaseActions();
            });
            return;
        }
        if (id == "database")
        {
            if (_databaseBusy || _operation?.IsRunning == true) throw new AutomationFault(AutomationFailure.Busy);
            await ShowAutomationModalAsync(CreateDatabaseSetupDialog); return;
        }
        if (id == "log") { outputDock.Show(); return; }
        if (id == "parties")
        {
            if (!_workspace.CanUseBusinessDirectory) throw new AutomationFault(AutomationFailure.AccessDenied);
            if (!_selectionTask.IsCompleted) throw new AutomationFault(AutomationFailure.Busy);
            _selectionTask = OpenPartyWorkspaceAsync(); await _selectionTask;
            if (!_documents.ContainsKey("parties")) throw new AutomationFault(AutomationFailure.ProviderUnavailable);
            return;
        }
        if (id == "caller")
        {
            if (!_workspace.CanUseBusinessDirectory) throw new AutomationFault(AutomationFailure.AccessDenied);
            using var json = JsonDocument.Parse(request.ContextJson.Length == 0 ? "{}" : request.ContextJson);
            if (json.RootElement.TryGetProperty("callId", out var value))
            {
                if (!Guid.TryParseExact(value.GetString(), "D", out var callId) || callId == Guid.Empty) throw new AutomationFault(AutomationFailure.InvalidArgument);
                if (!_callPages.TryGetValue(callId, out var active)) throw new AutomationFault(AutomationFailure.NotSupported);
                await OpenCallLookupAsync(active.Page, false);
            }
            else OpenCallerLookup();
            return;
        }
        if (id == "dial")
        {
            if (!_workspace.CanUseBusinessDirectory || request.Company is null) throw new AutomationFault(AutomationFailure.InvalidArgument);
            var number = request.Number ?? "";
            var existing = OwnedForms.OfType<FrmDial>().FirstOrDefault(dialog => !dialog.IsDisposed);
            if (existing is not null)
            {
                if (!_automationDials.TryGetValue(existing, out var context) || context.Company != request.Company || context.Number != number) throw new AutomationFault(AutomationFailure.Busy);
                existing.Show(); existing.Activate(); return;
            }
            var dial = new FrmDial(_workspace.Directory, _workspace.Telephony!, request.Company);
            _state?.AttachWindow(dial, "FrmDial"); dial.SetAutomationNumber(number);
            _automationDials.Add(dial, (request.Company, number));
            dial.FormClosed += (_, _) => _automationDials.Remove(dial);
            dial.Show(this); return;
        }
        if (id == "call")
        {
            using var json = JsonDocument.Parse(request.ContextJson.Length == 0 ? "{}" : request.ContextJson);
            if (!json.RootElement.TryGetProperty("callId", out var value) || !Guid.TryParseExact(value.GetString(), "D", out var callId) || callId == Guid.Empty) throw new AutomationFault(AutomationFailure.InvalidArgument);
            if (_callPages.TryGetValue(callId, out var active)) { ActivateDocument(active.Document); return; }
            var key = "call:" + callId.ToString("D");
            if (_documents.TryGetValue(key, out var existing)) { ActivateDocument(existing); return; }
            var page = new HistoryPage(); page.Bind(_workspace, _state);
            try { await page.ShowCallAsync(callId, token); }
            catch (AutomationCallNotFoundException) { page.Dispose(); throw new AutomationFault(AutomationFailure.NotFound); }
            catch { await page.StopAsync(); page.Dispose(); throw; }
            var host = new Panel { Dock = DockStyle.Fill }; page.Dock = DockStyle.Fill; host.Controls.Add(page);
            var document = tabbedView.AddDocument(host); document.Caption = "Gesprächsdetails";
            _documents.Add(key, document); ActivateDocument(document); return;
        }
        if (id is not ("home" or "history" or "mail" or "help" or "about")) throw new AutomationFault(AutomationFailure.NotSupported);
        Navigate(id);
    }

    private async Task OpenAutomationPartyAsync(PartyKey party, string number, string? pluginView, CancellationToken token)
    {
        var sourcePlugin = (_workspace!.Plugins as WawiCallPro.Interfaces.Plugins.IProductPluginSelection)?.ActivePluginId;
        var key = (party, pluginView ?? "master-data");
        if (_automationParties.TryGetValue(key, out var existing) && _pluginPages.TryGetValue(existing, out var existingHandle)
            && (existingHandle.View is not PartySearchPage existingPage || existingPage.DisplayedParty == party))
        { ActivateDocument(existing); return; }
        var plugin = await _workspace!.GetPluginAsync(token);
        if (pluginView is not null && !plugin.Views.Any(view => view.Id == pluginView)) throw new AutomationFault(AutomationFailure.NotSupported);
        IPluginViewHandle handle;
        if (pluginView is null)
        {
            if (_partyWorkspaces is null) throw new AutomationFault(AutomationFailure.NotSupported);
            handle = await _partyWorkspaces.OpenAsync(token);
            try
            {
                if (handle.View is not PartySearchPage page) throw new AutomationFault(AutomationFailure.NotSupported);
                await page.ShowRecordAsync(party, number, token);
            }
            catch { await handle.DisposeAsync(); throw; }
        }
        else handle = await _workspace.Views.OpenAsync(pluginView, party, token);
        try
        {
            token.ThrowIfCancellationRequested();
            if (_closing || !_workspace.CanUseBusinessDirectory || sourcePlugin != (_workspace.Plugins as WawiCallPro.Interfaces.Plugins.IProductPluginSelection)?.ActivePluginId
                || handle.View.IsDisposed) throw new AutomationFault(AutomationFailure.NotReady);
            var host = new Panel { Dock = DockStyle.Fill }; handle.View.Dock = DockStyle.Fill; host.Controls.Add(handle.View);
            var document = tabbedView.AddDocument(host);
            document.Caption = (party.Kind == PartyKind.Customer ? "Kunde " : "Lieferant ") + number;
            _pluginPages.Add(document, handle); _automationParties[key] = document; ActivateDocument(document);
        }
        catch { await handle.DisposeAsync(); throw; }
    }

    private Task ShowAutomationModalAsync(Func<Form> factory, Action<Form>? closed = null)
    {
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        BeginInvoke(new Action(() =>
        {
            try
            {
                using var waiting = new UiWaitCursor(this);
                using var dialog = factory();
                dialog.Shown += (_, _) => shown.TrySetResult();
                dialog.ShowDialog(this); closed?.Invoke(dialog);
            }
            catch (Exception)
            {
                if (!shown.TrySetException(new AutomationFault(AutomationFailure.ProviderUnavailable)))
                    AppendOutput("Der Dialog konnte nicht vollständig abgeschlossen werden. Bitte Einstellungen und Verbindungen prüfen.");
            }
            finally { if (!shown.Task.IsCompleted) shown.TrySetException(new AutomationFault(AutomationFailure.ProviderUnavailable)); }
        }));
        return shown.Task;
    }
}
