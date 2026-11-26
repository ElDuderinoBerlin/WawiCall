using System.Collections.Immutable;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task HistoryLoadsAllInternalPagesAndFindsLateRowsWithoutLosingSelectionOrDraft() => RunStaAsync(async () =>
    {
        var source = new PagedHistory(1003);
        using var page = new HistoryPage(source, new CallNotesSessionFactory(source)); CreateHandles(page);
        await InvokeActionAsync(page, "Refresh_Click"); var view = Field<GridView>(page, "view");
        CollectionAssert.AreEqual(new[] { 0, 500, 1000 }, source.Queries.Select(query => query.Page.Offset).ToArray());
        Assert.IsTrue(source.Queries.All(query => query.Page.Limit == 500)); Assert.AreEqual(1003, view.DataRowCount);
        view.ApplyFindFilter("late-1002"); Assert.AreEqual(1, view.DataRowCount);
        Assert.AreEqual(source.Calls[^1].Id, view.GetRowCellValue(0, "Id"));
        view.ApplyFindFilter(""); await FocusHistoryCallAsync(page, source.Calls[^1].Id);
        Field<MemoEdit>(page, "noteEdit").Text = "Entwurf auf später Abrufseite";
        await InvokeActionAsync(page, "Refresh_Click");
        Assert.AreEqual(source.Calls[^1].Id, view.GetFocusedRowCellValue("Id"));
        Assert.AreEqual("Entwurf auf später Abrufseite", Field<MemoEdit>(page, "noteEdit").Text);
        await page.StopAsync();
    });

    [TestMethod]
    [DataRow(2026, 3, 29, 23)][DataRow(2026, 10, 25, 25)]
    public Task HistoryInclusiveLocalDayHonorsUtcBoundsOnRealSqlite(int year, int month, int day, int berlinHours) => WithWorkspace(async (workspace, services, _) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date, TimeZoneInfo.Local));
        var end = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1), TimeZoneInfo.Local));
        if (TimeZoneInfo.Local.Id == "W. Europe Standard Time") Assert.AreEqual(TimeSpan.FromHours(berlinHours), end - start);
        var company = new Company(new(LexwareProductLine.Pro, "test-history", "1"), "Testmandant");
        await services.GetRequiredService<ICompanyRepository>().WriteAsync(new(company, null, start));
        var ids = new List<Guid>();
        foreach (var time in new[] { start.AddTicks(-1), start, end.AddTicks(-1), end })
        {
            var call = await services.GetRequiredService<ICallJournal>().BeginAsync(new() { Id = Guid.NewGuid(), Revision = 0, Company = company.Key,
                Direction = CallDirection.Incoming, State = CallState.Created, StartedAtUtc = time, StateChangedAtUtc = time,
                LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Withheld) });
            ids.Add(call.Id);
        }
        using var page = new HistoryPage(); page.Bind(workspace, null); CreateHandles(page);
        Field<DateEdit>(page, "fromEdit").DateTime = date; Field<DateEdit>(page, "toEdit").DateTime = date;
        await InvokeActionAsync(page, "Refresh_Click"); var grid = Field<GridView>(page, "view"); Assert.AreEqual(2, grid.DataRowCount);
        CollectionAssert.AreEquivalent(new[] { ids[1], ids[2] }, Enumerable.Range(0, grid.DataRowCount).Select(row => (Guid)grid.GetRowCellValue(row, "Id")).ToArray());
        Assert.AreEqual("Unterdrückt", grid.GetRowCellValue(0, "Rufnummer"));
        Assert.AreEqual("Testmandant", grid.GetRowCellValue(0, "Mandant"));
        Assert.AreEqual(Environment.MachineName, grid.GetRowCellValue(0, "Rechner"));
        Assert.IsFalse(grid.Columns["WindowsBenutzer"].Visible);
        Assert.IsTrue(grid.Columns["WindowsBenutzer"].OptionsColumn.ShowInCustomizationForm);
        Assert.IsFalse(string.IsNullOrWhiteSpace(grid.GetRowCellValue(0, "WindowsBenutzer") as string));

        using var legacy = new HistoryPage();
        CreateHandles(legacy);
        var legacyView = Field<GridView>(legacy, "view");
        foreach (var field in new[] { "Mandant", "Rechner", "WindowsBenutzer", "Angenommen", "Ende", "Endgrund" })
            legacyView.Columns.Remove(legacyView.Columns[field]);
        legacyView.OptionsLayout.Columns.StoreAllOptions = true;
        grid.OptionsLayout.Columns.StoreAllOptions = true;
        legacyView.Columns["Rufnummer"].Width = 219;
        using var legacyLayout = new MemoryStream();
        legacyView.SaveLayoutToStream(legacyLayout);
        legacyLayout.Position = 0;
        grid.RestoreLayoutFromStream(legacyLayout);
        Assert.AreEqual(219, grid.Columns["Rufnummer"].Width);
        Assert.IsTrue(grid.Columns["Mandant"].Visible);
        Assert.IsTrue(grid.Columns["Rechner"].Visible);
        await page.StopAsync();
    });

    [TestMethod]
    public Task HistoryInvalidRangeAndEmptyResultsRemainExplicit() => RunStaAsync(async () =>
    {
        var source = new PagedHistory(1); var log = new LogWindowService();
        using var page = new HistoryPage(source, new CallNotesSessionFactory(source), log); CreateHandles(page);
        await InvokeActionAsync(page, "Refresh_Click"); var view = Field<GridView>(page, "view"); var queries = source.Queries.Count;
        Field<DateEdit>(page, "fromEdit").DateTime = DateTime.Today.AddDays(1); Field<DateEdit>(page, "toEdit").DateTime = DateTime.Today;
        await InvokeActionAsync(page, "Refresh_Click"); Assert.AreEqual(queries, source.Queries.Count); Assert.AreEqual(1, view.DataRowCount);
        Assert.Contains("Beginn", log.Drain().Single().Message);
        Field<DateEdit>(page, "fromEdit").DateTime = DateTime.Today; source.Calls = [];
        await InvokeActionAsync(page, "Refresh_Click"); Assert.AreEqual(0, view.DataRowCount);
        Assert.AreEqual("", Field<MemoEdit>(page, "detailsEdit").Text); Assert.IsFalse(Field<SimpleButton>(page, "saveNoteButton").Enabled);
        await page.StopAsync();
    });

    [TestMethod]
    public Task HistoryUnknownNumberAndMissingStoredAssociationStayExplicit() => RunStaAsync(async () =>
    {
        var source = new PagedHistory(1); source.Calls[0] = source.Calls[0] with { RemoteAddress = new(NumberPresentation.Unknown) };
        using var page = new HistoryPage(source, new CallNotesSessionFactory(source)); CreateHandles(page);
        await InvokeActionAsync(page, "Refresh_Click"); Assert.AreEqual("Unbekannt", Field<GridView>(page, "view").GetFocusedRowCellValue("Rufnummer"));
        var text = Field<MemoEdit>(page, "detailsEdit").Text; Assert.Contains("Keine Geschäftspartnerzuordnung gespeichert", text); Assert.Contains("Nicht gespeichert", text);
        Assert.IsFalse(Field<MemoEdit>(page, "noteEdit").Properties.ReadOnly); await page.StopAsync();
    });

    [TestMethod]
    public Task HistoryCancellationDuringSecondPageRetainsPreviousRowsAndDraft() => RunStaAsync(async () =>
    {
        var source = new PagedHistory(1); using var page = new HistoryPage(source, new CallNotesSessionFactory(source)); CreateHandles(page);
        await InvokeActionAsync(page, "Refresh_Click"); var previous = source.Calls.Single().Id;
        Field<MemoEdit>(page, "noteEdit").Text = "Bleibt bei Abbruch";
        var many = new PagedHistory(501); source.Calls = [source.Calls[0], .. many.Calls];
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); CancellationToken captured = default;
        source.BeforePage = async (query, token) => { if (query.Page.Offset == 500) { captured = token; entered.TrySetResult(); await release.Task; } };
        var loading = InvokeActionAsync(page, "Refresh_Click"); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stopping = page.StopAsync();
        try { Assert.IsTrue(captured.IsCancellationRequested); Assert.IsFalse(stopping.IsCompleted); }
        finally { release.TrySetResult(); }
        await stopping; await loading;
        Assert.AreEqual(1, Field<GridView>(page, "view").DataRowCount); Assert.AreEqual(previous, Field<GridView>(page, "view").GetFocusedRowCellValue("Id"));
        Assert.AreEqual("Bleibt bei Abbruch", Field<MemoEdit>(page, "noteEdit").Text); Assert.IsTrue(page.HasUnsavedNotes);
    });

    [TestMethod]
    public Task HistoryLateDetailReadCannotReplaceTheNewFocusedCall() => RunStaAsync(async () =>
    {
        var source = new HistoryNotesDouble(); using var page = new HistoryPage(source, new CallNotesSessionFactory(source)); CreateHandles(page);
        await InvokeActionAsync(page, "Refresh_Click");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.BeforeRead = async _ => { entered.TrySetResult(); await release.Task; };
        var grid = Field<GridView>(page, "view"); grid.FocusedRowHandle = grid.LocateByValue("Id", source.Calls[1].Id);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); grid.FocusedRowHandle = grid.LocateByValue("Id", source.Calls[0].Id);
        release.TrySetResult(); await WaitForOperationAsync(page);
        Assert.AreEqual(source.Calls[0].Id, Field<Guid?>(page, "_selected")); Assert.IsFalse(Field<MemoEdit>(page, "noteEdit").Properties.ReadOnly);
        await page.StopAsync();
    });

    private sealed class PagedHistory : ICallHistoryService
    {
        internal CallSnapshot[] Calls;
        internal readonly List<CallHistoryQuery> Queries = [];
        internal Func<CallHistoryQuery, CancellationToken, Task>? BeforePage;
        internal PagedHistory(int count)
        {
            var start = new DateTimeOffset(DateTime.Today).ToUniversalTime();
            Calls = Enumerable.Range(0, count).Select(index => new CallSnapshot { Id = Guid.NewGuid(), Revision = 0, Direction = CallDirection.Incoming,
                State = CallState.Created, StartedAtUtc = start.AddSeconds(index), StateChangedAtUtc = start.AddSeconds(index),
                LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, "late-" + index) }).ToArray();
        }
        public async Task<Page<CallSnapshot>> GetCallsAsync(CallHistoryQuery query, CancellationToken token = default)
        {
            Queries.Add(query); if (BeforePage is not null) await BeforePage(query, token);
            var rows = Calls.Where(call => call.StartedAtUtc >= query.StartedFromUtc && call.StartedAtUtc < query.StartedToUtc).ToArray();
            return new([.. rows.Skip(query.Page.Offset).Take(query.Page.Limit)], rows.Length > query.Page.Offset + query.Page.Limit);
        }
        public Task<CallHistory?> GetCallAsync(Guid id, CancellationToken token = default) => Task.FromResult<CallHistory?>(new() { Call = Calls.Single(call => call.Id == id) });
        public Task<CallNote> SaveNoteAsync(Guid id, Guid note, string text, CancellationToken token = default) => throw new AssertFailedException("No notes are saved by this fixture.");
    }
}
