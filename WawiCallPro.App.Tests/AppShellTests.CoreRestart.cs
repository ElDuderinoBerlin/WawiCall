using System.Collections.Immutable;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Core.Threading;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Persistence;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task CoreWorkflowPersistsThreeCallKindsAcrossFreshHostRestart() => RunStaAsync(async () =>
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wcp-core-restart-" + Guid.NewGuid().ToString("N")));
        using var context = new WindowsFormsSynchronizationContext();
        var expected = new List<(CallSnapshot Call, PartyKey Party, Guid Note, string Text, ImmutableArray<CallEvent> Events)>();
        Guid? identity = null; byte[]? configuration = null; object? firstHistory = null;
        try
        {
            var phone = new CoreWorkflowPhone();
            var first = BuildCoreWorkflowHost(root, context, phone);
            try
            {
                await first.StartAsync(); var services = first.Services; var workspace = services.GetRequiredService<AppWorkspace>();
                await SeedCallerCachesAsync(workspace, services);
                identity = await services.GetRequiredService<IWorkstationIdentity>().GetAsync(); Assert.IsNotNull(identity);
                configuration = File.ReadAllBytes(new ConfigurationLocation(root).FilePath);
                firstHistory = workspace.History;
                var companies = await workspace.Directory.GetCompaniesAsync(); await workspace.Companies.SelectAsync(companies[0]);
                var uiState = services.GetRequiredService<AppUiState>();
                var searchFactory = new PartySearchWorkspaceFactory(workspace.Directory, new SearchAdapter(workspace.Directory), new PartySearchPresentation(uiState), workspace.Dispatcher);
                using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, uiState,
                    new LogWindowService(), new AudioCatalogDouble(), new UnconfiguredMailViewSource(),
                    new PartySearchWorkspaceAccess(workspace.Plugins, searchFactory, workspace.Dispatcher));
                form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
                form.Navigate("parties"); await Field<Task>(form, "_selectionTask");
                var search = (PartySearchPage)form.GetActiveDocumentView()!; await search.ReloadAsync();
                Field<LookUpEdit>(search, "companyEdit").EditValue = companies[0].Key;
                Field<GridView>(search, "view").FindFilterText = "Testkunde"; await search.SearchAsync();
                Assert.AreEqual(1, Field<GridView>(search, "view").DataRowCount);
                var persistence = services.GetRequiredService<ISipCallPersistence>();
                async Task Publish(CallSnapshot snapshot)
                {
                    phone.Current = await persistence.SaveAsync(snapshot); await form.ObserveCallAsync(phone.Current);
                }
                async Task<CallSnapshot> Begin(CallDirection direction)
                {
                    var now = DateTimeOffset.UtcNow.AddMinutes(-2);
                    var created = await services.GetRequiredService<ICallJournal>().BeginAsync(new() { Id = Guid.NewGuid(), Revision = 0,
                        State = CallState.Created, Direction = direction, StartedAtUtc = now, StateChangedAtUtc = now,
                        LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "+49301234567") });
                    await Publish(created with { Revision = 1, State = direction == CallDirection.Outgoing ? CallState.Dialing : CallState.Ringing,
                        StateChangedAtUtc = now.AddSeconds(1) }); return phone.Current!;
                }
                phone.Dial = async (company, target) =>
                {
                    Assert.AreEqual(companies[1].Key, company); Assert.AreEqual("+49301234567", target);
                    return (await Begin(CallDirection.Outgoing)).Id;
                };
                phone.Command = async (command, id) =>
                {
                    var call = phone.Current!; Assert.AreEqual(call.Id, id);
                    await Publish(call with { Revision = call.Revision + 1, State = command == "answer" ? CallState.Connected : CallState.Ended,
                        StateChangedAtUtc = call.StartedAtUtc.AddSeconds(command == "answer" ? 3 : 13),
                        AnsweredAtUtc = command == "answer" ? call.StartedAtUtc.AddSeconds(3) : call.AnsweredAtUtc,
                        EndedAtUtc = command == "answer" ? null : call.StartedAtUtc.AddSeconds(13),
                        EndReason = command == "answer" ? null : CallEndReason.Completed });
                };
                foreach (var kind in new[] { "incoming", "outgoing", "missed" })
                {
                    CallSnapshot call;
                    if (kind == "outgoing")
                    {
                        using var dial = form.CreateDialDialog(); dial.PrepareForBackgroundRun(); await dial.ReloadAsync();
                        Field<LookUpEdit>(dial, "companyEdit").EditValue = companies[1].Key;
                        Field<TextEdit>(dial, "destinationEdit").Text = "+49301234567"; await dial.DialAsync();
                        Assert.AreEqual(phone.Current!.Id, dial.CallId); call = phone.Current; await dial.StopAsync();
                    }
                    else call = await Begin(CallDirection.Incoming);
                    var pages = Field<Dictionary<Guid, (DevExpress.XtraBars.Docking2010.Views.BaseDocument Document, CallDetailsPage Page)>>(form, "_callPages");
                    var page = pages[call.Id].Page;
                    var popup = form.OwnedForms.OfType<FrmCallerLookup>().Single(item => item.CallId == call.Id);
                    Assert.AreEqual(CallerMatchDecisionKind.Ambiguous, popup.Page.Decision!.Kind);
                    var matches = Field<GridView>(popup.Page, "view"); matches.FocusedRowHandle = matches.LocateByValue("Name", "Firma B Kontakt");
                    await InvokeActionAsync(popup.Page, "Choose_Click");
                    var selected = popup.Page.Decision!.Selection!; Assert.AreEqual(CallerMatchSelectionKind.Manual, selected.Kind);
                    Assert.AreEqual(companies[1].Key, selected.Party.Company); Assert.AreEqual(companies[0].Key, workspace.Companies.Current.Company!.Key);
                    var controls = Field<CallControlPanel>(page, "callControls");
                    if (kind == "incoming") await InvokeActionAsync(controls, "Answer_Click");
                    if (kind == "outgoing") await Publish(phone.Current! with { Revision = phone.Current!.Revision + 1,
                        State = CallState.Connected, AnsweredAtUtc = call.StartedAtUtc.AddSeconds(3), StateChangedAtUtc = call.StartedAtUtc.AddSeconds(3) });
                    var notes = Field<CallNotesPanel>(popup.Page, "notesPanel");
                    var text = "A1 " + kind + " · Ä 电话"; Field<MemoEdit>(notes, "draftEdit").Text = text;
                    typeof(CallNotesPanel).GetMethod("Save_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(notes, [null, EventArgs.Empty]);
                    await page.Notes.PendingOperation;
                    Assert.IsFalse(page.HasUnsavedNotes);
                    if (kind == "missed") await Publish(phone.Current! with { Revision = phone.Current!.Revision + 1, State = CallState.Ended,
                        StateChangedAtUtc = call.StartedAtUtc.AddSeconds(13), EndedAtUtc = call.StartedAtUtc.AddSeconds(13), EndReason = CallEndReason.Missed });
                    else await InvokeActionAsync(controls, "End_Click");
                    var stored = (await workspace.History.GetCallAsync(call.Id))!;
                    Assert.AreEqual(CallState.Ended, stored.Call.State); Assert.AreEqual(kind == "missed" ? CallEndReason.Missed : CallEndReason.Completed, stored.Call.EndReason);
                    Assert.AreEqual(text, stored.Notes.Single().Text);
                    Assert.AreEqual(selected.Party, stored.Parties.Single(item => item.IsSelected).Party);
                    Assert.IsTrue(stored.Events.Length > 0);
                    Assert.AreEqual(stored.Events.Length, stored.Events.Select(item => item.Sequence).Distinct().Count());
                    expected.Add((stored.Call, selected.Party, stored.Notes.Single().Id, text, stored.Events));
                    // Replaying the same final observation must not insert another call or note.
                    await persistence.SaveAsync(stored.Call); await form.ObserveCallAsync(stored.Call);
                    popup.Close(); await WaitForDialStateAsync(() => popup.IsDisposed);
                }
                Assert.AreEqual(3, (await workspace.History.GetCallsAsync(new CallHistoryQuery(new PageRequest(0, 100)))).Items.Length);
                await form.PrepareToCloseAsync();
            }
            finally { await first.StopAsync(); await ((IAsyncDisposable)first).DisposeAsync(); }

            var second = BuildCoreWorkflowHost(root, context, new CoreWorkflowPhone());
            try
            {
                await second.StartAsync(); var workspace = second.Services.GetRequiredService<AppWorkspace>();
                Assert.AreNotSame(firstHistory, workspace.History); CollectionAssert.AreEqual(configuration!, File.ReadAllBytes(new ConfigurationLocation(root).FilePath));
                Assert.AreEqual(identity, await second.Services.GetRequiredService<IWorkstationIdentity>().GetAsync());
                Assert.HasCount(2, (await workspace.Settings.LoadAsync()).Options.Companies.Where(item => item.IncludeInPhoneCache).ToArray());
                using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, second.Services.GetRequiredService<AppUiState>());
                form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents(); form.Navigate("history");
                var history = (HistoryPage)form.GetActiveDocumentView()!; await InvokeActionAsync(history, "Refresh_Click");
                var grid = Field<GridView>(history, "view"); Assert.AreEqual(3, grid.DataRowCount);
                var all = await workspace.History.GetCallsAsync(new CallHistoryQuery(new PageRequest(0, 100))); Assert.AreEqual(3, all.Items.Length); Assert.IsFalse(all.HasMore);
                foreach (var item in expected)
                {
                    var stored = (await workspace.History.GetCallAsync(item.Call.Id))!;
                    Assert.AreEqual(item.Call, stored.Call); Assert.AreEqual(item.Party, stored.Parties.Single(party => party.IsSelected).Party);
                    Assert.AreEqual(item.Note, stored.Notes.Single().Id); Assert.AreEqual(item.Text, stored.Notes.Single().Text);
                    CollectionAssert.AreEqual(item.Events.ToArray(), stored.Events.ToArray());
                    grid.FocusedRowHandle = grid.LocateByValue("Id", item.Call.Id); await WaitForOperationAsync(history);
                    Assert.Contains(item.Text, Field<MemoEdit>(history, "detailsEdit").Text);
                    if (item.Call.EndReason == CallEndReason.Missed) Assert.IsNull(item.Call.AnsweredAtUtc);
                    else Assert.AreEqual(TimeSpan.FromSeconds(10), item.Call.EndedAtUtc - item.Call.AnsweredAtUtc);
                }
                await form.PrepareToCloseAsync();
            }
            finally { await second.StopAsync(); await ((IAsyncDisposable)second).DisposeAsync(); }
        }
        finally
        {
            if (!Path.GetFullPath(root).StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("wcp-core-restart-", StringComparison.Ordinal)) throw new AssertFailedException();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    });

    private static IHost BuildCoreWorkflowHost(string root, SynchronizationContext context, CoreWorkflowPhone phone) =>
        WawiCallHost.CreateBuilder(typeof(MainForm).Assembly).ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(root)); services.AddSingleton<IHostLifetime, AppHostLifetime>();
            services.AddSingleton<IUiDispatcher>(new SynchronizationContextUiDispatcher(context)); services.AddSingleton<IBusinessDirectory, TestDirectory>();
            PhoneSearchIndexRegistration.AddPhoneSearchIndex(services); services.AddSingleton<IProductPluginRuntime>(new TestRuntime(new TestPlugin()));
            services.AddSingleton<WawiCallPro.Interfaces.PhoneNumbers.IPhoneCacheService, PhoneCacheService>(); services.AddSingleton<IPluginViewAccess, PluginViewAccess>();
            services.AddSingleton(new StartupExecutionMode(true)); services.AddSingleton<AppUiState>(); services.AddSingleton<AppWorkspace>(); services.AddSingleton<CallNotesSessionFactory>();
            WawiCallPro.Office.MailServiceRegistration.AddMailConnectors(services);
            services.AddSingleton<IAutoStartRegistry, StartupRegistryDouble>(); services.AddSingleton<IAppAutoStart>(provider => new WindowsAppAutoStart(provider.GetRequiredService<IAutoStartRegistry>(),
                new("WawiCallPro.App.Test", "\"C:\\Fixture App\\WawiCallPro.App.Test.exe\""), new StartupExecutionMode(true)));
            services.AddSipTelephony(false); services.AddSingleton<ITelephonyService>(phone);
        }).Build();

    private sealed class CoreWorkflowPhone : ITelephonyService
    {
        internal CallSnapshot? Current;
        internal Func<CompanyKey, string, Task<Guid>>? Dial;
        internal Func<string, Guid, Task>? Command;
        public RegistrationSnapshot Registration { get; } = new(RegistrationState.Registered, DateTimeOffset.UtcNow);
        public event EventHandler<RegistrationChangedEventArgs>? RegistrationChanged { add { } remove { } }
        public event EventHandler<CallChangedEventArgs>? CallChanged { add { } remove { } }
        public ImmutableArray<CallSnapshot> GetCalls() => Current is null ? [] : [Current];
        public Task<Guid> DialAsync(CompanyKey company, string target, CancellationToken token = default) => Dial!(company, target);
        public Task AnswerAsync(Guid id, CancellationToken token = default) => Command!("answer", id);
        public Task HangUpAsync(Guid id, CancellationToken token = default) => Command!("end", id);
        public Task RejectAsync(Guid id, CancellationToken token = default) => throw new NotSupportedException();
        public Task HoldAsync(Guid id, CancellationToken token = default) => throw new NotSupportedException();
        public Task ResumeAsync(Guid id, CancellationToken token = default) => throw new NotSupportedException();
        public Task TransferAsync(Guid id, string target, CancellationToken token = default) => throw new NotSupportedException();
        public Task SendDtmfAsync(Guid id, char digit, CancellationToken token = default) => throw new NotSupportedException();
    }
}
