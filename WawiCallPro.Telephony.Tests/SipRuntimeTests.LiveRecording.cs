using System.Collections.Concurrent;
using System.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Telephony.Tests;

public sealed partial class SipRuntimeTests
{
    [TestMethod]
    public async Task LiveRecordingKeepsItsSessionAcrossRtpDtmfAndHoldResume()
    {
        await using var fixture = await Fixture.CreateAsync();
        LiveMedia? media = null;
        fixture.Media.Wrap = inner => media = new(inner);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);

        using var peer = new SIPUserAgent();
        using var peerMedia = new TestMedia();
        var peerDigits = new ConcurrentQueue<byte>();
        var localDigits = new ConcurrentQueue<DtmfReceivedEventArgs>();
        peer.OnDtmfTone += (tone, _) => peerDigits.Enqueue(tone);
        fixture.Runtime.DtmfReceived += (_, tone) => localDigits.Enqueue(tone);

        var incoming = peer.Call(fixture.Destination, "caller", "synthetic", peerMedia.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().SingleOrDefault()?.State == CallState.Ringing);
        var id = fixture.Runtime.GetCalls().Single().Id;
        await fixture.Runtime.AnswerAsync(id);
        Assert.IsTrue(await incoming.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsNotNull(media);
        await fixture.Runtime.StartLiveRecordingAsync(id);

        await fixture.Runtime.SendDtmfAsync(id, '#');
        await UntilAsync(() => peerDigits.Contains((byte)11));
        await fixture.Runtime.HoldAsync(id);
        Assert.AreEqual(CallState.Held, fixture.Runtime.GetCalls().Single().State);
        Assert.AreEqual(LiveRecordingState.Recording, fixture.Runtime.GetRecording(id).State);
        await fixture.Runtime.ResumeAsync(id);
        Assert.AreEqual(CallState.Connected, fixture.Runtime.GetCalls().Single().State);
        await peer.SendDtmf(5);
        await UntilAsync(() => localDigits.Any(tone => tone.CallId == id && tone.Digit == '5'));
        Assert.AreEqual(LiveRecordingState.Recording, fixture.Runtime.GetRecording(id).State);
        Assert.AreEqual(1, media.Starts);
        Assert.AreEqual(0, media.Stops);

        await fixture.Runtime.HangUpAsync(id);
        await UntilAsync(() => fixture.Media.Closed == 1);
        Assert.AreEqual(1, media.Stops);
        Assert.AreEqual(LiveRecordingState.Completed, fixture.Runtime.GetRecording(id).State);
    }

