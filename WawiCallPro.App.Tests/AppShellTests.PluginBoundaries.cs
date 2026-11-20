using System.Collections.Immutable;
using System.Reflection;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Plugin.Windows;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public Task BlockedLexwareStillAllowsHistoryAndIncomingCallCommands(bool registryFailure)
    {
        var phone = new CommandTelephony();
        return WithWorkspace(async (workspace, services, _) =>
        {
            await workspace.Database.CreateAndMigrateAsync(); var call = await CreateWorkspaceCallAsync(services);
            call = await services.GetRequiredService<ISipCallPersistence>().SaveAsync(call with { Revision = 1, State = CallState.Ringing, StateChangedAtUtc = call.StartedAtUtc.AddSeconds(1) });
            ((TestRuntime)workspace.Plugins).BusinessAvailable = false;
            var logs = new LogWindowService(); using var loggers = LoggerFactory.Create(builder => builder.AddProvider(new UiLogProvider(logs)));
            var installation = new LexwareInstallation(new MissingRegistry(registryFailure), loggers.CreateLogger<LexwareInstallation>());
            installation.Check(); Assert.IsTrue(logs.Drain().Any(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Lexware", StringComparison.Ordinal)));
            ((TestDirectory)workspace.Directory).ReadCatalog = _ => throw new AssertFailedException("Business source must remain blocked.");
            using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>(), logs);
            form.PrepareForBackgroundRun(); form.Show(); Application.DoEvents();
            foreach (var name in new[] { "partiesItem", "callerItem", "dialItem" }) Assert.IsFalse(Field<DevExpress.XtraBars.BarButtonItem>(form, name).Enabled);
            form.Navigate("history"); var history = (HistoryPage)form.GetActiveDocumentView()!; await InvokeActionAsync(history, "Refresh_Click");
            Assert.AreEqual(1, Field<GridView>(history, "view").DataRowCount);
            phone.Current = call; await form.ObserveCallAsync(call);
            var pages = Field<Dictionary<Guid, (DevExpress.XtraBars.Docking2010.Views.BaseDocument Document, CallDetailsPage Page)>>(form, "_callPages");
            var controls = Field<CallControlPanel>(pages[call.Id].Page, "callControls");
            Assert.IsTrue(Field<SimpleButton>(controls, "answerButton").Enabled); await InvokeActionAsync(controls, "Answer_Click");
            Assert.AreEqual(("Answer", call.Id), phone.Commands.Single());
            phone.Current = null; await form.PrepareToCloseAsync();
        }, phone);
    }

    [TestMethod]
    [DataRow(false)][DataRow(true)]
    public Task PluginSwitchWaitsForSearchAndKeepsThePreviouslyAssignedCall(bool lateFailure) => WithWorkspace(async (workspace, services, plugin) =>
    {
        await SeedCallerCachesAsync(workspace, services); var companies = await workspace.Directory.GetCompaniesAsync();
        await workspace.Companies.SelectAsync(companies[0]);
        var factory = new PartySearchWorkspaceFactory(workspace.Directory, new SearchAdapter(workspace.Directory), new PartyPresentation(), workspace.Dispatcher);
        using var form = new MainForm(workspace.Application, workspace.Configuration, workspace, services.GetRequiredService<AppUiState>(),
            new LogWindowService(), new AudioCatalogDouble(), new UnconfiguredMailViewSource(), new PartySearchWorkspaceAccess(workspace.Plugins, factory, workspace.Dispatcher));
        form.PrepareForBackgroundRun(); form.Show(); form.Navigate("parties"); await Field<Task>(form, "_selectionTask");
        var search = (PartySearchPage)form.GetActiveDocumentView()!; await search.ReloadAsync();
        Field<LookUpEdit>(search, "companyEdit").EditValue = companies[0].Key;
        Field<GridView>(search, "view").ApplyFindFilter("Testkunde"); await search.SearchAsync(); Assert.AreEqual(1, Field<GridView>(search, "view").DataRowCount);
        var call = await CreateWorkspaceCallAsync(services); await form.ObserveCallAsync(call);
        var popup = form.OwnedForms.OfType<FrmCallerLookup>().Single(); var matches = Field<GridView>(popup.Page, "view");
        Assert.HasCount(2, popup.Page.Decision!.Groups); Assert.HasCount(6, popup.Page.Decision.Groups.SelectMany(group => group.Candidates).ToArray());
        matches.FocusedRowHandle = matches.LocateByValue("Name", "Firma B Kontakt"); await InvokeActionAsync(popup.Page, "Choose_Click");
        var selected = popup.Page.Decision!.Selection!.Party; Assert.AreEqual(PartyKind.SupplierContact, selected.Kind); Assert.AreEqual(companies[1].Key, selected.Company);
        Assert.AreEqual(companies[0].Key, workspace.Companies.Current.Company!.Key); Assert.AreSame(search, form.GetActiveDocumentView());
        var saved = await services.GetRequiredService<ISipCallPersistence>().SaveAsync(call with { Revision = 1, State = CallState.Ended,
            StateChangedAtUtc = call.StartedAtUtc.AddSeconds(3), EndedAtUtc = call.StartedAtUtc.AddSeconds(3), EndReason = CallEndReason.Missed });
        await form.ObserveCallAsync(saved); var before = (await workspace.History.GetCallAsync(call.Id))!;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<PartyPage>(TaskCreationOptions.RunContinuationsAsynchronously); PartyQuery? pendingQuery = null;
        var directory = (TestDirectory)workspace.Directory;
        directory.Read = query => { pendingQuery = query; entered.TrySetResult(); return release.Task; };
        var loading = search.SearchAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var preparing = (Task)typeof(MainForm).GetMethod("PreparePluginSwitchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null)!;
        try { Assert.IsFalse(preparing.IsCompleted); Assert.IsFalse(search.IsDisposed); }
        finally
        {
            if (lateFailure) release.TrySetException(new InvalidOperationException("private-old-source"));
            else release.TrySetResult(new(pendingQuery!.Company, new([new Party { Key = new(pendingQuery.Company, PartyKind.Customer, "late"), DisplayName = "Late old plugin" }], false)));
        }
        await loading; await preparing; Assert.IsTrue(search.IsDisposed);
        await ((TestRuntime)workspace.Plugins).SwitchAsync("Test.Dummy");
        Assert.IsNull(workspace.Companies.Current.Company); Assert.IsFalse(form.GetActiveDocumentView() is PartySearchPage);
        var after = (await workspace.History.GetCallAsync(call.Id))!;
        Assert.AreEqual(before.Call, after.Call); CollectionAssert.AreEqual(before.Parties.ToArray(), after.Parties.ToArray());
        Assert.AreEqual(selected, after.Parties.Single(party => party.IsSelected).Party);
        directory.Read = null; form.Navigate("parties"); await Field<Task>(form, "_selectionTask");
        var replacement = (PartySearchPage)form.GetActiveDocumentView()!; Assert.AreNotSame(search, replacement); await replacement.ReloadAsync();
        Assert.AreEqual(0, Field<GridView>(replacement, "view").DataRowCount); await form.PrepareToCloseAsync();
    });
}
