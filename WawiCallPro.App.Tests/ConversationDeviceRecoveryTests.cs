using System.Collections.Concurrent;
using System.Net;
using CSCore.CoreAudioAPI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class ConversationDeviceRecoveryTests
{
    private static readonly AudioFormat Pcmu = new(SDPWellKnownMediaFormatsEnum.PCMU);

    [TestMethod]
    public async Task SavedDevicesSwitchAndRecoverDuringOneRtpSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-device-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new ConfigurationStore(new ConfigurationLocation(root), new ApplicationOptionsValidator(new WindowsUserSecretProtector()));
            await settings.SaveAsync(new() { Devices = new() { CaptureDeviceId = "mic-a", PlaybackDeviceId = "speaker-a", PlaybackVolumePercent = 35 } });
            var configuration = new ApplicationConfigurationService(settings);
            await configuration.StartAsync(CancellationToken.None);
            using var services = new ServiceCollection().AddAudioConversion().AddLogging().BuildServiceProvider();
            var hardware = new Hardware();
            var factory = new WindowsSipMediaSessionFactory(services.GetRequiredService<ISipRtpPcmBridgeFactory>(), hardware,
                configuration, TimeProvider.System, NullLogger.Instance);
            var account = new SipAccountOptions { Connection = new() { BindAddress = "127.0.0.1" } };
            await using var owned = (ISipPcmMediaSession)factory.Create(account, configuration.Current.Options.Devices);
            var media = (VoIPMediaSession)owned.Session;
            var stableSource = media.Media.AudioSource;
            var stableSink = media.Media.AudioSink;
            using var peer = new VoIPMediaSession(format => format.Codec == AudioCodecsEnum.PCMU);
            var peerPackets = new ConcurrentQueue<RTPPacket>();
            peer.OnRtpPacketReceived += (_, kind, packet) => { if (kind == SDPMediaTypesEnum.audio) peerPackets.Enqueue(packet); };
            var observed = new PcmProbe();
            await using var subscription = owned.PcmSource.Subscribe(observed);

            Assert.AreEqual(SetDescriptionResultEnum.OK, peer.SetRemoteDescription(SdpType.offer, media.CreateOffer(IPAddress.Loopback)));
            Assert.AreEqual(SetDescriptionResultEnum.OK, media.SetRemoteDescription(SdpType.answer, peer.CreateAnswer(IPAddress.Loopback)));
            await peer.Start();
            await media.Start();
            owned.ApplyPlaybackSettings();
            await UntilAsync(() => hardware.Current("mic-a")?.Running == true && hardware.Current("speaker-a")?.Running == true);
            var originalCapture = hardware.Current("mic-a")!;
            var originalRender = hardware.Current("speaker-a")!;
            Assert.AreEqual(0.35F, originalRender.Volume);
            originalCapture.Send();
            peer.SendAudio(160, new AudioEncoder().EncodeAudio(new short[160], Pcmu));
            await UntilAsync(() => peerPackets.Count >= 1 && Volatile.Read(ref originalRender.Received) >= 1);

            var snapshot = await settings.LoadAsync();
            await settings.SaveAsync(snapshot.Options with { Devices = snapshot.Options.Devices with { CaptureDeviceId = "mic-b", PlaybackDeviceId = "speaker-b" } }, snapshot);
            await UntilAsync(() => originalCapture.Closed && originalRender.Closed);
            Assert.AreSame(stableSource, media.Media.AudioSource);
            Assert.AreSame(stableSink, media.Media.AudioSink);
            var nextRender = hardware.Current("speaker-b")!;
            var nextCapture = hardware.Current("mic-b")!;
            nextCapture.Send();
            peer.SendAudio(160, new AudioEncoder().EncodeAudio(new short[160], Pcmu));
            await UntilAsync(() => peerPackets.Count >= 2 && Volatile.Read(ref nextRender.Received) >= 1);

            hardware.MarkMissing("speaker-b");
            await UntilAsync(() => nextRender.Closed);
            var receivedBefore = Volatile.Read(ref observed.Received);
            peer.SendAudio(160, new AudioEncoder().EncodeAudio(new short[160], Pcmu));
            nextCapture.Send();
            await UntilAsync(() => Volatile.Read(ref observed.Received) > receivedBefore && peerPackets.Count >= 3);
            Assert.IsFalse(nextCapture.Closed);

            hardware.MarkAvailable("speaker-b");
            await UntilAsync(() => hardware.Current("speaker-b") is { Running: true } current && !ReferenceEquals(current, nextRender));
            var recoveredRender = hardware.Current("speaker-b")!;
            peer.SendAudio(160, new AudioEncoder().EncodeAudio(new short[160], Pcmu));
            await UntilAsync(() => Volatile.Read(ref recoveredRender.Received) >= 1);

            // A stopped WASAPI stream also needs a new device lease while Windows still lists it as active.
            nextCapture.Running = false;
            await UntilAsync(() => nextCapture.Closed && hardware.Current("mic-b")?.Running == true);
            hardware.Current("mic-b")!.Send();
            await UntilAsync(() => peerPackets.Count >= 4);
            Assert.HasCount(1, peerPackets.Select(packet => packet.Header.SyncSource).Distinct().ToArray());
            Assert.IsFalse(media.IsClosed);

            await owned.DisposeAsync();
            Assert.IsTrue(hardware.Devices.All(device => device.Closed));
            Assert.IsNull(await subscription.Completion);
            await configuration.StopAsync(CancellationToken.None);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task FailedReplacementKeepsWorkingDevicesAndHeldMicrophoneStaysStopped()
    {
        var hardware = new Hardware();
        var selection = new ConversationDeviceSelection("mic-a", "speaker-a");
        await using var encoder = new PcmObservingAudioEncoder(new AudioEncoder());
        await using var audio = new RecoveringConversationAudio(hardware, () => selection, encoder, new(), false, TimeProvider.System, NullLogger.Instance);
        audio.SetAudioSourceFormat(Pcmu);
        audio.SetAudioSinkFormat(Pcmu);
        await audio.StartAudio();
        await audio.StartAudioSink();
        audio.RefreshDevices();
        var originalCapture = hardware.Current("mic-a")!;
        var originalRender = hardware.Current("speaker-a")!;

        selection = new("mic-b", "speaker-b");
        hardware.FailOpen = true;
        audio.RefreshDevices();
        Assert.IsTrue(originalCapture.Running);
        Assert.IsTrue(originalRender.Running);
        hardware.FailOpen = false;

        await audio.PauseAudio();
        audio.RefreshDevices();
        var heldCapture = hardware.Current("mic-b")!;
        Assert.IsTrue(originalCapture.Closed);
        Assert.IsTrue(originalRender.Closed);
        Assert.IsFalse(heldCapture.Running);
        await audio.ResumeAudio();
        Assert.IsTrue(heldCapture.Running);
    }

    [TestMethod]
    public async Task NativeMutedPlaybackSwitchesBetweenTwoDevicesAndRecovers()
    {
        if (Environment.GetEnvironmentVariable("WCP_VERIFY_RECORDING_OUTPUT_DEVICE") != "1")
            Assert.Inconclusive("Native WASAPI output is opt-in.");

        using var enumerator = new MMDeviceEnumerator();
        using var devices = enumerator.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active);
        var ids = new List<string>();
        foreach (var device in devices)
        {
            using (device) ids.Add(device.DeviceID);
        }
        if (ids.Count < 2) Assert.Inconclusive("Two active WASAPI render endpoints are required.");

        var hardware = new NativePlaybackProbe();
        await using var encoder = new PcmObservingAudioEncoder(new AudioEncoder());
        var selection = new ConversationDeviceSelection(null, ids[0]);
        await using var audio = new RecoveringConversationAudio(hardware, () => selection, encoder,
            new() { PlaybackVolumePercent = 0, MicrophoneMuted = true }, false, TimeProvider.System, NullLogger.Instance);
        audio.SetAudioSinkFormat(Pcmu);
        await audio.StartAudioSink();
        audio.RefreshDevices();
        var initial = audio.ReadPlaybackBinding();
        Assert.AreEqual(ids[0], initial.DeviceId);

        selection = selection with { PlaybackId = ids[1] };
        audio.RefreshDevices();
        var switched = audio.ReadPlaybackBinding();
        Assert.AreEqual(ids[1], switched.DeviceId);
        Assert.IsTrue(initial.Changed.IsCancellationRequested);
        audio.GotEncodedMediaFrame(new(0, Pcmu, 20, new AudioEncoder().EncodeAudio(new short[160], Pcmu)));

        hardware.Missing = ids[1];
        audio.RefreshDevices();
        Assert.IsTrue(switched.Changed.IsCancellationRequested);
        Assert.Throws<PcmRecordingException>(() => audio.ReadPlaybackBinding());
        hardware.Missing = null;
        audio.RefreshDevices();
        var recovered = audio.ReadPlaybackBinding();
        Assert.AreEqual(ids[1], recovered.DeviceId);
        await audio.DisposeAsync();
        Assert.IsTrue(recovered.Changed.IsCancellationRequested);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        while (!condition()) await Task.Delay(15, timeout.Token);
    }

    private sealed class PcmProbe : IReceivedRtpPcmObserver
    {
        internal int Received;
        public bool TryObserve(ConversationAudioSide side, int sampleRate, int channels, ReadOnlySpan<short> samples) => true;
        public bool TryObserveReceived(RtpPcmPacketPosition packet, int sampleRate, int channels, ReadOnlySpan<short> samples)
        {
            Interlocked.Increment(ref Received);
            return true;
        }
    }

    private sealed class Hardware : IConversationAudioHardware
    {
        internal bool FailOpen;
        internal readonly ConcurrentQueue<Device> Devices = new();
        private readonly ConcurrentDictionary<string, bool> _missing = new(StringComparer.Ordinal);
        internal Device? Current(string id) => Devices.LastOrDefault(device => device.Id == id);
        internal void MarkMissing(string id) => _missing[id] = true;
        internal void MarkAvailable(string id) => _missing.TryRemove(id, out _);
        public string? Resolve(string? preferred, ConversationAudioDirection direction) =>
            preferred is not null && !_missing.ContainsKey(preferred) ? preferred : null;
        public IConversationAudioDevice Open(string id, ConversationAudioDirection direction, PcmObservingAudioEncoder encoder, AudioFormat format)
        {
            if (FailOpen) throw new InvalidOperationException("Synthetic unavailable device");
            var device = new Device(id, encoder, format) { Running = direction == ConversationAudioDirection.Playback };
            Devices.Enqueue(device);
            return device;
        }
    }

    private sealed class NativePlaybackProbe : IConversationAudioHardware
    {
        private readonly ConversationAudioHardware _native = new();
        internal volatile string? Missing;
        public string? Resolve(string? preferred, ConversationAudioDirection direction) =>
            preferred == Missing ? null : _native.Resolve(preferred, direction);
        public IConversationAudioDevice Open(string id, ConversationAudioDirection direction, PcmObservingAudioEncoder encoder, AudioFormat format) =>
            _native.Open(id, direction, encoder, format);
    }

    private sealed class Device(string id, PcmObservingAudioEncoder encoder, AudioFormat format) : IConversationAudioDevice, IAudioSource, IAudioSink
    {
        internal string Id => id;
        internal volatile bool Running;
        internal volatile bool Closed;
        internal int Received;
        internal float Volume;
        bool IConversationAudioDevice.Running => Running;
        public IAudioSource Source => this;
        public IAudioSink Sink => this;
        public void SetVolume(float volume) => Volume = volume;
        public void Dispose() { Running = false; Closed = true; }
        internal void Send() => OnAudioSourceEncodedSample?.Invoke(160, encoder.EncodeAudio(new short[160], format));
        public event EncodedSampleDelegate? OnAudioSourceEncodedSample;
        public event Action<EncodedAudioFrame>? OnAudioSourceEncodedFrameReady { add { } remove { } }
        public event RawAudioSampleDelegate? OnAudioSourceRawSample { add { } remove { } }
        public event SourceErrorDelegate? OnAudioSourceError { add { } remove { } }
        public event SourceErrorDelegate? OnAudioSinkError { add { } remove { } }
        public Task StartAudio() { Running = true; return Task.CompletedTask; }
        public Task ResumeAudio() => StartAudio();
        public Task PauseAudio() { Running = false; return Task.CompletedTask; }
        public Task CloseAudio() => PauseAudio();
        public Task StartAudioSink() => StartAudio();
        public Task ResumeAudioSink() => ResumeAudio();
        public Task PauseAudioSink() => PauseAudio();
        public Task CloseAudioSink() => PauseAudio();
        public bool IsAudioSourcePaused() => !Running;
        public bool HasEncodedAudioSubscribers() => OnAudioSourceEncodedSample is not null;
        public List<AudioFormat> GetAudioSourceFormats() => [format];
        public List<AudioFormat> GetAudioSinkFormats() => [format];
        public void RestrictFormats(Func<AudioFormat, bool> filter) { }
        public void SetAudioSourceFormat(AudioFormat audioFormat) { }
        public void SetAudioSinkFormat(AudioFormat audioFormat) { }
        public void ExternalAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample) => throw new NotSupportedException();
        public void GotAudioRtp(IPEndPoint endpoint, uint ssrc, uint sequence, uint timestamp, int payloadId, bool marker, byte[] payload) => throw new NotSupportedException();
        public void GotEncodedMediaFrame(EncodedAudioFrame frame)
        {
            encoder.DecodeAudio(frame.EncodedAudio, frame.AudioFormat);
            Interlocked.Increment(ref Received);
        }
    }
}
