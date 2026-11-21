using System.Collections.Immutable;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Diagnostics;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod, DataRow("incoming"), DataRow("outgoing"), DataRow("ambiguous"), DataRow("withheld"), DataRow("foreign-outgoing"), DataRow("manual"), DataRow("late-storage"), DataRow("cancel"), DataRow("line"), DataRow("wrong-line")]
    public Task AutomationHeadlessCallsPersistAssociationsAndNotesAcrossRestart(string scenario) => RunStaAsync(async () =>
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wcp-com-history-" + Guid.NewGuid().ToString("N")));
        await using var h = await AppComTelephonyTests.Harness.CreateAsync(); var f = h.Fixture;
        var beforeForms = Application.OpenForms.Count;
        CallHistory? expected = null;
        try
        {
            var host = ComHistoryHost(root, f);
            try
            {
                await host.StartAsync(); var services = host.Services;
                await services.GetRequiredService<ICommunicationDatabase>().CreateAndMigrateAsync();
                var settings = services.GetRequiredService<ConfigurationStore>(); var saved = await settings.LoadAsync();
                await settings.SaveAsync(saved.Options with { Companies = [new() { Company = f.A, DisplayName = "Firma A", IncludeInPhoneCache = true },
                    new() { Company = f.B, DisplayName = "Firma B", IncludeInPhoneCache = true }] }, saved);
                if (scenario is "line" or "wrong-line")
                {
                    var account = new SipAccountOptions { Server = "fixture.invalid", Username = "synthetic-account",
                        Password = services.GetRequiredService<WawiCallPro.Utilities.Security.ISecretProtector>().Protect("synthetic-password", SipAccountOptions.PasswordPurpose) };
                    saved = await settings.LoadAsync();
                    await settings.SaveAsync(saved.Options with { SipAccount = account, CallerLineAssignment = CallerLineConfiguration.Create(account, f.B) }, saved);
                    f.Phone.Line = scenario == "line" ? CallerLineConfiguration.ContextFor(account) : new("other-system", "other-line");
                }
                foreach (var key in new[] { f.A, f.B })
                {
                    await services.GetRequiredService<ICompanyRepository>().WriteAsync(new(new(key, key == f.A ? "Firma A" : "Firma B"), null, DateTimeOffset.UtcNow));
                    var matches = key == f.A ? scenario != "foreign-outgoing" : scenario is "ambiguous" or "manual" or "foreign-outgoing" or "line" or "wrong-line";
                    var party = new Party { Key = new(key, key == f.A ? PartyKind.Customer : PartyKind.Supplier, "01"), Number = "00123",
                        DisplayName = key == f.A ? "Historischer Kunde" : "Historischer Lieferant",
                        Phones = [new(matches ? "+49301234567" : "+49307654321", PhoneKind.Telephone, new("fixture", "01", "phone"), matches ? "+49301234567" : "+49307654321")] };
                    await services.GetRequiredService<IPartyCacheRepository>().ReplaceAsync(new(key, 0, [party], DateTimeOffset.UtcNow,
                        services.GetRequiredService<IPhoneCacheMetadata>().ParserVersion,
                        services.GetRequiredService<IPhoneCacheMetadata>().GetContextVersion(new() { Company = key })));
                }
                var history = services.GetRequiredService<ICallHistoryService>();
                var persistence = services.GetRequiredService<ISipCallPersistence>();
                var index = services.GetRequiredService<IPhoneSearchIndexService>();
                var interpreter = services.GetRequiredService<IPhoneNumberInterpreter>();
                var decisions = services.GetRequiredService<ICallerMatchDecisionService>();
                var associations = services.GetRequiredService<ICallAssociationService>();
                f.Phone.Persist = async call =>
                {
                    if (await history.GetCallAsync(call.Id) is null)
                        await persistence.SaveAsync(call with { Revision = 0, State = CallState.Created, StateChangedAtUtc = call.StartedAtUtc });
                    await persistence.SaveAsync(call);
                    if (scenario == "manual" && call.Revision == 1)
                    {
                        await index.RefreshAsync();
                        var result = index.Find(interpreter.InterpretIncoming(call.RemoteAddress));
                        var choice = decisions.Choose(decisions.Evaluate(result, new()), new(f.B, PartyKind.Supplier, "01"));
                        await associations.SaveAsync(call.Id, choice);
                    }
                };
                var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource<ImmutableArray<Company>>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (scenario == "cancel") f.Source.Catalog = _ => { entered.TrySetResult(); return release.Task; };
                await using var matching = new AppCallAssociation(f.Service, history, index, decisions, interpreter, associations,
                    settings, f.Access, new LogWindowService(), TimeProvider.System);
                await matching.StartAsync(default);
                await AppComTelephonyTests.Done(h.App, h.App.ConnectTelephony());
                Guid id;
                if (scenario is "outgoing" or "foreign-outgoing" or "manual")
                {
                    var operation = await AppComTelephonyTests.Done(h.App, h.App.Dial("+49301234567", h.Company, Guid.NewGuid().ToString("D")));
                    Assert.AreEqual("Succeeded", operation.State, operation.Message); id = Guid.Parse(operation.CallId);
                }
                else
                {
                    var now = DateTimeOffset.UtcNow; id = Guid.NewGuid();
                    var incoming = new CallSnapshot { Id = id, Revision = 1, Direction = CallDirection.Incoming, State = CallState.Ringing,
                        StartedAtUtc = now, StateChangedAtUtc = now, LocalAddress = new(NumberPresentation.Unknown),
                        RemoteAddress = scenario == "withheld" ? new(NumberPresentation.Withheld) : new(NumberPresentation.Available, "+49301234567") };
                    if (scenario == "late-storage")
                    {
                        var persist = f.Phone.Persist!; f.Phone.Persist = null;
                        await f.Phone.PublishAsync(incoming);
                        Assert.IsNull(await history.GetCallAsync(id)); Assert.AreEqual(1, matching.PendingCount);
                        await persist(incoming); f.Phone.Persist = persist;
                    }
                    else await f.Phone.PublishAsync(incoming);
                }
                if (scenario == "cancel")
                {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    var stopped = matching.StopAsync(default); Assert.IsFalse(stopped.IsCompleted);
                    release.SetResult([new(f.A, "Firma A"), new(f.B, "Firma B")]); await stopped;
                }
                await AppComTelephonyTests.Until(() => matching.PendingCount == 0);
                var matched = (await history.GetCallAsync(id))!;
                if (scenario is "incoming" or "outgoing" or "late-storage") Assert.AreEqual(f.A, matched.Parties.Single(party => party.IsSelected).Party.Company);
                else if (scenario is "manual" or "line") Assert.AreEqual(f.B, matched.Parties.Single(party => party.IsSelected).Party.Company);
                else Assert.IsFalse(matched.Parties.Any(party => party.IsSelected));
                if (scenario == "ambiguous") Assert.AreEqual(2, matched.Parties.Length);
                if (scenario == "foreign-outgoing") Assert.AreEqual(f.A, matched.Call.Company);
                if (scenario is "withheld" or "cancel") Assert.IsNull(matched.Lookup);
                else Assert.IsNotNull(matched.Lookup);
                var note = await history.SaveNoteAsync(id, Guid.NewGuid(), "Fensterloser COM-Test · Ä", default);
                Assert.AreEqual(0, h.Presentation.Shown); Assert.AreEqual(beforeForms, Application.OpenForms.Count);
                Assert.AreEqual("Succeeded", (await AppComTelephonyTests.Done(h.App, h.App.HangUp(id.ToString("D")))).State);
                await matching.StopAsync(default);
                Assert.AreEqual(0, f.Phone.CallSubscribers);
                expected = await history.GetCallAsync(id); Assert.AreEqual(CallState.Ended, expected!.Call.State);
                Assert.AreEqual(note.Id, expected.Notes.Single().Id);
            }
            finally { await host.StopAsync(); await ((IAsyncDisposable)host).DisposeAsync(); }
            var restarted = ComHistoryHost(root, f);
            try
            {
                await restarted.StartAsync(); var history = restarted.Services.GetRequiredService<ICallHistoryService>();
                var actual = (await history.GetCallAsync(expected!.Call.Id))!;
                Assert.AreEqual(expected.Call, actual.Call);
                Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(expected.Lookup), System.Text.Json.JsonSerializer.Serialize(actual.Lookup));
                CollectionAssert.AreEqual(expected.Parties.ToArray(), actual.Parties.ToArray());
                CollectionAssert.AreEqual(expected.Events.ToArray(), actual.Events.ToArray());
                CollectionAssert.AreEqual(expected.Notes.ToArray(), actual.Notes.ToArray());
                using var page = new HistoryPage(history, new CallNotesSessionFactory(history));
                await page.ShowCallAsync(actual.Call.Id, default);
                Assert.Contains("Fensterloser COM-Test", Field<MemoEdit>(page, "detailsEdit").Text);
                await page.StopAsync();
            }
            finally { await restarted.StopAsync(); await ((IAsyncDisposable)restarted).DisposeAsync(); }
        }
        finally
        {
            if (!Path.GetFullPath(root).StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("wcp-com-history-", StringComparison.Ordinal)) throw new AssertFailedException();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    });

    private static IHost ComHistoryHost(string root, ComTelephonyFixture f) => WawiCallHost.CreateBuilder(typeof(MainForm).Assembly)
        .ConfigureServices(services =>
        {
            services.AddSingleton(new ConfigurationLocation(root)); services.AddSingleton<IHostLifetime, AppHostLifetime>();
            services.AddSingleton(new StartupExecutionMode(true));
            services.AddSingleton<IUiDispatcher, WawiCallPro.Core.Threading.InlineUiDispatcher>();
            services.AddSingleton<IBusinessDirectory>(f.Source); services.AddSingleton<IProductPluginRuntime>(f.Source);
            PhoneSearchIndexRegistration.AddPhoneSearchIndex(services); services.AddSingleton<IPhoneCacheService, PhoneCacheService>();
            services.AddSingleton<ISipCallPersistence, SipCallPersistence>();
        }).Build();
}
