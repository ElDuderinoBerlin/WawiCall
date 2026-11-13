using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using WawiCallPro.Utilities.Operations;
using System.Reflection;
using System.Drawing.Imaging;
using WawiCallPro.Telephony.Sip;
using System.Runtime.CompilerServices;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraTab;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Views;
using WawiCallPro.Core.Threading;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Security;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Tests;

[TestClass, DoNotParallelize]
public sealed class AppShellTests
{
    [TestMethod]
    [DataRow(CallState.Created)]
    [DataRow(CallState.Dialing)]
    [DataRow(CallState.Ringing)]
    [DataRow(CallState.Connected)]
    [DataRow(CallState.Held)]
    public Task ActiveCallsBlockShutdownRestartPluginSwitchAndTheirDocument(CallState state)
    {
        var telephony = new CommandTelephony();
        return WithWorkspace(async (workspace, services, plugin) =>
        {
            await SeedCallerCachesAsync(workspace, services);
            using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
            form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
            var call = await CreateWorkspaceCallAsync(services); await form.ObserveCallAsync(call);
            var entry = Field<Dictionary<Guid, (DevExpress.XtraBars.Docking2010.Views.BaseDocument Document, CallDetailsPage Page)>>(form, "_callPages")[call.Id];
            var popup = form.OwnedForms.OfType<FrmCallerLookup>().Single();
            // Even an already-ended UI snapshot must not override the live service's current state.
            await form.ObserveCallAsync(call with { Revision = call.Revision + 1, State = CallState.Ended });
            telephony.Current = call with { State = state };
            form.RestartRequested = true;
            await Assert.ThrowsAsync<ActiveCallCloseException>(() => form.PrepareToCloseAsync());
            Assert.IsFalse(form.RestartRequested); Assert.IsFalse(Field<bool>(form, "_closing"));
            Assert.IsFalse(entry.Page.Notes.IsStopping);
            await Assert.ThrowsAsync<ActiveCallCloseException>(() => (Task)typeof(MainForm).GetMethod("CloseDocumentAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [entry.Document])!);
            await Assert.ThrowsAsync<ActiveCallCloseException>(() => (Task)typeof(MainForm).GetMethod("PreparePluginSwitchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!);
            Assert.IsFalse(popup.IsDisposed); Assert.IsFalse(entry.Page.IsDisposed); Assert.IsFalse(entry.Page.Notes.IsStopping);
            Assert.AreEqual("Test.Lexware", ((TestRuntime)workspace.Plugins).ActivePluginId);
            Assert.IsEmpty(telephony.Commands, "Blocked requests must not issue a phone command.");
            form.ReportActiveCall();
            Assert.AreEqual(new ActiveCallCloseException().Message, Field<LabelControl>(form, "connectionLabel").Text);
            Assert.AreEqual(new ActiveCallCloseException().Message, PageOperation.Describe(new ActiveCallCloseException()));
            telephony.Current = call with { State = CallState.Ended };
            await form.PrepareToCloseAsync(); Assert.IsTrue(entry.Page.Notes.IsStopping);
        }, telephony);
    }

    [TestMethod]
    [DataRow(CallState.Ended)]
    [DataRow(CallState.Failed)]
    public Task TerminalCallsAllowShutdownEvenIfTheUiHasNotReceivedItsFinalEvent(CallState state)
    {
        var telephony = new CommandTelephony();
        return WithWorkspace(async (workspace, services, plugin) =>
        {
            await SeedCallerCachesAsync(workspace, services);
            using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
            form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
            var call = await CreateWorkspaceCallAsync(services); await form.ObserveCallAsync(call);
            telephony.Current = call with { State = state };
            form.EnsureViewsCanClose(); await form.PrepareToCloseAsync();
            Assert.IsEmpty(telephony.Commands);
        }, telephony);
    }

    [TestMethod]
    public Task AnotherActiveCallDoesNotPreventClosingAnUnrelatedCallDocument()
    {
        var telephony = new CommandTelephony();
        return WithWorkspace(async (workspace, services, plugin) =>
        {
            await SeedCallerCachesAsync(workspace, services);
            using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
            form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
            var first = await CreateWorkspaceCallAsync(services); await form.ObserveCallAsync(first);
            var second = await CreateWorkspaceCallAsync(services); await form.ObserveCallAsync(second);
            var pages = Field<Dictionary<Guid, (DevExpress.XtraBars.Docking2010.Views.BaseDocument Document, CallDetailsPage Page)>>(form, "_callPages");
            var closedPage = pages[first.Id].Page; var activePage = pages[second.Id].Page;
            telephony.Current = second with { State = CallState.Connected };
            await (Task)typeof(MainForm).GetMethod("CloseDocumentAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [pages[first.Id].Document])!;
            Assert.IsTrue(closedPage.IsDisposed); Assert.IsFalse(activePage.IsDisposed); Assert.IsFalse(activePage.Notes.IsStopping);
            Assert.HasCount(1, pages); Assert.IsEmpty(telephony.Commands);
            await Assert.ThrowsAsync<ActiveCallCloseException>(() => form.PrepareToCloseAsync());
            telephony.Current = null; await form.PrepareToCloseAsync();
        }, telephony);
    }

    [TestMethod]
    public Task ActiveCallWithoutUiDocumentBlocksActualPluginActivationAndAllowsRetryAfterEnding()
    {
        var telephony = new CommandTelephony();
        return WithWorkspace(async (workspace, services, plugin) =>
        {
            using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
            form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
            telephony.Current = CommandCall(); // The notification has not yet reached the UI.
            Assert.ThrowsExactly<ActiveCallCloseException>(form.EnsureViewsCanClose);
            using var settings = form.CreateSettingsDialog(true); settings.PrepareForBackgroundRun(); settings.Show(form);
            var settingsPage = Field<SettingsPage>(settings, "settingsPage"); await settingsPage.ReloadAsync();
            var management = Field<PluginPage>(settingsPage, "pluginPage");
            await management.HandleCardActionAsync("activate", 1);
            Assert.AreEqual("Test.Lexware", ((TestRuntime)workspace.Plugins).ActivePluginId);
            Assert.AreEqual(new ActiveCallCloseException().Message, Field<LabelControl>(management, "statusLabel").Text);
            Assert.IsTrue(Field<SimpleButton>(settingsPage, "saveButton").Enabled);
            Assert.AreNotEqual("Test.Dummy", (await workspace.Settings.LoadAsync()).Options.User.ActivePluginId);
            Assert.IsEmpty(telephony.Commands);
            telephony.Current = null;
            await management.HandleCardActionAsync("activate", 1);
            Assert.AreEqual("Test.Dummy", ((TestRuntime)workspace.Plugins).ActivePluginId);
            Assert.AreEqual("Test.Dummy", (await workspace.Settings.LoadAsync()).Options.User.ActivePluginId);
            await settings.StopAsync(); await form.PrepareToCloseAsync();
        }, telephony);
    }

    [TestMethod]
    public Task CallNotesShareDraftsKeepStableIdsAfterLostAcknowledgementsAndRejectForeignResults() => RunStaAsync(async () =>
    {
        var history = new NotesHistory(CommandCall());
        using var session = new CallNotesSessionFactory(history).Create(history.Call.Id);
        using var wrongWindow = new FrmCallerLookup(); wrongWindow.BindCall(CommandCall(), (_, _) => Task.CompletedTask);
        Assert.ThrowsExactly<InvalidOperationException>(() => wrongWindow.BindNotes(session));
        using var first = new CallNotesPanel(); using var second = new CallNotesPanel();
        first.Bind(session); second.Bind(session);
        Field<MemoEdit>(first, "draftEdit").Text = "Rückruf morgen · Ä 电话";
        Assert.AreEqual(session.Draft, Field<MemoEdit>(second, "draftEdit").Text);
        history.LoseAcknowledgement = true;
        await session.SaveAsync();
        Assert.AreEqual("Rückruf morgen · Ä 电话", session.Draft);
        Assert.IsTrue(session.HasUnsavedChanges); Assert.HasCount(1, history.Stored);
        StringAssert.Contains(Field<LabelControl>(second, "statusLabel").Text, "Entwurf bleibt erhalten");
        await session.LoadAsync(); Assert.AreEqual("Rückruf morgen · Ä 电话", session.Draft);
        await session.SaveAsync();
        Assert.HasCount(1, history.Stored); Assert.AreEqual(history.Attempts[0], history.Attempts[1]);
        Assert.AreEqual("", Field<MemoEdit>(first, "draftEdit").Text); Assert.IsFalse(session.HasUnsavedChanges);
        StringAssert.Contains(Field<MemoEdit>(second, "notesEdit").Text, "Rückruf morgen");
        session.SetDraft("Zweiter Entwurf");
        history.Read = _ => Task.FromResult<CallHistory?>(new() { Call = history.Call with { Id = Guid.NewGuid() } });
        await session.LoadAsync(); Assert.AreEqual("Zweiter Entwurf", session.Draft); Assert.HasCount(1, session.Notes);
        Assert.DoesNotContain("Unexpected", session.Status);
        session.DiscardDraft(); Assert.IsFalse(session.HasUnsavedChanges);
        await session.StopAsync();
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task CallNotesShutdownAwaitsIgnoringStorageAndPreventsLatePublication(bool saving) => RunStaAsync(async () =>
    {
        var history = new NotesHistory(CommandCall());
        using var session = new CallNotesSessionFactory(history).Create(history.Call.Id);
        session.SetDraft("Bleibt bei Abbruch erhalten");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        history.Before = async token => { captured = token; entered.SetResult(); await release.Task; };
        var pending = saving ? session.SaveAsync() : session.LoadAsync(); await entered.Task;
        var duplicate = saving ? session.SaveAsync() : session.LoadAsync(); Assert.AreSame(pending, duplicate);
        var stopping = session.StopAsync();
        Assert.IsTrue(captured.IsCancellationRequested); Assert.IsFalse(stopping.IsCompleted);
        Assert.AreEqual("Bleibt bei Abbruch erhalten", session.Draft);
        release.SetResult(); await stopping; await pending;
        Assert.AreEqual("Bleibt bei Abbruch erhalten", session.Draft); Assert.IsTrue(session.Notes.IsEmpty);
        if (saving) { Assert.HasCount(1, history.Attempts); Assert.HasCount(1, history.Stored); }
    });

    [TestMethod]
    public Task CallNotesPersistAfterCallEndAndPopupCloseWithoutBusinessAccessOrCrossCallDrafts() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        ((TestRuntime)services.GetRequiredService<IProductPluginRuntime>()).BusinessAvailable = false;
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
        var call = await CreateWorkspaceCallAsync(services); await form.ObserveCallAsync(call);
        var page = Field<Dictionary<Guid, (DevExpress.XtraBars.Docking2010.Views.BaseDocument Document, CallDetailsPage Page)>>(form, "_callPages")[call.Id].Page;
        var popup = form.OwnedForms.OfType<FrmCallerLookup>().Single();
        var notes = Field<CallNotesPanel>(popup.Page, "notesPanel");
        Field<MemoEdit>(notes, "draftEdit").Text = "Nach Gesprächsende speichern · Ä 电话";
        Assert.AreEqual(page.Notes.Draft, Field<MemoEdit>(Field<CallNotesPanel>(page, "notesPanel"), "draftEdit").Text);
        await Assert.ThrowsAsync<UnsavedCallNotesException>(() => form.PrepareToCloseAsync());
        var document = Field<Dictionary<Guid, (DevExpress.XtraBars.Docking2010.Views.BaseDocument Document, CallDetailsPage Page)>>(form, "_callPages")[call.Id].Document;
        await Assert.ThrowsAsync<UnsavedCallNotesException>(() => (Task)typeof(MainForm).GetMethod("CloseDocumentAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [document])!);
        form.ReportUnsavedChanges(); StringAssert.Contains(Field<LabelControl>(form, "connectionLabel").Text, "Gesprächsnotizen");
        popup.Close(); await WaitForDialStateAsync(() => popup.IsDisposed);
        Assert.IsTrue(page.HasUnsavedNotes);
        await form.ObserveCallAsync(call with { Revision = call.Revision + 1, State = CallState.Ended });
        Assert.IsTrue(page.HasUnsavedNotes);
        var otherCall = await CreateWorkspaceCallAsync(services); await form.ObserveCallAsync(otherCall);
        var other = Field<Dictionary<Guid, (DevExpress.XtraBars.Docking2010.Views.BaseDocument Document, CallDetailsPage Page)>>(form, "_callPages")[otherCall.Id].Page;
        Assert.AreEqual("", other.Notes.Draft);
        typeof(CallNotesPanel).GetMethod("Save_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Field<CallNotesPanel>(page, "notesPanel"), [null, EventArgs.Empty]);
        await page.Notes.PendingOperation;
        Assert.IsFalse(page.HasUnsavedNotes);
        Assert.AreEqual("Nach Gesprächsende speichern · Ä 电话", (await workspace.History.GetCallAsync(call.Id))!.Notes.Single().Text);
        Assert.IsTrue((await workspace.History.GetCallAsync(otherCall.Id))!.Notes.IsEmpty);
        using var reopened = workspace.CallNotes.Create(call.Id); await reopened.LoadAsync(); Assert.HasCount(1, reopened.Notes);
        using var history = new HistoryPage(); history.Bind(workspace, null); await InvokeActionAsync(history, "Refresh_Click");
        var grid = Field<DevExpress.XtraGrid.Views.Grid.GridView>(history, "view");
        for (var row = 0; row < grid.DataRowCount; row++)
            if ((Guid)grid.GetRowCellValue(row, "Id") == call.Id) grid.FocusedRowHandle = row;
        await WaitForOperationAsync(history);
        StringAssert.Contains(Field<MemoEdit>(history, "detailsEdit").Text, "Nach Gesprächsende speichern");
        await reopened.StopAsync(); await history.StopAsync(); await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task CallNotesCannotStartANewDraftWhileApplicationShutdownAwaitsOtherWork() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
        var call = await CreateWorkspaceCallAsync(services); await form.ObserveCallAsync(call);
        var page = Field<Dictionary<Guid, (DevExpress.XtraBars.Docking2010.Views.BaseDocument Document, CallDetailsPage Page)>>(form, "_callPages")[call.Id].Page;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = Field<PageOperation>(form, "_operation").RunAsync(_ => release.Task);
        var closing = form.PrepareToCloseAsync();
        Assert.IsFalse(closing.IsCompleted); Assert.IsTrue(page.Notes.IsStopping);
        page.Notes.SetDraft("Must not be accepted after the close check."); Assert.AreEqual("", page.Notes.Draft);
        release.SetResult(); await closing; await pending;
    });

    [TestMethod]
    public Task CallNotesRenderInBothWindowsEmptyFilledSmallLargeFontAndScaledWithoutVisibleWindows() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply(); await SeedCallerCachesAsync(workspace, services);
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.GetFullPath(".artifacts/phase8-call-notes/render"); Directory.CreateDirectory(output);
        try
        {
            foreach (var (width, height, fontSize, scale, skin, palette) in new[] { (900, 650, 10F, 1F, oldSkin, oldPalette),
                (600, 480, 8F, 1F, oldSkin, oldPalette), (640, 540, 16F, 1F, "Office 2019 Black", ""),
                (600, 480, 10F, 1.5F, oldSkin, oldPalette), (900, 650, 10F, 1.25F, "Basic", "Blue Dark") })
            foreach (var filled in new[] { false, true })
            foreach (var popup in new[] { false, true })
            {
                if (string.IsNullOrEmpty(palette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin, palette);
                using var font = new Font("Tahoma", fontSize); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                var call = await CreateWorkspaceCallAsync(services);
                if (filled) await workspace.History.SaveNoteAsync(call.Id, Guid.NewGuid(), "Rückruf vereinbart · Ä 电话\r\nMehrzeilige Gesprächsnotiz mit einer längeren Beschreibung, die bei kleiner Fensterbreite umbrechen muss.");
                using var detail = new CallDetailsPage(); detail.Bind(workspace, call); await detail.Notes.LoadAsync();
                if (filled) detail.Notes.SetDraft("Entwurf: Kunde bittet um Rückruf.\r\nDiese Notiz bleibt diesem Gespräch zugeordnet.");
                using var host = new FrmCallerLookup(workspace, null); host.PrepareForBackgroundRun(); host.Font = font; host.ClientSize = new(width, height);
                CallNotesPanel panel;
                if (popup)
                {
                    host.BindCall(call, (_, _) => Task.CompletedTask); host.BindNotes(detail.Notes);
                    Field<XtraTabControl>(host.Page, "tabs").SelectedTabPage = Field<XtraTabPage>(host.Page, "notesTab");
                    panel = Field<CallNotesPanel>(host.Page, "notesPanel");
                }
                else
                {
                    foreach (Control child in host.Controls.Cast<Control>().ToArray()) child.Dispose(); host.Controls.Clear();
                    detail.Dock = DockStyle.Fill; host.Controls.Add(detail);
                    Field<XtraTabControl>(detail, "tabs").SelectedTabPage = Field<XtraTabPage>(detail, "notesTab");
                    panel = Field<CallNotesPanel>(detail, "notesPanel");
                }
                host.Show(); if (scale != 1F) host.Scale(new SizeF(scale, scale)); host.PerformLayout(); Application.DoEvents();
                CheckTableBounds(host);
                Assert.IsTrue(Field<MemoEdit>(panel, "draftEdit").Height >= 25, "The draft must keep usable space.");
                Assert.AreEqual(Field<SimpleButton>(panel, "saveButton").Size, Field<SimpleButton>(panel, "discardButton").Size);
                using var shot = UiAudit.Capture(host); shot.Save(Path.Combine(output, $"notes-{popup}-{filled}-{width}-{fontSize:0}-{scale:0.0}-{skin}.png"), ImageFormat.Png);
                detail.Notes.DiscardDraft(); if (popup) await host.StopAsync(); await detail.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });

    private sealed class NotesHistory(CallSnapshot call) : ICallHistoryService
    {
        internal CallSnapshot Call { get; } = call;
        internal readonly Dictionary<Guid, CallNote> Stored = [];
        internal readonly List<Guid> Attempts = [];
        internal bool LoseAcknowledgement;
        internal Func<CancellationToken, Task>? Before;
        internal Func<CancellationToken, Task<CallHistory?>>? Read;
        public async Task<CallHistory?> GetCallAsync(Guid callId, CancellationToken cancellationToken = default)
        {
            if (Before is not null) await Before(cancellationToken);
            return Read is not null ? await Read(cancellationToken) : new() { Call = Call, Notes = Stored.Values.ToImmutableArray() };
        }
        public async Task<CallNote> SaveNoteAsync(Guid callId, Guid noteId, string text, CancellationToken cancellationToken = default)
        {
            Attempts.Add(noteId);
            if (Before is not null) await Before(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var note = new CallNote(noteId, callId, text, Stored.TryGetValue(noteId, out var previous) ? previous.CreatedAtUtc : now, now);
            Stored[noteId] = note;
            if (LoseAcknowledgement) { LoseAcknowledgement = false; throw new InvalidOperationException("storage-sentinel"); }
            return note;
        }
        public Task<Page<CallSnapshot>> GetCallsAsync(CallHistoryQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [TestMethod]
    public Task CallCommandsFollowObservedStateAndKeepTheCallIdentity() => RunStaAsync(async () =>
    {
        using var panel = new CallControlPanel(); panel.CreateControl();
        var service = new CommandTelephony(); var call = CommandCall(); service.Current = call;
        panel.Bind(service, call);
        Assert.IsTrue(Field<SimpleButton>(panel, "answerButton").Enabled);
        Assert.AreEqual("Ablehnen", Field<SimpleButton>(panel, "endButton").Text);
        Assert.IsFalse(Field<SimpleButton>(panel, "holdButton").Enabled);
        await InvokeActionAsync(panel, "Answer_Click");
        Assert.AreEqual("Klingelt", Field<LabelControl>(panel, "statusLabel").Text, "Command completion alone is not an observed connection.");
        await InvokeActionAsync(panel, "End_Click");
        await Task.Run(() => service.Publish(call with { Revision = 2, State = CallState.Connected }));
        await WaitForDialStateAsync(() => Field<SimpleButton>(panel, "holdButton").Enabled);
        await InvokeActionAsync(panel, "Hold_Click");
        service.Publish(call with { Revision = 3, State = CallState.Held });
        await WaitForDialStateAsync(() => Field<SimpleButton>(panel, "holdButton").Text == "Fortsetzen");
        await InvokeActionAsync(panel, "Hold_Click");
        await InvokeActionAsync(panel, "End_Click");
        CollectionAssert.AreEqual(new[] { "Answer", "Reject", "Hold", "Resume", "HangUp" }, service.Commands.Select(item => item.Command).ToArray());
        Assert.IsTrue(service.Commands.All(item => item.Id == call.Id));
        panel.Observe(call with { Revision = 2, State = CallState.Ended });
        panel.Observe(call with { Id = Guid.NewGuid(), Revision = 100, State = CallState.Ended });
        Assert.IsTrue(Field<SimpleButton>(panel, "holdButton").Enabled);
        panel.Observe(call with { Revision = 4, State = CallState.Ended });
        panel.Observe(call with { Revision = 5, State = CallState.Connected });
        Assert.IsFalse(Field<SimpleButton>(panel, "endButton").Enabled);
        await InvokeActionAsync(panel, "End_Click"); Assert.HasCount(5, service.Commands);
        await panel.StopAsync(); Assert.AreEqual(0, service.Subscribers);
        Assert.ThrowsExactly<InvalidOperationException>(() => panel.Bind(null, call));
    });

    [TestMethod]
    public Task CallCommandsRejectDuplicatesReportFailuresAndAwaitCancellationOnClose() => RunStaAsync(async () =>
    {
        var service = new CommandTelephony(); var call = CommandCall() with { Direction = CallDirection.Outgoing };
        using var dialog = new FrmCallerLookup(); dialog.PrepareForBackgroundRun();
        dialog.BindCall(call, (_, _) => Task.CompletedTask);
        var panel = Field<CallControlPanel>(dialog, "callControls"); panel.Bind(service, call); service.Current = call;
        dialog.Show();
        service.Execute = (_, _) => throw new TelephonyException("busy");
        await InvokeActionAsync(panel, "End_Click");
        Assert.AreEqual(new TelephonyException("busy").Message, Field<LabelControl>(panel, "statusLabel").Text);
        Assert.IsTrue(Field<SimpleButton>(panel, "endButton").Enabled);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken captured = default;
        service.Execute = (_, token) => { captured = token; return release.Task; };
        panel.GetType().GetMethod("End_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, [null, EventArgs.Empty]);
        Assert.IsFalse(Field<SimpleButton>(panel, "endButton").Enabled);
        panel.GetType().GetMethod("End_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, [null, EventArgs.Empty]);
        Assert.HasCount(2, service.Commands);
        dialog.Close(); await Task.Delay(30);
        Assert.IsTrue(captured.IsCancellationRequested); Assert.IsFalse(dialog.IsDisposed);
        Assert.AreEqual(0, service.Subscribers);
        service.Publish(call with { Revision = 2, State = CallState.Connected });
        release.SetResult(); await WaitForDialStateAsync(() => dialog.IsDisposed);
    });

    [TestMethod]
    public Task IncomingCallControlsRemainAvailableWhenTheBusinessSourceIsBlocked() => WithWorkspace(async (workspace, services, plugin) =>
    {
        ((TestRuntime)services.GetRequiredService<IProductPluginRuntime>()).BusinessAvailable = false;
        var reads = 0; ((TestDirectory)workspace.Directory).ReadCatalog = _ => { reads++; throw new InvalidOperationException("Source must not be read."); };
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
        var call = CommandCall();
        await form.ObserveCallAsync(call);
        var popup = form.OwnedForms.OfType<FrmCallerLookup>().Single();
        var controls = Field<CallControlPanel>(popup, "callControls");
        Assert.IsTrue(controls.Visible); Assert.IsTrue(Field<SimpleButton>(controls, "answerButton").Enabled);
        Assert.IsFalse(Field<SimpleButton>(popup.Page, "searchButton").Enabled);
        StringAssert.Contains(Field<LabelControl>(popup.Page, "statusLabel").Text, "Log-Fenster");
        Assert.AreEqual(0, reads);
        await form.ObserveCallAsync(call with { Revision = 2, State = CallState.Ended });
        Assert.IsFalse(Field<SimpleButton>(controls, "answerButton").Enabled);
        await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task CallControlsRenderRingingHeldAndEndedWithSmallWindowsFontsDpiAndSkins() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.GetFullPath(".artifacts/phase8-call-controls/render"); Directory.CreateDirectory(output);
        try
        {
            foreach (var (width, size, scale, skin, palette) in new[] { (600, 8F, 1F, oldSkin, oldPalette), (640, 16F, 1F, "Office 2019 Black", ""),
                (600, 10F, 1.5F, oldSkin, oldPalette), (900, 10F, 1.25F, "Basic", "Blue Dark") })
            foreach (var state in new[] { CallState.Ringing, CallState.Held, CallState.Ended })
            {
                if (string.IsNullOrEmpty(palette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin, palette);
                using var font = new Font("Tahoma", size); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var dialog = new FrmCallerLookup(); dialog.PrepareForBackgroundRun(); dialog.Font = font; dialog.ClientSize = new(width, 500);
                var call = CommandCall() with { State = state };
                dialog.BindCall(call, (_, _) => Task.CompletedTask);
                var panel = Field<CallControlPanel>(dialog, "callControls"); panel.Bind(new CommandTelephony { Current = call }, call);
                dialog.Show(); if (scale != 1F) dialog.Scale(new SizeF(scale, scale));
                dialog.PerformLayout(); Application.DoEvents(); CheckTableBounds(dialog);
                var answer = Field<SimpleButton>(panel, "answerButton");
                Assert.AreEqual(answer.Size, Field<SimpleButton>(panel, "endButton").Size);
                Assert.AreEqual(answer.Size, Field<SimpleButton>(panel, "holdButton").Size);
                Assert.IsTrue(panel.Bottom <= dialog.ClientSize.Height);
                using var shot = UiAudit.Capture(dialog); shot.Save(Path.Combine(output, $"controls-{width}-{size:0}-{scale:0.0}-{skin}-{state}.png"), ImageFormat.Png);
                await dialog.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });

    private static CallSnapshot CommandCall()
    {
        var now = DateTimeOffset.UtcNow;
        return new() { Id = Guid.NewGuid(), Revision = 1, Direction = CallDirection.Incoming, State = CallState.Ringing,
            LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "+49301234567"), StartedAtUtc = now, StateChangedAtUtc = now };
    }
    private sealed class CommandTelephony : ITelephonyService
    {
        internal CallSnapshot? Current;
        internal readonly List<(string Command, Guid Id)> Commands = [];
        internal Func<string, CancellationToken, Task>? Execute;
        private EventHandler<CallChangedEventArgs>? _calls;
        internal int Subscribers => _calls?.GetInvocationList().Length ?? 0;
        public RegistrationSnapshot Registration { get; } = new(RegistrationState.Registered, DateTimeOffset.UtcNow);
        public event EventHandler<RegistrationChangedEventArgs>? RegistrationChanged { add { } remove { } }
        public event EventHandler<CallChangedEventArgs>? CallChanged { add => _calls += value; remove => _calls -= value; }
        public ImmutableArray<CallSnapshot> GetCalls() => Current is null ? [] : [Current];
        internal void Publish(CallSnapshot call) { Current = call; _calls?.Invoke(this, new(call)); }
        private Task Run(string command, Guid id, CancellationToken token) { Commands.Add((command, id)); return Execute?.Invoke(command, token) ?? Task.CompletedTask; }
        public Task AnswerAsync(Guid callId, CancellationToken cancellationToken = default) => Run("Answer", callId, cancellationToken);
        public Task RejectAsync(Guid callId, CancellationToken cancellationToken = default) => Run("Reject", callId, cancellationToken);
        public Task HangUpAsync(Guid callId, CancellationToken cancellationToken = default) => Run("HangUp", callId, cancellationToken);
        public Task HoldAsync(Guid callId, CancellationToken cancellationToken = default) => Run("Hold", callId, cancellationToken);
        public Task ResumeAsync(Guid callId, CancellationToken cancellationToken = default) => Run("Resume", callId, cancellationToken);
        public Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task TransferAsync(Guid callId, string destination, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendDtmfAsync(Guid callId, char digit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task MissingLexwareWarnsInLogAndDisablesSourceActionsWhileDummyAndLocalToolsRemainAvailable(bool checkFails) => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var runtime = (TestRuntime)workspace.Plugins;
        runtime.BusinessAvailable = false;
        var logs = new LogWindowService();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new UiLogProvider(logs)));
        var installation = new LexwareInstallation(new MissingRegistry(checkFails), loggerFactory.CreateLogger<LexwareInstallation>());
        installation.Check(); // The warning predates creation/binding of the main log view.
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.GetFullPath(".artifacts/lexware-installation"); Directory.CreateDirectory(output);
        try
        {
            foreach (var (width, fontSize, skin, palette, scale) in new[] { (1100, 10F, "Basic", "", 1F),
                (900, 8F, "Basic", "Blue Dark", 1.25F), (1100, 16F, "Office 2019 Black", "", 1F) })
            {
                if (palette.Length == 0) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin, palette);
                using var font = new Font("Tahoma", fontSize); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                // Refill the startup queue for each independent form.
                WawiCallPro.Utilities.Operations.OperationEvents.Write(loggerFactory.CreateLogger("test"), checkFails
                    ? WawiCallPro.Utilities.Operations.OperationEvent.LexwareCheckFailed : WawiCallPro.Utilities.Operations.OperationEvent.LexwareMissing);
                using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>(), logs);
                form.PrepareForBackgroundRun(); form.Font = font; form.ClientSize = new(width, 760); form.Show();
                Assert.IsFalse(Field<DevExpress.XtraBars.BarButtonItem>(form, "callerItem").Enabled);
                Assert.IsFalse(Field<DevExpress.XtraBars.BarButtonItem>(form, "dialItem").Enabled);
                Assert.IsTrue(Field<DevExpress.XtraBars.BarButtonItem>(form, "historyItem").Enabled);
                Assert.IsTrue(Field<DevExpress.XtraBars.BarButtonItem>(form, "databaseStatusItem").Enabled);
                form.Navigate("caller");
                Assert.IsEmpty(form.OwnedForms.OfType<FrmCallerLookup>());
                var log = Field<LogPanel>(form, "logPanel"); log.Drain();
                var entries = Field<ListBoxControl>(log, "listLog").Items.Cast<object>().Select(item => item.ToString()!).ToArray();
                Assert.IsTrue(entries.Any(text => text.Contains("Warnung:") && text.Contains("Lexware")));
                Assert.IsFalse(entries.Any(text => text.Contains("synthetic-sensitive-value")));
                using var settings = form.CreateSettingsDialog(true); settings.PrepareForBackgroundRun(); settings.Font = font;
                settings.Show(form); settings.MinimumSize = Size.Empty; settings.ClientSize = new(Math.Min(width, 980), 700);
                await Field<SettingsPage>(settings, "settingsPage").ReloadAsync();
                var page = Field<SettingsPage>(settings, "settingsPage");
                Assert.IsFalse(Field<XtraTabPage>(page, "cacheTab").PageEnabled);
                Assert.IsTrue(Field<XtraTabPage>(page, "sipTab").PageEnabled);
                var plugins = Field<PluginPage>(page, "pluginPage");
                Assert.IsFalse(Field<SimpleButton>(plugins, "capabilitiesButton").Enabled);
                var view = Field<DevExpress.XtraGrid.Views.Items.ItemsView>(plugins, "catalogView");
                var blocked = view.GetRow(0)!;
                Assert.AreEqual(false, blocked.GetType().GetProperty("IsAvailable")!.GetValue(blocked));
                var dummy = view.GetRow(1)!;
                Assert.AreEqual(true, dummy.GetType().GetProperty("IsAvailable")!.GetValue(dummy));
                if (scale != 1F) { form.Scale(new SizeF(scale, scale)); settings.Scale(new SizeF(scale, scale)); }
                form.PerformLayout(); settings.PerformLayout(); Application.DoEvents();
                using (var shot = UiAudit.Capture(form)) shot.Save(Path.Combine(output, $"log-{checkFails}-{fontSize}-{skin}.png"), ImageFormat.Png);
                using (var shot = UiAudit.Capture(settings)) shot.Save(Path.Combine(output, $"plugins-{checkFails}-{fontSize}-{skin}.png"), ImageFormat.Png);
                CheckTableBounds(page);
                await settings.StopAsync(); await form.PrepareToCloseAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });

    private sealed class MissingRegistry(bool fails) : ILexwareRegistryProbe
    {
        public bool IsProInstalled() => fails ? throw new IOException("synthetic-sensitive-value") : false;
        public bool IsFinOfficeInstalled() => false;
    }

    [TestMethod]
    public Task DialRibbonReusesItsDialogAndSipStatusUpdatesDuringDatabaseWork() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show();
        var item = Field<DevExpress.XtraBars.BarButtonItem>(form, "dialItem");
        Assert.IsNotNull(item.ImageOptions.SvgImage);
        Assert.IsFalse(string.IsNullOrWhiteSpace(item.Hint));
        var ribbon = Field<DevExpress.XtraBars.Ribbon.RibbonControl>(form, "ribbon");
        ribbon.Minimized = false; ribbon.SelectedPage = Field<DevExpress.XtraBars.Ribbon.RibbonPage>(form, "startRibbon");
        form.PerformLayout(); Application.DoEvents();
        var output = Path.GetFullPath(".artifacts/phase8-dial"); Directory.CreateDirectory(output);
        using (var shot = UiAudit.Capture(form)) shot.Save(Path.Combine(output, "main-ribbon-status.png"), ImageFormat.Png);
        item.PerformClick(); item.PerformClick();
        Assert.HasCount(1, form.OwnedForms.OfType<FrmDial>().ToArray());
        var dial = form.OwnedForms.OfType<FrmDial>().Single();
        var status = Field<DevExpress.XtraBars.BarStaticItem>(form, "telephonyStatusItem");
        var feedback = Field<LabelControl>(form, "connectionLabel");
        typeof(MainForm).GetField("_databaseBusy", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, true);
        feedback.Text = "Sicherung läuft …";
        var update = workspace.Telephony!.GetType().GetMethod("SetRegistration", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await Task.Run(() => update.Invoke(workspace.Telephony, [RegistrationState.Reconnecting, "RegistrationRetry"]));
        await WaitForDialStateAsync(() => status.Caption == "SIP: Verbindungsaufbau …");
        Assert.AreEqual("Sicherung läuft …", feedback.Text);
        Assert.IsTrue(Field<LabelControl>((HomeDashboard)form.GetActiveDocumentView()!, "sipDetail").Text.Contains("unterbrochen"));
        typeof(MainForm).GetField("_databaseBusy", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, false);
        await Task.Run(() => update.Invoke(workspace.Telephony, [RegistrationState.Registered, null]));
        await WaitForDialStateAsync(() => status.Caption == "SIP: Angemeldet");
        Assert.AreEqual("SIP-Konto angemeldet.", feedback.Text);
        await form.PrepareToCloseAsync();
        Assert.IsFalse(Field<SimpleButton>(dial, "dialButton").Enabled);
    });

    [TestMethod]
    public Task DialDialogUsesExplicitCompanyAndPreventsDuplicateRequestsWithoutChangingWorkspace() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var companies = await workspace.Directory.GetCompaniesAsync();
        await workspace.Companies.SelectAsync(companies[0]);
        var telephony = new DialTelephony();
        using var dialog = new FrmDial(workspace.Directory, telephony);
        await dialog.ReloadAsync();
        var lookup = Field<LookUpEdit>(dialog, "companyEdit");
        var target = Field<TextEdit>(dialog, "destinationEdit");
        Assert.IsNull(lookup.EditValue, "Loading the catalog must not silently choose a company.");
        target.Text = "  sip:synthetic@127.0.0.1  ";
        await dialog.DialAsync();
        Assert.AreEqual(0, telephony.Attempts);
        lookup.EditValue = companies[1].Key;
        Assert.AreEqual(DevExpress.XtraEditors.Controls.SearchMode.AutoSearch, lookup.Properties.SearchMode);
        Assert.IsFalse(lookup.Properties.ShowHeader || lookup.Properties.ShowFooter);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        telephony.BeforeDial = _ => release.Task;
        var first = dialog.DialAsync();
        await dialog.DialAsync();
        Assert.AreEqual(1, telephony.Attempts);
        Assert.IsFalse(Field<SimpleButton>(dialog, "dialButton").Enabled);
        release.SetResult(); await first;
        Assert.AreEqual(companies[1].Key, telephony.Company);
        Assert.AreEqual("sip:synthetic@127.0.0.1", telephony.Destination);
        Assert.AreEqual(telephony.CallId, dialog.CallId);
        Assert.AreEqual(companies[0].Key, workspace.Companies.Current.Company!.Key);
        await dialog.StopAsync();
    });

    [TestMethod]
    public Task DialDialogTracksRegistrationAndActiveCallsAndKeepsInputAfterFailure() => RunStaAsync(async () =>
    {
        var directory = new TestDirectory(new("test", "Pro"));
        var company = (await directory.GetCompaniesAsync())[0];
        var telephony = new DialTelephony();
        using var dialog = new FrmDial(directory, telephony, company.Key);
        dialog.PrepareForBackgroundRun(); dialog.Show(); await dialog.ReloadAsync();
        var target = Field<TextEdit>(dialog, "destinationEdit"); target.Text = "+49301234567";
        var button = Field<SimpleButton>(dialog, "dialButton");
        Assert.IsTrue(button.Enabled);
        foreach (var registration in new[] { RegistrationState.Stopped, RegistrationState.Registering, RegistrationState.Reconnecting, RegistrationState.Failed })
        {
            await Task.Run(() => telephony.Register(registration));
            await WaitForDialStateAsync(() => !button.Enabled);
            await dialog.DialAsync(); Assert.AreEqual(0, telephony.Attempts);
            telephony.Register(RegistrationState.Registered);
            await WaitForDialStateAsync(() => button.Enabled);
        }
        telephony.SetActiveCall(true);
        await WaitForDialStateAsync(() => !button.Enabled);
        telephony.SetActiveCall(false);
        await WaitForDialStateAsync(() => button.Enabled);
        telephony.Failure = new TelephonyException("InvalidDestination");
        await dialog.DialAsync();
        Assert.IsNull(dialog.CallId);
        Assert.AreEqual("+49301234567", target.Text);
        Assert.AreEqual(company.Key, Field<LookUpEdit>(dialog, "companyEdit").EditValue);
        Assert.AreEqual(telephony.Failure.Message, Field<LabelControl>(dialog, "statusLabel").Text);
        Assert.IsTrue(button.Enabled);
        telephony.Failure = null;
        button.PerformClick();
        await WaitForDialStateAsync(() => dialog.IsDisposed);
        Assert.AreEqual(telephony.CallId, dialog.CallId);
        Assert.AreEqual(0, telephony.Subscribers);
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task DialDialogClosesOnceAndAwaitsCancellationOfPendingLoadOrDial(bool duringDial) => RunStaAsync(async () =>
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var telephony = new DialTelephony();
        var directory = new TestDirectory(new("test", "Pro"));
        if (!duringDial) directory.ReadCatalog = async token =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            await release.Task;
            return [];
        };
        using var dialog = new FrmDial(directory, telephony);
        dialog.PrepareForBackgroundRun(); dialog.Show();
        Task dialing = Task.CompletedTask;
        if (duringDial)
        {
            await dialog.ReloadAsync();
            Field<LookUpEdit>(dialog, "companyEdit").EditValue = (await directory.GetCompaniesAsync())[0].Key;
            Field<TextEdit>(dialog, "destinationEdit").Text = "123";
            telephony.BeforeDial = async token =>
            {
                using var registration = token.Register(() => cancelled.TrySetResult());
                await release.Task; token.ThrowIfCancellationRequested();
            };
            dialing = dialog.DialAsync();
        }
        dialog.Close();
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsTrue(dialog.Visible, "Closing must await services even if they ignore cancellation temporarily.");
        release.SetResult(); await dialing;
        await WaitForDialStateAsync(() => dialog.IsDisposed);
        Assert.IsNull(dialog.CallId);
        Assert.AreEqual(0, telephony.Subscribers);
    });

    [TestMethod]
    public Task DialDialogRendersEmptyAndFilledAtSmallWidthLargeFontAndScaledLayouts() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        var output = Path.GetFullPath(".artifacts/phase8-dial/render"); Directory.CreateDirectory(output);
        try
        {
            foreach (var (width, fontSize, scale, skin, palette) in new[] { (560, 8F, 1F, oldSkin, oldPalette), (420, 10F, 1F, oldSkin, oldPalette),
                (540, 16F, 1F, "Office 2019 Black", ""), (420, 10F, 1.5F, oldSkin, oldPalette), (560, 8F, 1.25F, "Basic", "Blue Dark") })
            foreach (var populated in new[] { false, true })
            {
                if (!string.IsNullOrEmpty(palette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin, palette);
                else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Tahoma", fontSize); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                var directory = new TestDirectory(new("test", "Pro"));
                if (!populated) directory.ReadCatalog = _ => Task.FromResult(ImmutableArray<Company>.Empty);
                var telephony = new DialTelephony();
                using var dialog = new FrmDial(directory, telephony); dialog.PrepareForBackgroundRun();
                dialog.Font = font; dialog.ClientSize = new(width, 220); dialog.Show(); await dialog.ReloadAsync();
                Assert.IsFalse(Field<LabelControl>(dialog, "statusLabel").Text.Contains("Bitte warten"));
                if (!populated) Assert.AreEqual("Keine berechtigten Mandanten verfügbar.", Field<LabelControl>(dialog, "statusLabel").Text);
                if (populated)
                {
                    Field<LookUpEdit>(dialog, "companyEdit").EditValue = (await directory.GetCompaniesAsync())[1].Key;
                    Field<TextEdit>(dialog, "destinationEdit").Text = "+49301234567";
                    telephony.Register(RegistrationState.Reconnecting);
                    await WaitForDialStateAsync(() => Field<LabelControl>(dialog, "statusLabel").Text.Contains("unterbrochen"));
                }
                if (scale != 1F) dialog.Scale(new SizeF(scale, scale)); dialog.PerformLayout(); Application.DoEvents();
                using var shot = UiAudit.Capture(dialog); shot.Save(Path.Combine(output, $"dial-{width}-{fontSize:0}-{scale:0.0}-{skin}-{palette}-{populated}.png"), ImageFormat.Png);
                CheckTableBounds(dialog);
                Assert.AreEqual(Field<SimpleButton>(dialog, "dialButton").Size, Field<SimpleButton>(dialog, "cancelButton").Size);
                Assert.IsTrue(Field<TableLayoutPanel>(dialog, "layout").Bottom <= dialog.ClientSize.Height,
                    $"{width}/{fontSize}/{scale}/{populated}: layout={Field<TableLayoutPanel>(dialog, "layout").Bounds}, client={dialog.ClientSize}, minimum={dialog.MinimumSize}");
                await dialog.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin, oldPalette);
        }
    });

    private static async Task WaitForDialStateAsync(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!ready() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsTrue(ready(), "The dial dialog did not finish its UI update.");
    }

    private sealed class DialTelephony : ITelephonyService
    {
        public RegistrationSnapshot Registration { get; private set; } = new(RegistrationState.Registered, DateTimeOffset.UtcNow);
        private EventHandler<RegistrationChangedEventArgs>? _registrations;
        private EventHandler<CallChangedEventArgs>? _calls;
        private ImmutableArray<CallSnapshot> _active = [];
        internal int Subscribers => (_registrations?.GetInvocationList().Length ?? 0) + (_calls?.GetInvocationList().Length ?? 0);
        internal int Attempts;
        internal CompanyKey? Company;
        internal string? Destination;
        internal Guid CallId = Guid.NewGuid();
        internal Exception? Failure;
        internal Func<CancellationToken, Task>? BeforeDial;
        public event EventHandler<RegistrationChangedEventArgs>? RegistrationChanged { add => _registrations += value; remove => _registrations -= value; }
        public event EventHandler<CallChangedEventArgs>? CallChanged { add => _calls += value; remove => _calls -= value; }
        public ImmutableArray<CallSnapshot> GetCalls() => _active;
        internal void Register(RegistrationState state) { Registration = new(state, DateTimeOffset.UtcNow); _registrations?.Invoke(this, new(Registration)); }
        internal void SetActiveCall(bool active)
        {
            var now = DateTimeOffset.UtcNow;
            var call = new CallSnapshot { Id = CallId, Revision = 1, Direction = CallDirection.Outgoing,
                State = active ? CallState.Connected : CallState.Ended, AnsweredAtUtc = now, EndedAtUtc = active ? null : now,
                EndReason = active ? null : CallEndReason.Completed, LocalAddress = new(NumberPresentation.Unknown),
                RemoteAddress = new(NumberPresentation.Withheld), StartedAtUtc = now, StateChangedAtUtc = now };
            _active = active ? [call] : [];
            _calls?.Invoke(this, new(call));
        }
        public async Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken cancellationToken = default)
        {
            Attempts++; Company = company; Destination = destination;
            if (BeforeDial is not null) await BeforeDial(cancellationToken);
            if (Failure is not null) throw Failure;
            return CallId;
        }
        public Task AnswerAsync(Guid callId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RejectAsync(Guid callId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task HangUpAsync(Guid callId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task HoldAsync(Guid callId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ResumeAsync(Guid callId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task TransferAsync(Guid callId, string destination, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendDtmfAsync(Guid callId, char digit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [TestMethod]
    public Task PhoneMaintenanceIsRegisteredOnlyForInteractiveApplicationStarts() => RunStaAsync(async () =>
    {
        using var context = new WindowsFormsSynchronizationContext();
        using var exceptions = new AppExceptionBoundary(new MaintenanceCrashReporter());
        foreach (var suppress in new[] { false, true })
        {
            var host = AppCompositionRoot.Build(context, exceptions, new StartupExecutionMode(suppress), new MaintenanceInstanceGuard());
            try { Assert.AreEqual(!suppress, host.Services.GetService<PhoneSearchMaintenance>() is not null); }
            finally { await ((IAsyncDisposable)host).DisposeAsync(); }
        }
    });
    private sealed class MaintenanceCrashReporter : ICrashReporter
    { public CrashReceipt Capture(Exception exception, CrashOrigin origin) => throw new AssertFailedException("Composition must not run background work."); }
    private sealed class MaintenanceInstanceGuard : IAppInstanceGuard
    {
        public bool TryAcquire() => true;
        public void RequestExistingInstanceActivation() { }
        public bool TryConsumeActivation() => false;
        public event EventHandler? ActivationRequested { add { } remove { } }
        public void Dispose() { }
    }

    [TestMethod]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public Task DirectXChangeAsksOnlyAfterSuccessfulSaveAndCanRestartInEitherDirection(bool initial, bool changed) => WithWorkspace(async (workspace, services, plugin) =>
    {
        var snapshot = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(snapshot.Options with { User = snapshot.Options.User with { UseDirectXRendering = initial } }, snapshot);
        await workspace.Configuration.StartAsync(CancellationToken.None);
        using var dialog = new FrmSettings(workspace);
        var page = Field<SettingsPage>(dialog, "settingsPage");
        var editor = Field<CheckEdit>(page, "directXEdit"); Assert.AreEqual(initial, editor.Checked);
        editor.Checked = changed; var prompts = 0;
        await Field<PageOperation>(page, "_operation").RunAsync(token => page.SaveAndOfferRestartAsync(() =>
        {
            prompts++; Assert.AreEqual(changed, workspace.Settings.LoadAsync().GetAwaiter().GetResult().Options.User.UseDirectXRendering);
            return true;
        }, token));
        Assert.AreEqual(1, prompts); Assert.IsTrue(dialog.RestartRequested);
        Assert.AreEqual(initial, workspace.Configuration.Current.Options.User.UseDirectXRendering, "Running renderer preference must remain the startup snapshot.");
        await dialog.StopAsync();
    });
    [TestMethod]
    public Task DirectXRestartCanBeDeclinedAndUnchangedOrFailedSavesDoNotAsk() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var baseline = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(baseline.Options with { User = baseline.Options.User with { UseDirectXRendering = false } }, baseline);
        await workspace.Configuration.StartAsync(CancellationToken.None);
        using var page = new SettingsPage(); page.Bind(workspace); var prompts = 0; var restarts = 0;
        page.RestartRequested += (_, _) => restarts++;
        var editor = Field<CheckEdit>(page, "directXEdit");
        await page.SaveAndOfferRestartAsync(() => { prompts++; return true; }, CancellationToken.None);
        Assert.AreEqual(0, prompts);
        editor.Checked = true;
        await page.SaveAndOfferRestartAsync(() => { prompts++; return false; }, CancellationToken.None);
        Assert.AreEqual(1, prompts); Assert.AreEqual(0, restarts); Assert.IsTrue((await workspace.Settings.LoadAsync()).Options.User.UseDirectXRendering);
        await page.SaveAndOfferRestartAsync(() => { prompts++; return true; }, CancellationToken.None);
        Assert.AreEqual(1, prompts, "Repeated save of an unchanged preference must not ask again.");
        editor.Checked = false;
        await page.SaveAndOfferRestartAsync(() => { prompts++; return true; }, CancellationToken.None);
        Assert.AreEqual(1, prompts, "Returning to the active renderer does not need a restart.");
        var snapshot = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(snapshot.Options with { User = snapshot.Options.User with { StartMinimized = !snapshot.Options.User.StartMinimized } }, snapshot);
        editor.Checked = true;
        await Assert.ThrowsExactlyAsync<ConfigurationException>(() => page.SaveAndOfferRestartAsync(() => { prompts++; return true; }, CancellationToken.None));
        Assert.AreEqual(1, prompts); Assert.AreEqual(0, restarts);
        await page.StopAsync();
    });
    [TestMethod]
    public void DirectXStartupAndRestartArgumentsUseOnlyTheSavedPreferenceAndSameApplication()
    {
        var calls = 0;
        AppUiStartup.ApplyRenderingPreference(false, () => calls++); Assert.AreEqual(0, calls);
        AppUiStartup.ApplyRenderingPreference(true, () => calls++); Assert.AreEqual(1, calls);
        var app = AppRestartLauncher.CreateStartInfo(@"D:\Program Files\WawiCallPro\WawiCallPro.App.FinOffice.exe", @"D:\Program Files\WawiCallPro\WawiCallPro.App.FinOffice.dll");
        Assert.AreEqual(@"D:\Program Files\WawiCallPro\WawiCallPro.App.FinOffice.exe", app.FileName);
        Assert.AreEqual(0, app.ArgumentList.Count); Assert.IsFalse(app.UseShellExecute);
        var dotnet = AppRestartLauncher.CreateStartInfo(@"C:\Program Files\dotnet\dotnet.exe", @"D:\App\WawiCallPro.App.Pro.dll");
        Assert.AreEqual(@"D:\App\WawiCallPro.App.Pro.dll", dotnet.ArgumentList.Single());
    }
    [TestMethod]
    public Task DirectXCheckboxRendersInGeneralSettingsWithoutVisibleWindows() => WithWorkspace(async (workspace, services, plugin) =>
    {
        Assert.IsTrue(workspace.Configuration.Current.Options.User.UseDirectXRendering);
        AppUiStartup.Apply();
        var output = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(RenderOutput()))!, "directx-settings", workspace.Application.ProductLine); Directory.CreateDirectory(output);
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        try
        {
            foreach (var (width, height, fontSize, scale, skin) in new[] { (980, 760, 10F, 1F, "Basic"), (680, 580, 16F, 1F, "Basic"),
                (980, 760, 10F, 1.5F, "Basic"), (680, 580, 16F, 1F, "Office 2019 Black") })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var settings = new FrmSettings(workspace); settings.PrepareForBackgroundRun(); settings.Font = font; settings.ClientSize = new(width, height);
                settings.Show(); Application.DoEvents();
                settings.MinimumSize = Size.Empty; settings.ClientSize = new(width, height);
                var page = Field<SettingsPage>(settings, "settingsPage"); Field<CheckEdit>(page, "directXEdit").Checked = true;
                if (scale != 1F) settings.Scale(new SizeF(scale, scale)); settings.PerformLayout(); Application.DoEvents();
                using var shot = UiAudit.Capture(settings); shot.Save(Path.Combine(output, $"general-{width}-{fontSize:0}-{scale:0.0}-{skin}.png"), ImageFormat.Png);
                CheckTableBounds(settings);
                Assert.IsTrue(Field<CheckEdit>(page, "directXEdit").Visible);
                Assert.IsTrue(Field<LabelControl>(page, "generalHint").Bottom <= Field<TableLayoutPanel>(page, "generalLayout").ClientSize.Height,
                    "The general settings hint must fit without a scrollbar.");
                await settings.StopAsync();
            }
        }
        finally { DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin); WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance; }
    });

    [TestMethod]
    public Task NumberDiagnosticsSearchesUnloadedRowsAndKeepsSourcesCompanyBound() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var source = new DiagnosticSource();
        using var page = new PhoneNumberDiagnosticsPage(); page.Bind(source, services.GetRequiredService<AppUiState>());
        CreateHandles(page);
        await DiagnosticActionAsync(page, "LoadCompanies_Click");
        var lookup = Field<LookUpEdit>(page, "companyEdit"); lookup.EditValue = source.A.Key; await page.PendingRequests;
        var view = Field<DevExpress.XtraGrid.Views.Grid.GridView>(page, "view");
        Assert.AreEqual(100, view.DataRowCount);
        await DiagnosticActionAsync(page, "Next_Click"); Assert.AreEqual(21, view.DataRowCount);
        view.ApplyFindFilter("owner-needle"); await page.PendingRequests;
        Assert.AreEqual("owner-needle", source.LastSearch); Assert.AreEqual(1, view.DataRowCount);
        Assert.Contains("owner-needle", Field<MemoEdit>(page, "detailsEdit").Text);
        Assert.Contains("SourceRecord", Field<MemoEdit>(page, "detailsEdit").Text);
        Assert.IsNull(workspace.Companies.Current.Company);
        lookup.EditValue = source.B.Key; await page.PendingRequests;
        Assert.AreEqual(0, view.DataRowCount); Assert.DoesNotContain("SourceRecord", Field<MemoEdit>(page, "detailsEdit").Text);
        await page.StopAsync();
    });
    [TestMethod]
    public Task NumberDiagnosticsRejectLateCompanyResponsesAndShutdownWaitsForIgnoringSources() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var source = new DiagnosticSource(); using var page = new PhoneNumberDiagnosticsPage(); page.Bind(source, null);
        CreateHandles(page); await DiagnosticActionAsync(page, "LoadCompanies_Click");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.BeforeRead = async company => { if (company == source.A.Key) { entered.TrySetResult(); await release.Task; } };
        var lookup = Field<LookUpEdit>(page, "companyEdit"); lookup.EditValue = source.A.Key; await entered.Task;
        lookup.EditValue = source.B.Key;
        await Task.Delay(20); release.SetResult(); await page.PendingRequests;
        var view = Field<DevExpress.XtraGrid.Views.Grid.GridView>(page, "view"); Assert.AreEqual(0, view.DataRowCount);
        entered = new(TaskCreationOptions.RunContinuationsAsynchronously); release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lookup.EditValue = source.A.Key; await entered.Task;
        var stop = page.StopAsync(); Assert.IsFalse(stop.IsCompleted);
        release.SetResult(); await stop; Assert.AreEqual(0, view.DataRowCount);
    });
    [TestMethod]
    public Task NumberDiagnosticsRendersInSettingsWithSourcesEmptyRowsSmallWindowsAndBothSkins() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var output = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(RenderOutput()))!, "phase6-number-diagnostics", "render"); Directory.CreateDirectory(output);
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        try
        {
            foreach (var (width, height, fontSize, scale, skin) in new[] { (900, 650, 10F, 1F, "Basic"), (640, 480, 10F, 1F, "Basic"),
                (900, 650, 16F, 1F, "Basic"), (900, 650, 10F, 1.5F, "Basic"), (900, 650, 16F, 1F, "Office 2019 Black") })
            foreach (var loaded in new[] { false, true })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var host = new FrmCallerLookup(); host.PrepareForBackgroundRun();
                foreach (Control child in host.Controls.Cast<Control>().ToArray()) child.Dispose(); host.Controls.Clear();
                host.Font = font; host.ClientSize = new(width, height);
                var source = new DiagnosticSource(); using var page = new PhoneNumberDiagnosticsPage { Dock = DockStyle.Fill }; page.Bind(source, null);
                host.Controls.Add(page); host.Show(); Application.DoEvents();
                await DiagnosticActionAsync(page, "LoadCompanies_Click");
                Field<LookUpEdit>(page, "companyEdit").EditValue = loaded ? source.A.Key : source.B.Key; await page.PendingRequests;
                if (scale != 1F) host.Scale(new SizeF(scale, scale)); host.PerformLayout(); Application.DoEvents(); CheckTableBounds(host);
                Assert.IsTrue(Field<GridControl>(page, "grid").Height > 60); Assert.IsTrue(Field<MemoEdit>(page, "detailsEdit").Height > 40);
                using var shot = UiAudit.Capture(host); shot.Save(Path.Combine(output, $"diagnostics-{width}-{fontSize:0}-{scale:0.0}-{skin}-{loaded}.png"), ImageFormat.Png);
                await page.StopAsync();
            }
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            using var settings = new FrmSettings(workspace); settings.PrepareForBackgroundRun(); settings.Font = oldFont;
            var settingsPage = Field<SettingsPage>(settings, "settingsPage");
            settings.SettingsTabs.SelectedTabPage = Field<XtraTabPage>(settingsPage, "cacheTab");
            Field<XtraTabControl>(settingsPage, "cachePages").SelectedTabPage = Field<XtraTabPage>(settingsPage, "diagnosticsTab");
            settings.Show(); settings.PerformLayout(); Application.DoEvents(); CheckTableBounds(settings);
            using var parentShot = UiAudit.Capture(settings); parentShot.Save(Path.Combine(output, "settings-diagnostics.png"), ImageFormat.Png);
            await settings.StopAsync();
        }
        finally { DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin); WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance; }
    });
    private static async Task DiagnosticActionAsync(PhoneNumberDiagnosticsPage page, string method)
    {
        page.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(page, [null, EventArgs.Empty]);
        await page.PendingRequests;
    }
    private sealed class DiagnosticSource : IPhoneNumberDiagnostics
    {
        internal Company A { get; } = new(new(LexwareProductLine.Pro, "installation-A", "01"), "Diagnosefirma A mit längerem Namen Ä");
        internal Company B { get; } = new(new(LexwareProductLine.Pro, "installation-B", "01"), "Diagnosefirma B");
        internal string? LastSearch;
        internal Func<CompanyKey, Task>? BeforeRead;
        public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default) => Task.FromResult<ImmutableArray<Company>>([A, B]);
        public async Task<Page<PhoneNumberDiagnostic>> GetPageAsync(CompanyKey company, PageRequest page, string? search = null, CancellationToken cancellationToken = default)
        {
            LastSearch = search; if (BeforeRead is not null) await BeforeRead(company);
            var matches = company == B.Key ? [] : Enumerable.Range(0, 121).Select(index => new PhoneNumberDiagnostic(Guid.NewGuid(),
                new(A.Key, PartyKind.SupplierContact, "contact-" + index, index == 120 ? "owner-needle" : "owner-01"),
                new("SourceRecord", "AddressRecord-" + index, "Telephone"), PhoneKind.Telephone, "Nicht auswertbarer Quellwert Ä · " + index,
                "number.not-a-number", "Der Quellwert enthält keine auswertbare Rufnummer.", DateTimeOffset.UtcNow.AddSeconds(-index)))
                .Where(item => string.IsNullOrEmpty(search) || item.Party.OwnerExternalId!.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
            return new(matches.Skip(page.Offset).Take(page.Limit).ToImmutableArray(), matches.Length > page.Offset + page.Limit);
        }
    }

    [TestMethod]
    public Task SupersededSearchResultCannotAssignACallOrStartDetailLoads() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        var call = await CreateWorkspaceCallAsync(services);
        using var page = new CallDetailsPage(); page.Bind(workspace, call);
        var decision = await BuildWorkspaceDecisionAsync(workspace);
        await workspace.PhoneSearch.RefreshAsync();
        await page.AcceptAsync(workspace.CallerDecisions.Choose(decision, decision.Groups[0].Candidates.First().Party), CancellationToken.None);
        Assert.IsNull(page.Context); Assert.AreEqual(0, plugin.Created);
        Assert.IsNull((await workspace.History.GetCallAsync(call.Id))!.Lookup);
        Assert.Contains("Suchstand geändert", Field<LabelControl>(page, "statusLabel").Text);
        await page.StopAsync();
    });
    [TestMethod]
    public Task SipStateWriteRetriesWhenManualAssociationWinsItsFirstPersistenceVersion() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        var call = await CreateWorkspaceCallAsync(services);
        var decision = await BuildWorkspaceDecisionAsync(workspace);
        var selected = workspace.CallerDecisions.Choose(decision, decision.Groups[1].Candidates.First().Party);
        var racing = new AssociationDuringStateWrite(services.GetRequiredService<ICallRepository>(), workspace.CallAssociations, selected);
        var sink = new SipCallPersistence(racing, services.GetRequiredService<ICallOriginProvider>(), TimeProvider.System);
        var saved = await sink.SaveAsync(call with { Revision = 1, State = CallState.Ringing, StateChangedAtUtc = call.StateChangedAtUtc.AddSeconds(1) });
        Assert.AreEqual(2, racing.Writes); Assert.AreEqual(selected.Selection!.Party.Company, saved.Company);
        var history = (await workspace.History.GetCallAsync(call.Id))!;
        Assert.AreEqual("manual", history.Lookup!.SelectionReason); Assert.AreEqual(1, history.Events.Length);
    });

    [TestMethod]
    public Task WrongPartyDetailResponseIsRejectedWithoutOpeningPluginViews() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        using var page = new CallDetailsPage(); page.Bind(workspace, await CreateWorkspaceCallAsync(services));
        var decision = await BuildWorkspaceDecisionAsync(workspace);
        var wanted = decision.Groups[0].Candidates.First().Party;
        ((TestDirectory)workspace.Directory).ReadParty = (party, token) => Task.FromResult<Party?>(new()
        { Key = decision.Groups[1].Candidates.First().Party, DisplayName = "wrong-company-sentinel" });
        await page.AcceptAsync(workspace.CallerDecisions.Choose(decision, wanted), CancellationToken.None); await page.PendingLoads;
        Assert.DoesNotContain("wrong-company-sentinel", Field<LabelControl>(page, "detailsLabel").Text);
        Assert.AreEqual(0, plugin.Created); await page.StopAsync();
    });

    private sealed class AssociationDuringStateWrite(ICallRepository inner, ICallAssociationService association, CallerMatchDecision decision) : ICallRepository
    {
        internal int Writes;
        public Task<StoredCall?> GetAsync(Guid callId, CancellationToken cancellationToken = default) => inner.GetAsync(callId, cancellationToken);
        public Task<Page<CallSnapshot>> GetPageAsync(CallHistoryQuery query, CancellationToken cancellationToken = default) => inner.GetPageAsync(query, cancellationToken);
        public async Task<StoredCall> WriteAsync(CallWrite write, CancellationToken cancellationToken = default)
        {
            if (++Writes == 1) await association.SaveAsync(write.Call.Id, decision, cancellationToken);
            return await inner.WriteAsync(write, cancellationToken);
        }
    }
    [TestMethod]
    public Task CallEventRoutingKeepsNewerStatesAndDoesNotReplaceTheMainDocument() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
        var original = form.GetActiveDocumentView();
        var call = await CreateWorkspaceCallAsync(services);
        form.GetType().GetMethod("TelephonyCall_Changed", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [null, new CallChangedEventArgs(call)]);
        await Task.Delay(30); await form.PendingCallUpdates;
        Assert.AreSame(original, form.GetActiveDocumentView());
        var lookup = form.OwnedForms.OfType<FrmCallerLookup>().Single(lookup => lookup.CallId == call.Id);
        await form.ObserveCallAsync(call with { Revision = 2, State = CallState.Connected });
        await form.ObserveCallAsync(call with { Revision = 1, State = CallState.Ended });
        Assert.IsTrue(Field<SimpleButton>(lookup.Page, "searchButton").Enabled, "An old terminal event cannot stop the current call workspace.");
        await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task CallDetailsRenderEmptyLoadedAndPluginContextsWithoutVisibleWindows() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply(); await SeedCallerCachesAsync(workspace, services);
        var decision = await BuildWorkspaceDecisionAsync(workspace);
        var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var output = Path.GetFullPath(".artifacts/phase6-call-context/render"); Directory.CreateDirectory(output);
        try
        {
            foreach (var (width, height, fontSize, scale, skin) in new[] { (900, 650, 10F, 1F, oldSkin), (640, 480, 10F, 1F, oldSkin),
                (900, 650, 16F, 1F, oldSkin), (900, 650, 10F, 1.5F, oldSkin), (900, 650, 16F, 1F, "Office 2019 Black") })
            foreach (var loaded in new[] { false, true })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize); WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var host = new FrmCallerLookup(); host.PrepareForBackgroundRun();
                foreach (Control child in host.Controls.Cast<Control>().ToArray()) child.Dispose();
                host.Controls.Clear(); host.Font = font; host.ClientSize = new(width, height);
                using var page = new CallDetailsPage { Dock = DockStyle.Fill };
                page.Bind(workspace, await CreateWorkspaceCallAsync(services)); host.Controls.Add(page);
                host.Show(); Application.DoEvents();
                if (loaded) { await page.AcceptAsync(workspace.CallerDecisions.Choose(decision, decision.Groups[1].Candidates.First().Party), CancellationToken.None); await page.PendingLoads; }
                if (scale != 1F) host.Scale(new SizeF(scale, scale));
                host.PerformLayout(); Application.DoEvents();
                Assert.IsTrue(Field<XtraTabControl>(page, "tabs").Height > 80);
                Assert.IsTrue(Field<TableLayoutPanel>(page, "actions").Right <= page.Width);
                using var shot = UiAudit.Capture(host); shot.Save(Path.Combine(output, $"call-{width}-{fontSize:0}-{scale:0.0}-{skin}-{loaded}.png"), ImageFormat.Png);
                await page.StopAsync();
            }
        }
        finally { DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin); WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance; }
    });
    [TestMethod]
    public Task CallWorkspacesKeepMainEditsAndHaveIndependentPartyContextsAndHistory() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        var companies = await workspace.Directory.GetCompaniesAsync();
        await workspace.Companies.SelectAsync(companies[0]);
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
        await form.OpenPartyAsync(new(companies[0].Key, PartyKind.Customer, "01"));
        var original = plugin.CreatedControls.ToArray(); foreach (var control in original) control.Tag = true;
        var call = await CreateWorkspaceCallAsync(services);
        await form.ObserveCallAsync(call);
        var page = Field<Dictionary<Guid, (DevExpress.XtraBars.Docking2010.Views.BaseDocument Document, CallDetailsPage Page)>>(form, "_callPages")[call.Id].Page;
        var lookup = form.OwnedForms.OfType<FrmCallerLookup>().Single(lookup => lookup.CallId == call.Id);
        var decision = workspace.CallerDecisions.Choose(lookup.Page.Decision!, new(companies[1].Key, PartyKind.Customer, "01"));
        await page.AcceptAsync(decision, CancellationToken.None); await page.PendingLoads;
        Assert.AreEqual(call.Id, page.Context!.CallId); Assert.AreEqual(companies[1].Key, page.Context.Party.Company);
        Assert.AreEqual(companies[0].Key, workspace.Companies.Current.Company!.Key);
        Assert.IsTrue(original.All(control => !control.IsDisposed));
        Assert.AreEqual(companies[1].Key, (await workspace.History.GetCallAsync(call.Id))!.Call.Company);
        Assert.ThrowsExactly<UnsavedPluginChangesException>(form.EnsureViewsCanClose);
        foreach (var control in original) control.Tag = false;
        var second = await CreateWorkspaceCallAsync(services); await form.ObserveCallAsync(second);
        var other = Field<Dictionary<Guid, (DevExpress.XtraBars.Docking2010.Views.BaseDocument Document, CallDetailsPage Page)>>(form, "_callPages")[second.Id].Page;
        var otherDecision = form.OwnedForms.OfType<FrmCallerLookup>().Single(lookup => lookup.CallId == second.Id).Page.Decision!;
        await other.AcceptAsync(workspace.CallerDecisions.Choose(otherDecision, new(companies[0].Key, PartyKind.Customer, "01")), CancellationToken.None);
        await other.PendingLoads;
        Assert.AreNotEqual(page.Context.CallId, other.Context!.CallId); Assert.AreNotEqual(page.Context.Party.Company, other.Context.Party.Company);
        plugin.CreatedControls.Except(original).First(control => !control.IsDisposed).Tag = true;
        Assert.ThrowsExactly<UnsavedPluginChangesException>(form.EnsureViewsCanClose);
        foreach (var control in plugin.CreatedControls.Where(control => !control.IsDisposed)) control.Tag = false;
        await form.PrepareToCloseAsync();
        Assert.AreEqual(plugin.Created, plugin.Disposed);
    });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task LateCallDetailsCannotPublishAfterSelectionChangeOrCallEnd(bool endCall) => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        var call = await CreateWorkspaceCallAsync(services);
        using var page = new CallDetailsPage(); page.Bind(workspace, call);
        using var shell = new Form { Opacity = 0, ShowInTaskbar = false };
        shell.Controls.Add(page); page.Dock = DockStyle.Fill;
        var result = await BuildWorkspaceDecisionAsync(workspace);
        var first = result.Groups[0].Candidates.First().Party;
        var second = result.Groups[1].Candidates.First().Party;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ((TestDirectory)workspace.Directory).ReadParty = async (party, token) =>
        { if (party == first) { entered.TrySetResult(); await release.Task; } return new() { Key = party, DisplayName = party == first ? "Late A" : "Current B" }; };
        await page.AcceptAsync(workspace.CallerDecisions.Choose(result, first), CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (endCall) await page.ObserveAsync(call with { Revision = 1, State = CallState.Ended });
        else await page.AcceptAsync(workspace.CallerDecisions.Choose(result, second), CancellationToken.None);
        release.SetResult(); await page.PendingLoads;
        Assert.DoesNotContain("Late A", Field<LabelControl>(page, "detailsLabel").Text);
        if (!endCall) { Assert.Contains("Current B", Field<LabelControl>(page, "detailsLabel").Text); Assert.AreEqual(second, page.Context!.Party); }
        else Assert.AreEqual(0, plugin.Created);
        await page.StopAsync();
    });

    [TestMethod]
    public Task LateAsyncPluginViewIsDisposedAndDirtyCallTabsBlockClosing() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        var call = await CreateWorkspaceCallAsync(services);
        var decision = await BuildWorkspaceDecisionAsync(workspace);
        var first = decision.Groups[0].Candidates.First().Party;
        var next = decision.Groups[1].Candidates.First().Party;
        using var page = new CallDetailsPage(); page.Bind(workspace, call);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        plugin.CreateAsync = async (id, party, token) =>
        { if (party == first) { entered.TrySetResult(); await release.Task; } return plugin.CreateView(id, party); };
        await page.AcceptAsync(workspace.CallerDecisions.Choose(decision, first), CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await page.AcceptAsync(workspace.CallerDecisions.Choose(decision, next), CancellationToken.None);
        release.SetResult(); await page.PendingLoads;
        Assert.AreEqual(1, plugin.Disposed); Assert.AreEqual(3, plugin.Created);
        var tabs = Field<XtraTabControl>(page, "tabs");
        Assert.HasCount(2, tabs.TabPages.Cast<XtraTabPage>().Where(tab => tab.Tag is CallViewContext));
        foreach (XtraTabPage tab in tabs.TabPages) if (tab.Tag is CallViewContext context) Assert.AreEqual(next, context.Party);
        plugin.CreatedControls.First(control => !control.IsDisposed).Tag = true;
        Assert.IsTrue(page.HasUnsavedChanges);
        foreach (var control in plugin.CreatedControls.Where(control => !control.IsDisposed)) control.Tag = false;
        await page.StopAsync(); Assert.AreEqual(plugin.Created, plugin.Disposed);
    });

    [TestMethod]
    public Task SipStatePersistenceKeepsAssignedCompanyAndManualEvidenceAfterFurtherTransitions() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        var call = await CreateWorkspaceCallAsync(services);
        var decision = await BuildWorkspaceDecisionAsync(workspace);
        var selected = workspace.CallerDecisions.Choose(decision, decision.Groups[1].Candidates.First().Party);
        await workspace.CallAssociations.SaveAsync(call.Id, selected);
        var saved = await services.GetRequiredService<ISipCallPersistence>().SaveAsync(call with { Revision = 1, State = CallState.Ringing, StateChangedAtUtc = call.StateChangedAtUtc.AddSeconds(1) });
        Assert.AreEqual(selected.Selection!.Party.Company, saved.Company);
        var history = (await workspace.History.GetCallAsync(call.Id))!;
        Assert.AreEqual(selected.Selection.Party, history.Parties.Single(party => party.IsSelected).Party);
        Assert.AreEqual("manual", history.Lookup!.SelectionReason);
    });

    private static async Task<CallerMatchDecision> BuildWorkspaceDecisionAsync(AppWorkspace workspace)
    {
        await workspace.PhoneSearch.RefreshAsync();
        return workspace.CallerDecisions.Evaluate(workspace.PhoneSearch.Find(workspace.PhoneInterpreter.InterpretIncoming(new(NumberPresentation.Available, "+49301234567"))), new());
    }
    private static Task<CallSnapshot> CreateWorkspaceCallAsync(IServiceProvider services)
    {
        var now = DateTimeOffset.UtcNow;
        return services.GetRequiredService<ICallJournal>().BeginAsync(new() { Id = Guid.NewGuid(), Revision = 0, State = CallState.Created,
            Direction = CallDirection.Incoming, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "+49301234567"),
            StartedAtUtc = now, StateChangedAtUtc = now });
    }
    [TestMethod]
    public Task ClosingCallerLookupWaitsForItsPendingSourceAndDiscardsLateResults() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        using var form = new FrmCallerLookup(workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ((TestDirectory)workspace.Directory).ReadCatalog = async token => { entered.TrySetResult(); await release.Task; return []; };
        Field<TextEdit>(form.Page, "numberEdit").Text = "+49301234567";
        form.Page.GetType().GetMethod("Search_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form.Page, [null, EventArgs.Empty]);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        form.Close();
        Assert.IsFalse(form.IsDisposed, "Closing must await a source that has not returned yet.");
        release.SetResult();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!form.IsDisposed && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsTrue(form.IsDisposed);
        Assert.IsNull(form.Page.Decision);
    });
    [TestMethod]
    public Task CallerLineMappingIsConfiguredOnlyInSettingsAndUsedByActualLookup() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        var companies = await workspace.Directory.GetCompaniesAsync();
        using (var settings = new FrmSettings(workspace))
        {
            settings.PrepareForBackgroundRun(); settings.Show(); Application.DoEvents();
            var page = Field<SettingsPage>(settings, "settingsPage");
            await page.ReloadAsync();
            Field<TextEdit>(page, "serverEdit").Text = "fixture.invalid";
            Field<TextEdit>(page, "usernameEdit").Text = "test-account";
            Field<TextEdit>(page, "passwordEdit").Text = "synthetic-password";
            await InvokeActionAsync(page, "LoadLineCompanies_Click");
            Field<LookUpEdit>(page, "lineCompanyEdit").EditValue = companies[1].Key;
            await InvokeActionAsync(page, "ConfirmLine_Click");
            await InvokeActionAsync(page, "Save_Click");
            var saved = (await workspace.Settings.LoadAsync()).Options;
            Assert.IsTrue(CallerLineConfiguration.Resolve(saved)!.IsVerified);
            Assert.AreEqual(companies[1].Key, saved.CallerLineAssignment!.Company);
            Assert.IsTrue(saved.Companies.All(company => company.IncludeInPhoneCache));
            await settings.StopAsync();
        }
        var cache = services.GetRequiredService<IPartyCacheRepository>();
        var customer = await cache.GetAsync(new(companies[1].Key, PartyKind.Customer, "01"));
        var state = await cache.GetStateAsync(companies[1].Key);
        await cache.ReplaceAsync(new(companies[1].Key, state.Revision, [customer!], DateTimeOffset.UtcNow, state.ParserVersion, state.ContextVersion));
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents(); form.Navigate("caller");
        var lookup = form.OwnedForms.OfType<FrmCallerLookup>().Single().Page;
        Field<TextEdit>(lookup, "numberEdit").Text = "+49301234567";
        await InvokeActionAsync(lookup, "Search_Click");
        Assert.AreEqual(CallerMatchDecisionKind.LineUnique, lookup.Decision!.Kind);
        Assert.AreEqual(companies[1].Key, lookup.Decision.Selection!.Party.Company);
        Assert.HasCount(4, lookup.Decision.Groups.SelectMany(group => group.Candidates));
        Assert.Contains("Leitung", Field<LabelControl>(lookup, "statusLabel").Text);
        Assert.AreEqual(0, plugin.Created);
        await form.PrepareToCloseAsync();
    });
    [TestMethod]
    public Task CallerLookupGroupsRealCachesAndPreservesManualChoiceWithoutSwitchingViews() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        var companies = await workspace.Directory.GetCompaniesAsync();
        await workspace.Companies.SelectAsync(companies[0]);
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
        form.Navigate("caller");
        var page = form.OwnedForms.OfType<FrmCallerLookup>().Single().Page;
        Field<TextEdit>(page, "numberEdit").Text = "+49301234567";
        await InvokeActionAsync(page, "Search_Click");
        Assert.AreEqual(CallerMatchDecisionKind.Ambiguous, page.Decision!.Kind);
        Assert.HasCount(2, page.Decision.Groups);
        var view = Field<DevExpress.XtraGrid.Views.Grid.GridView>(page, "view");
        Assert.AreEqual(6, view.DataRowCount); Assert.AreEqual(1, view.GroupCount);
        var row = view.LocateByValue("Name", "Firma B Kontakt");
        Assert.IsTrue(row >= 0); view.FocusedRowHandle = row;
        Assert.IsTrue(Field<SimpleButton>(page, "chooseButton").Enabled);
        Assert.AreEqual("Firma B Lieferant (200)", view.GetRowCellValue(row, "Organisation"));
        Field<SimpleButton>(page, "chooseButton").PerformClick();
        Assert.AreEqual(PartyKind.SupplierContact, page.Decision.Selection!.Party.Kind);
        Assert.AreEqual(companies[1].Key, page.Decision.Selection.Party.Company);
        Assert.AreEqual(CallerMatchSelectionKind.Manual, page.Decision.Selection.Kind);
        Assert.AreEqual(companies[0].Key, workspace.Companies.Current.Company!.Key);
        Assert.AreEqual(0, plugin.Created, "A detached choice does not replace open plugin views.");
        await InvokeActionAsync(page, "Search_Click");
        Assert.AreEqual(CallerMatchSelectionKind.Manual, page.Decision!.Selection!.Kind);
        Assert.AreEqual(companies[1].Key, page.Decision.Selection.Party.Company);
        await workspace.PhoneSearch.RefreshAsync();
        view.FocusedRowHandle = view.LocateByValue("Name", "Firma B Kontakt");
        Field<SimpleButton>(page, "chooseButton").PerformClick();
        Assert.Contains("Suchstand geändert", Field<LabelControl>(page, "statusLabel").Text);
        Assert.IsFalse(Field<SimpleButton>(page, "chooseButton").Enabled);
        await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task CallerLookupRejectsInvalidInputAndCancelsItsActualPendingIndexRefresh() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services);
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents(); form.Navigate("caller");
        var page = form.OwnedForms.OfType<FrmCallerLookup>().Single().Page;
        var source = (TestDirectory)workspace.Directory;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.ReadCatalog = async token => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return []; };
        Field<TextEdit>(page, "numberEdit").Text = "0301234567";
        await InvokeActionAsync(page, "Search_Click");
        Assert.IsFalse(entered.Task.IsCompleted); Assert.IsNull(page.Decision);
        Field<TextEdit>(page, "numberEdit").Text = "+49301234567";
        page.GetType().GetMethod("Search_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [null, EventArgs.Empty]);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsFalse(Field<SimpleButton>(page, "searchButton").Enabled);
        Assert.IsTrue(Field<SimpleButton>(page, "cancelButton").Enabled);
        Field<SimpleButton>(page, "cancelButton").PerformClick();
        await WaitForOperationAsync(page);
        Assert.IsTrue(Field<SimpleButton>(page, "searchButton").Enabled);
        Assert.IsFalse(Field<SimpleButton>(page, "chooseButton").Enabled);
        Assert.IsNull(page.Decision);
        Assert.Contains("abgebrochen", Field<LabelControl>(page, "statusLabel").Text);
        await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task CallerLookupRendersOnlyItsChangedSurfaceWithEmptyFilledAndLineConflictStates() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        await SeedCallerCachesAsync(workspace, services);
        await workspace.PhoneSearch.RefreshAsync();
        var incoming = workspace.PhoneInterpreter.InterpretIncoming(new(NumberPresentation.Available, "+49301234567"));
        var result = workspace.PhoneSearch.Find(incoming);
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var originalSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var output = Path.GetFullPath(".artifacts/phase6-caller-choice/render");
        Directory.CreateDirectory(output);
        try
        {
            var variants = new[] { (900, 700, 10F, 1F, originalSkin), (720, 640, 10F, 1F, originalSkin),
                (900, 700, 16F, 1F, originalSkin), (900, 700, 10F, 1.5F, originalSkin), (900, 700, 16F, 1F, "Office 2019 Black") };
            foreach (var (width, height, fontSize, scale, skin) in variants)
            foreach (var filled in new[] { false, true })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new FrmCallerLookup(workspace, services.GetRequiredService<AppUiState>());
                form.PrepareForBackgroundRun(); form.Font = font; form.ClientSize = new(width, height);
                form.Show(); Application.DoEvents();
                var page = form.Page;
                Field<TextEdit>(page, "numberEdit").Text = "+49 30 1234567";
                var context = new CallerMatchContext(null, new(new("fixture", "line", "DE"), new(result.Index.Companies[0].Company.ProductLine, "unavailable", "1"), "Zentrale", true));
                page.Display(workspace.CallerDecisions.Evaluate(filled ? result : result with { Candidates = [] }, context));
                if (scale != 1F) form.Scale(new SizeF(scale, scale));
                form.PerformLayout(); Application.DoEvents();
                using var image = UiAudit.Capture(form);
                image.Save(Path.Combine(output, $"caller-{width}-{fontSize:0}-{scale:0.0}-{skin}-{filled}.png"), ImageFormat.Png);
                if (filled && width == 720)
                {
                    foreach (var tabName in new[] { "scopeTab", "detailsTab" })
                    {
                        Field<XtraTabControl>(page, "tabs").SelectedTabPage = Field<XtraTabPage>(page, tabName);
                        form.PerformLayout(); Application.DoEvents();
                        using var detailImage = UiAudit.Capture(form);
                        detailImage.Save(Path.Combine(output, "caller-" + tabName + ".png"), ImageFormat.Png);
                    }
                    Field<XtraTabControl>(page, "tabs").SelectedTabPage = Field<XtraTabPage>(page, "resultsTab");
                }
                var actions = Field<TableLayoutPanel>(page, "actions");
                Assert.IsTrue(actions.Right <= page.ClientSize.Width, "Footer buttons exceed the available width.");
                Assert.IsTrue(Field<DevExpress.XtraTab.XtraTabControl>(page, "tabs").Height > 80, $"Results need usable remaining height: {width}x{height}, font {fontSize}, scale {scale}, page {page.Size}, tabs {Field<DevExpress.XtraTab.XtraTabControl>(page, "tabs").Size}.");
                var buttons = new[] { "searchButton", "cancelButton", "chooseButton" }.Select(name => Field<SimpleButton>(page, name)).ToArray();
                Assert.AreEqual(1, buttons.Select(button => button.Size).Distinct().Count(), "Related actions need equal preferred sizes.");
                foreach (var button in buttons) Assert.IsTrue(button.Width >= button.GetPreferredSize(Size.Empty).Width);
                await form.StopAsync();
                if (!filled)
                {
                    using var settings = new FrmSettings(workspace);
                    settings.PrepareForBackgroundRun(); settings.Font = font; settings.MinimumSize = Size.Empty;
                    settings.ClientSize = new(width, height); settings.Show(); Application.DoEvents();
                    var settingsPage = Field<SettingsPage>(settings, "settingsPage");
                    settings.SettingsTabs.SelectedTabPage = Field<XtraTabPage>(settingsPage, "sipTab");
                    Field<XtraTabControl>(settingsPage, "sipPages").SelectedTabPage = Field<XtraTabPage>(settingsPage, "lineTab");
                    if (fontSize > 10F)
                    {
                        Field<TextEdit>(settingsPage, "serverEdit").Text = "fixture.invalid";
                        Field<TextEdit>(settingsPage, "usernameEdit").Text = "fixture-account";
                        await InvokeActionAsync(settingsPage, "LoadLineCompanies_Click");
                        Field<LookUpEdit>(settingsPage, "lineCompanyEdit").EditValue = result.Index.Companies[0].Company;
                        await InvokeActionAsync(settingsPage, "ConfirmLine_Click");
                    }
                    if (scale != 1F) settings.Scale(new SizeF(scale, scale));
                    settings.PerformLayout(); Application.DoEvents();
                    Assert.IsTrue(Field<TableLayoutPanel>(settingsPage, "lineButtons").Right <= Field<TableLayoutPanel>(settingsPage, "lineLayout").Width);
                    using var settingsImage = UiAudit.Capture(settings);
                    settingsImage.Save(Path.Combine(output, $"line-settings-{width}-{fontSize:0}-{scale:0.0}-{skin}.png"), ImageFormat.Png);
                    await settings.StopAsync();
                }
            }
        }
        finally
        {
            DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(originalSkin);
            WindowsFormsSettings.DefaultFont = originalFont; AppearanceObject.DefaultFont = originalAppearance;
        }
    });

    private static async Task SeedCallerCachesAsync(AppWorkspace workspace, IServiceProvider services)
    {
        await workspace.Database.CreateAndMigrateAsync();
        var companies = await workspace.Directory.GetCompaniesAsync();
        var settings = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(settings.Options with { Companies = companies.Select(company => new CompanySettings
        { Company = company.Key, DisplayName = company.DisplayName, IncludeInPhoneCache = true }).ToImmutableArray() }, settings);
        foreach (var company in companies)
        {
            await services.GetRequiredService<ICompanyRepository>().WriteAsync(new(company, null, DateTimeOffset.UtcNow));
            var customer = new Party { Key = new(company.Key, PartyKind.Customer, "01"), DisplayName = company.DisplayName + " Kunde", Number = "100",
                Phones = [new("+49301234567", PhoneKind.Telephone, new("fixture", "01", "phone"), "+49301234567")] };
            var supplier = customer with { Key = new(company.Key, PartyKind.Supplier, "02"), DisplayName = company.DisplayName + " Lieferant", Number = "200" };
            var contact = customer with { Key = new(company.Key, PartyKind.SupplierContact, "03", "02"), DisplayName = company.DisplayName + " Kontakt", Number = null };
            await services.GetRequiredService<IPartyCacheRepository>().ReplaceAsync(new(company.Key, 0, [customer, supplier, contact], DateTimeOffset.UtcNow,
                services.GetRequiredService<IPhoneCacheMetadata>().ParserVersion,
                services.GetRequiredService<IPhoneCacheMetadata>().GetContextVersion(new() { Company = company.Key })));
        }
    }
    [TestMethod]
    public Task MatchingRulesAreCompanyBoundVersionedAndRenderInSettingsWithoutVisibleWindows() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        await workspace.Database.CreateAndMigrateAsync();
        var companies = await workspace.Directory.GetCompaniesAsync();
        foreach (var company in companies)
            await services.GetRequiredService<ICompanyRepository>().WriteAsync(new(company, null, DateTimeOffset.UtcNow.AddMinutes(-1)));
        using (var form = new FrmSettings(workspace))
        {
            form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
            var page = Field<SettingsPage>(form, "settingsPage"); await page.ReloadAsync();
            await InvokeActionAsync(page, "LoadCompanies_Click");
            form.SettingsTabs.SelectedTabPage = Field<XtraTabPage>(page, "cacheTab");
            Field<XtraTabControl>(page, "cachePages").SelectedTabPage = Field<XtraTabPage>(page, "matchingTab");
            var companyEdit = Field<LookUpEdit>(page, "matchingCompaniesEdit");
            var enabled = Field<CheckEdit>(page, "matchingEnabledEdit");
            var width = Field<SpinEdit>(page, "matchingWidthEdit");
            for (var i = 0; i < 2; i++)
            {
                companyEdit.EditValue = companies[i].Key;
                Assert.IsFalse(Field<SimpleButton>(page, "saveMatchingRuleButton").Enabled);
                await InvokeActionAsync(page, "LoadMatchingRule_Click");
                Assert.IsFalse(enabled.Checked); Assert.AreEqual(5m, width.Value);
                enabled.Checked = true; width.Value = 4 + i;
                await InvokeActionAsync(page, "SaveMatchingRule_Click");
                var saved = await workspace.PhoneMatching.GetProfileAsync(companies[i].Key);
                Assert.IsTrue(saved.Settings.StemMatchingEnabled); Assert.AreEqual(4 + i, saved.Settings.StartWidth);
            }
            Assert.AreEqual(4, (await workspace.PhoneMatching.GetProfileAsync(companies[0].Key)).Settings.StartWidth);
            var current = await workspace.PhoneMatching.GetProfileAsync(companies[1].Key);
            await workspace.PhoneMatching.SaveProfileAsync(current.Settings with { StartWidth = 6 }, current.Version);
            width.Value = 7;
            await InvokeActionAsync(page, "SaveMatchingRule_Click");
            Assert.AreEqual(6, (await workspace.PhoneMatching.GetProfileAsync(companies[1].Key)).Settings.StartWidth);
            Assert.Contains("Regel laden", Field<LabelControl>(page, "statusLabel").Text);
            await InvokeActionAsync(page, "LoadMatchingRule_Click"); Assert.AreEqual(6m, width.Value);
            Assert.IsEmpty((await workspace.Settings.LoadAsync()).Options.Companies, "Saving a shared rule never changes the user's cache selection.");
            companyEdit.Properties.DataSource = new[] { new { Key = new CompanyKey(companies[0].Key.ProductLine, "not-cached", "01"), Mandant = "Noch nicht im Cache" } };
            companyEdit.EditValue = new CompanyKey(companies[0].Key.ProductLine, "not-cached", "01");
            await InvokeActionAsync(page, "LoadMatchingRule_Click");
            Assert.Contains("Cachequellen", Field<LabelControl>(page, "statusLabel").Text);
            Assert.IsFalse(Field<SimpleButton>(page, "saveMatchingRuleButton").Enabled);
            await form.StopAsync();
        }
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        try
        {
            foreach (var skin in new[] { "Basic", "Office 2019 Black" })
            foreach (var size in new[] { 10F, 16F })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new FrmSettings(workspace);
                form.PrepareForBackgroundRun(); form.MinimumSize = Size.Empty; form.ClientSize = new Size(760, 700);
                form.Show(); Application.DoEvents();
                var page = Field<SettingsPage>(form, "settingsPage"); await page.ReloadAsync();
                form.SettingsTabs.SelectedTabPage = Field<XtraTabPage>(page, "cacheTab");
                Field<XtraTabControl>(page, "cachePages").SelectedTabPage = Field<XtraTabPage>(page, "matchingTab");
                void Capture(string state)
                {
                    form.PerformLayout(); Application.DoEvents(); CheckTableBounds(form);
                    var matchingLayout = Field<TableLayoutPanel>(page, "matchingLayout");
                    var actions = Field<TableLayoutPanel>(page, "matchingButtons");
                    Assert.IsTrue(matchingLayout.ClientRectangle.Contains(actions.Bounds), "All rule actions must remain visible.");
                    var output = Path.Combine(RenderOutput(), "matching-rules"); Directory.CreateDirectory(output);
                    using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                    bitmap.Save(Path.Combine(output, $"Matching-{skin}-{size}-{state}.png"), ImageFormat.Png);
                }
                Capture("empty");
                await InvokeActionAsync(page, "LoadCompanies_Click");
                var lookup = Field<LookUpEdit>(page, "matchingCompaniesEdit");
                lookup.Properties.DataSource = new[] { new { Key = companies[1].Key, Mandant = "Musterunternehmen mit längerem Mandantennamen und zusätzlicher Installationsbezeichnung" } };
                lookup.EditValue = companies[1].Key;
                await InvokeActionAsync(page, "LoadMatchingRule_Click");
                Capture("populated");
                form.Scale(new SizeF(1.5F, 1.5F)); Capture("scale150");
                await form.StopAsync();
            }
        }
        finally { WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance; DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin); }
    });

    [TestMethod]
    public Task DatabaseSetupWizardCreatesOnlyOnExplicitSetupAndSavesOnFinish() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new FrmDatabaseSetup(workspace.DatabaseSetup, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
        await form.CurrentOperation;
        Field<SimpleButton>(form, "btnNext").PerformClick();
        Field<SimpleButton>(form, "btnTestConnection").PerformClick(); await form.CurrentOperation;
        Assert.IsFalse(File.Exists(Path.Combine(workspace.Paths.RootDirectory, "communications.db")));
        Assert.IsFalse((await workspace.Settings.LoadAsync()).FileFound);
        Field<SimpleButton>(form, "btnNext").PerformClick(); await form.CurrentOperation;
        Assert.IsTrue(File.Exists(Path.Combine(workspace.Paths.RootDirectory, "communications.db")));
        Assert.IsFalse((await workspace.Settings.LoadAsync()).FileFound, "Setting up a schema does not yet save settings.");
        Assert.IsTrue(Field<SimpleButton>(form, "btnFinish").Enabled);
        Field<SimpleButton>(form, "btnFinish").PerformClick(); await form.CurrentOperation;
        Assert.AreEqual(DialogResult.OK, form.DialogResult);
        Assert.AreEqual(Path.Combine(workspace.Paths.RootDirectory, "communications.db"), (await workspace.Settings.LoadAsync()).Options.Database.SqliteFileName);
    });

    [TestMethod]
    public Task ClosingDatabaseSetupCancelsAndAwaitsItsConnectionTest() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var blocker = new PendingDatabaseSetup(workspace.DatabaseSetup);
        using var form = new FrmDatabaseSetup(blocker, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents(); await form.CurrentOperation;
        Field<SimpleButton>(form, "btnNext").PerformClick();
        Field<SimpleButton>(form, "btnTestConnection").PerformClick();
        await blocker.Entered.Task;
        Assert.IsFalse(Field<SimpleButton>(form, "btnNext").Enabled);
        form.Close();
        await blocker.Cancelled.Task;
        Assert.IsFalse(form.IsDisposed, "The dialog must await the test before disposing its controls.");
        blocker.Release.TrySetResult();
        await form.CurrentOperation;
        await form.StopAsync();
        Application.DoEvents();
        Assert.IsTrue(form.IsDisposed);
    });

    [TestMethod]
    public Task DatabaseSetupControlsRenderAllProvidersAndKeepQuotedPasswordsOutOfPreviews() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        const string secret = "synthetic;quoted=\"password\"";
        try
        {
            foreach (var skin in new[] { "Basic", "Office 2019 Black" })
            foreach (var fontSize in new[] { 10F, 16F })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new FrmDatabaseSetup(workspace.DatabaseSetup, services.GetRequiredService<AppUiState>());
                form.PrepareForBackgroundRun(); form.MinimumSize = Size.Empty; form.ClientSize = new Size(960, 740);
                form.Show(); Application.DoEvents(); await form.CurrentOperation;
                void Capture(string name)
                {
                    form.PerformLayout(); Application.DoEvents(); CheckTableBounds(form);
                    var output = Path.Combine(RenderOutput(), "database-setup"); Directory.CreateDirectory(output);
                    using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                    bitmap.Save(Path.Combine(output, $"{skin}-{fontSize}-{name}.png"), ImageFormat.Png);
                }
                Capture("provider");
                var first = Field<UcDbStep1DbType>(form, "_ucStep1");
                var second = Field<UcDbStep2Connection>(form, "_ucStep2");
                foreach (var type in new[] { DatabaseType.Sqlite, DatabaseType.SqlServer, DatabaseType.MySql, DatabaseType.PostgreSql })
                {
                    first.SelectedDbType = type;
                    Field<SimpleButton>(form, "btnNext").PerformClick();
                    var connection = type switch
                    {
                        DatabaseType.SqlServer => new Microsoft.Data.SqlClient.SqlConnectionStringBuilder { DataSource = "fixture.invalid,1433", InitialCatalog = "WawiCallPro", UserID = "fixture", Password = secret }.ConnectionString,
                        DatabaseType.MySql => new MySqlConnector.MySqlConnectionStringBuilder { Server = "fixture.invalid", Database = "WawiCallPro", UserID = "fixture", Password = secret }.ConnectionString,
                        DatabaseType.PostgreSql => new Npgsql.NpgsqlConnectionStringBuilder { Host = "fixture.invalid", Database = "WawiCallPro", Username = "fixture", Password = secret }.ConnectionString,
                        _ => null
                    };
                    second.LoadDraft(new() { Type = type, SqlitePath = Path.Combine(workspace.Paths.RootDirectory, "communications.db"), ConnectionString = connection });
                    Assert.IsTrue(second.ValidateInputs());
                    if (connection is not null)
                    {
                        var rebuilt = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = second.ReadDraft().ConnectionString! };
                        Assert.AreEqual(secret, rebuilt["Password"]);
                    }
                    Assert.DoesNotContain("synthetic", Field<TextEdit>(second, "txtCsPreview").Text);
                    Capture(type + "-filled");
                    form.ClientSize = new Size(760, 560); Capture(type + "-small");
                    form.Scale(new SizeF(1.5F, 1.5F)); Capture(type + "-scale150");
                    form.Scale(new SizeF(2F / 3F, 2F / 3F)); form.ClientSize = new Size(960, 740);
                    var content = type switch { DatabaseType.Sqlite => (Control)Field<WawiCallPro.App.Views.DbConnection.UcConSqlite>(second, "ucSqlite"), DatabaseType.SqlServer => Field<WawiCallPro.App.Views.DbConnection.UcConSqlServer>(second, "ucSqlServer"), _ => Field<WawiCallPro.App.Views.DbConnection.UcConMyPg>(second, type == DatabaseType.PostgreSql ? "ucPostgres" : "ucMyPg") };
                    foreach (var edit in content.Controls.Find(type == DatabaseType.Sqlite ? "txtSqliteFile" : type == DatabaseType.SqlServer ? "txtSqlServer" : "txtHost", true).OfType<TextEdit>()) edit.Text = "";
                    Assert.IsFalse(second.ValidateInputs()); Capture(type + "-empty");
                    Field<SimpleButton>(form, "btnBack").PerformClick();
                }
                typeof(FrmDatabaseSetup).GetMethod("SelectStep", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [2]);
                var result = Field<UcDbStep3Migration>(form, "_ucStep3");
                result.ShowProgress(); Capture("progress");
                result.ShowResult(true, "Schema aktuell. Nach dem Übernehmen die Anwendung neu starten."); Capture("result");
                form.Scale(new SizeF(1.5F, 1.5F)); Capture("result-scale150");
                await form.StopAsync();
                using var settings = new FrmSettings(workspace);
                settings.PrepareForBackgroundRun(); settings.MinimumSize = Size.Empty; settings.ClientSize = new Size(760, 700);
                settings.Show(); Application.DoEvents();
                var page = Field<SettingsPage>(settings, "settingsPage"); await page.ReloadAsync();
                settings.SettingsTabs.SelectedTabPage = Field<XtraTabPage>(page, "databaseTab");
                settings.PerformLayout(); Application.DoEvents(); CheckTableBounds(settings);
                using var summaryImage = WawiCallPro.App.Diagnostics.UiAudit.Capture(settings);
                summaryImage.Save(Path.Combine(RenderOutput(), "database-setup", $"{skin}-{fontSize}-settings-summary.png"), ImageFormat.Png);
                await settings.StopAsync();
            }
        }
        finally { WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance; DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin); }
    });

    [TestMethod]
    public Task SipStandardAndExpertSettingsRenderWithoutVisibleWindows() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        try
        {
            foreach (var skin in new[] { "Basic", "Office 2019 Black" })
            foreach (var fontSize in new[] { 10F, 16F })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new FrmSettings(workspace);
                form.PrepareForBackgroundRun();
                form.MinimumSize = Size.Empty;
                form.ClientSize = new Size(760, 700);
                form.Show(); Application.DoEvents();
                var page = Field<SettingsPage>(form, "settingsPage");
                await page.ReloadAsync();
                form.SettingsTabs.SelectedTabPage = Field<XtraTabPage>(page, "sipTab");
                var sipPages = Field<XtraTabControl>(page, "sipPages");
                for (var index = 0; index < 2; index++)
                {
                    sipPages.SelectedTabPageIndex = index;
                    form.PerformLayout(); Application.DoEvents(); CheckTableBounds(form);
                    var destination = Path.Combine(RenderOutput(), "sip-settings");
                    Directory.CreateDirectory(destination);
                    using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                    bitmap.Save(Path.Combine(destination, $"Sip-{skin}-{fontSize}-{index}.png"), ImageFormat.Png);
                    if (index == 0)
                    {
                        var sipScroll = Field<XtraScrollableControl>(page, "sipScroll");
                        var actions = Field<TableLayoutPanel>(page, "sipActions");
                        sipScroll.ScrollControlIntoView(actions);
                        form.PerformLayout(); Application.DoEvents();
                        var button = Field<SimpleButton>(page, "sipConnectButton");
                        Assert.IsTrue(sipScroll.RectangleToScreen(sipScroll.ClientRectangle).Contains(button.RectangleToScreen(button.ClientRectangle)), "The connection buttons must be fully reachable after scrolling.");
                        using var actionsImage = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                        actionsImage.Save(Path.Combine(destination, $"Sip-{skin}-{fontSize}-actions.png"), ImageFormat.Png);
                    }
                }
                Assert.IsInstanceOfType<SipAdvancedSettings>(Field<DevExpress.XtraVerticalGrid.PropertyGridControl>(page, "sipProperties").SelectedObject);
                form.Scale(new SizeF(1.5F, 1.5F)); form.PerformLayout(); CheckTableBounds(form);
                await form.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
        }
    });

    [TestMethod]
    public Task WindowStateRestoresPositionSizeMaximizationAndClampsMissingMonitorWithoutTouchingAuditState() => RunStaAsync(() =>
    {
        var product = "WindowStateTests-" + Guid.NewGuid().ToString("N");
        var root = @"Software\WawiCallPro\" + product;
        var descriptor = new ApplicationDescriptor("Isolated window state test", product);
        var state = new AppUiState(descriptor, new(false));
        try
        {
            using var form = new DevExpress.XtraEditors.XtraForm { MinimumSize = new Size(500, 350) };
            _ = form.Handle;
            var area = Screen.PrimaryScreen!.WorkingArea;
            var bounds = new Rectangle(area.Left + 20, area.Top + 20, 720, 520);
            form.Bounds = bounds;
            state.SaveWindow(form, "Settings");
            form.Bounds = new Rectangle(0, 0, 500, 350);
            state.RestoreWindow(form, "Settings");
            Assert.AreEqual(bounds, form.Bounds);
            form.WindowState = FormWindowState.Maximized;
            state.SaveWindow(form, "Settings");
            form.WindowState = FormWindowState.Normal;
            state.RestoreWindow(form, "Settings");
            Assert.AreEqual(FormWindowState.Maximized, form.WindowState);
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Windows\Settings", true)!)
            {
                key.SetValue("Maximized", 0); key.SetValue("Left", 50000); key.SetValue("Top", 50000);
                key.SetValue("Width", 50000); key.SetValue("Height", 50000);
            }
            state.RestoreWindow(form, "Settings");
            Assert.IsTrue(Screen.FromRectangle(form.Bounds).WorkingArea.Contains(form.Bounds));
            var audit = new AppUiState(descriptor, new(true));
            audit.SaveWindow(form, "Audit");
            using var auditKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(root + @"\App\v1\Windows\Audit");
            Assert.IsNull(auditKey);
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(root, false); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public void PluginContractsCarrySharedAssemblyAttributeAndStayInDefaultContext()
    {
        foreach (var assembly in new[] { typeof(Quantarix.Plugin.Abstractions.IPlugin).Assembly,
            typeof(IWawiCallPlugin).Assembly, typeof(IWawiCallPluginViews).Assembly })
        {
            Assert.IsNotNull(assembly.GetCustomAttribute<Quantarix.Plugin.Abstractions.SharedAssemblyAttribute>());
            Assert.AreSame(System.Runtime.Loader.AssemblyLoadContext.Default, System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(assembly));
        }
    }

    [TestMethod]
    public Task PluginSwitchProgressRemainsVisibleAndSettingsAreLockedUntilCompletion() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var beforeFont = WindowsFormsSettings.DefaultFont;
        var beforeAppearance = AppearanceObject.DefaultFont;
        var beforeSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var beforePalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            foreach (var skin in new[] { beforeSkin, "Office 2019 Black" }.Distinct())
            foreach (var fontSize in new[] { 10F, 16F })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                var selection = (IProductPluginSelection)workspace.Plugins;
                await selection.SwitchAsync("Test.Lexware");
                using var settings = new FrmSettings(workspace);
                settings.PrepareForBackgroundRun(); settings.Show();
                var page = Field<SettingsPage>(settings, "settingsPage");
                await page.ReloadAsync(); settings.SelectPluginsTab();
                settings.MinimumSize = Size.Empty; settings.ClientSize = new Size(720, 650);
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                page.ConfigurePluginSwitch(async () => { entered.TrySetResult(); await release.Task; });
                var management = Field<PluginPage>(page, "pluginPage");
                var switching = management.HandleCardActionAsync("activate", 1);
                try
                {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.IsFalse(Field<SimpleButton>(page, "saveButton").Enabled);
                    Assert.IsFalse(Field<SimpleButton>(page, "reloadButton").Enabled);
                    Assert.IsFalse(Field<SimpleButton>(management, "refreshButton").Enabled);
                    Assert.IsTrue(Field<DevExpress.XtraWaitForm.ProgressPanel>(management, "switchProgress").Visible);
                    Assert.Contains("Vorgänge", Field<LabelControl>(management, "statusLabel").Text);
                    settings.PerformLayout(); CheckTableBounds(settings);
                    var output = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(RenderOutput()))!, "plugin-cards");
                    Directory.CreateDirectory(output);
                    using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(settings);
                    bitmap.Save(Path.Combine(output, $"Switch-{skin}-{fontSize}.png"), ImageFormat.Png);
                }
                finally { release.TrySetResult(); await switching; }
                Assert.IsFalse(Field<DevExpress.XtraWaitForm.ProgressPanel>(management, "switchProgress").Visible);
                Assert.IsTrue(Field<SimpleButton>(page, "saveButton").Enabled);
                Assert.AreEqual("Test.Dummy", selection.ActivePluginId);
                await settings.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = beforeFont; AppearanceObject.DefaultFont = beforeAppearance;
            if (string.IsNullOrEmpty(beforePalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(beforeSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(beforeSkin, beforePalette);
        }
    });

    [TestMethod]
    public Task PluginCardActivationIsSingleFlightAndClosingCancelsThePendingSwitch() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var page = new PluginPage();
        page.Bind(workspace, null);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cancelled = false;
        page.ConfigureSelection(async (id, token) =>
        {
            calls++;
            Assert.AreEqual("Test.Dummy", id);
            entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { cancelled = token.IsCancellationRequested; }
        });
        page.RefreshCatalog(); CreateHandles(page);
        Field<GridControl>(page, "catalogGrid").ForceInitialize();
        await page.HandleCardActionAsync("activate", 0);
        Assert.AreEqual(0, calls, "The active plugin must not be activated again.");
        var pending = page.HandleCardActionAsync("activate", 1);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(Field<GridControl>(page, "catalogGrid").Enabled);
        Assert.IsTrue(Field<DevExpress.XtraWaitForm.ProgressPanel>(page, "switchProgress").Visible);
        await page.HandleCardActionAsync("activate", 1);
        await page.HandleCardActionAsync("details", 0);
        Assert.AreEqual(1, calls);
        await page.StopAsync(); await pending;
        Assert.IsFalse(Field<DevExpress.XtraWaitForm.ProgressPanel>(page, "switchProgress").Visible);
        Assert.IsTrue(cancelled);
        Assert.AreEqual("Test.Lexware", ((IProductPluginSelection)workspace.Plugins).ActivePluginId);
    });

    [TestMethod]
    public Task PluginCardsRenderInSettingsWithEmptyCatalogBothSkinsAndLargeFonts() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var fontBefore = WindowsFormsSettings.DefaultFont;
        var appearanceBefore = AppearanceObject.DefaultFont;
        var skinBefore = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var paletteBefore = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            foreach (var skin in new[] { skinBefore, "Office 2019 Black" }.Distinct())
            foreach (var fontSize in new[] { 10F, 16F })
            foreach (var small in new[] { true, false })
            foreach (var populated in new[] { false, true })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                ((TestRuntime)workspace.Plugins).HasCatalog = populated;
                using var main = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
                using var settings = main.CreateSettingsDialog(true);
                settings.PrepareForBackgroundRun();
                settings.Show();
                await Field<SettingsPage>(settings, "settingsPage").ReloadAsync();
                settings.SelectPluginsTab();
                settings.MinimumSize = Size.Empty;
                CreateHandles(settings);
                settings.ClientSize = small ? new Size(720, 650) : new Size(980, 760);
                settings.PerformLayout();
                var management = Field<PluginPage>(Field<SettingsPage>(settings, "settingsPage"), "pluginPage");
                var grid = Field<GridControl>(management, "catalogGrid");
                grid.ForceInitialize();
                var cards = Field<DevExpress.XtraGrid.Views.Items.ItemsView>(management, "catalogView");
                cards.LayoutChanged();
                Assert.AreEqual(populated ? 2 : 0, cards.RowCount);
                CheckTableBounds(settings);
                var output = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(RenderOutput()))!, "plugin-cards");
                Directory.CreateDirectory(output);
                using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(settings);
                bitmap.Save(Path.Combine(output, $"Plugins-{skin}-{fontSize}-{small}-{populated}.png"), ImageFormat.Png);
                if (populated)
                {
                    await management.HandleCardActionAsync("details", 1);
                    Assert.IsTrue(Field<GroupControl>(management, "detailsGroup").Visible);
                    Assert.IsFalse(Field<SimpleButton>(management, "capabilitiesButton").Enabled);
                    await management.HandleCardActionAsync("details", 0);
                    Assert.IsTrue(Field<SimpleButton>(management, "capabilitiesButton").Enabled);
                    settings.PerformLayout();
                    using var details = WawiCallPro.App.Diagnostics.UiAudit.Capture(settings);
                    details.Save(Path.Combine(output, $"Details-{skin}-{fontSize}-{small}.png"), ImageFormat.Png);
                    var detailsScroll = Field<XtraScrollableControl>(management, "detailsScroll");
                    var capabilities = Field<SimpleButton>(management, "capabilitiesButton");
                    detailsScroll.ScrollControlIntoView(capabilities);
                    var buttonPosition = detailsScroll.PointToClient(capabilities.PointToScreen(Point.Empty));
                    Assert.IsTrue(buttonPosition.Y >= 0 && buttonPosition.Y + capabilities.Height <= detailsScroll.ClientSize.Height,
                        "The capabilities action must remain reachable with a large font.");
                    using var bottom = WawiCallPro.App.Diagnostics.UiAudit.Capture(settings);
                    bottom.Save(Path.Combine(output, $"Details-{skin}-{fontSize}-{small}-bottom.png"), ImageFormat.Png);
                }
                if (small && populated)
                {
                    Field<SimpleButton>(management, "backButton").PerformClick();
                    settings.Scale(new SizeF(1.5F, 1.5F)); settings.PerformLayout(); CheckTableBounds(settings);
                    using var scaled = WawiCallPro.App.Diagnostics.UiAudit.Capture(settings);
                    scaled.Save(Path.Combine(output, $"Plugins-{skin}-{fontSize}-scale150.png"), ImageFormat.Png);
                }
                Assert.AreEqual(0d, settings.Opacity);
                Assert.IsFalse(settings.ShowInTaskbar);
                Assert.AreNotEqual(settings.Handle, Quantarix.Windows.Win32.User32.GetForegroundWindow());
                await settings.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = fontBefore; AppearanceObject.DefaultFont = appearanceBefore;
            if (string.IsNullOrEmpty(paletteBefore)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skinBefore);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skinBefore, paletteBefore);
        }
    });

    [TestMethod]
    public void DisplayVersionUsesMajorMinorAndGitRevision()
    {
        Assert.AreEqual("2.3.789", ApplicationVersion.Format(new Version(2, 3, 0, 789)));
        Assert.AreEqual("?", ApplicationVersion.Format(null));
        var assembly = typeof(MainForm).Assembly;
        var version = assembly.GetName().Version!;
        Assert.AreEqual(0, version.Build);
        Assert.IsTrue(version.Revision > 0);
        Assert.AreEqual(version.ToString(), assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
        Assert.AreEqual(ApplicationVersion.Display, assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
    }

    [TestMethod]
    public Task HelpAndAboutAreLargeSettingsRibbonActions() => RunStaAsync(() =>
    {
        using var form = new MainForm();
        form.PrepareForBackgroundRun();
        form.Show();
        var settings = Field<DevExpress.XtraBars.Ribbon.RibbonPage>(form, "settingsRibbon");
        Assert.IsTrue(settings.Groups.Contains(Field<DevExpress.XtraBars.Ribbon.RibbonPageGroup>(form, "helpGroup")));
        Assert.AreEqual("Hilfe", Field<DevExpress.XtraBars.BarButtonItem>(form, "helpItem").Caption);
        Assert.AreEqual("Über", Field<DevExpress.XtraBars.BarButtonItem>(form, "aboutItem").Caption);
        Field<DevExpress.XtraBars.Ribbon.RibbonControl>(form, "ribbon").SelectedPage = settings;
        form.ClientSize = new Size(1100, 760);
        CreateHandles(form); form.PerformLayout();
        using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
        var output = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(RenderOutput()))!, "lx-startup-log");
        bitmap.Save(Path.Combine(output, "Settings-ribbon.png"), ImageFormat.Png);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ReferenceDashboardAndDockLayoutRenderWithoutVisibleWindows() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var originalSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var originalPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            foreach (var skin in new[] { originalSkin, "Office 2019 Black" }.Distinct())
            foreach (var size in new[] { 10F, 16F })
            foreach (var small in new[] { false, true })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
                form.PrepareForBackgroundRun();
                form.ClientSize = small ? new Size(1000, 800) : new Size(1500, 1000);
                form.Show(); Application.DoEvents();
                form.MinimumSize = Size.Empty;
                form.ClientSize = small ? new Size(1000, 800) : new Size(1500, 1000);
                CreateHandles(form); form.PerformLayout();
                var dashboard = (HomeDashboard)form.GetActiveDocumentView()!;
                Assert.AreEqual("Plugin geladen", Field<LabelControl>(dashboard, "pluginValue").Text);
                Assert.AreEqual("Nicht konfiguriert", Field<LabelControl>(dashboard, "sipValue").Text);
                Assert.IsTrue(Field<DevExpress.XtraBars.Docking.ControlContainer>(form, "outputContainer").Width > 500);
                Assert.IsTrue(dashboard.Width > 500);
                var buttons = new[] { "refreshButton", "historyButton", "settingsButton" }
                    .Select(name => Field<SimpleButton>(dashboard, name)).ToArray();
                Assert.AreEqual(1, buttons.Select(button => button.Width).Distinct().Count());
                Assert.IsTrue(buttons.All(button => button.Width >= button.GetPreferredSize(Size.Empty).Width));
                Assert.IsTrue(buttons[0].Width < dashboard.Width / 3, "Toolbar buttons must keep a compact width.");
                CheckTableBounds(dashboard);
                var output = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(RenderOutput()))!, "layout-reference");
                Directory.CreateDirectory(output);
                using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                bitmap.Save(Path.Combine(output, $"Main-{skin}-{size}-{small}.png"), ImageFormat.Png);
                var scroll = Field<XtraScrollableControl>(dashboard, "scroll");
                Assert.IsTrue(scroll.VerticalScroll.Visible || Field<TableLayoutPanel>(dashboard, "body").Height <= scroll.Height,
                    $"Dashboard scroll missing: body={Field<TableLayoutPanel>(dashboard, "body").Height}, viewport={scroll.Height}");
                scroll.ScrollControlIntoView(Field<LabelControl>(dashboard, "editionDetail"));
                Application.DoEvents();
                using var bottom = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                bottom.Save(Path.Combine(output, $"Main-{skin}-{size}-{small}-bottom.png"), ImageFormat.Png);
                scroll.VerticalScroll.Value = 0;
                if (size == 10F && !small)
                {
                    dashboard.Bind(null, null, workspace.Application);
                    Assert.AreEqual("Kein Plugin geladen", Field<LabelControl>(dashboard, "pluginValue").Text);
                    using var empty = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                    empty.Save(Path.Combine(output, $"Main-{skin}-empty.png"), ImageFormat.Png);
                    dashboard.Bind(workspace, workspace.Configuration, workspace.Application);
                }
                var ribbon = Field<DevExpress.XtraBars.Ribbon.RibbonControl>(form, "ribbon");
                ribbon.SelectedPage = Field<DevExpress.XtraBars.Ribbon.RibbonPage>(form, "databaseRibbon");
                ribbon.Minimized = false;
                form.PerformLayout(); Application.DoEvents();
                using var databaseRibbon = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                databaseRibbon.Save(Path.Combine(output, $"DatabaseRibbon-{skin}-{size}-{small}.png"), ImageFormat.Png);
                ribbon.SelectedPage = Field<DevExpress.XtraBars.Ribbon.RibbonPage>(form, "settingsRibbon");
                using var settingsRibbon = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                settingsRibbon.Save(Path.Combine(output, $"SettingsRibbon-{skin}-{size}-{small}.png"), ImageFormat.Png);
                ribbon.SelectedPage = Field<DevExpress.XtraBars.Ribbon.RibbonPage>(form, "databaseRibbon");
                form.Scale(new SizeF(1.5F, 1.5F)); form.PerformLayout();
                CheckTableBounds(dashboard);
                using var scaled = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                scaled.Save(Path.Combine(output, $"Main-{skin}-{size}-{small}-scale150.png"), ImageFormat.Png);
                await form.PrepareToCloseAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = originalFont; AppearanceObject.DefaultFont = originalAppearance;
            if (string.IsNullOrEmpty(originalPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(originalSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(originalSkin, originalPalette);
        }
    });

    [TestMethod]
    public Task SettingsDialogRendersEveryPageWithEmptyAndPopulatedDataWithoutVisibleWindows() => WithWorkspace(async (workspace, services, plugin) =>
    {
        AppUiStartup.Apply();
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        try
        {
            foreach (var populated in new[] { false, true })
            foreach (var fontSize in new[] { 10F, 16F })
            foreach (var small in new[] { false, true })
            {
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var form = new FrmSettings();
                var page = Field<SettingsPage>(form, "settingsPage");
                ((TestRuntime)workspace.Plugins).HasCatalog = populated;
                page.Bind(workspace);
                if (populated)
                    page.Apply(new ConfigurationSnapshot(new ApplicationOptions
                    {
                        SipAccount = new SipAccountOptions { Server = "telefonanlage.test.invalid", Username = "Telefon-Arbeitsplatz", Port = 5061, Transport = SipTransport.Tls },
                        Companies = [new CompanySettings { Company = new(LexwareProductLine.Pro, "fixture", "01"), IncludeInPhoneCache = true, DisplayName = "Musterunternehmen mit längerem Mandantennamen" }]
                    }, true));
                form.Font = font;
                _ = form.Handle;
                form.MinimumSize = Size.Empty;
                form.ClientSize = small ? new Size(720, 650) : new Size(980, 760);
                var tabs = Field<XtraTabControl>(page, "tabs");
                Assert.AreEqual(Field<SimpleButton>(page, "saveButton").Size, Field<SimpleButton>(page, "reloadButton").Size);
                Assert.AreEqual(Field<SimpleButton>(page, "saveButton").Size, Field<SimpleButton>(form, "closeButton").Size);
                foreach (XtraTabPage tab in tabs.TabPages)
                {
                    tabs.SelectedTabPage = tab;
                    CreateHandles(form);
                    form.PerformLayout();
                    CheckTableBounds(form);
                    if (tab.Text == "Plugins")
                    {
                        var catalog = Field<GridControl>(Field<PluginPage>(page, "pluginPage"), "catalogGrid");
                        Assert.IsTrue(catalog.Height >= 100, $"Plugin list collapsed: {catalog.Height}");
                    }
                    using var bitmap = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                    var output = RenderOutput();
                    Directory.CreateDirectory(output);
                    bitmap.Save(Path.Combine(output, $"Settings-{tab.Text}-{populated}-{fontSize:0}-{small}.png"), ImageFormat.Png);
                }
                form.Scale(new SizeF(1.5F, 1.5F));
                form.PerformLayout();
                CheckTableBounds(form);
                page.SelectPluginsTab();
                form.PerformLayout();
                using var scaled = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
                scaled.Save(Path.Combine(RenderOutput(), $"Settings-Plugins-{populated}-{fontSize:0}-{small}-scale150.png"), ImageFormat.Png);
                Assert.IsFalse(form.Visible);
            }
        }
        finally { WindowsFormsSettings.DefaultFont = originalFont; AppearanceObject.DefaultFont = originalAppearance; }
        await Task.CompletedTask;
    });

    [TestMethod]
    public Task PluginManagementLivesInSettingsAndSwitchPersistsWithoutLosingEdits() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        var company = new Company(new(LexwareProductLine.Pro, "fixture", "1"), "Testmandant");
        await workspace.Companies.SelectAsync(company);
        await form.OpenPartyAsync(new(company.Key, PartyKind.Customer, "1"));
        Assert.AreEqual(2, plugin.Created);
        using var settings = form.CreateSettingsDialog(true);
        var page = Field<SettingsPage>(settings, "settingsPage");
        _ = settings.Handle;
        CreateHandles(settings);
        Assert.AreEqual("Plugins", settings.SettingsTabs.SelectedTabPage.Text);
        var management = Field<PluginPage>(page, "pluginPage");
        Assert.AreEqual(2, ((Array)Field<GridControl>(management, "catalogGrid").DataSource).Length);
        Find<CheckEdit>(page, "notificationsEdit").Checked = false;
        var catalog = Field<DevExpress.XtraGrid.Views.Items.ItemsView>(management, "catalogView");
        catalog.FocusedRowHandle = 1;
        await management.HandleCardActionAsync("activate", 1);
        Assert.AreEqual("Plugin gewechselt und Auswahl gespeichert.", Field<LabelControl>(management, "statusLabel").Text);
        Assert.AreEqual(2, plugin.Disposed);
        Assert.IsNull(workspace.Companies.Current.Company);
        Assert.AreEqual("Test.Dummy", (await workspace.Settings.LoadAsync()).Options.User.ActivePluginId);
        Assert.IsFalse(Find<CheckEdit>(page, "notificationsEdit").Checked);
        await page.SaveAsync(default);
        Assert.IsFalse((await workspace.Settings.LoadAsync()).Options.User.ShowCallNotifications);
        Assert.AreEqual("Test.Dummy", (await workspace.Settings.LoadAsync()).Options.User.ActivePluginId);
        catalog.FocusedRowHandle = 0;
        await management.HandleCardActionAsync("activate", 0);
        Assert.AreEqual("Test.Lexware", (await workspace.Settings.LoadAsync()).Options.User.ActivePluginId);
        Assert.IsFalse((await workspace.Settings.LoadAsync()).Options.User.ShowCallNotifications);
        await Assert.ThrowsAsync<TelephonyException>(() => workspace.TelephonyConnection!.ConnectAsync());
        await Task.Delay(10);
        Assert.Contains("fehlgeschlagen", Field<LabelControl>(page, "sipStatus").Text);
        await settings.StopAsync();
        await form.PrepareToCloseAsync();
    });

    [TestMethod]
    public Task ApplicationBrandHasSvgAndMultiResolutionExecutableIcon() => RunStaAsync(() =>
    {
        using var form = new MainForm();
        Assert.IsNotNull(form.IconOptions.SvgImage);
        Assert.IsNotNull(form.IconOptions.Icon);
        using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("WawiCallPro.App.AppIcon.ico")!;
        using var reader = new BinaryReader(stream);
        Assert.AreEqual((ushort)0, reader.ReadUInt16());
        Assert.AreEqual((ushort)1, reader.ReadUInt16());
        Assert.AreEqual((ushort)7, reader.ReadUInt16());
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "WawiCallProSolution.sln"))) repository = repository.Parent;
        Assert.IsNotNull(repository);
        var config = typeof(MainForm).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var edition = ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly).ProductLine;
        var executable = Path.Combine(repository.FullName, "WawiCallPro.App", "bin", config, "net11.0-windows10.0.19041.0", "WawiCallPro.App." + edition + ".exe");
        Assert.IsTrue(File.Exists(executable));
        using var extracted = Icon.ExtractAssociatedIcon(executable);
        Assert.IsNotNull(extracted);
        using var bitmap = extracted.ToBitmap();
        var blue = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 8);
        Assert.IsTrue(Math.Abs(blue.R - 18) < 10 && Math.Abs(blue.G - 104) < 10 && Math.Abs(blue.B - 196) < 10, "Executable does not contain the application brand icon.");
        form.ClientSize = new Size(1240, 780);
        CreateHandles(form);
        form.PerformLayout();
        using var preview = WawiCallPro.App.Diagnostics.UiAudit.Capture(form);
        var destination = Path.Combine(repository.FullName, ".artifacts", "lx-startup-log");
        Directory.CreateDirectory(destination);
        preview.Save(Path.Combine(destination, "Main-icon.png"), ImageFormat.Png);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task ToolbarButtonGroupsReactToTextFontDpiAndDynamicControls() => RunStaAsync(() =>
    {
        using var table = new TableLayoutPanel { AutoSize = true, ColumnCount = 3 };
        using var first = new SimpleButton { Text = "Laden" };
        using var second = new SimpleButton { Text = "Speichern" };
        table.Controls.Add(first, 0, 0); table.Controls.Add(second, 1, 0);
        UiButtonSizing.Attach(table);
        Assert.AreEqual(first.Size, second.Size);
        var original = first.Size;
        first.Text = "Gesamten Gesprächsverlauf aktualisieren";
        Assert.IsTrue(first.Width > original.Width);
        Assert.AreEqual(first.Size, second.Size);
        using var font = new Font(first.Font.FontFamily, first.Font.Size + 8);
        second.Font = font;
        Assert.IsTrue(first.Height > original.Height, $"Original: {original}; resized: {first.Size}; fonts: {first.Font} / {second.Font}");
        Assert.AreEqual(first.Size, second.Size);
        first.Size = new(first.Width + 30, first.Height + 10);
        typeof(Control).GetMethod("OnDpiChangedAfterParent", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(first, [EventArgs.Empty]);
        Assert.AreEqual(first.Size, second.Size);
        using var added = new SimpleButton { Text = "Abbrechen" };
        table.Controls.Add(added, 2, 0);
        Assert.AreEqual(first.Size, added.Size);
        Assert.IsFalse(added.AutoSize);
        Assert.IsTrue(added.Width - TextRenderer.MeasureText(added.Text, added.Font).Width >= added.LogicalToDeviceUnits(24));
        UiButtonSizing.Attach(table);
        second.Text = "OK";
        Assert.AreEqual(first.Size, second.Size);
        Assert.AreEqual(first.Size, added.Size);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task LogSearchCtrlFTogglesFromTheLogEditorAndCloseButton() => RunStaAsync(() =>
    {
        using var host = new MainForm();
        host.PrepareForBackgroundRun();
        using var panel = new LogPanel();
        host.Controls.Add(panel); panel.BringToFront(); host.Show();
        panel.Append("Isolated search message");
        var list = Field<ListBoxControl>(panel, "listLog");
        var search = Field<LogListBoxSearchController>(panel, "searchController");
        var editor = Field<ButtonEdit>(search, "_txtSearch");
        var button = Field<DevExpress.XtraEditors.Controls.EditorButton>(search, "closeButton");
        Assert.AreEqual(1, editor.Properties.Buttons.Count);
        Assert.AreSame(button, editor.Properties.Buttons[0]);
        Assert.AreEqual(DevExpress.XtraEditors.Controls.ButtonPredefines.Clear, button.Kind);
        Assert.AreEqual("Suchtext", editor.Properties.NullValuePrompt);
        Assert.IsFalse(search.Visible);
        foreach (var useClearButton in new[] { false, true })
        {
            list.Focus();
            var key = new KeyEventArgs(Keys.Control | Keys.F);
            Assert.IsTrue(search.HandleLogKeyDown(key));
            Assert.IsTrue(key.Handled && key.SuppressKeyPress);
            Assert.IsTrue(search.Visible);
            Assert.IsTrue(editor.ContainsFocus);
            Assert.IsFalse(Field<LabelControl>(search, "_lblResult").Visible);
            editor.Text = "s";
            Assert.IsFalse(Field<LabelControl>(search, "_lblResult").Visible);
            editor.Text = "search";
            Assert.IsTrue(Field<LabelControl>(search, "_lblResult").Visible);
            Assert.Contains("<backcolor=Yellow>", list.Items[0].ToString()!);
            if (useClearButton) editor.PerformClick(button);
            else
            {
                var message = Message.Create(editor.Handle, 0x0100, (IntPtr)Keys.F, IntPtr.Zero);
                var handled = (bool)typeof(Control).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(editor, [message, Keys.Control | Keys.F])!;
                Assert.IsTrue(handled);
            }
            Assert.IsFalse(search.Visible);
            Assert.AreEqual("", editor.Text);
            Assert.DoesNotContain("<backcolor=Yellow>", list.Items[0].ToString()!);
            Assert.IsTrue(list.ContainsFocus);
        }
        Assert.IsFalse(search.HandleLogKeyDown(new KeyEventArgs(Keys.Control | Keys.Shift | Keys.F)));
        Assert.IsFalse(search.HandleLogKeyDown(new KeyEventArgs(Keys.Control | Keys.Alt | Keys.F)));
        Assert.IsFalse(search.Visible);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task LogListMatchesLxFormattingSearchAndPlainTextWithoutVisibleWindows() => RunStaAsync(() =>
    {
        AppUiStartup.Apply();
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = AppearanceObject.DefaultFont;
        var originalSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var originalPalette = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSvgPaletteName;
        try
        {
            foreach (var (skin, palette, family, size, width) in new[] {
                ("Basic", "Blue Dark", "Tahoma", 8F, 600), ("Basic", "", "Segoe UI", 10F, 1000),
                ("Office 2019 Black", "", "Segoe UI", 16F, 600) })
            {
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin, palette);
                using var font = new Font(family, size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                var logs = new LogWindowService();
                logs.LogNormal("Verbindung bereit"); logs.LogWarning("Verbindung prüfen"); logs.LogError("Fehler <tag> mit mehrzeiligem Inhalt\nWeitere Information");
                using var panel = new LogPanel(); panel.Bind(logs);
                using var host = new MainForm(); host.PrepareForBackgroundRun(); host.Font = font;
                host.Controls.Add(panel); panel.BringToFront(); host.Show();
                var list = Field<ListBoxControl>(panel, "listLog");
                Assert.AreEqual(3, list.ItemCount);
                Assert.AreEqual(SelectionMode.MultiExtended, list.SelectionMode);
                Assert.Contains("<color=Gold>", list.Items[1].ToString()!);
                Assert.Contains("<color=Red>", list.Items[2].ToString()!);
                Assert.DoesNotContain("<tag>", list.Items[2].ToString()!);
                Assert.DoesNotContain("<color", LogListBoxText.ToPlainText(list.Items[2].ToString()!));
                var search = Field<LogListBoxSearchController>(panel, "searchController");
                Assert.IsTrue(search.HandleLogKeyDown(new KeyEventArgs(Keys.Control | Keys.F)));
                panel.Size = new Size(width, 260);
                CreateHandles(panel); panel.PerformLayout();
                Assert.IsFalse(Field<LabelControl>(search, "_lblResult").Visible);
                var emptyDestination = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(RenderOutput()))!, "lx-startup-log");
                Directory.CreateDirectory(emptyDestination);
                using (var emptySearch = new Bitmap(panel.Width, panel.Height))
                {
                    panel.DrawToBitmap(emptySearch, new Rectangle(Point.Empty, panel.Size));
                    emptySearch.Save(Path.Combine(emptyDestination, $"Log-empty-search-{skin}-{width}-{size:0}.png"), ImageFormat.Png);
                }
                Field<TextEdit>(search, "_txtSearch").Text = "Verbindung";
                Assert.Contains("2 Treffer", Field<LabelControl>(search, "_lblResult").Text);
                Assert.Contains("<backcolor=Yellow>", list.Items[0].ToString()!);
                panel.Size = new Size(width, 260);
                CreateHandles(panel); panel.PerformLayout();
                CheckTableBounds(panel);
                Assert.IsTrue(list.Bottom <= search.Top, "The search bar belongs below the log list, as in LxTranslate.");
                Assert.IsTrue(search.Height <= Math.Ceiling(search.Font.GetHeight() * 2),
                    $"Search bar is too tall for a single input row: {search.Height}");
                using var bitmap = new Bitmap(panel.Width, panel.Height);
                panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty,panel.Size));
                var destination = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(RenderOutput()))!, "lx-startup-log");
                Directory.CreateDirectory(destination);
                bitmap.Save(Path.Combine(destination, $"Log-{skin}-{width}-{size:0}.png"), ImageFormat.Png);
                if (width == 600 && size == 16F)
                {
                    panel.Scale(new SizeF(1.5F, 1.5F));
                    panel.PerformLayout(); CheckTableBounds(panel);
                    using var scaled = new Bitmap(panel.Width, panel.Height);
                    panel.DrawToBitmap(scaled, new Rectangle(Point.Empty, panel.Size));
                    scaled.Save(Path.Combine(destination, $"Log-{skin}-scale150.png"), ImageFormat.Png);
                    list.Items.Clear();
                    LogListBoxSearchController.ApplyCurrentSearch(list);
                    Assert.Contains("0 Treffer", Field<LabelControl>(search, "_lblResult").Text);
                    CheckTableBounds(panel);
                    using var emptyLog = new Bitmap(panel.Width, panel.Height);
                    panel.DrawToBitmap(emptyLog, new Rectangle(Point.Empty, panel.Size));
                    emptyLog.Save(Path.Combine(destination, $"Log-{skin}-empty-scale150.png"), ImageFormat.Png);
                }
                var listHeightWithSearch = list.Height;
                typeof(LogListBoxSearchController).GetMethod("SearchKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(search, [null, new KeyEventArgs(Keys.Escape)]);
                panel.PerformLayout();
                Assert.IsTrue(list.Height > listHeightWithSearch, "Closing search must return its space to the log list.");
                Assert.AreEqual(0.0, host.Opacity);
                Assert.IsFalse(host.ShowInTaskbar);
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = originalFont; AppearanceObject.DefaultFont = originalAppearance;
            if (string.IsNullOrEmpty(originalPalette)) DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(originalSkin);
            else DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(originalSkin, originalPalette);
        }
        return Task.CompletedTask;
    });

    [TestMethod]
    public async Task LogQueueIsBoundedAndBackendFormattersAndSecretsNeverReachDisplay()
    {
        var logs = new LogWindowService();
        using var provider = new UiLogProvider(logs);
        var logger = provider.CreateLogger("Foreign.Library");
        logger.Log(LogLevel.Error, new EventId(7), "private-password-token", new IOException("private-exception"),
            (_, _) => throw new AssertFailedException("The UI provider must not call foreign formatters."));
        OperationEvents.Write(logger, OperationEvent.HostStarted);
        var entries = logs.Drain();
        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual("Anwendungsdienste sind bereit.", entries[1].Message);
        Assert.IsTrue(entries.All(entry => !entry.Message.Contains("private", StringComparison.Ordinal)));
        await Task.Run(() => { for (var i=0;i<2200;i++) logs.LogNormal("Testmeldung"); });
        var count = 0; while ((entries = logs.Drain()).Count > 0) count += entries.Count;
        Assert.AreEqual(2000, count);
    }

    private static string RenderOutput([CallerFilePath] string source = "") => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(source))!, ".artifacts", "app-shell", "settings-ui");
    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls) CreateHandles(child);
    }
    private static void CheckTableBounds(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (parent is XtraTabControl tabs && control is XtraTabPage tab && tabs.SelectedTabPage != tab) continue;
            if (parent is DevExpress.XtraBars.Navigation.NavigationFrame frame && control is DevExpress.XtraBars.Navigation.NavigationPage page && frame.SelectedPage != page) continue;
            if (parent is TableLayoutPanel { AutoScroll: false } && control is LabelControl or SimpleButton or TextEdit)
                Assert.IsTrue(control.Left >= 0 && control.Top >= 0 && control.Right <= parent.ClientSize.Width + 1 && control.Bottom <= parent.ClientSize.Height + 1,
                    $"Clipped {control.Name}: {control.Bounds} in {parent.ClientSize}");
            CheckTableBounds(control);
        }
    }

    [TestMethod]
    public Task CacheCompaniesAreSelectedOnlyInSettingsAndRemainIndependentOfTheActiveCompany() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var page = new SettingsPage(); page.Bind(workspace);
        await InvokeActionAsync(page, "LoadCompanies_Click");
        var view = Field<DevExpress.XtraGrid.Views.Grid.GridView>(page, "cacheView");
        view.SelectRow(0);
        await page.SaveAsync(CancellationToken.None);
        var saved = await workspace.Settings.LoadAsync();
        var selected = saved.Options.Companies.Where(company => company.IncludeInPhoneCache).ToArray();
        Assert.HasCount(1, selected);
        Assert.AreEqual("Firma A", selected[0].DisplayName);
        Assert.IsNull(workspace.Companies.Current.Company);
        view.ClearSelection();
        await page.SaveAsync(CancellationToken.None);
        Assert.IsFalse((await workspace.Settings.LoadAsync()).Options.Companies.Any(company => company.IncludeInPhoneCache));
        Assert.IsNull(typeof(MainForm).GetField("companyLookup", BindingFlags.Instance | BindingFlags.NonPublic));
    });

    [TestMethod]
    public Task CacheRefreshSavesSelectionReportsResultsAndRestoresButtonsAfterCancellation() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        using var page = new SettingsPage();
        page.Bind(workspace);
        page.ConfigureLayoutPersistence(services.GetRequiredService<AppUiState>());
        await InvokeActionAsync(page, "LoadCompanies_Click");
        var view = Field<DevExpress.XtraGrid.Views.Grid.GridView>(page, "cacheView");
        view.SelectRow(0);
        await InvokeActionAsync(page, "RefreshCache_Click");
        var selected = (await workspace.Settings.LoadAsync()).Options.Companies.Single(company => company.IncludeInPhoneCache);
        var cache = services.GetRequiredService<IPartyCacheRepository>();
        Assert.AreEqual(1, (await cache.GetStateAsync(selected.Company)).Revision);
        Assert.AreEqual("+49301234567", (await cache.GetAsync(new(selected.Company, PartyKind.Customer, "01")))!.Phones.Single().E164);
        Assert.Contains("1 von 1", Field<LabelControl>(page, "statusLabel").Text);
        Assert.Contains("1 Nummern", view.GetRowCellValue(0, "CacheStatus").ToString()!);
        Assert.IsNull(workspace.Companies.Current.Company);

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var directory = (TestDirectory)workspace.Directory;
        directory.Read = async query =>
        {
            entered.SetResult();
            await release.Task;
            return new(query.Company, new([], false));
        };
        typeof(SettingsPage).GetMethod("RefreshCache_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [null, EventArgs.Empty]);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsFalse(Field<SimpleButton>(page, "refreshCacheButton").Enabled);
            Assert.IsFalse(Field<GridControl>(page, "cacheGrid").Enabled);
            Assert.IsTrue(Field<SimpleButton>(page, "cancelCacheButton").Enabled);
            typeof(SettingsPage).GetMethod("CancelCache_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [null, EventArgs.Empty]);
            Assert.IsTrue(Field<PageOperation>(page, "_operation").IsRunning, "Cancellation must await the source response.");
        }
        finally { release.TrySetResult(); }
        // StopAsync also exercises closing the dialog while a cancelled source call is still completing.
        await page.StopAsync();
        Assert.AreEqual(1, (await cache.GetStateAsync(selected.Company)).Revision);
        Assert.IsTrue(Field<SimpleButton>(page, "refreshCacheButton").Enabled);
        Assert.IsTrue(Field<SimpleButton>(page, "saveButton").Enabled);
        Assert.IsTrue(Field<GridControl>(page, "cacheGrid").Enabled);
        Assert.IsFalse(Field<SimpleButton>(page, "cancelCacheButton").Enabled);
        Assert.Contains("abgebrochen", Field<LabelControl>(page, "statusLabel").Text);
    });

    [TestMethod]
    public Task SettingsRoundtripPreservesSecretsDeviceAndCompanyScopesAndRejectsAStaleEditor() => WithWorkspace(async (workspace, services, plugin) =>
    {
        var original = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(original.Options with
        {
            SipAccount = new SipAccountOptions { Server = "test.invalid", Username = "fixture", Password = workspace.Secrets.Protect("test-secret-sentinel", SipAccountOptions.PasswordPurpose) },
            Devices = original.Options.Devices with { CaptureDeviceId = "capture-fixture", PlaybackDeviceId = "playback-fixture" },
            Companies = [new CompanySettings { Company = new(LexwareProductLine.Pro, "fixture", "01"), DefaultPhoneRegion = "DE", LocalAreaCode = "030" }]
        }, original);
        var snapshot = await workspace.Settings.LoadAsync();
        using var first = new SettingsPage(); first.Bind(workspace); first.Apply(snapshot);
        using var second = new SettingsPage(); second.Bind(workspace); second.Apply(snapshot);
        Find<CheckEdit>(first, "notificationsEdit").Checked = false;
        Find<CheckEdit>(first, "sipEnabledEdit").Checked = true;
        var expert = (SipAdvancedSettings)Field<DevExpress.XtraVerticalGrid.PropertyGridControl>(first, "sipProperties").SelectedObject;
        expert.LocalPort = 5072;
        expert.RegistrationExpirySeconds = 300;
        await first.SaveAsync(CancellationToken.None);
        var saved = await workspace.Settings.LoadAsync();
        Assert.IsFalse(saved.Options.User.ShowCallNotifications);
        Assert.IsTrue(saved.Options.SipAccount!.RegisterOnStartup);
        Assert.AreEqual(5072, saved.Options.SipAccount.Connection.LocalPort);
        Assert.AreEqual(300, saved.Options.SipAccount.Connection.RegistrationExpirySeconds);
        Assert.AreEqual("capture-fixture", saved.Options.Devices.CaptureDeviceId);
        Assert.AreEqual("playback-fixture", saved.Options.Devices.PlaybackDeviceId);
        Assert.AreEqual(snapshot.Options.Companies[0], saved.Options.Companies[0]);
        using var password = workspace.Secrets.Unprotect(saved.Options.SipAccount!.Password!, SipAccountOptions.PasswordPurpose);
        Assert.AreEqual("test-secret-sentinel", password.Reveal());
        Assert.DoesNotContain("test-secret-sentinel", await File.ReadAllTextAsync(services.GetRequiredService<ConfigurationLocation>().FilePath));
        await Assert.ThrowsAsync<ConfigurationException>(() => second.SaveAsync(CancellationToken.None));
        Assert.AreEqual("Sqlite", Field<LabelControl>(first, "providerValue").Text);
        Assert.AreEqual(CommunicationDatabaseProvider.Sqlite, (await workspace.Settings.LoadAsync()).Options.Database.Provider);
    });

    [TestMethod]
    public Task DatabaseRibbonAndHistoryUseRealStorageWithoutCreatingOnReadAndSaveANote() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var database = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        database.PrepareForBackgroundRun();
        _ = database.Handle;
        typeof(Form).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(database, [EventArgs.Empty]);
        await ClickRibbonAsync(database, "databaseStatusItem");
        Assert.IsFalse(File.Exists(Path.Combine(workspace.Paths.RootDirectory, "communications.db")));
        await workspace.Database.CreateAndMigrateAsync();
        await ClickRibbonAsync(database, "databaseStatusItem");
        Assert.Contains("betriebsbereit", Field<LabelControl>(database, "connectionLabel").Text);
        Assert.AreEqual(1, Field<DevExpress.XtraBars.Docking2010.Views.Tabbed.TabbedView>(database, "tabbedView").Documents.Count);
        Assert.IsTrue(Field<DevExpress.XtraBars.BarButtonItem>(database, "databaseBackupItem").Enabled);
        Assert.AreEqual(DatabaseSchemaState.Current, (await workspace.Database.GetStatusAsync()).State);
        var now = DateTimeOffset.UtcNow;
        var call = await services.GetRequiredService<ICallJournal>().BeginAsync(new CallSnapshot
        {
            Id = Guid.NewGuid(), Revision = 0, Direction = CallDirection.Incoming, State = CallState.Ringing,
            LocalAddress = new(NumberPresentation.Available, "100"), RemoteAddress = new(NumberPresentation.Available, "+49301234567"),
            StartedAtUtc = now, StateChangedAtUtc = now
        });
        using var history = new HistoryPage(); history.Bind(workspace, services.GetRequiredService<AppUiState>());
        await InvokeActionAsync(history, "Refresh_Click");
        Assert.AreEqual(1, ((Array)Field<GridControl>(history, "grid").DataSource).Length);
        Assert.Contains("+49301234567", Field<MemoEdit>(history, "detailsEdit").Text);
        Field<MemoEdit>(history, "noteEdit").Text = "Isolierte UI-Notiz";
        await InvokeActionAsync(history, "SaveNote_Click");
        Assert.AreEqual("Isolierte UI-Notiz", (await workspace.History.GetCallAsync(call.Id))!.Notes.Single().Text);
        history.Size = new Size(900, 650);
        CreateHandles(history);
        var historyTabs = Field<XtraTabControl>(history, "historyTabs");
        foreach (XtraTabPage tab in historyTabs.TabPages)
        {
            historyTabs.SelectedTabPage = tab;
            history.PerformLayout();
            using var bitmap = new Bitmap(history.Width, history.Height);
            history.DrawToBitmap(bitmap, new Rectangle(Point.Empty, history.Size));
            Directory.CreateDirectory(RenderOutput());
            bitmap.Save(Path.Combine(RenderOutput(), "History-populated-" + historyTabs.SelectedTabPageIndex + ".png"), ImageFormat.Png);
        }
    });

    [TestMethod]
    public Task HistoryLoadsEntireDateRangeAndKeepsActionsCompact() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        var oldFont = WindowsFormsSettings.DefaultFont;
        var oldAppearance = AppearanceObject.DefaultFont;
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var output = Path.Combine(RenderOutput(), "history-layout");
        Directory.CreateDirectory(output);
        try
        {
            foreach (var populated in new[] { false, true })
            {
                if (populated)
                {
                    var journal = services.GetRequiredService<ICallJournal>();
                    var now = DateTimeOffset.UtcNow;
                    for (var index = 0; index < 501; index++)
                        await journal.BeginAsync(new()
                        {
                            Id = Guid.NewGuid(), Revision = 0, Direction = CallDirection.Incoming, State = CallState.Ringing,
                            LocalAddress = new(NumberPresentation.Available, "100"),
                            RemoteAddress = new(NumberPresentation.Available, "caller-" + index),
                            StartedAtUtc = now, StateChangedAtUtc = now
                        });
                }
                foreach (var (width, family, fontSize, scale, skin, palette) in new[] {
                    (1400, "Segoe UI", 10F, 1F, "Basic", ""), (900, "Tahoma", 8F, 1.25F, "Basic", "Blue Dark"),
                    (640, "Segoe UI", 16F, 1F, "Office 2019 Black", ""), (720, "Segoe UI", 10F, 1.5F, "Basic", "") })
                {
                    DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin, palette);
                    using var font = new Font(family, fontSize);
                    WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                    using var page = new HistoryPage();
                    page.Font = font; page.Size = new(width, 560); page.Bind(workspace, null);
                    using var host = new MainForm();
                    host.PrepareForBackgroundRun(); host.MinimumSize = Size.Empty; host.Font = font;
                    host.Controls.Add(page); page.BringToFront(); host.ClientSize = page.Size;
                    host.Show();
                    CreateHandles(page);
                    if (scale != 1F) page.Scale(new SizeF(scale, scale));
                    page.PerformLayout();
                    await InvokeActionAsync(page, "Refresh_Click");
                    Assert.AreEqual(populated ? 501 : 0, ((Array)Field<GridControl>(page, "grid").DataSource).Length);
                    var view = Field<DevExpress.XtraGrid.Views.Grid.GridView>(page, "view");
                    Assert.AreEqual("Bis", Field<LabelControl>(page, "toLabel").Text);
                    Assert.IsFalse(view.OptionsFind.AlwaysVisible);
                    Assert.IsFalse(view.IsFindPanelVisible);
                    var historyGrid = Field<GridControl>(page, "grid");
                    historyGrid.Focus();
                    typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(historyGrid, [new KeyEventArgs(Keys.Control | Keys.F)]);
                    Assert.IsTrue(view.IsFindPanelVisible, "Ctrl+F must open the built-in GridView find panel.");
                    if (populated)
                    {
                        view.ApplyFindFilter("caller-500");
                        Assert.AreEqual(1, view.DataRowCount, "The built-in search must include records beyond the old first page.");
                        view.ApplyFindFilter("");
                        await WaitForOperationAsync(page);
                    }
                    var button = Field<SimpleButton>(page, "refreshButton");
                    view.HideFindPanel();
                    Assert.IsFalse(view.IsFindPanelVisible);
                    var from = Field<DateEdit>(page, "fromEdit");
                    var to = Field<DateEdit>(page, "toEdit");
                    Assert.AreEqual(from.Width, to.Width);
                    Assert.IsTrue(button.Height <= Math.Ceiling(button.Font.GetHeight() * 2.5) + page.LogicalToDeviceUnits(2), "The common toolbar metrics must not produce an oversized action.");
                    var fromLabel = Field<LabelControl>(page, "fromLabel");
                    Assert.IsTrue(Math.Abs(fromLabel.PointToScreen(new Point(0, fromLabel.Height / 2)).Y - from.PointToScreen(new Point(0, from.Height / 2)).Y) <= page.LogicalToDeviceUnits(2));
                    var filters = Field<TableLayoutPanel>(page, "filters");
                    if (filters.GetRow(button) == filters.GetRow(Field<TableLayoutPanel>(page, "fromGroup")))
                        Assert.IsTrue(Math.Abs(button.PointToScreen(new Point(0, button.Height / 2)).Y - from.PointToScreen(new Point(0, from.Height / 2)).Y) <= page.LogicalToDeviceUnits(2));
                    Assert.IsTrue(button.Width < page.ClientSize.Width / 2);
                    Assert.IsTrue(button.Width - TextRenderer.MeasureText(button.Text, button.Font).Width >= page.LogicalToDeviceUnits(24));
                    CheckTableBounds(page);
                    using var shot = new Bitmap(page.Width, page.Height);
                    page.DrawToBitmap(shot, new Rectangle(Point.Empty, page.Size));
                    shot.Save(Path.Combine(output, $"history-{populated}-{width}-{fontSize}-{scale}-{skin}-{palette}.png"), ImageFormat.Png);
                    await page.StopAsync();
                }
            }
        }
        finally
        {
            DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
        }
    });

    [TestMethod]
    public Task ProtectedSipReplaySurvivesLostAcknowledgementAndPrecedesSqliteRecovery() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        var persistence = services.GetRequiredService<ISipCallPersistence>();
        var database = services.GetRequiredService<ApplicationConfigurationService>().Current.Options.Database;
        var origin = await services.GetRequiredService<ICallOriginProvider>().CaptureAsync();
        var now = DateTimeOffset.UtcNow.AddSeconds(-20);
        var ringing = new CallSnapshot { Id = Guid.NewGuid(), Revision = 1, Origin = origin, Direction = CallDirection.Incoming, State = CallState.Ringing,
            LocalAddress = new(NumberPresentation.Available, "isolated-line"), RemoteAddress = new(NumberPresentation.Withheld),
            StartedAtUtc = now, StateChangedAtUtc = now };
        var connected = ringing with { Revision = 2, State = CallState.Connected, AnsweredAtUtc = now.AddSeconds(3), StateChangedAtUtc = now.AddSeconds(3) };
        var ended = connected with { Revision = 3, State = CallState.Ended, EndedAtUtc = now.AddSeconds(10), StateChangedAtUtc = now.AddSeconds(10), EndReason = CallEndReason.RemoteHangup };
        var unfinished = ringing with { Id = Guid.NewGuid() };
        using (var original = new ProtectedSipCallOutbox(workspace.Paths, workspace.Secrets, workspace.Application))
        {
            await original.OpenAsync(database);
            foreach (var call in new[] { ringing, connected, ended, unfinished }) await original.AppendAsync(call);
            // The first commit succeeded but the process stopped before acknowledging its journal entry.
            await persistence.SaveAsync(ringing);
        }
        using (var restarted = new ProtectedSipCallOutbox(workspace.Paths, workspace.Secrets, workspace.Application))
        {
            foreach (var call in await restarted.OpenAsync(database))
            {
                await persistence.SaveAsync(call);
                restarted.Acknowledge(call);
            }
            await persistence.RecoverAsync();
        }
        var completed = (await workspace.History.GetCallAsync(ended.Id))!;
        Assert.AreEqual(ended, completed.Call);
        Assert.HasCount(3, completed.Events);
        Assert.AreEqual(TimeSpan.FromSeconds(7), completed.Call.EndedAtUtc - completed.Call.AnsweredAtUtc);
        var interrupted = (await workspace.History.GetCallAsync(unfinished.Id))!;
        Assert.AreEqual(CallEndReason.Interrupted, interrupted.Call.EndReason);
        Assert.AreEqual(origin, interrupted.Call.Origin);
        Assert.HasCount(2, interrupted.Events);
    });

    [TestMethod]
    public Task SipStatePersistenceRetainsOriginAndRecoversOnlyUnfinishedWorkstationCalls() => WithWorkspace(async (workspace, services, plugin) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        var persistence = services.GetRequiredService<ISipCallPersistence>();
        var now = DateTimeOffset.UtcNow;
        var initial = new CallSnapshot { Id = Guid.NewGuid(), Revision = 0, Direction = CallDirection.Incoming, State = CallState.Created,
            LocalAddress = new(NumberPresentation.Available, "workstation"), RemoteAddress = new(NumberPresentation.Withheld),
            StartedAtUtc = now, StateChangedAtUtc = now };
        var saved = await persistence.SaveAsync(initial);
        Assert.IsNotNull(saved.Origin);
        var ringing = saved with { Revision = 1, State = CallState.Ringing, StateChangedAtUtc = now.AddSeconds(1) };
        await persistence.SaveAsync(ringing);
        var repository = services.GetRequiredService<ICallRepository>();
        var beforeRetry = (await repository.GetAsync(initial.Id))!;
        Assert.AreEqual(ringing, await persistence.SaveAsync(ringing));
        Assert.AreEqual(beforeRetry.Version, (await repository.GetAsync(initial.Id))!.Version);
        await persistence.RecoverAsync();
        var history = await workspace.History.GetCallAsync(initial.Id);
        Assert.IsNotNull(history);
        Assert.AreEqual(CallState.Failed, history.Call.State);
        Assert.AreEqual(CallEndReason.Interrupted, history.Call.EndReason);
        Assert.AreEqual(saved.Origin, history.Call.Origin);
        Assert.AreEqual(2L, history.Call.Revision);
        Assert.HasCount(2, history.Events);
        Assert.AreEqual(history.Call, await persistence.SaveAsync(ringing));
        Assert.HasCount(2, (await workspace.History.GetCallAsync(initial.Id))!.Events);
        await persistence.RecoverAsync();
        Assert.AreEqual(2L, (await workspace.History.GetCallAsync(initial.Id))!.Call.Revision);
    });

    [TestMethod]
    public Task DatabaseRibbonBlocksConcurrentCommandsAndShutdownAwaitsCancellation() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        form.PrepareForBackgroundRun();
        _ = form.Handle;
        typeof(Form).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [EventArgs.Empty]);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<CancellationToken, Task> pending = async token =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            entered.SetResult();
            await release.Task;
            token.ThrowIfCancellationRequested();
        };
        var running = (Task)typeof(MainForm).GetMethod("RunDatabaseAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [pending])!;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsTrue(new[] { "databaseStatusItem", "databaseSetupItem", "databaseBackupItem", "databaseRestoreItem" }
                .All(name => !Field<DevExpress.XtraBars.BarButtonItem>(form, name).Enabled));
            Field<DevExpress.XtraBars.BarButtonItem>(form, "databaseSetupItem").PerformClick();
            Assert.IsFalse(File.Exists(Path.Combine(workspace.Paths.RootDirectory, "communications.db")));
            var closing = form.PrepareToCloseAsync();
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsFalse(closing.IsCompleted, "Shutdown must await the database operation before disposing its controls.");
            release.TrySetResult();
            await closing;
            await running;
        }
        finally { release.TrySetResult(); await running; }
    });

    [TestMethod]
    public Task NavigationReusesTabsAndCompanyBoundPluginViewsAreDisposedBeforeShutdown() => WithWorkspace(async (workspace, services, plugin) =>
    {
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>());
        _ = form.Handle;
        typeof(Form).GetMethod("OnLoad", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, [EventArgs.Empty]);
        foreach (var name in new[] { "database", "history", "help", "about", "database" }) form.Navigate(name);
        Assert.AreSame(Field<DevExpress.XtraBars.Ribbon.RibbonPage>(form, "databaseRibbon"), Field<DevExpress.XtraBars.Ribbon.RibbonControl>(form, "ribbon").SelectedPage);
        Assert.AreEqual(4, Field<DevExpress.XtraBars.Docking2010.Views.Tabbed.TabbedView>(form, "tabbedView").Documents.Count);
        var navigation = Field<DevExpress.XtraBars.Navigation.AccordionControl>(form, "navigation");
        CollectionAssert.AreEqual(new[] { "Startseite", "Gesprächshistorie", "Einstellungen" }, navigation.Elements.Select(element => element.Text).ToArray());
        Assert.IsFalse(Field<DevExpress.XtraBars.Ribbon.RibbonControl>(form, "ribbon").Items.OfType<DevExpress.XtraBars.BarButtonItem>().Any(item => item.Caption.Contains("Plugin", StringComparison.OrdinalIgnoreCase)));
        using var navigationImage = new Bitmap(navigation.Width, navigation.Height);
        navigation.DrawToBitmap(navigationImage, new Rectangle(Point.Empty, navigation.Size));
        var navigationOutput = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(RenderOutput()))!, "lx-startup-log");
        navigationImage.Save(Path.Combine(navigationOutput, "Navigation-text.png"), ImageFormat.Png);
        Assert.AreEqual(0, plugin.Created);
        var company = new Company(new(LexwareProductLine.Pro, "fixture", "1"), "Testmandant");
        await workspace.Companies.SelectAsync(company);
        await form.OpenPartyAsync(new(company.Key, PartyKind.Customer, "1"));
        Assert.AreEqual(2, plugin.Created);
        Assert.AreEqual(6, Field<DevExpress.XtraBars.Docking2010.Views.Tabbed.TabbedView>(form, "tabbedView").Documents.Count);
        await form.PrepareToCloseAsync();
        Assert.AreEqual(2, plugin.Disposed);
        Assert.IsFalse(form.Visible);
        Assert.IsTrue(new[] { "homeItem", "historyItem", "closeItem", "trayItem", "exitItem", "databaseStatusItem", "databaseSetupItem", "databaseBackupItem", "databaseRestoreItem", "settingsItem", "resetItem", "fontItem", "helpItem", "aboutItem" }
            .All(name => Field<DevExpress.XtraBars.BarButtonItem>(form, name).ImageOptions.SvgImage is not null));
    });

    [TestMethod]
    public Task ClosingAPageCancelsAndAwaitsItsOutstandingWork() => RunStaAsync(async () =>
    {
        using var owner = new Panel(); using var label = new LabelControl(); using var operation = new PageOperation(owner, label);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = operation.RunAsync(async token =>
        {
            started.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { await released.Task; }
        });
        await started.Task;
        var stop = operation.StopAsync();
        Assert.IsFalse(stop.IsCompleted);
        released.SetResult();
        await stop; await work;
        Assert.IsFalse(operation.IsRunning);
    });

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public Task SettingsClosesAfterOneRequestAndAwaitsPendingWork(bool useButton, bool pendingWork) => RunStaAsync(async () =>
    {
        using var dialog = new FrmSettings();
        dialog.PrepareForBackgroundRun();
        _ = dialog.Handle;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task CheckCloseAsync()
        {
            try
            {
                var page = Field<SettingsPage>(dialog, "settingsPage");
                using var operation = new PageOperation(page, Field<LabelControl>(page, "statusLabel"));
                typeof(SettingsPage).GetField("_operation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(page, operation);
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var work = pendingWork ? operation.RunAsync(async token =>
                {
                    try { await Task.Delay(Timeout.Infinite, token); }
                    finally { await release.Task; }
                }) : Task.CompletedTask;
                if (useButton) Field<SimpleButton>(dialog, "closeButton").PerformClick();
                else dialog.Close();
                if (pendingWork)
                {
                    Assert.IsTrue(dialog.Visible);
                    release.SetResult();
                }
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while (dialog.Visible && DateTime.UtcNow < deadline) await Task.Delay(10);
                Assert.IsFalse(dialog.Visible, "A single close request must close the modal dialog after its work stops.");
                await work;
                completed.TrySetResult();
            }
            catch (Exception exception) { completed.TrySetException(exception); }
            finally { if (dialog.Visible) dialog.Dispose(); }
        }
        dialog.BeginInvoke(new Action(() => _ = CheckCloseAsync()));
        dialog.ShowDialog();
        await completed.Task;
    });

    private sealed class PendingDatabaseSetup(WawiCallPro.Utilities.Persistence.IDatabaseSetupService inner) : WawiCallPro.Utilities.Persistence.IDatabaseSetupService
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ConfigurationSnapshot> LoadAsync(CancellationToken cancellationToken = default) => inner.LoadAsync(cancellationToken);
        public WawiCallPro.Utilities.Persistence.DatabaseSetupDraft ReadDraft(ConfigurationSnapshot snapshot) => inner.ReadDraft(snapshot);
        public async Task<WawiCallPro.Utilities.Persistence.DatabaseConnectionTestResult> TestAsync(WawiCallPro.Utilities.Persistence.DatabaseSetupDraft draft, CancellationToken cancellationToken = default)
        {
            using var registration = cancellationToken.Register(() => Cancelled.TrySetResult());
            Entered.TrySetResult();
            await Release.Task;
            cancellationToken.ThrowIfCancellationRequested();
            return new(true, "Completed");
        }
        public Task<DatabaseMigrationStatus> InspectAsync(WawiCallPro.Utilities.Persistence.DatabaseSetupDraft draft, CancellationToken cancellationToken = default) => inner.InspectAsync(draft, cancellationToken);
        public Task<DatabaseMigrationStatus> InitializeAsync(WawiCallPro.Utilities.Persistence.DatabaseSetupDraft draft, bool approveEmpty, IProgress<DatabaseMigrationProgress>? progress = null, CancellationToken cancellationToken = default) => inner.InitializeAsync(draft, approveEmpty, progress, cancellationToken);
        public Task SaveAsync(WawiCallPro.Utilities.Persistence.DatabaseSetupDraft draft, ConfigurationSnapshot expected, CancellationToken cancellationToken = default) => inner.SaveAsync(draft, expected, cancellationToken);
    }

    private static Task WithWorkspace(Func<AppWorkspace, IServiceProvider, TestPlugin, Task> action, ITelephonyService? telephony = null) => RunStaAsync(async () =>
    {
        var root = Path.Combine(Path.GetTempPath(), "WawiCallPro-shell-" + Guid.NewGuid().ToString("N"));
        var plugin = new TestPlugin();
        using var context = new WindowsFormsSynchronizationContext();
        var host = WawiCallHost.CreateBuilder(typeof(MainForm).Assembly).ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(root));
            services.AddSingleton<IHostLifetime, AppHostLifetime>();
            services.AddSingleton<IUiDispatcher>(new SynchronizationContextUiDispatcher(context));
            services.AddSingleton<IBusinessDirectory, TestDirectory>();
            WawiCallPro.Utilities.Persistence.PhoneSearchIndexRegistration.AddPhoneSearchIndex(services);
            services.AddSingleton<IProductPluginRuntime>(new TestRuntime(plugin));
            services.AddSingleton<WawiCallPro.Interfaces.PhoneNumbers.IPhoneCacheService, WawiCallPro.Utilities.Persistence.PhoneCacheService>();
            services.AddSingleton<IPluginViewAccess, PluginViewAccess>();
            services.AddSingleton(new StartupExecutionMode(true));
            services.AddSingleton<AppUiState>(); services.AddSingleton<AppWorkspace>();
            services.AddSingleton<CallNotesSessionFactory>();
                services.AddSipTelephony(false);
            if (telephony is not null) services.AddSingleton(telephony);
        }).Build();
        try
        {
            await host.StartAsync();
            await action(host.Services.GetRequiredService<AppWorkspace>(), host.Services, plugin);
        }
        finally
        {
            await host.StopAsync();
            await ((IAsyncDisposable)host).DisposeAsync();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    });

    private static async Task ClickRibbonAsync(MainForm form, string item)
    {
        Field<DevExpress.XtraBars.BarButtonItem>(form, item).PerformClick();
        await WaitForOperationAsync(form);
    }

    private static async Task InvokeActionAsync(Control page, string handler)
    {
        page.GetType().GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, [null, EventArgs.Empty]);
        await WaitForOperationAsync(page);
    }
    private static async Task WaitForOperationAsync(Control page)
    {
        var operation = Field<PageOperation>(page, "_operation");
        var timeout = DateTime.UtcNow.AddSeconds(15);
        while (operation.IsRunning && DateTime.UtcNow < timeout) await Task.Delay(10);
        Assert.IsFalse(operation.IsRunning, "UI action did not finish.");
    }
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
    private static T Find<T>(Control parent, string name) where T : Control => parent.Controls.Find(name, true).OfType<T>().Single();
    private static async Task RunStaAsync(Func<Task> action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);
            EventHandler? start = null;
            start = async (_, _) =>
            {
                Application.Idle -= start;
                try { await action(); done.SetResult(); }
                catch (Exception exception) { done.SetException(exception); }
                finally { Application.ExitThread(); }
            };
            Application.Idle += start;
            Application.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(40));
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(2)));
    }
    private sealed class TestDirectory(ApplicationDescriptor application) : IBusinessDirectory
    {
        internal Func<PartyQuery, Task<PartyPage>>? Read;
        internal Func<CancellationToken, Task<ImmutableArray<Company>>>? ReadCatalog;
        internal Func<PartyKey, CancellationToken, Task<Party?>>? ReadParty;
        private LexwareProductLine ProductLine => Enum.Parse<LexwareProductLine>(application.ProductLine);
        public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default) => ReadCatalog?.Invoke(cancellationToken) ?? Task.FromResult(ImmutableArray.Create(
            new Company(new(ProductLine, "installation-A", "1"), "Firma A"),
            new Company(new(ProductLine, "installation-B", "1"), "Firma B")));
        public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default) => Read is not null ? Read(query) : Task.FromResult(new PartyPage(query.Company, new([new Party
        {
            Key = new(query.Company, PartyKind.Customer, "01"), DisplayName = "Testkunde",
            Phones = [new("+49 30 1234567", PhoneKind.Telephone, new("fixture", "01", "telephone"))]
        }], false)));
        public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default) => ReadParty?.Invoke(key, cancellationToken) ?? Task.FromResult<Party?>(new() { Key = key, DisplayName = "Aktuelle Stammdaten", Number = "100" });
    }
    private sealed class TestRuntime(TestPlugin plugin) : IProductPluginRuntime, IProductPluginSelection, IProductPluginAvailability
    {
        internal bool BusinessAvailable = true;
        public bool CanUseActivePlugin => BusinessAvailable || ActivePluginId == "Test.Dummy";
        public bool CanActivate(string id) => BusinessAvailable || id == "Test.Dummy";
        internal bool HasCatalog = true;
        public bool IsLoaded => true;
        public string? ActivePluginId { get; private set; } = "Test.Lexware";
        public IReadOnlyList<ProductPluginChoice> GetAvailablePlugins() => !HasCatalog ? [] : [new("Test.Lexware", "Lexware – Testadapter", "1.0"), new("Test.Dummy", "Dummy-Plugin (Testbetrieb)", "1.0")];
        public Task SwitchAsync(string id, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); ActivePluginId = id; return Task.CompletedTask; }
        public Task<T> UseAsync<T>(Func<IWawiCallPlugin, CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) => operation(plugin, cancellationToken);
    }
    private sealed class TestPlugin : IWawiCallPlugin, IWawiCallPluginViews, IAsyncWawiCallPluginViews
    {
        internal int Created;
        internal int Disposed;
        internal Func<string, PartyKey, CancellationToken, Task<IPluginViewLease>>? CreateAsync;
        internal readonly List<Control> CreatedControls = [];
        internal readonly List<PartyKey> CreatedParties = [];
        public string Name => "Isoliertes Ansichtsplugin";
        public Version Version { get; } = new(1, 0);
        public LexwareProductLine ProductLine => LexwareProductLine.Pro;
        public IBusinessDirectory Directory => throw new NotSupportedException();
        public void Initialize(Quantarix.Plugin.Abstractions.IPluginHost host) { }
        public void Configure(IWawiCallHostServices services) { }
        public void Execute() => throw new NotSupportedException();
        public void Shutdown() { }
        public IReadOnlyList<PluginViewDescriptor> GetViews() => [new("master", "Stammdaten"), new("orders", "Aufträge")];
        public IPluginViewLease CreateView(string id, PartyKey party)
        { Created++; var panel = new Panel(); CreatedControls.Add(panel); CreatedParties.Add(party); return new Lease(panel, () => Disposed++); }
        public Task<IPluginViewLease> CreateViewAsync(string id, PartyKey party, CancellationToken cancellationToken) =>
            CreateAsync?.Invoke(id, party, cancellationToken) ?? Task.FromResult(CreateView(id, party));
        private sealed class Lease(Control view, Action disposed) : IPluginViewLease, IPluginViewEditState
        {
            public Control View => view;
            public bool HasUnsavedChanges => view.Tag is true;
            public void Dispose() { view.Dispose(); disposed(); }
        }
    }
}
