using DevExpress.XtraEditors;
using DevExpress.XtraTab;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App.Views;

/// <summary>One call owns its selection and loads. Existing plugin tabs retain their original party and edits.</summary>
public partial class CallDetailsPage : XtraUserControl
{
    private AppWorkspace? _workspace;
    private ICallViewScope? _scope;
    private readonly CancellationTokenSource _stopping = new();
    private readonly List<Task> _loads = [];
    private Task _pendingLoads = Task.CompletedTask;
    private readonly Dictionary<XtraTabPage, IPluginViewHandle> _views = [];
    private Task? _stopTask;
    private CallerMatchDecision? _acceptedDecision;
    private bool _resourcesDisposed;
    private CallNotesSession? _notes;
    internal event Action<CallDetailsPage>? ChooseRequested;
    internal CallSnapshot Call => _scope!.Call;
    internal CallViewContext? Context => _scope?.Current;
    internal Task PendingLoads => _pendingLoads;
    internal CallNotesSession Notes => _notes!;
    internal bool HasUnsavedNotes => _notes?.HasUnsavedChanges == true;
    internal bool HasUnsavedChanges => HasUnsavedNotes || _views.Values.Any(view => view.HasUnsavedChanges);
    public CallDetailsPage()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        FontChanged += Layout_Changed; SizeChanged += Layout_Changed;
        var appearance = LookAndFeel; appearance.StyleChanged += Layout_Changed;
        Disposed += (_, _) => appearance.StyleChanged -= Layout_Changed;
    }
    private bool _arranging;
    private void Layout_Changed(object? sender, EventArgs e)
    {
        if (_arranging || chooseButton is null || IsDisposed) return;
        _arranging = true;
        try
        {
            UiButtonSizing.Apply(chooseButton, reloadButton);
        }
        finally { _arranging = false; }
    }
    internal void Bind(AppWorkspace workspace, CallSnapshot call)
    {
        _workspace = workspace; _scope = workspace.CallScopes.Create(call);
        callControls.Bind(workspace.Telephony, call);
        _notes = workspace.CallNotes.Create(call.Id);
        notesPanel.Bind(_notes);
        ShowCall();
    }
    internal async Task ObserveAsync(CallSnapshot call)
    { if (_scope is not null && _stopTask is null) { await _scope.ObserveAsync(call); callControls.Observe(Call); ShowCall(); } }
    private void ShowCall()
    {
        if (_scope is null) return;
        callLabel.Text = "Anruf " + Call.StartedAtUtc.LocalDateTime.ToString("T") + " · "
            + (Call.RemoteAddress.Presentation == NumberPresentation.Withheld ? "Unterdrückte Nummer" : Call.RemoteAddress.OriginalValue ?? "Unbekannte Nummer")
            + " · " + (Call.State switch { CallState.Created => "Neu", CallState.Dialing => "Rufaufbau", CallState.Ringing => "Klingelt", CallState.Connected => "Verbunden", CallState.Held => "Gehalten", CallState.Ended => "Beendet", _ => "Fehlgeschlagen" });
        chooseButton.Enabled = !_scope.IsEnded && _workspace?.CanUseBusinessDirectory == true;
        reloadButton.Enabled = chooseButton.Enabled && Context is not null;
        if (_scope.IsEnded) statusLabel.Text = "Gespräch beendet. Bereits geöffnete Fachansichten bleiben erhalten.";
    }
    internal async Task AcceptAsync(CallerMatchDecision decision, CancellationToken token)
    {
        if (_scope is null || _workspace is null || _scope.IsEnded || _stopTask is not null) return;
        var currentIndex = _workspace.PhoneSearch.GetStatus();
        if (currentIndex.Generation != decision.IndexGeneration || decision.Selection is not null
            && (currentIndex.State is not (PhoneSearchScopeState.Ready or PhoneSearchScopeState.Partial)
                || decision.Selection.Kind != CallerMatchSelectionKind.Manual && !currentIndex.IsComplete))
        { statusLabel.Text = "Suchstand geändert oder abgelaufen. Die Anrufauswahl wurde nicht übernommen; bitte erneut suchen."; return; }
        await _scope.SelectAsync(decision with { Selection = null });
        var history = await _workspace.CallAssociations.SaveAsync(Call.Id, decision, token);
        if (_scope.IsEnded) return;
        if (history.Parties.SingleOrDefault(party => party.IsSelected)?.Party != decision.Selection?.Party)
        {
            if (_acceptedDecision is not null) await _scope.SelectAsync(_acceptedDecision);
            statusLabel.Text = "Die bereits gespeicherte manuelle Zuordnung bleibt erhalten. Bitte ausdrücklich erneut entscheiden."; return;
        }
        token.ThrowIfCancellationRequested();
        var context = await _scope.SelectAsync(decision);
        _acceptedDecision = decision;
        ShowCall();
        selectionLabel.Text = context is null ? "Keine Zuordnung für diesen Anruf." : "Auswahl für diesen Anruf: " + context.CompanyName + " · " + context.PartyName;
        detailsLabel.Text = context is null ? "Keine Stammdaten zugeordnet." : "Details für diese Auswahl werden geladen …";
        if (context is not null) QueueLoad();
    }
    private void QueueLoad()
    {
        if (_scope?.Current is null || _scope.IsEnded || _stopTask is not null) return;
        _loads.RemoveAll(task => task.IsCompleted);
        _loads.Add(LoadAsync());
        _pendingLoads = Task.WhenAll(_loads.ToArray());
    }
    private async Task LoadAsync()
    {
        using var operation = _scope!.BeginOperation(_stopping.Token);
        var context = operation.Context;
        try
        {
            statusLabel.Text = "Details werden für " + context.CompanyName + " · " + context.PartyName + " geladen …";
            var party = await Task.Run(() => _workspace!.Directory.GetPartyAsync(context.Party, operation.CancellationToken), operation.CancellationToken);
            if (party is not null && party.Key != context.Party) throw new InvalidOperationException("Unexpected source identity.");
            if (!await operation.PublishAsync(_workspace!.Dispatcher, () =>
            {
                detailsLabel.Text = party is null ? "Der gewählte Datensatz ist in dieser Firma nicht mehr verfügbar." :
                    context.CompanyName + " · " + party.DisplayName + "\nNummer: " + (party.Number ?? "–") + " · E-Mail: " + (party.Email ?? "–")
                    + "\n" + string.Join("\n", party.Phones.Select(phone => phone.OriginalValue))
                    + "\n" + string.Join("\n", party.Addresses.Select(address => string.Join(" ", new[] { address.Street, address.HouseNumber, address.PostalCode, address.City }.Where(value => !string.IsNullOrEmpty(value)))));
            })) return;
            if (party is null) return;
            // The generic master-data tab works with every product adapter, even without optional Windows views.
            var plugin = await _workspace.GetPluginAsync(operation.CancellationToken);
            foreach (var descriptor in plugin.Views)
            {
                IPluginViewHandle? handle = await _workspace.Views.OpenAsync(descriptor.Id, context.Party, operation.CancellationToken);
                try
                {
                    if (await operation.PublishAsync(_workspace.Dispatcher, () =>
                    {
                        var tab = new XtraTabPage { Text = context.CompanyName + " · " + Role(context) + " " + context.PartyName + " · " + descriptor.DisplayName, Tag = context };
                        handle.View.Dock = DockStyle.Fill; tab.Controls.Add(handle.View);
                        _views.Add(tab, handle); tabs.TabPages.Add(tab);
                    })) handle = null;
                }
                finally { if (handle is not null) await handle.DisposeAsync(); }
            }
            await operation.PublishAsync(_workspace.Dispatcher, () => statusLabel.Text = "Details für diese Anrufauswahl geladen. Bestehende Fach-Tabs behalten ihren Kontext.");
        }
        catch (OperationCanceledException) when (operation.CancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            try { await operation.PublishAsync(_workspace!.Dispatcher, () => { statusLabel.Text = PageOperation.Describe(exception); detailsLabel.Text = "Aktuelle Details sind für diese Auswahl nicht verfügbar."; }); }
            catch (OperationCanceledException) when (operation.CancellationToken.IsCancellationRequested) { }
        }
    }
    internal Task StopAsync() => _stopTask ??= StopCoreAsync();
    private async Task StopCoreAsync()
    {
        await _stopping.CancelAsync();
        await callControls.StopAsync();
        if (_notes is not null) await _notes.StopAsync();
        if (_scope is not null) await _scope.DisposeAsync();
        await Task.WhenAll(_loads.ToArray());
        foreach (var handle in _views.Values) await handle.DisposeAsync();
        _views.Clear();
    }
    private void Choose_Click(object? sender, EventArgs e) => ChooseRequested?.Invoke(this);
    private static string Role(CallViewContext context) => context.Party.Kind switch
    { WawiCallPro.Interfaces.Parties.PartyKind.Customer => "Kunde", WawiCallPro.Interfaces.Parties.PartyKind.Supplier => "Lieferant", WawiCallPro.Interfaces.Parties.PartyKind.CustomerContact => "Kundenkontakt", _ => "Lieferantenkontakt" };
    private void Reload_Click(object? sender, EventArgs e) => QueueLoad();
    private void DisposePage()
    {
        if (_resourcesDisposed) return;
        _resourcesDisposed = true; _stopping.Cancel(); _stopping.Dispose();
        _notes?.Dispose();
    }
}
