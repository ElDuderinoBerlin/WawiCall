using System.Collections.Immutable;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using DevExpress.XtraEditors;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Database.Repositories;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task HistoryRecordingFileActionsExportOriginalAndEnqueueOneMatchingMp3() => WithWorkspace(async (workspace, services, _) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        var workstation = services.GetRequiredService<IWorkstationIdentity>();
        var local = await workstation.InitializeAsync();
        var context = services.GetRequiredService<DatabaseRepositoryContext>();
        var calls = services.GetRequiredService<ICallRepository>();
        var now = DateTimeOffset.UtcNow;
        var origin = new CallOrigin(local, "TEST", "TEST\\History");
        var call = new CallSnapshot
        {
            Id = Guid.NewGuid(), Direction = CallDirection.Incoming, Revision = 0, State = CallState.Connected,
            StartedAtUtc = now, AnsweredAtUtc = now, StateChangedAtUtc = now,
            LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Unknown)
        };
        var stored = await calls.WriteAsync(new(call, null, [], [], origin));
        var recording = new Recording(Guid.NewGuid(), call.Id, "recorded.wav", "wav", null, RecordingAvailability.Pending);
        var journal = services.GetRequiredService<IRecordingJournal>();
        await journal.BeginAsync(new(recording, null, now),
            new(new(recording.Id, call.Id, now, null, PcmRecordingState.Capturing, 8000, 2), null, now));

        Directory.CreateDirectory(workspace.Paths.RecordingsDirectory);
        var wave = Path.Combine(workspace.Paths.RecordingsDirectory, recording.RelativePath);
        const int frames = 160;
        using (var writer = new BinaryWriter(File.Create(wave)))
        {
            writer.Write("RIFF"u8); writer.Write(36 + frames * 4); writer.Write("WAVEfmt "u8); writer.Write(16);
            writer.Write((ushort)1); writer.Write((ushort)2); writer.Write(8000); writer.Write(32000);
            writer.Write((ushort)4); writer.Write((ushort)16); writer.Write("data"u8); writer.Write(frames * 4);
            writer.Write(new byte[frames * 4]);
        }
        var end = now.AddMilliseconds(20);
        var capture = new RecordingCapture(recording.Id, call.Id, now, end, PcmRecordingState.Completed, 8000, 2, frames);
        await journal.WriteAsync(new(recording with { Availability = RecordingAvailability.Available, Duration = capture.Duration }, 1, end),
            new(capture, 1, end));
        await calls.WriteAsync(new(stored.History.Call with { State = CallState.Ended, Revision = 1, StateChangedAtUtc = end,
            EndedAtUtc = end, EndReason = CallEndReason.Completed }, stored.Version, [], [], origin));

        var artifacts = services.GetRequiredService<IRecordingArtifactRepository>();
        var captures = services.GetRequiredService<IRecordingCaptureRepository>();
        var jobs = services.GetRequiredService<IRecordingConversionRepository>();
        var requests = new RecordingConversionRequests(artifacts, captures, jobs, services.GetRequiredService<IRecordingConversionJournal>(),
            calls, workstation, services.GetRequiredService<IRecordingJournalOutbox>(), services.GetRequiredService<IRecordingConversionOutbox>(),
            services.GetRequiredService<IRecordingArtifactFileInspector>(), services.GetRequiredService<IRecordingFileProbe>(), new RecordingConversionSignals(), TimeProvider.System);
        var files = new HistoryRecordingFiles(new(context), workstation, artifacts, captures, jobs, requests,
            services.GetRequiredService<IRecordingExport>(), new HistoryFormatDouble());

        using var page = new HistoryPage(workspace.History, workspace.CallNotes, new HistoryPlaybackDouble(call.Id), files)
            { Dock = DockStyle.Fill };
        using var host = new FrmSettings(workspace);
        host.PrepareForBackgroundRun();
        host.Controls.Add(page);
        page.BringToFront();
        host.Show();
        CreateHandles(page);
        Assert.IsFalse(Field<SimpleButton>(page, "exportRecordingButton").Visible);
        await page.LoadAsync();
        Assert.IsTrue(Field<SimpleButton>(page, "exportRecordingButton").Visible);
        Assert.IsTrue(Field<SimpleButton>(page, "convertRecordingButton").Visible);
        var images = Path.Combine(RenderOutput(), "history-layout");
        Directory.CreateDirectory(images);
        var oldSkin = DevExpress.LookAndFeel.UserLookAndFeel.Default.ActiveSkinName;
        var oldAppearance = DevExpress.Utils.AppearanceObject.DefaultFont;
        var oldFont = WindowsFormsSettings.DefaultFont;
        try
        {
            foreach (var (skin, size, width) in new[] { ("Basic", 10F, 900), ("Office 2019 Black", 16F, 640) })
            {
                using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = font;
                DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(skin);
                host.Font = font;
                host.MinimumSize = Size.Empty;
                host.ClientSize = new Size(width, 540);
                host.PerformLayout();
                Application.DoEvents();
                CheckTableBounds(page);
                using var bitmap = new Bitmap(page.Width, page.Height);
                page.DrawToBitmap(bitmap, page.ClientRectangle);
                bitmap.Save(Path.Combine(images, $"history-recording-files-{size}.png"));
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont;
            DevExpress.Utils.AppearanceObject.DefaultFont = oldAppearance;
            DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(oldSkin);
        }
        await page.StopAsync();
        await host.StopAsync();

        var choice = (await files.GetExportChoicesAsync(call.Id, default)).Single();
        Assert.AreEqual(RecordingArtifactFormat.Wave, choice.Format);
        var destination = Path.Combine(Path.GetTempPath(), "wcp-history-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            await Assert.ThrowsExactlyAsync<HistoryRecordingFileException>(() => files.ExportAsync(Guid.NewGuid(), choice, destination, default));
            await files.ExportAsync(call.Id, choice, destination, default);
            CollectionAssert.AreEqual(await File.ReadAllBytesAsync(wave), await File.ReadAllBytesAsync(destination));
            Assert.AreEqual(RecordingConversionState.Pending, await files.QueueMp3Async(call.Id, default));
            var first = await jobs.GetForSourceAsync(choice.ArtifactId);
            Assert.AreEqual(new Mp3EncodingFormat(8000, 2, 96000), first?.Job.Format);
            Assert.AreEqual(RecordingConversionState.Pending, await files.QueueMp3Async(call.Id, default));
            Assert.AreEqual(first, await jobs.GetForSourceAsync(choice.ArtifactId));
            CollectionAssert.AreEqual(new[] { choice }, (await files.GetExportChoicesAsync(call.Id, default)).ToArray());
        }
        finally { if (File.Exists(destination)) File.Delete(destination); }
    });

    private sealed class HistoryFormatDouble : IMp3Converter
    {
        public Task<ImmutableArray<Mp3EncodingFormat>> GetSupportedFormatsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ImmutableArray<Mp3EncodingFormat>>([new(8000, 1, 128000), new(8000, 2, 64000), new(8000, 2, 96000)]);
        public Task<Mp3ConversionResult> ConvertAsync(Mp3ConversionRequest request, CancellationToken cancellationToken = default) =>
            throw new AssertFailedException("The history command queues work for the existing background worker.");
    }
}
