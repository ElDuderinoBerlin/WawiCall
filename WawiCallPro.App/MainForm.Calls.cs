using DevExpress.XtraBars.Docking2010.Views;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App;

public partial class MainForm
{
    private readonly Dictionary<Guid, (BaseDocument Document, CallDetailsPage Page)> _callPages = [];
    private readonly List<Task> _callUpdates = [];
    private Task _pendingCallUpdates = Task.CompletedTask;
    internal Task PendingCallUpdates => _pendingCallUpdates;
    private void QueueCall(CallSnapshot call)
    {
        if (_closing || _workspace is null) return;
        _callUpdates.RemoveAll(task => task.IsCompleted);
        _callUpdates.Add(ObserveCallSafelyAsync(call));
        _pendingCallUpdates = Task.WhenAll(_callUpdates.ToArray());
    }
    private async Task ObserveCallSafelyAsync(CallSnapshot call)
    {
        try { await ObserveCallAsync(call); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { if (!IsDisposed) connectionLabel.Text = PageOperation.Describe(exception); }
    }
    internal async Task ObserveCallAsync(CallSnapshot call)
    {
        if (_closing || _workspace is null) return;
        if (_callPages.TryGetValue(call.Id, out var existing))
        {
            await existing.Page.ObserveAsync(call);
            foreach (var lookup in OwnedForms.OfType<FrmCallerLookup>().Where(form => form.CallId == call.Id))
            {
                lookup.ObserveCall(existing.Page.Call);
                if (existing.Page.Call.State is CallState.Ended or CallState.Failed) await lookup.Page.EndCallAsync();
            }
            return;
        }
        if (call.State is CallState.Ended or CallState.Failed) return;
        var page = new CallDetailsPage(); page.Bind(_workspace, call);
        page.ChooseRequested += Call_ChooseRequested;
        var previous = tabbedView.ActiveDocument ?? _lastDocument;
        var host = new Panel { Dock = DockStyle.Fill }; page.Dock = DockStyle.Fill; host.Controls.Add(page);
        var document = tabbedView.AddDocument(host);
        document.Caption = "Anruf " + call.StartedAtUtc.LocalDateTime.ToString("T");
        _callPages.Add(call.Id, (document, page));
        if (previous is not null) ActivateDocument(previous);
        await OpenCallLookupAsync(page, true);
    }
    private async void Call_ChooseRequested(CallDetailsPage page)
    {
        try { await OpenCallLookupAsync(page, false); }
        catch (Exception exception) { connectionLabel.Text = PageOperation.Describe(exception); }
    }
    private async Task OpenCallLookupAsync(CallDetailsPage page, bool incoming)
    {
        if (page.IsDisposed || _closing) return;
        if (OwnedForms.OfType<FrmCallerLookup>().FirstOrDefault(form => form.CallId == page.Call.Id && !form.IsDisposed) is { } existing)
        { if (!incoming) { existing.Show(); if (!_backgroundRun) existing.Activate(); } return; }
        var lookup = CreateCallerDialog();
        lookup.BindCall(page.Call, page.AcceptAsync);
        lookup.BindNotes(page.Notes);
        if (_backgroundRun) lookup.PrepareForBackgroundRun();
        if (incoming) lookup.ShowForIncoming(this); else lookup.Show(this);
        await lookup.Page.SearchAsync();
        await page.Notes.LoadAsync();
    }
    internal void EnsureViewsCanClose()
    {
        if (_callPages.Values.Any(entry => entry.Page.HasUnsavedNotes)) throw new UnsavedCallNotesException();
        if (_pluginPages.Values.Any(view => view.HasUnsavedChanges) || _callPages.Values.Any(entry => entry.Page.HasUnsavedChanges))
            throw new UnsavedPluginChangesException();
    }
    internal void ReportUnsavedChanges() => connectionLabel.Text = _callPages.Values.Any(entry => entry.Page.HasUnsavedNotes)
        ? new UnsavedCallNotesException().Message : new UnsavedPluginChangesException().Message;
}
