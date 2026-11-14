using DevExpress.XtraBars.Docking2010.Views;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Plugin.Windows;

namespace WawiCallPro.App;

public partial class MainForm
{
    private readonly Dictionary<Guid, (BaseDocument Document, CallDetailsPage Page)> _callPages = [];
    private readonly HashSet<Guid> _callNotificationsHandled = [];
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
            await ShowCallNotificationAsync(existing.Page);
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
        {
            if (!incoming) { _callNotificationsHandled.Add(page.Call.Id); existing.Show(); if (!_backgroundRun) existing.Activate(); }
            return;
        }
        var lookup = CreateCallerDialog();
        lookup.BindCall(page.Call, page.AcceptAsync);
        lookup.BindNotes(page.Notes);
        if (_backgroundRun) lookup.PrepareForBackgroundRun();
        // Retain lookup/selection work for every call, even when automatic windows are disabled.
        // Ownership also keeps the hidden window in the normal close/cancellation lifecycle.
        lookup.Owner = this;
        if (incoming) await ShowCallNotificationAsync(page);
        else { _callNotificationsHandled.Add(page.Call.Id); lookup.Show(this); }
        await lookup.Page.SearchAsync();
        await page.Notes.LoadAsync();
    }
    private async Task ShowCallNotificationAsync(CallDetailsPage page)
    {
        if (_closing || page.IsDisposed || page.Call.Direction != CallDirection.Incoming || page.Call.State != CallState.Ringing
            || !_callNotificationsHandled.Add(page.Call.Id)) return;
        try
        {
            var settings = await _workspace!.Settings.LoadAsync(_telephonyUiLifetime.Token);
            if (!settings.Options.User.ShowCallNotifications || _closing || page.IsDisposed || page.Call.State != CallState.Ringing) return;
            if (OwnedForms.OfType<FrmCallerLookup>().FirstOrDefault(form => form.CallId == page.Call.Id && !form.IsDisposed) is { Visible: false } lookup)
                lookup.ShowForIncoming(this);
        }
        catch (OperationCanceledException) when (_closing || IsDisposed) { }
        catch (Exception exception) { if (!_closing && !IsDisposed) connectionLabel.Text = PageOperation.Describe(exception); }
    }
    internal void EnsureViewsCanClose()
    {
        EnsureCallsAreIdle();
        if (_callPages.Values.Any(entry => entry.Page.HasUnsavedNotes) || HasUnsavedHistoryNotes) throw new UnsavedCallNotesException();
        if (_pluginPages.Values.Any(view => view.HasUnsavedChanges) || _callPages.Values.Any(entry => entry.Page.HasUnsavedChanges))
            throw new UnsavedPluginChangesException();
    }
    private void EnsureCallsAreIdle(Guid? callId = null)
    {
        // The service is authoritative even before queued UI events create or update a call page.
        if (_workspace?.Telephony?.GetCalls().Any(call => (callId is null || call.Id == callId)
            && call.State is not (CallState.Ended or CallState.Failed)) != true) return;
        RestartRequested = false;
        throw new ActiveCallCloseException();
    }
    internal void ReportActiveCall() => connectionLabel.Text = new ActiveCallCloseException().Message;
    private bool HasUnsavedHistoryNotes => _documents.Values.Any(document =>
        document.Control is Panel { Controls.Count: > 0 } host && host.Controls[0] is HistoryPage { HasUnsavedNotes: true });
    internal void ReportUnsavedChanges() => connectionLabel.Text = _callPages.Values.Any(entry => entry.Page.HasUnsavedNotes) || HasUnsavedHistoryNotes
        ? new UnsavedCallNotesException().Message : new UnsavedPluginChangesException().Message;
}
