using System.Buffers.Binary;
using System.Collections.Immutable;
using DevExpress.Utils;
using DevExpress.XtraEditors;
using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task LiveRecordingRejectsForeignOrAlteredLocalAnchorsBeforeCreatingJournalRows() => WithWorkspace(async (workspace, services, _) =>
    {
        using var audio = new ServiceCollection().AddAudioConversion().BuildServiceProvider();
        var persisted = new PersistedDuplexPcmRecordingFactory(audio.GetRequiredService<IDuplexPcmRecordingFactory>(),
            services.GetRequiredService<IRecordingJournal>(), workspace.Paths, TimeProvider.System);
        var source = new LiveRecordingSourceDouble();
        await using var prepared = new LiveRecordingPreparation(source, TimeProvider.System);
        source.Send(1000, 100); source.Receive(1, 2000, 200); await prepared.Ready;
        var request = prepared.CreateRequest(new(Guid.NewGuid(), Guid.NewGuid(), workspace.Paths.ResolveRecordingPath("synthetic.wav")));
        await Assert.ThrowsAsync<ArgumentException>(() => persisted.StartAsync(source, request));
        await Assert.ThrowsAsync<ArgumentException>(() => persisted.StartAsync(prepared, request with
            { Received = request.Received with { RecordingOffset = TimeSpan.FromSeconds(1) } }));
        Assert.IsFalse(Directory.Exists(workspace.Paths.RecordingsDirectory));
        Assert.IsFalse(File.Exists(Path.Combine(workspace.Paths.RootDirectory, "communications.db")));
    });

    [TestMethod]
    public Task LiveRecordingPersistsTheActualTwoSideWaveInTheExistingSqliteHistory() => WithWorkspace(async (workspace, services, _) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        var clock = new LiveRecordingClock();
        var now = clock.GetUtcNow();
        var companies = services.GetRequiredService<ICompanyRepository>();
        var company = new Company(new(LexwareProductLine.Pro, "test-recordings", "1"), "Musterfirma");
        await companies.WriteAsync(new(company, null, now));
        var calls = services.GetRequiredService<ISipCallPersistence>();
        var connected = CommandCall() with
        { Company = company.Key, State = CallState.Connected, StartedAtUtc = now.AddSeconds(-1), StateChangedAtUtc = now, AnsweredAtUtc = now };
        var call = await calls.SaveAsync(connected);
        var otherCall = await calls.SaveAsync(connected with { Id = Guid.NewGuid() });
        using var audio = new ServiceCollection().AddAudioConversion().BuildServiceProvider();
        var persisted = new PersistedDuplexPcmRecordingFactory(audio.GetRequiredService<IDuplexPcmRecordingFactory>(),
            services.GetRequiredService<IRecordingJournal>(), workspace.Paths, clock);
        var source = new LiveRecordingSourceDouble();
        var otherSource = new LiveRecordingSourceDouble();
        var factory = new LivePcmRecordingFactory(persisted, clock);
        var callRepository = services.GetRequiredService<ICallRepository>();
        await using var media = new LiveRecordingMediaSession(new LiveRecordingTests.NativeMedia(source), persisted, factory, workspace.Paths,
            callRepository, clock, companies);
        await using var otherMedia = new LiveRecordingMediaSession(new LiveRecordingTests.NativeMedia(otherSource), persisted, factory, workspace.Paths,
            callRepository, clock, companies);
        var stem = RecordingFileName.Format(RecordingFileName.DefaultPattern, clock.GetLocalNow(), null, Guid.Empty, null, company.DisplayName);
        Directory.CreateDirectory(workspace.Paths.RecordingsDirectory);
        var existingPath = workspace.Paths.ResolveRecordingPath(stem + ".wav");
        await File.WriteAllTextAsync(existingPath, "vorhandene Datei");
        await StartAsync(media, source, call.Id);
        await StartAsync(otherMedia, otherSource, otherCall.Id);
        var id = media.RecordingStatus.RecordingId;
        clock.Advance(60);
        await media.StopLiveAsync(CancellationToken.None);
        await otherMedia.StopLiveAsync(CancellationToken.None);
        var history = (await workspace.History.GetCallAsync(call.Id))!;
        var stored = history.Recordings.Single();
        Assert.AreEqual(id, stored.Id); Assert.AreEqual(RecordingAvailability.Available, stored.Availability);
        Assert.IsTrue(stored.Duration > TimeSpan.Zero);
        Assert.AreEqual(stem + "_2.wav", stored.RelativePath);
        using var wave = File.OpenRead(workspace.Paths.ResolveRecordingPath(stored.RelativePath));
        var header = WawiCallPro.Core.Audio.PcmWaveHeader.Read(wave, default);
        Assert.AreEqual(1, header.Channels);
        wave.Position = header.DataOffset + header.SampleRate / 100 * sizeof(short);
        var sample = new byte[sizeof(short)];
        wave.ReadExactly(sample);
        Assert.AreEqual(1234 + 2345, BinaryPrimitives.ReadInt16LittleEndian(sample), "Both voices must be present in the same frame.");
        Assert.AreEqual("vorhandene Datei", await File.ReadAllTextAsync(existingPath));
        Assert.AreEqual(0, source.Subscribers);
        var otherRecording = (await workspace.History.GetCallAsync(otherCall.Id))!.Recordings.Single();
        Assert.AreEqual(stem + "_3.wav", otherRecording.RelativePath);
        Assert.IsTrue(File.Exists(workspace.Paths.ResolveRecordingPath(otherRecording.RelativePath)));

        static async Task StartAsync(LiveRecordingMediaSession session, LiveRecordingSourceDouble input, Guid callId)
        {
            var start = session.StartLiveAsync(callId, CancellationToken.None);
            for (var attempt = 0; attempt < 100 && input.Subscribers == 0 && !start.IsCompleted; attempt++) await Task.Delay(10);
            Assert.AreEqual(1, input.Subscribers);
            input.Send(1000, 1234);
            input.Receive(1, 2000, 2345);
            await start;
        }
    });

    [TestMethod]
    public Task LiveRecordingStorageFailurePreservesCallAndWritesFailedCaptureWithoutWave() => WithWorkspace(async (workspace, services, _) =>
    {
        await workspace.Database.CreateAndMigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var call = await services.GetRequiredService<ISipCallPersistence>().SaveAsync(CommandCall() with
        { State = CallState.Connected, StartedAtUtc = now.AddSeconds(-1), StateChangedAtUtc = now, AnsweredAtUtc = now });
        using var audio = new ServiceCollection().AddAudioConversion().BuildServiceProvider();
        var persisted = new PersistedDuplexPcmRecordingFactory(audio.GetRequiredService<IDuplexPcmRecordingFactory>(),
            services.GetRequiredService<IRecordingJournal>(), workspace.Paths, TimeProvider.System);
        var source = new LiveRecordingSourceDouble();
        var native = new LiveRecordingTests.NativeMedia(source);
        await using var media = new LiveRecordingMediaSession(native, persisted, new LivePcmRecordingFactory(persisted, TimeProvider.System), workspace.Paths);
        await File.WriteAllTextAsync(workspace.Paths.RecordingsDirectory, "synthetic path blocker");
        var start = media.StartLiveAsync(call.Id, CancellationToken.None);
        source.Send(1000, 100);
        source.Receive(1, 2000, 200);
        await Assert.ThrowsAsync<PcmRecordingException>(() => start);
        Assert.AreEqual(LiveRecordingFailure.StorageFailed, media.RecordingStatus.Failure);
        Assert.AreEqual(0, source.Subscribers);
        Assert.AreEqual(0, native.Disposals);
        var history = (await workspace.History.GetCallAsync(call.Id))!;
        var failed = history.Recordings.Single();
        Assert.AreEqual(RecordingAvailability.Failed, failed.Availability);
        Assert.AreEqual(CallState.Connected, history.Call.State);
        Assert.IsFalse(File.Exists(workspace.Paths.ResolveRecordingPath(failed.RelativePath)));
    });

    [TestMethod]
    public Task LiveRecordingButtonsShareCallStateAcrossViewsAndKeepHangupAvailableDuringPreparation() => RunStaAsync(async () =>
    {
        var call = CommandCall() with { State = CallState.Connected };
        var service = new RecordingTelephony(call);
        using var first = new CallControlPanel(() => true);
        using var second = new CallControlPanel(() => throw new InvalidOperationException("Stopping does not require consent."));
        first.CreateControl(); second.CreateControl();
        first.Bind(service, call); second.Bind(service, call);
        service.StartRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = first.ToggleRecordingAsync();
        await WaitForDialStateAsync(() => Field<SimpleButton>(second, "recordingButton").Text == "Aufnahme abbrechen");
        Assert.IsTrue(Field<SimpleButton>(first, "endButton").Enabled);
        Assert.IsTrue(Field<SimpleButton>(second, "recordingButton").Enabled);
        Assert.AreEqual(1, service.Starts);
        service.StartRelease.SetResult(); await start;
        await WaitForDialStateAsync(() => Field<SimpleButton>(second, "recordingButton").Text == "Aufnahme stoppen");
        await second.ToggleRecordingAsync();
        await WaitForDialStateAsync(() => Field<LabelControl>(first, "recordingStatus").Text.StartsWith("Aufnahme gespeichert"));
        Assert.AreEqual(1, service.Stops);
        service.Publish(new(Guid.NewGuid(), null, LiveRecordingState.Failed)); Application.DoEvents();
        Assert.Contains("gespeichert", Field<LabelControl>(first, "recordingStatus").Text);
        await first.StopAsync(); await second.StopAsync(); Assert.AreEqual(0, service.RecordingSubscribers);
    });

    [TestMethod]
    public Task RecordingRequiresConsentAndAnOngoingCallAfterThePrompt() => RunStaAsync(async () =>
    {
        var call = CommandCall() with { State = CallState.Connected };
        var service = new RecordingTelephony(call);
        using var declined = new CallControlPanel(() => false);
        declined.Bind(service, call);
        await declined.ToggleRecordingAsync();
        Assert.AreEqual(0, service.Starts);

        CallControlPanel? endingPanel = null;
        using var ending = new CallControlPanel(() =>
        {
            endingPanel!.Observe(call with { State = CallState.Ended, Revision = call.Revision + 1 });
            return true;
        });
        endingPanel = ending;
        ending.Bind(service, call);
        await ending.ToggleRecordingAsync();
        Assert.AreEqual(0, service.Starts);
    });

    [TestMethod]
    public Task ClosingRecordingControlsCancelsOnlyTheirPendingStartAndUnsubscribes() => RunStaAsync(async () =>
    {
        var call = CommandCall() with { State = CallState.Connected }; var service = new RecordingTelephony(call);
        using var panel = new CallControlPanel(() => true); panel.CreateControl(); panel.Bind(service, call);
        service.StartRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = panel.ToggleRecordingAsync(); await panel.StopAsync(); await start;
        Assert.AreEqual(0, service.RecordingSubscribers); Assert.IsTrue(service.StartCancelled);
    });

    [TestMethod]
    public Task LiveRecordingControlsRenderReadyRecordingAndFinishedWithSmallWindowsFontsAndSkins() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply(); var oldFont = WindowsFormsSettings.DefaultFont; var oldAppearance = AppearanceObject.DefaultFont;
        var look = DevExpress.LookAndFeel.UserLookAndFeel.Default; var oldSkin = look.ActiveSkinName; var oldPalette = look.ActiveSvgPaletteName;
        var output = Path.Combine(RenderOutput(), "live-recording"); Directory.CreateDirectory(output);
        try
        {
            foreach (var (size, skin, scale) in new[] { (10F, "Basic", 1F), (16F, "Office 2019 Black", 1F), (10F, "Basic", 1.5F) })
            foreach (var state in new[] { LiveRecordingState.Idle, LiveRecordingState.Starting, LiveRecordingState.Recording, LiveRecordingState.Completed, LiveRecordingState.Failed })
            {
                look.SetSkinStyle(skin); using var font = new Font("Segoe UI", size);
                WindowsFormsSettings.DefaultFont = AppearanceObject.DefaultFont = font;
                using var dialog = new FrmCallerLookup(); dialog.PrepareForBackgroundRun(); dialog.Font = font; dialog.ClientSize = new(760, 700);
                var call = CommandCall() with { State = state == LiveRecordingState.Completed ? CallState.Ended : CallState.Connected };
                dialog.BindCall(call, (_, _) => Task.CompletedTask); var service = new RecordingTelephony(call);
                service.Publish(new(call.Id, Guid.NewGuid(), state)
                {
                    Failure = state == LiveRecordingState.Failed ? LiveRecordingFailure.StorageFailed : null
                });
                var panel = Field<CallControlPanel>(dialog, "callControls"); panel.Bind(service, call);
                dialog.Show(); if (scale != 1F) dialog.Scale(new SizeF(scale, scale)); dialog.PerformLayout(); Application.DoEvents();
                CheckTableBounds(dialog);
                using var picture = WawiCallPro.App.Diagnostics.UiAudit.Capture(dialog);
                picture.Save(Path.Combine(output, $"{size}-{skin}-{scale}-{state}.png"), System.Drawing.Imaging.ImageFormat.Png);
                await dialog.StopAsync();
                // The same controls also live above the document's tabs, which must retain usable space.
                using var details = new CallDetailsPage { Font = font, Size = new Size(760, 700) };
                Field<CallControlPanel>(details, "callControls").Bind(service, call);
                details.CreateControl();
                if (scale != 1F) details.Scale(new SizeF(scale, scale));
                details.PerformLayout(); Application.DoEvents(); CheckTableBounds(details);
                using var detailImage = new Bitmap(details.Width, details.Height);
                details.DrawToBitmap(detailImage, new Rectangle(Point.Empty, details.Size));
                detailImage.Save(Path.Combine(output, $"details-{size}-{skin}-{scale}-{state}.png"), System.Drawing.Imaging.ImageFormat.Png);
                await details.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = oldFont; AppearanceObject.DefaultFont = oldAppearance;
            if (string.IsNullOrEmpty(oldPalette)) look.SetSkinStyle(oldSkin); else look.SetSkinStyle(oldSkin, oldPalette);
        }
    });

    private sealed class RecordingTelephony(CallSnapshot call) : ITelephonyService, ILiveCallRecordingService
    {
        private LiveRecordingSnapshot _recording = new(call.Id, null, LiveRecordingState.Idle);
        private EventHandler<LiveRecordingChangedEventArgs>? _changed;
        internal int Starts, Stops;
        internal bool StartCancelled;
        internal TaskCompletionSource? StartRelease;
        internal int RecordingSubscribers => _changed?.GetInvocationList().Length ?? 0;
        public LiveRecordingSnapshot GetRecording(Guid id) => _recording;
        public event EventHandler<LiveRecordingChangedEventArgs>? RecordingChanged { add => _changed += value; remove => _changed -= value; }
        internal void Publish(LiveRecordingSnapshot status) { if (status.CallId == call.Id) _recording = status; _changed?.Invoke(this, new(status)); }
        public async Task StartLiveRecordingAsync(Guid id, CancellationToken token = default)
        {
            Starts++; Publish(new(id, Guid.NewGuid(), LiveRecordingState.Starting));
            try { if (StartRelease is { } release) await release.Task.WaitAsync(token); }
            catch (OperationCanceledException) { StartCancelled = true; throw; }
            Publish(_recording with { State = LiveRecordingState.Recording });
        }
        public Task StopLiveRecordingAsync(Guid id, CancellationToken token = default)
        { Stops++; Publish(_recording with { State = LiveRecordingState.Completed }); return Task.CompletedTask; }
        public RegistrationSnapshot Registration { get; } = new(RegistrationState.Registered, DateTimeOffset.UtcNow);
        public event EventHandler<RegistrationChangedEventArgs>? RegistrationChanged { add { } remove { } }
        public event EventHandler<CallChangedEventArgs>? CallChanged { add { } remove { } }
        public ImmutableArray<CallSnapshot> GetCalls() => [call];
        public Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken token = default) => throw new NotSupportedException();
        public Task AnswerAsync(Guid id, CancellationToken token = default) => Task.CompletedTask;
        public Task RejectAsync(Guid id, CancellationToken token = default) => Task.CompletedTask;
        public Task HangUpAsync(Guid id, CancellationToken token = default) => Task.CompletedTask;
        public Task HoldAsync(Guid id, CancellationToken token = default) => Task.CompletedTask;
        public Task ResumeAsync(Guid id, CancellationToken token = default) => Task.CompletedTask;
        public Task TransferAsync(Guid id, string destination, CancellationToken token = default) => Task.CompletedTask;
        public Task SendDtmfAsync(Guid id, char digit, CancellationToken token = default) => Task.CompletedTask;
    }
}
