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
    private readonly Dictionary<Guid, FrmToast> _callToasts = [];
    private AppToast? _incomingCallToast;
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
            var current = existing.Page.Call;
            if (_callToasts.TryGetValue(current.Id, out var toast))
            {
                if (current.State == CallState.Ringing) UpdateIncomingToast(toast, current, FindCallLookup(current.Id));
                else toast.Close();
            }
            foreach (var lookup in OwnedForms.OfType<FrmCallerLookup>().Where(form => form.CallId == call.Id).ToArray())
            {
                lookup.ObserveCall(existing.Page.Call);
                if (existing.Page.Call.State is CallState.Ended or CallState.Failed) await lookup.CompleteCallAsync(call.Id);
            }
            await ShowCallNotificationAsync(existing.Page);
            return;
        }
        if (call.State is CallState.Ended or CallState.Failed) return;
        var page = new CallDetailsPage(); page.Bind(_workspace, call);
        page.ConfigureTabPersistence(_state);
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
        using var waiting = new UiWaitCursor(this);
        FrmCallerLookup? reusable = null;
        if (incoming && page.Call.Direction == CallDirection.Incoming)
        {
            reusable = OwnedForms.OfType<FrmCallerLookup>().LastOrDefault(form => form.CanReuse);
            if (reusable is not null)
            {
                var settings = await _workspace!.Settings.LoadAsync(_telephonyUiLifetime.Token);
                if (_closing || page.IsDisposed || page.Call.State is CallState.Ended or CallState.Failed
                    || OwnedForms.OfType<FrmCallerLookup>().Any(form => form.CallId == page.Call.Id && !form.IsDisposed)) return;
                if (!settings.Options.User.ReuseCallDialog || !settings.Options.User.ShowCallNotifications || !reusable.CanReuse)
                    reusable = null;
            }
        }
        reusable?.PrepareForReuse();
        var lookup = reusable ?? CreateCallerDialog();
        lookup.BindCall(page.Call, page.AcceptAsync);
        lookup.BindNotes(page.Notes);
        var callerPage = lookup.Page;
        if (_backgroundRun) lookup.PrepareForBackgroundRun();
        // Retain lookup/selection work for every call, even when automatic windows are disabled.
        // Ownership also keeps the hidden window in the normal close/cancellation lifecycle.
        lookup.Owner = this;
        if (incoming) await ShowCallNotificationAsync(page);
        else { _callNotificationsHandled.Add(page.Call.Id); lookup.Show(this); }
        if (page.Call.State is CallState.Ended or CallState.Failed)
        {
            await lookup.CompleteCallAsync(page.Call.Id);
            return;
        }
        await callerPage.SearchAsync();
        await page.Notes.LoadAsync();
    }
    private async Task ShowCallNotificationAsync(CallDetailsPage page)
    {
        if (_closing || page.IsDisposed || page.Call.Direction != CallDirection.Incoming || page.Call.State != CallState.Ringing
            || FindCallLookup(page.Call.Id) is null
            || !_callNotificationsHandled.Add(page.Call.Id)) return;
        try
        {
            var settings = await _workspace!.Settings.LoadAsync(_telephonyUiLifetime.Token);
            if (_closing || page.IsDisposed || page.Call.State != CallState.Ringing) return;
            var lookup = FindCallLookup(page.Call.Id);
            if (settings.Options.User.ShowIncomingCallToasts)
            {
                try { ShowIncomingCallToast(page, lookup); }
                catch (Exception) { _workspace!.LogWindow?.LogWarning("Anruf-Toast konnte nicht angezeigt werden."); }
            }
            if (settings.Options.User.ShowCallNotifications && lookup is { Visible: false }) lookup.ShowForIncoming(this);
        }
        catch (OperationCanceledException) when (_closing || IsDisposed) { }
        catch (Exception exception) { if (!_closing && !IsDisposed) connectionLabel.Text = PageOperation.Describe(exception); }
    }

    private FrmCallerLookup? FindCallLookup(Guid id) =>
        OwnedForms.OfType<FrmCallerLookup>().FirstOrDefault(form => form.CallId == id && !form.IsDisposed);

    private void ShowIncomingCallToast(CallDetailsPage page, FrmCallerLookup? lookup)
    {
        var call = page.Call;
        var card = (_incomingCallToast ??= new AppToast(components)).ShowCard(this, "Eingehender Anruf",
            DescribeIncomingCaller(call, lookup), call.StartedAtUtc.LocalDateTime.ToString("T"),
            [
                new("Annehmen", _ => RunIncomingToastCommandAsync(call.Id, answer: true)),
                new("Ablehnen", _ => RunIncomingToastCommandAsync(call.Id, answer: false)),
                new("Details", _ => OpenIncomingToastDetailsAsync(page))
            ], _backgroundRun);
        _callToasts.Add(call.Id, card);
        card.FormClosed += (_, _) => _callToasts.Remove(call.Id);
    }

    private static void UpdateIncomingToast(FrmToast toast, CallSnapshot call, FrmCallerLookup? lookup) =>
        toast.SetContent("Eingehender Anruf", DescribeIncomingCaller(call, lookup), call.StartedAtUtc.LocalDateTime.ToString("T"));

    private static string DescribeIncomingCaller(CallSnapshot call, FrmCallerLookup? lookup)
    {
        var address = call.RemoteAddress;
        var source = address.Presentation == NumberPresentation.Available
            ? string.IsNullOrWhiteSpace(address.OriginalValue) ? address.E164 : address.OriginalValue
            : null;
        if (address.Presentation == NumberPresentation.Withheld) return "Rufnummer unterdrückt";
        var number = string.Concat((source ?? "").Where(character => !char.IsControl(character)).Take(96)).Trim();
        if (number.Length == 0) return "Rufnummer unbekannt";
        return lookup?.Page.DescribeIncomingRegion(address) is { Length: > 0 } region ? number + " (" + region + ")" : number;
    }

    private async Task RunIncomingToastCommandAsync(Guid id, bool answer)
    {
        if (_closing || _workspace?.Telephony is not { } phone) return;
        if (!phone.GetCalls().Any(call => call.Id == id && call.Direction == CallDirection.Incoming && call.State == CallState.Ringing))
        {
            if (_callToasts.TryGetValue(id, out var stale)) stale.Close();
            return;
        }
        if (answer) await phone.AnswerAsync(id, _telephonyUiLifetime.Token);
        else await phone.RejectAsync(id, _telephonyUiLifetime.Token);
        if (_callToasts.TryGetValue(id, out var toast)) toast.Close();
    }

    private async Task OpenIncomingToastDetailsAsync(CallDetailsPage page)
    {
        if (page.IsDisposed || !ReferenceEquals(RestoreFromTray(), this)) return;
        await OpenCallLookupAsync(page, false);
        if (_callToasts.TryGetValue(page.Call.Id, out var toast)) toast.Close();
    }
    internal void EnsureViewsCanClose()
    {
        if (_callPages.Values.Any(entry => entry.Page.HasUnsavedNotes) || HasUnsavedHistoryNotes) throw new UnsavedCallNotesException();
        if (_pluginPages.Values.Any(view => view.HasUnsavedChanges) || _callPages.Values.Any(entry => entry.Page.HasUnsavedChanges))
            throw new UnsavedPluginChangesException();
    }
    private async Task EndActiveCallsAsync()
    {
        if (_workspace?.Telephony is not { } phone) return;
        foreach (var call in phone.GetCalls().Where(AppTelephonyService.IsActive))
        {
            try { await phone.HangUpAsync(call.Id); }
            catch (TelephonyException error) when (error.Code == "CallNotActive"
                && !phone.GetCalls().Any(current => current.Id == call.Id && AppTelephonyService.IsActive(current)))
            {
                // The remote party ended this call before our command reached the service.
            }
        }
        EnsureCallsAreIdle();
    }
    private void EnsureCallsAreIdle(Guid? callId = null)
    {
        // The service is authoritative even before queued UI events create or update a call page.
        if (_workspace?.Telephony?.GetCalls().Any(call => (callId is null || call.Id == callId)
            && AppTelephonyService.IsActive(call)) != true) return;
        RestartRequested = false;
        throw new ActiveCallCloseException();
    }
    internal void ReportActiveCall() => connectionLabel.Text = new ActiveCallCloseException().Message;
    private bool HasUnsavedHistoryNotes => _documents.Values.Any(document =>
        document.Control is Panel { Controls.Count: > 0 } host && host.Controls[0] is HistoryPage { HasUnsavedNotes: true });
    internal void ReportUnsavedChanges() => connectionLabel.Text = _callPages.Values.Any(entry => entry.Page.HasUnsavedNotes) || HasUnsavedHistoryNotes
        ? new UnsavedCallNotesException().Message : new UnsavedPluginChangesException().Message;
}
