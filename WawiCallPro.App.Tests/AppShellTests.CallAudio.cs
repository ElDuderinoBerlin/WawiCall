using System.Collections.Immutable;
using DevExpress.XtraEditors;
using Quantarix.DevExpressExtensions.Controls;
using WawiCallPro.App.Diagnostics;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task AudioMeterShowsTheCurrentLevelByDefaultAndKeepsHistoryOnlyInRollingMode() => RunStaAsync(() =>
    {
        using var meter = new AudioLevelMeter { Size = new(128, 16) };
        Assert.AreEqual(AudioLevelMeterRenderMode.Static, meter.RenderMode);
        var silence = RenderPixels();

        meter.Position = 50;
        var halfLevel = RenderPixels();
        Assert.IsFalse(silence.SequenceEqual(halfLevel));
        meter.Position = 100;
        meter.Position = 50;
        CollectionAssert.AreEqual(halfLevel, RenderPixels());
        meter.Position = 0;
        CollectionAssert.AreEqual(silence, RenderPixels());

        meter.RenderMode = AudioLevelMeterRenderMode.Rolling;
        meter.Position = 100;
        meter.Position = 0;
        Assert.IsFalse(silence.SequenceEqual(RenderPixels()));
        meter.Clear();
        CollectionAssert.AreEqual(silence, RenderPixels());

        meter.RenderMode = AudioLevelMeterRenderMode.Static;
        meter.SetSpectrum([0, 0, 0, 0]);
        var silentSpectrum = RenderPixels();
        meter.SetSpectrum([80, 0, 0, 0]);
        var lowerBand = RenderPixels();
        Assert.IsFalse(silentSpectrum.SequenceEqual(lowerBand));
        meter.Position = 100;
        CollectionAssert.AreEqual(lowerBand, RenderPixels());
        meter.SetSpectrum([0, 0, 0, 80]);
        Assert.IsFalse(lowerBand.SequenceEqual(RenderPixels()));
        meter.Clear();
        CollectionAssert.AreEqual(silentSpectrum, RenderPixels());
        return Task.CompletedTask;

        int[] RenderPixels()
        {
            using var bitmap = new Bitmap(meter.Width, meter.Height);
            meter.DrawToBitmap(bitmap, new Rectangle(Point.Empty, meter.Size));
            return Enumerable.Range(0, bitmap.Width * bitmap.Height)
                .Select(index => bitmap.GetPixel(index % bitmap.Width, index / bitmap.Width).ToArgb()).ToArray();
        }
    });

    [TestMethod]
    public Task CallAudioControlsUseTheActiveCallAndRenderInBothHosts() => RunStaAsync(async () =>
    {
        AppUiStartup.Apply();
        var output = Path.Combine(RenderOutput(), "call-audio");
        Directory.CreateDirectory(output);
        var originalFont = WindowsFormsSettings.DefaultFont;
        var originalAppearance = DevExpress.Utils.AppearanceObject.DefaultFont;
        var look = DevExpress.LookAndFeel.UserLookAndFeel.Default;
        var originalSkin = look.ActiveSkinName;
        var originalPalette = look.ActiveSvgPaletteName;
        try
        {
            foreach (var (fontSize, scale, width, height, skin) in new[]
                { (10F, 1F, 1120, 550, "Basic"), (16F, 1F, 650, 690, "Office 2019 Black"), (10F, 1.5F, 780, 630, "Basic") })
            {
                if (skin == "Basic") look.SetSkinStyle(skin, "Blue Dark"); else look.SetSkinStyle(skin);
                using var font = new Font("Segoe UI", fontSize);
                WindowsFormsSettings.DefaultFont = DevExpress.Utils.AppearanceObject.DefaultFont = font;
                using var dialog = new FrmCallerLookup { Font = font, ClientSize = new Size(width, height) };
                dialog.PrepareForBackgroundRun();
                var call = CommandCall() with { State = CallState.Connected };
                var service = new CallAudioDouble(call);
                dialog.BindCall(call, (_, _) => Task.CompletedTask);
                var panel = Field<CallControlPanel>(dialog, "callControls");
                panel.Bind(service, call);
                dialog.Show();
                dialog.ClientSize = new(width, height);
                using var detailsHost = new FrmCallerLookup { Font = font, Text = "Gesprächsdetails" };
                detailsHost.PrepareForBackgroundRun();
                using var callerLayout = detailsHost.Controls[0];
                detailsHost.Controls.Remove(callerLayout);
                using var details = new CallDetailsPage { Font = font, Dock = DockStyle.Fill };
                detailsHost.Controls.Add(details);
                details.BringToFront();
                var detailsPanel = Field<CallControlPanel>(details, "callControls");
                detailsPanel.Bind(service, call);
                detailsHost.Show();
                detailsHost.MinimumSize = Size.Empty;
                detailsHost.ClientSize = new(width, height);
                if (scale != 1F) { dialog.Scale(new SizeF(scale, scale)); detailsHost.Scale(new SizeF(scale, scale)); }
                details.PerformLayout(); dialog.PerformLayout();
                CheckTableBounds(panel); CheckTableBounds(detailsPanel);
                if (scale == 1F && fontSize == 10F)
                    Assert.AreEqual(0, Field<TableLayoutPanel>(panel, "actionStrip").GetRow(Field<TableLayoutPanel>(panel, "recordingActions")));

                foreach (var controls in new[] { panel, detailsPanel })
                {
                    var level = Field<SpinEdit>(controls, "conversationVolume");
                    var microphone = Field<AudioLevelMeter>(controls, "microphonePeak");
                    var microphoneLabel = Field<LabelControl>(controls, "microphonePeakLabel");
                    var speaker = Field<AudioLevelMeter>(controls, "playbackPeak");
                    var speakerLabel = Field<LabelControl>(controls, "playbackPeakLabel");
                    var muteButton = Field<SimpleButton>(controls, "muteMicrophone");
                    Assert.AreSame(level.Parent, microphone.Parent);
                    Assert.AreEqual(level.Left, microphone.Left);
                    Assert.AreEqual(Field<LabelControl>(controls, "volumeLabel").Left, microphoneLabel.Left);
                    var audioTable = Field<TableLayoutPanel>(controls, "audioLayout");
                    var wrapped = audioTable.GetRow(speaker) == 2;
                    Assert.AreEqual(wrapped ? microphoneLabel.Left : muteButton.Left, speakerLabel.Left);
                    if (wrapped) Assert.AreEqual(microphone.Left, speaker.Left);
                    if (width == 1120) Assert.IsFalse(wrapped);
                    Assert.IsTrue(speaker.Left >= speakerLabel.Right && speaker.Right <= audioTable.ClientSize.Width,
                        $"{width}/{fontSize}/{scale}: {speakerLabel.Bounds}, {speaker.Bounds} in {audioTable.ClientSize}");
                    Assert.IsTrue(Math.Abs((speakerLabel.Top + speakerLabel.Height / 2) - (speaker.Top + speaker.Height / 2)) <= 1);
                    if (Field<TableLayoutPanel>(controls, "actionStrip").GetRow(Field<TableLayoutPanel>(controls, "recordingActions")) == 0)
                    {
                        var answer = Field<SimpleButton>(controls, "answerButton");
                        var record = Field<SimpleButton>(controls, "recordingButton");
                        Assert.AreEqual(answer.PointToScreen(Point.Empty).Y, record.PointToScreen(Point.Empty).Y);
                        Assert.AreEqual(answer.Height, record.Height);
                    }
                }

                var volume = Field<SpinEdit>(panel, "conversationVolume");
                var mute = Field<SimpleButton>(detailsPanel, "muteMicrophone");
                Assert.AreEqual(75m, volume.Value);
                Assert.AreEqual(43, Field<AudioLevelMeter>(panel, "microphonePeak").Position);
                Assert.AreEqual(65, Field<AudioLevelMeter>(detailsPanel, "playbackPeak").Position);
                Assert.IsGreaterThanOrEqualTo(56, Field<AudioLevelMeter>(panel, "microphonePeak").Width);
                Assert.IsGreaterThanOrEqualTo(56, Field<AudioLevelMeter>(detailsPanel, "playbackPeak").Width);
                Assert.IsGreaterThanOrEqualTo(11, Field<AudioLevelMeter>(panel, "microphonePeak").Height);
                Assert.IsGreaterThanOrEqualTo(11, Field<AudioLevelMeter>(detailsPanel, "playbackPeak").Height);
                volume.Value = 40;
                mute.PerformClick();
                panel.Observe(call);
                Assert.AreEqual(new(40, true, 0, 65), service.Audio);
                Assert.AreEqual("Stumm aufheben", Field<SimpleButton>(panel, "muteMicrophone").Text);
                Assert.AreEqual(0, Field<AudioLevelMeter>(panel, "microphonePeak").Position);

                service.Audio = service.Audio with
                {
                    PlaybackSpectrum = [0, 4, 18, 82, 54, 25, 12, 35, 63, 45, 31, 22, 12, 5, 0, 0]
                };
                panel.Observe(call);
                detailsPanel.Observe(call);

                var prefix = $"{typeof(MainForm).Assembly.GetName().Name}-{fontSize:0}-{scale:0.0}";
                using (var callerImage = UiAudit.Capture(dialog)) callerImage.Save(Path.Combine(output, prefix + "-caller.png"));
                using (var detailsImage = UiAudit.Capture(detailsHost)) detailsImage.Save(Path.Combine(output, prefix + "-details.png"));

                panel.Observe(call with { State = CallState.Held, Revision = call.Revision + 1 });
                Assert.AreEqual(0, Field<AudioLevelMeter>(panel, "playbackPeak").Position);
                panel.Observe(call with { State = CallState.Ended, Revision = call.Revision + 2 });
                Assert.IsFalse(Field<TableLayoutPanel>(panel, "audioLayout").Visible);
                Assert.IsFalse(Field<System.Windows.Forms.Timer>(panel, "audioTimer").Enabled);
                await detailsPanel.StopAsync();
                await dialog.StopAsync();
            }
        }
        finally
        {
            WindowsFormsSettings.DefaultFont = originalFont;
            DevExpress.Utils.AppearanceObject.DefaultFont = originalAppearance;
            if (string.IsNullOrEmpty(originalPalette)) look.SetSkinStyle(originalSkin); else look.SetSkinStyle(originalSkin, originalPalette);
        }
    });

    private sealed class CallAudioDouble(CallSnapshot call) : ITelephonyService, IConversationAudioService
    {
        internal ConversationAudioSnapshot Audio { get; set; } = new(75, false, 43, 65);
        public ConversationAudioSnapshot? GetAudio(Guid callId) => callId == call.Id ? Audio : null;
        public Task SetPlaybackVolumeAsync(Guid callId, int percent, CancellationToken token)
        { Assert.AreEqual(call.Id, callId); Audio = Audio with { PlaybackVolumePercent = percent }; return Task.CompletedTask; }
        public Task SetMicrophoneMutedAsync(Guid callId, bool muted, CancellationToken token)
        { Assert.AreEqual(call.Id, callId); Audio = Audio with { MicrophoneMuted = muted, MicrophonePeakPercent = muted ? 0 : 43 }; return Task.CompletedTask; }
        public RegistrationSnapshot Registration { get; } = new(RegistrationState.Registered, DateTimeOffset.UtcNow);
        public ImmutableArray<CallSnapshot> GetCalls() => [call];
        public event EventHandler<RegistrationChangedEventArgs>? RegistrationChanged { add { } remove { } }
        public event EventHandler<CallChangedEventArgs>? CallChanged { add { } remove { } }
        public Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken token) => throw new NotSupportedException();
        public Task AnswerAsync(Guid callId, CancellationToken token) => throw new NotSupportedException();
        public Task RejectAsync(Guid callId, CancellationToken token) => throw new NotSupportedException();
        public Task HangUpAsync(Guid callId, CancellationToken token) => throw new NotSupportedException();
        public Task HoldAsync(Guid callId, CancellationToken token) => throw new NotSupportedException();
        public Task ResumeAsync(Guid callId, CancellationToken token) => throw new NotSupportedException();
        public Task TransferAsync(Guid callId, string destination, CancellationToken token) => throw new NotSupportedException();
        public Task SendDtmfAsync(Guid callId, char digit, CancellationToken token) => throw new NotSupportedException();
    }
}
