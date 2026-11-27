using CSCore;
using CSCore.Codecs.WAV;
using CSCore.SoundIn;
using DevExpress.XtraEditors;
using DtmfDetection;
using DtmfDetection.CSCore;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task DialKeypadEditsTheDestinationWithoutStartingACall() => RunStaAsync(() =>
    {
        using var dialog = new FrmDial();
        dialog.PrepareForBackgroundRun();
        dialog.Show();
        var edit = Field<TextEdit>(dialog, "destinationEdit");
        edit.Text = "523";
        edit.SelectionStart = 1;
        edit.SelectionLength = 1;
        var keypad = Field<DialKeypad>(dialog, "dialKeypad");
        Field<SimpleButton>(keypad, "one").PerformClick();
        Assert.AreEqual("513", edit.Text);
        Field<SimpleButton>(dialog, "eraseButton").PerformClick();
        Assert.AreEqual("53", edit.Text);
        Assert.IsNull(dialog.CallId);
        CheckTableBounds(dialog);
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task DialKeypadSendsToTheConnectedCallWithoutSelectingACompany() => RunStaAsync(async () =>
    {
        var call = CommandCall() with { State = CallState.Connected };
        var service = new CommandTelephony { Current = call };
        var application = WawiCallPro.Utilities.Hosting.ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly);
        using var dialog = new FrmDial(new TestDirectory(application), service);
        dialog.PrepareForBackgroundRun();
        dialog.Show();
        await dialog.ReloadAsync();
        var toggle = Field<CheckButton>(dialog, "dtmfToggle");
        Assert.IsTrue(toggle.Checked);
        Assert.IsTrue(toggle.Enabled);
        var keypad = Field<DialKeypad>(dialog, "dialKeypad");
        var digit = Field<SimpleButton>(keypad, "one");
        Assert.AreEqual(6, digit.Appearance.FontSizeDelta);
        digit.PerformClick();
        CollectionAssert.AreEqual(new[] { ("Dtmf:1", call.Id) }, service.Commands.ToArray());
        Assert.IsEmpty(Field<TextEdit>(dialog, "destinationEdit").Text);
        service.Publish(call with { State = CallState.Held });
        await dialog.InvokeAsync(() => Assert.IsFalse(keypad.Enabled));
        using var shot = WawiCallPro.App.Diagnostics.UiAudit.Capture(dialog);
        var output = Path.Combine(RenderOutput(), "dtmf");
        Directory.CreateDirectory(output);
        shot.Save(Path.Combine(output, typeof(MainForm).Assembly.GetName().Name + "-dial.png"),
            System.Drawing.Imaging.ImageFormat.Png);
        await dialog.StopAsync();
    });

    [TestMethod]
    public Task DtmfKeypadQueuesTonesForTheConnectedCallAndDropsTheQueueOnHold() => RunStaAsync(async () =>
    {
        using var dialog = new FrmCallerLookup();
        dialog.PrepareForBackgroundRun();
        dialog.ClientSize = new Size(640, 540);
        var call = CommandCall();
        var service = new CommandTelephony { Current = call };
        dialog.BindCall(call, (_, _) => Task.CompletedTask);
        var panel = Field<CallControlPanel>(dialog, "callControls");
        panel.Bind(service, call);
        dialog.Show();
        var keypad = Field<DialKeypad>(panel, "dtmfKeypad");
        Assert.IsTrue(keypad.Visible);
        Assert.IsFalse(keypad.Enabled);
        panel.Observe(call with { State = CallState.Connected, Revision = 2 });
        Assert.IsTrue(keypad.Enabled);
        panel.PerformLayout();
        CheckTableBounds(panel);
        var output = Path.Combine(RenderOutput(), "dtmf");
        Directory.CreateDirectory(output);
        using (var shot = WawiCallPro.App.Diagnostics.UiAudit.Capture(dialog))
            shot.Save(Path.Combine(output, typeof(MainForm).Assembly.GetName().Name + "-tonwahl.png"),
                System.Drawing.Imaging.ImageFormat.Png);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Execute = (command, _) => command == "Dtmf:1" ? release.Task : Task.CompletedTask;
        var pending = panel.SendDtmfAsync('1');
        await panel.SendDtmfAsync('#');
        await panel.SendDtmfAsync('2');
        release.SetResult();
        await pending;
        CollectionAssert.AreEqual(new[] { "Dtmf:1", "Dtmf:#", "Dtmf:2" }, service.Commands.Select(item => item.Command).ToArray());
        Assert.IsTrue(service.Commands.All(item => item.Id == call.Id));
        release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        pending = panel.SendDtmfAsync('1');
        await panel.SendDtmfAsync('*');
        panel.Observe(call with { State = CallState.Held, Revision = 3 });
        release.SetResult();
        await pending;
        Assert.HasCount(4, service.Commands);
        Assert.IsTrue(keypad.Visible);
        Assert.IsFalse(keypad.Enabled);
        await panel.StopAsync();
    });

    [TestMethod]
    public void DtmfWaveAnalysisUsesSamplePositionsAndEmitsTheLastToneAtEndOfFile()
    {
        var expected = new[] { DtmfTone.One, DtmfTone.Two, DtmfTone.Three, DtmfTone.A,
            DtmfTone.Four, DtmfTone.Five, DtmfTone.Six, DtmfTone.B, DtmfTone.Seven, DtmfTone.Eight,
            DtmfTone.Nine, DtmfTone.C, DtmfTone.Star, DtmfTone.Zero, DtmfTone.Hash, DtmfTone.D };
        var path = Path.Combine(Path.GetTempPath(), $"wawi-dtmf-{Guid.NewGuid():N}.wav");
        try
        {
            var pcm = expected.SelectMany((tone, index) => index == expected.Length - 1
                ? GenerateTone(tone) : GenerateTone(tone).Concat(new byte[1600])).ToArray();
            using (var writer = new WaveWriter(path, new WaveFormat(8000, 16, 1)))
                writer.Write(pcm, 0, pcm.Length);
            using var wave = new WaveFileReader(path);
            var detected = wave.DtmfTones().ToArray();
            CollectionAssert.AreEqual(expected, detected.Select(tone => tone.DtmfTone).ToArray());
            for (var index = 0; index < detected.Length; index++)
            {
                Assert.AreEqual(index * 305.0, detected[index].Position.TotalMilliseconds, 27);
                Assert.AreEqual(205.0, detected[index].Duration.TotalMilliseconds, 32);
            }
            wave.Position = 0;
            Assert.IsTrue(wave.Read(new byte[2], 0, 2) > 0);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public async Task DtmfLiveCaptureStopsAnIdleWorkerAndCanRestartWithNewAudio()
    {
        using var input = new DtmfInputDouble();
        using var analyzer = new LiveAudioDtmfAnalyzer(input);
        analyzer.StartCapturing();
        Assert.IsTrue(analyzer.IsCapturing);
        await Task.Run(analyzer.StopCapturing).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsFalse(analyzer.IsCapturing);
        var result = new TaskCompletionSource<DtmfTone>(TaskCreationOptions.RunContinuationsAsynchronously);
        analyzer.DtmfToneStopped += tone => result.TrySetResult(tone.DtmfTone);
        analyzer.CaptureFailed += error => result.TrySetException(error);
        analyzer.StartCapturing();
        var wave = GenerateTone(DtmfTone.Five).Concat(new byte[1600]).ToArray();
        input.Push(wave[..120]);
        input.Push(wave[120..]);
        Assert.AreEqual(DtmfTone.Five, await result.Task.WaitAsync(TimeSpan.FromSeconds(3)));
        await Task.Run(analyzer.StopCapturing).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsFalse(analyzer.IsCapturing);
        Assert.IsFalse(input.Disposed);
    }

    private static byte[] GenerateTone(DtmfTone tone)
    {
        var samples = new byte[1640 * 2];
        for (var index = 0; index < samples.Length / 2; index++)
        {
            var time = 2.0 * Math.PI * index / 8000;
            var value = (short)(8500 * (Math.Sin(time * tone.LowTone) + Math.Sin(time * tone.HighTone)));
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(samples.AsSpan(2 * index), value);
        }
        return samples;
    }

    private sealed class DtmfInputDouble : ISoundIn
    {
        public event EventHandler<DataAvailableEventArgs>? DataAvailable;
        public event EventHandler<RecordingStoppedEventArgs>? Stopped;
        public WaveFormat WaveFormat { get; } = new(8000, 16, 1);
        public RecordingState RecordingState { get; private set; }
        internal bool Disposed { get; private set; }
        public void Initialize() { }
        public void Start() => RecordingState = RecordingState.Recording;
        public void Stop()
        {
            RecordingState = RecordingState.Stopped;
            Stopped?.Invoke(this, new RecordingStoppedEventArgs());
        }
        internal void Push(byte[] data) => DataAvailable?.Invoke(this, new(data, 0, data.Length, WaveFormat));
        public void Dispose() => Disposed = true;
    }
}
