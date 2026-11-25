using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid.Views.Grid;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Interfaces.History;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task HistoryPlaybackButtonIsPerRowAndStopsWhenThePageCloses() => WithWorkspace(async (workspace, services, _) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        var journal = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ICallJournal>(services);
        var now = DateTimeOffset.UtcNow;
        async Task<Guid> SaveCallAsync(string number)
        {
            var call = await journal.BeginAsync(new CallSnapshot
            {
                Id = Guid.NewGuid(), Revision = 0, Direction = CallDirection.Incoming, State = CallState.Ringing,
                LocalAddress = new(NumberPresentation.Available, "100"), RemoteAddress = new(NumberPresentation.Available, number),
                StartedAtUtc = now, StateChangedAtUtc = now
            });
            return call.Id;
        }

        var available = await SaveCallAsync("recorded");
        var unavailable = await SaveCallAsync("not-recorded");
        var player = new HistoryPlaybackDouble(available);
        using var page = new HistoryPage(workspace.History, workspace.CallNotes, player) { Dock = DockStyle.Fill };
        using var host = new FrmSettings(workspace);
        host.PrepareForBackgroundRun();
        host.Controls.Add(page);
        page.BringToFront();
        host.Show();
        CreateHandles(page);
        await page.LoadAsync();

        var view = Field<GridView>(page, "view");
        var column = view.Columns["Playback"];
        var recordedRow = view.LocateByValue("Id", available);
        var otherRow = view.LocateByValue("Id", unavailable);
        Assert.IsTrue(recordedRow >= 0 && otherRow >= 0);
        view.FocusedColumn = column;
        view.FocusedRowHandle = otherRow;
        await WaitForOperationAsync(page);
        view.ShowEditor();
        Assert.IsNull(view.ActiveEditor);

        var output = Path.Combine(RenderOutput(), "history-layout");
        Directory.CreateDirectory(output);
        using (var bitmap = new Bitmap(page.Width, page.Height))
        {
            page.DrawToBitmap(bitmap, page.ClientRectangle);
            bitmap.Save(Path.Combine(output, "history-recording-buttons.png"));
        }


        view.FocusedRowHandle = recordedRow;
        await WaitForOperationAsync(page);
        view.ShowEditor();
        Assert.IsInstanceOfType<ButtonEdit>(view.ActiveEditor);
        Assert.AreEqual("Abspielen", ((ButtonEdit)view.ActiveEditor!).Properties.Buttons[0].Caption);
        Assert.IsNotNull(((ButtonEdit)view.ActiveEditor!).Properties.Buttons[0].ImageOptions.SvgImage);
        var click = typeof(HistoryPage).GetMethod("Playback_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        click.Invoke(page, [view.ActiveEditor, new ButtonPressedEventArgs(((ButtonEdit)view.ActiveEditor!).Properties.Buttons[0])]);
        Assert.AreEqual(available, await player.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        view.ShowEditor();
        Assert.IsInstanceOfType<ButtonEdit>(view.ActiveEditor);
        Assert.AreEqual("Stoppen", ((ButtonEdit)view.ActiveEditor!).Properties.Buttons[0].Caption);
        var stop = page.StopAsync();
        await player.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await stop;
        await host.StopAsync();
    });

    private sealed class HistoryPlaybackDouble(Guid available) : IHistoryRecordingPlayback
    {
        public TaskCompletionSource<Guid> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IReadOnlySet<Guid>> FindAsync(IReadOnlyCollection<Guid> callIds, CancellationToken token) =>
            Task.FromResult<IReadOnlySet<Guid>>(callIds.Where(id => id == available).ToHashSet());

        public async Task PlayAsync(Guid callId, CancellationToken token)
        {
            Started.TrySetResult(callId);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { if (token.IsCancellationRequested) Cancelled.TrySetResult(); }
        }
    }
}
