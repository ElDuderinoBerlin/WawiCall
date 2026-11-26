using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Layout;
using WawiCallPro.Database.Contracts;
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
        var next = await SaveCallAsync("another recording");
        var unavailable = await SaveCallAsync("not-recorded");
        var player = new HistoryPlaybackDouble(available, next);
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

        Assert.AreEqual(2, Field<TableLayoutPanel>(page, "layout").RowCount);
        Assert.AreSame(Field<TableLayoutPanel>(page, "notesLayout"), Field<LabelControl>(page, "statusLabel").Parent);
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
        Assert.IsTrue(view.GetMasterRowExpanded(recordedRow));
        Assert.IsFalse(view.GetMasterRowExpanded(otherRow));
        var detail = view.GetDetailView(recordedRow, 0) as LayoutView;
        Assert.IsNotNull(detail);
        Assert.AreEqual("00:14", detail.GetRowCellValue(0, "Elapsed"));
        Assert.AreEqual("00:27", detail.GetRowCellValue(0, "Duration"));
        Assert.AreEqual(RecordingArtifactFormat.Wave, player.Transport!.Media!.Format);
        detail.FocusedColumn = detail.Columns["Progress"];
        detail.ShowEditor();
        Assert.IsInstanceOfType<TrackBarControl>(detail.ActiveEditor);
        ((TrackBarControl)detail.ActiveEditor!).Value = 5000;
        Assert.AreEqual(TimeSpan.FromSeconds(13.5).Ticks, player.Transport.TakeSeek());
        detail.HideEditor();
        var toggle = typeof(HistoryPage).GetMethod("TogglePlayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        toggle.Invoke(page, []);
        Assert.IsTrue(player.Transport.Paused);
        toggle.Invoke(page, []);
        Assert.IsFalse(player.Transport.Paused);
        Assert.AreEqual("Datei anzeigen", detail.GetRowCellValue(0, "File"));
        Assert.AreEqual("WAV · PCM 16 Bit · 8 kHz · Stereo", detail.GetRowCellValue(0, "Format"));
        var originalFont = page.Font;
        var defaultFont = WindowsFormsSettings.DefaultFont;
        var defaultAppearance = DevExpress.Utils.AppearanceObject.DefaultFont;
        var look = DevExpress.LookAndFeel.UserLookAndFeel.Default;
        var originalSkin = look.ActiveSkinName;
        var originalPalette = look.ActiveSvgPaletteName;
        var grid = view.GridControl;
        Rectangle closeBounds = Rectangle.Empty, trackBounds = Rectangle.Empty;
        void InspectPlayer(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
        {
            if (e.Column.FieldName == "Close") closeBounds = e.Bounds;
            if (e.Column.FieldName == "Progress") trackBounds = e.Bounds;
        }
        try
        {
            foreach (var (width, fontSize, scale, skin) in new[]
                { (900, 10F, 1F, "Basic"), (650, 16F, 1F, "Office 2019 Black"), (850, 10F, 1.5F, "Basic") })
            {
                using var font = new Font(originalFont.FontFamily, fontSize);
                if (skin == "Basic") look.SetSkinStyle(skin, "Blue Dark");
                else look.SetSkinStyle(skin);
                WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = page.Font = font;
                host.ClientSize = new Size(width, 620);
                if (scale != 1F) page.Scale(new SizeF(scale, scale));
                page.PerformLayout();
                Application.DoEvents();
                var rendered = view.GetDetailView(recordedRow, 0) as LayoutView;
                Assert.IsNotNull(rendered);
                player.FeedSpectrum();
                typeof(HistoryPage).GetMethod("RenderPlayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(page, []);
                closeBounds = trackBounds = Rectangle.Empty;
                rendered.CustomDrawCardFieldValue += InspectPlayer;
                using var bitmap = new Bitmap(page.Width, page.Height);
                page.DrawToBitmap(bitmap, page.ClientRectangle);
                rendered.CustomDrawCardFieldValue -= InspectPlayer;
                bitmap.Save(Path.Combine(output, $"history-recording-inline-{typeof(MainForm).Assembly.GetName().Name}-{width}-{fontSize:0}-{scale:0.0}.png"));
                Assert.IsTrue(closeBounds.Width > 0 && closeBounds.Right <= grid.ClientSize.Width,
                    $"Close button {closeBounds} exceeds grid width {grid.ClientSize.Width} at {width}/{fontSize}/{scale}.");
                Assert.IsGreaterThan(60, trackBounds.Width);
                page.Font = originalFont;
                WindowsFormsSettings.DefaultFont = defaultFont;
                DevExpress.Utils.AppearanceObject.DefaultFont = defaultAppearance;
            }
        }
        finally
        {
            page.Font = originalFont;
            WindowsFormsSettings.DefaultFont = defaultFont;
            DevExpress.Utils.AppearanceObject.DefaultFont = defaultAppearance;
            if (string.IsNullOrEmpty(originalPalette)) look.SetSkinStyle(originalSkin);
            else look.SetSkinStyle(originalSkin, originalPalette);
        }
        view.FocusedColumn = column;
        view.ShowEditor();
        Assert.IsInstanceOfType<ButtonEdit>(view.ActiveEditor);
        Assert.AreEqual("Stoppen", ((ButtonEdit)view.ActiveEditor!).Properties.Buttons[0].Caption);
        var nextRow = view.LocateByValue("Id", next);
        view.FocusedRowHandle = nextRow;
        view.FocusedColumn = column;
        view.ShowEditor();
        click.Invoke(page, [view.ActiveEditor, new ButtonPressedEventArgs(((ButtonEdit)view.ActiveEditor!).Properties.Buttons[0])]);
        await player.NextStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsFalse(view.GetMasterRowExpanded(recordedRow));
        Assert.IsTrue(view.GetMasterRowExpanded(nextRow));
        Assert.AreEqual(1, player.CancelledCount);

        await page.StopAsync();
        Assert.IsFalse(view.GetMasterRowExpanded(nextRow));
        Assert.AreEqual(2, player.CancelledCount);
        await host.StopAsync();
    });

    private sealed class HistoryPlaybackDouble(Guid available, Guid? next = null) : IHistoryRecordingPlayback
    {
        public TaskCompletionSource<Guid> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource NextStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CancelledCount { get; private set; }
        public HistoryPlaybackTransport? Transport { get; private set; }

        public Task<IReadOnlySet<Guid>> FindAsync(IReadOnlyCollection<Guid> callIds, CancellationToken token) =>
            Task.FromResult<IReadOnlySet<Guid>>(callIds.Where(id => id == available || id == next).ToHashSet());

        public async Task PlayAsync(Guid callId, CancellationToken token)
        {
            if (callId == next) NextStarted.TrySetResult();
            else Started.TrySetResult(callId);
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { if (token.IsCancellationRequested) CancelledCount++; }
        }

        public Task PlayAsync(Guid callId, HistoryPlaybackTransport transport, CancellationToken token)
        {
            Transport = transport;
            transport.SetMedia(new(RecordingArtifactFormat.Wave, 8000, 2, 16, TimeSpan.FromSeconds(27), true, @"C:\Test\Aufnahme.wav"));
            transport.SetPosition(TimeSpan.FromSeconds(14));
            FeedSpectrum();
            return PlayAsync(callId, token);
        }

        public void FeedSpectrum()
        {
            short[] samples = Enumerable.Range(0, 2048).Select(index => (short)(24000 * Math.Sin(Math.Tau * 700 * index / 8000))).ToArray();
            Transport!.AddSamples(8000, 1, samples);
        }
    }
}