    [TestMethod]
    [DataRow("remote")]
    [DataRow("local")]
    [DataRow("disconnect")]
    public async Task LiveRecordingCommandsAndCallEndShareTheActualSipMediaOwner(string ending)
    {
        await using var fixture = await Fixture.CreateAsync(); LiveMedia? media = null;
        fixture.Media.Wrap = inner => media = new(inner);
        await fixture.Runtime.ConnectAsync(); await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peer = new SIPUserAgent(); using var peerMedia = new TestMedia();
        var outgoing = peer.Call(fixture.Destination, "caller", "synthetic", peerMedia.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing));
        var id = fixture.Runtime.GetCalls().Single().Id;
        await Assert.ThrowsAsync<TelephonyException>(() => fixture.Runtime.StartLiveRecordingAsync(id));
        await fixture.Runtime.AnswerAsync(id); Assert.IsTrue(await outgoing.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsNotNull(media);
        await Assert.ThrowsAsync<TelephonyException>(() => fixture.Runtime.StartLiveRecordingAsync(Guid.NewGuid()));
        var changes = new List<LiveRecordingState>(); fixture.Runtime.RecordingChanged += (_, e) => changes.Add(e.Snapshot.State);
        await fixture.Runtime.StartLiveRecordingAsync(id);
        Assert.AreEqual(LiveRecordingState.Recording, fixture.Runtime.GetRecording(id).State);
        Assert.AreEqual(1, media.Starts);
        await fixture.Runtime.StopLiveRecordingAsync(id); await fixture.Runtime.StopLiveRecordingAsync(id);
        Assert.AreEqual(1, media.Stops);
        await fixture.Runtime.StartLiveRecordingAsync(id);
        if (ending == "remote") peer.Hangup();
        else if (ending == "local") await fixture.Runtime.HangUpAsync(id);
        else await fixture.Runtime.DisconnectAsync();
        await UntilAsync(() => fixture.Media.Closed == 1);
        Assert.AreEqual(LiveRecordingState.Completed, fixture.Runtime.GetRecording(id).State);
        Assert.AreEqual(2, media.Stops);
        await fixture.Runtime.StopLiveRecordingAsync(id);
        Assert.IsTrue(changes.Contains(LiveRecordingState.Completed));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RealLocalOrRemoteHangupCancelsAnOutstandingLiveStart(bool remote)
    {
        await using var fixture = await Fixture.CreateAsync(); LiveMedia? media = null;
        fixture.Media.Wrap = inner => media = new(inner) { DelayStart = true };
        await fixture.Runtime.ConnectAsync(); await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peer = new SIPUserAgent(); using var peerMedia = new TestMedia();
        var outgoing = peer.Call(fixture.Destination, "caller", "synthetic", peerMedia.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing));
        var id = fixture.Runtime.GetCalls().Single().Id;
        await fixture.Runtime.AnswerAsync(id); Assert.IsTrue(await outgoing.WaitAsync(TimeSpan.FromSeconds(5)));
        var start = fixture.Runtime.StartLiveRecordingAsync(id);
        await UntilAsync(() => media?.Starts == 1);
        if (remote) peer.Hangup(); else await fixture.Runtime.HangUpAsync(id).WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAsync<OperationCanceledException>(() => start.WaitAsync(TimeSpan.FromSeconds(3)));
        await UntilAsync(() => fixture.Media.Closed == 1);
        Assert.AreEqual(LiveRecordingState.Failed, fixture.Runtime.GetRecording(id).State);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task IncomingRecordingFollowsSavedOptInAndNeverStartsWhileRinging(bool enabled)
    {
        await using var fixture = await Fixture.CreateAsync();
        LiveMedia? media = null;
        fixture.Media.Wrap = inner => media = new(inner);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        if (enabled) await fixture.SetAutomaticRecordingAsync(true);
        using var peer = new SIPUserAgent();
        using var peerMedia = new TestMedia();
        var call = peer.Call(fixture.Destination, "caller", "synthetic", peerMedia.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().SingleOrDefault()?.State == CallState.Ringing);
        var id = fixture.Runtime.GetCalls().Single().Id;
        Assert.AreEqual(LiveRecordingState.Idle, fixture.Runtime.GetRecording(id).State);
        Assert.IsNull(media);
        await fixture.Runtime.AnswerAsync(id);
        Assert.IsTrue(await call.WaitAsync(TimeSpan.FromSeconds(5)));
        if (enabled) await UntilAsync(() => fixture.Runtime.GetRecording(id).State == LiveRecordingState.Recording);
        await fixture.Runtime.DisconnectAsync();
        Assert.IsNotNull(media);
        Assert.AreEqual(enabled ? 1 : 0, media.Starts);
        Assert.AreEqual(enabled ? 1 : 0, media.Stops);
        Assert.AreEqual(enabled ? LiveRecordingState.Completed : LiveRecordingState.Idle, fixture.Runtime.GetRecording(id).State);
    }

    [TestMethod]
    public async Task OutgoingAutoRecordingStartsOnceAndManualStopIsNotRearmedByResume()
    {
        await using var fixture = await Fixture.CreateAsync();
        LiveMedia? media = null;
        fixture.Media.Wrap = inner => media = new(inner);
        await fixture.SetAutomaticRecordingAsync(true);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var transport = new SIPTransport();
        var channel = new SIPUDPChannel(new IPEndPoint(IPAddress.Loopback, 0));
        transport.AddSIPChannel(channel);
        using var peer = new SIPUserAgent(transport, null, true);
        using var peerMedia = new TestMedia();
        peer.OnIncomingCall += async (agent, request) => await agent.Answer(agent.AcceptCall(request), peerMedia.Session);
        var callId = await fixture.Runtime.DialAsync(new(LexwareProductLine.Pro, "isolated", "1"), $"sip:peer@127.0.0.1:{channel.Port}");
        await UntilAsync(() => fixture.Runtime.GetRecording(callId).State == LiveRecordingState.Recording);
        Assert.IsNotNull(media);
        Assert.AreEqual(1, media.Starts);
        await fixture.Runtime.HoldAsync(callId);
        await fixture.Runtime.StopLiveRecordingAsync(callId);
        await fixture.Runtime.ResumeAsync(callId);
        await fixture.Runtime.HangUpAsync(callId);
        Assert.AreEqual(1, media.Starts);
        Assert.AreEqual(1, media.Stops);
        Assert.AreEqual(LiveRecordingState.Completed, fixture.Runtime.GetRecording(callId).State);
    }

    [TestMethod]
    [DataRow("stop")]
    [DataRow("hangup")]
    [DataRow("remote")]
    [DataRow("disconnect")]
    public async Task PendingAutomaticRecordingCanBeCancelledWithoutBlockingSip(string action)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Media.Wrap = inner => new LiveMedia(inner) { DelayStart = true };
        await fixture.SetAutomaticRecordingAsync(true);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peer = new SIPUserAgent();
        using var peerMedia = new TestMedia();
        var call = peer.Call(fixture.Destination, "caller", "synthetic", peerMedia.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().SingleOrDefault()?.State == CallState.Ringing);
        var id = fixture.Runtime.GetCalls().Single().Id;
        await fixture.Runtime.AnswerAsync(id).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.IsTrue(await call.WaitAsync(TimeSpan.FromSeconds(5)));
        await UntilAsync(() => fixture.Runtime.GetRecording(id).State == LiveRecordingState.Starting);
        switch (action)
        {
            case "stop":
                await fixture.Runtime.StopLiveRecordingAsync(id).WaitAsync(TimeSpan.FromSeconds(3));
                Assert.AreEqual(CallState.Connected, fixture.Runtime.GetCalls().Single().State);
                Assert.AreEqual(0, fixture.Media.Closed);
                break;
            case "hangup":
                await fixture.Runtime.HangUpAsync(id).WaitAsync(TimeSpan.FromSeconds(3));
                break;
            case "remote":
                peer.Hangup();
                break;
            case "disconnect":
                await fixture.Runtime.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(4));
                break;
        }
        if (action != "stop") await UntilAsync(() => fixture.Media.Closed == 1);
        await UntilAsync(() => fixture.Runtime.GetRecording(id).Failure == LiveRecordingFailure.Cancelled);
        Assert.AreEqual(LiveRecordingState.Failed, fixture.Runtime.GetRecording(id).State);
    }

    [TestMethod]
    public async Task UnavailableAutomaticRecordingReportsFailureWithoutEndingTheCall()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SetAutomaticRecordingAsync(true);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peer = new SIPUserAgent();
        using var peerMedia = new TestMedia();
        var call = peer.Call(fixture.Destination, "caller", "synthetic", peerMedia.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().SingleOrDefault()?.State == CallState.Ringing);
        var id = fixture.Runtime.GetCalls().Single().Id;
        await fixture.Runtime.AnswerAsync(id);
        Assert.IsTrue(await call.WaitAsync(TimeSpan.FromSeconds(5)));
        await UntilAsync(() => fixture.Runtime.GetRecording(id).Failure == LiveRecordingFailure.Unavailable);
        Assert.IsNull(fixture.Runtime.GetRecording(id).RecordingId);
        Assert.AreEqual(CallState.Connected, fixture.Runtime.GetCalls().Single().State);
        Assert.AreEqual(RegistrationState.Registered, fixture.Runtime.Registration.State);
        await fixture.Runtime.HangUpAsync(id);
    }

    private sealed class LiveMedia(ISipMediaSession inner) : ISipLiveRecordingMediaSession
    {
        private CancellationTokenSource? _startup;
        private LiveRecordingSnapshot _status = new(Guid.Empty, null, LiveRecordingState.Idle);
        internal int Starts, Stops;
        internal bool DelayStart;
        public LiveRecordingSnapshot RecordingStatus => _status;
        public event EventHandler<LiveRecordingChangedEventArgs>? RecordingChanged;
        public IMediaSession Session => inner.Session;
        public IConversationPcmSource PcmSource => throw new NotSupportedException();
        public void ApplyPlaybackSettings() => inner.ApplyPlaybackSettings();
        private void State(LiveRecordingState state, LiveRecordingFailure? failure = null)
        { _status = _status with { State = state, Failure = failure }; RecordingChanged?.Invoke(this, new(_status)); }
        public async Task StartLiveAsync(Guid id, CancellationToken token)
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(token); _startup = startup;
            Starts++; _status = new(id, Guid.NewGuid(), LiveRecordingState.Starting); State(LiveRecordingState.Starting);
            try { if (DelayStart) await Task.Delay(Timeout.Infinite, startup.Token); State(LiveRecordingState.Recording); }
            catch (OperationCanceledException) { State(LiveRecordingState.Failed, LiveRecordingFailure.Cancelled); throw; }
            finally { _startup = null; }
        }
        public void CancelPendingStart() => _startup?.Cancel();
        public async Task StopLiveAsync(CancellationToken token)
        {
            // The real media session waits for an in-flight start to release its command gate.
            while (_status.State == LiveRecordingState.Starting) await Task.Delay(10, token);
            if (_status.State != LiveRecordingState.Recording) return;
            Stops++;
            State(LiveRecordingState.Completed);
        }
        public Task<IDuplexPcmRecording> StartRecordingAsync(DuplexPcmRecordingRequest request, CancellationToken token = default) => throw new NotSupportedException();
        public async ValueTask DisposeAsync()
        { CancelPendingStart(); if (_status.State == LiveRecordingState.Recording) await StopLiveAsync(CancellationToken.None); await inner.DisposeAsync(); }
    }
}
