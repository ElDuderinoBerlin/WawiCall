using System.Runtime.InteropServices;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;
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
        if (Environment.GetEnvironmentVariable("WCP_VERIFY_HISTORY_DIRECTX") == "1")
            Field<DevExpress.XtraGrid.GridControl>(page, "grid").UseDirectXPaint = DevExpress.Utils.DefaultBoolean.True;
        using var host = new FrmSettings(workspace);
        host.PrepareForBackgroundRun();
        host.Controls.Add(page);
        page.BringToFront();
        host.Show();
        CreateHandles(page);
        await page.LoadAsync();

        var view = Field<GridView>(page, "view");
        var grid = view.GridControl;
        var column = view.Columns["Playback"];
        Assert.AreEqual(page.LogicalToDeviceUnits(36), column.Width);
        Assert.IsTrue(column.OptionsColumn.FixedWidth);
        Assert.IsFalse(column.OptionsColumn.AllowSize);
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


        view.FocusedColumn = view.Columns["Rufnummer"];
        ClickPlaybackCell(recordedRow);
        Assert.AreEqual(available, await player.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.IsTrue(view.GetMasterRowExpanded(recordedRow));
        Assert.IsFalse(view.GetMasterRowExpanded(otherRow));
        var detail = view.GetDetailView(recordedRow, 0) as LayoutView;
        Assert.IsNotNull(detail);
        Assert.AreEqual(DevExpress.XtraEditors.Controls.BorderStyles.NoBorder, detail.BorderStyle);
        Assert.IsFalse(detail.OptionsView.ShowCardBorderIfCaptionHidden);
        Assert.IsFalse(detail.OptionsCustomization.AllowFilter);
        Assert.IsFalse(detail.OptionsCustomization.AllowSort);
        Assert.AreEqual("00:14", detail.GetRowCellValue(0, "Elapsed"));
        Assert.AreEqual("00:27", detail.GetRowCellValue(0, "Duration"));
        Assert.AreEqual(RecordingArtifactFormat.Wave, player.Transport!.Media!.Format);
        var renderPlayer = typeof(HistoryPage).GetMethod("RenderPlayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var paintedPosition = -1;
        void InspectPosition(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
        {
            if (e.Column.FieldName == "Progress" && e.CellValue is int position) paintedPosition = position;
        }
        detail.CustomDrawCardFieldValue += InspectPosition;
        using (var frame = new Bitmap(page.Width, page.Height))
            foreach (var (seconds, expected) in new[] { (3, 1111), (9, 3333), (18, 6666) })
            {
                player.Transport.SetPosition(TimeSpan.FromSeconds(seconds));
                paintedPosition = -1;
                renderPlayer.Invoke(page, []);
                page.DrawToBitmap(frame, page.ClientRectangle);
                Assert.AreEqual(expected, paintedPosition);
            }
        detail.CustomDrawCardFieldValue -= InspectPosition;
        detail.FocusedColumn = detail.Columns["Progress"];
        detail.ShowEditor();
        Assert.IsInstanceOfType<TrackBarControl>(detail.ActiveEditor);
        Assert.AreEqual(6666, ((TrackBarControl)detail.ActiveEditor!).Value);
        Assert.AreEqual(-1L, player.Transport.TakeSeek());
        player.Transport.SetPosition(TimeSpan.FromSeconds(17));
        renderPlayer.Invoke(page, []);
        Assert.AreEqual(6296, ((TrackBarControl)detail.ActiveEditor!).Value);
        Assert.AreEqual(-1L, player.Transport.TakeSeek());
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

        // Advance through two PCM inputs with a stationary TrackBar; only the player's timer may update the meter.
        var lowFrame = await CaptureSpectrumFrameAsync(350);
        var highFrame = await CaptureSpectrumFrameAsync(2400);
        Assert.AreNotSame(lowFrame.Image, highFrame.Image, "The renderer must get a new image when the bands change.");
        Assert.IsGreaterThan(lowFrame.FirstBar, highFrame.FirstBar, "The strong band must move to the right for the higher tone.");

        async Task<(Bitmap Image, int FirstBar)> CaptureSpectrumFrameAsync(int frequency)
        {
            player.Transport.ClearSpectrum();
            player.FeedSpectrum(frequency);
            await Task.Delay(200);
            var spectrumBounds = Rectangle.Empty;
            detail.CustomDrawCardFieldValue += InspectSpectrum;
            using var frame = new Bitmap(page.Width, page.Height);
            try { page.DrawToBitmap(frame, page.ClientRectangle); }
            finally { detail.CustomDrawCardFieldValue -= InspectSpectrum; }
            var image = Field<Bitmap>(page, "_spectrumImage");
            var firstBar = Enumerable.Range(0, image.Width).FirstOrDefault(x =>
                Enumerable.Range(0, image.Height / 2).Any(y => image.GetPixel(x, y).A != 0), -1);
            Assert.IsGreaterThanOrEqualTo(0, firstBar);
            Assert.IsFalse(spectrumBounds.IsEmpty);
            var top = Enumerable.Range(0, image.Height / 2).First(y => image.GetPixel(firstBar, y).A != 0);
            var centerOffset = (spectrumBounds.Height - image.Height) / 2;
            var gridOrigin = page.PointToClient(grid.PointToScreen(Point.Empty));
            Assert.AreEqual(image.GetPixel(firstBar, top).ToArgb(),
                frame.GetPixel(gridOrigin.X + spectrumBounds.X + 2 + firstBar,
                    gridOrigin.Y + spectrumBounds.Y + centerOffset + top).ToArgb());
            return (image, firstBar);

            void InspectSpectrum(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
            {
                if (e.Column.FieldName == "Spectrum") spectrumBounds = e.Bounds;
            }
        }

        var originalFont = page.Font;
        var defaultFont = WindowsFormsSettings.DefaultFont;
        var defaultAppearance = DevExpress.Utils.AppearanceObject.DefaultFont;
        var look = DevExpress.LookAndFeel.UserLookAndFeel.Default;
        var originalSkin = look.ActiveSkinName;
        var originalPalette = look.ActiveSvgPaletteName;
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
                renderPlayer.Invoke(page, []);
                closeBounds = trackBounds = Rectangle.Empty;
                rendered.CustomDrawCardFieldValue += InspectPlayer;
                using var bitmap = new Bitmap(page.Width, page.Height);
                page.DrawToBitmap(bitmap, page.ClientRectangle);
                rendered.CustomDrawCardFieldValue -= InspectPlayer;
                var spectrum = Field<Bitmap>(page, "_spectrumImage");
                Assert.IsTrue(Enumerable.Range(0, spectrum.Width).Any(x =>
                    Enumerable.Range(0, spectrum.Height / 2).Any(y => spectrum.GetPixel(x, y).A != 0)),
                    "The inline meter must paint a bar above the baseline while stereo audio is playing.");
                bitmap.Save(Path.Combine(output, $"history-recording-inline-{typeof(MainForm).Assembly.GetName().Name}-{width}-{fontSize:0}-{scale:0.0}.png"));
                Assert.IsTrue(closeBounds.Width > 0 && closeBounds.Right <= grid.ClientSize.Width,
                    $"Close button {closeBounds} exceeds grid width {grid.ClientSize.Width} at {width}/{fontSize}/{scale}.");
                Assert.IsGreaterThan(60, trackBounds.Width);
                Assert.AreEqual(page.LogicalToDeviceUnits(36), column.Width);
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
        ClickPlaybackCell(nextRow);
        await player.NextStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsFalse(view.GetMasterRowExpanded(recordedRow));
        Assert.IsTrue(view.GetMasterRowExpanded(nextRow));
        Assert.AreEqual(1, player.CancelledCount);

        await page.StopAsync();
        Assert.IsFalse(view.GetMasterRowExpanded(nextRow));
        Assert.AreEqual(2, player.CancelledCount);
        await host.StopAsync();

        void ClickPlaybackCell(int rowHandle)
        {
            const uint leftButtonDown = 0x0201;
            const uint leftButtonUp = 0x0202;
            view.MakeRowVisible(rowHandle);
            var bounds = ((GridViewInfo)view.GetViewInfo()).GetGridCellInfo(rowHandle, column).Bounds;
            Assert.IsTrue(bounds.Width > 0 && bounds.Height > 0);
            var point = (nint)(((bounds.Top + bounds.Height / 2) << 16) | (bounds.Left + bounds.Width / 2));
            var handle = view.GridControl.Handle;
            SendHistoryGridMouseMessage(handle, leftButtonDown, 1, point);
            SendHistoryGridMouseMessage(handle, leftButtonUp, 0, point);
        }
    });

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern nint SendHistoryGridMouseMessage(nint handle, uint message, nint flags, nint point);

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

        public void FeedSpectrum() => FeedSpectrum(700);

        public void FeedSpectrum(int frequency)
        {
            var samples = new short[4096];
            for (var frame = 0; frame < samples.Length / 2; frame++)
            {
                var sample = (short)(24000 * Math.Sin(Math.Tau * frequency * frame / 8000));
                samples[2 * frame] = sample;
                samples[2 * frame + 1] = (short)-sample;
            }
            Transport!.AddSamples(8000, 2, samples);
        }
    }
}
