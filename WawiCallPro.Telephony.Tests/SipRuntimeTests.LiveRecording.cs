using SIPSorcery.SIP.App;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Telephony.Tests;

public sealed partial class SipRuntimeTests
{
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
        private void State(LiveRecordingState state) { _status = _status with { State = state }; RecordingChanged?.Invoke(this, new(_status)); }
        public async Task StartLiveAsync(Guid id, CancellationToken token)
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(token); _startup = startup;
            Starts++; _status = new(id, Guid.NewGuid(), LiveRecordingState.Starting); State(LiveRecordingState.Starting);
            try { if (DelayStart) await Task.Delay(Timeout.Infinite, startup.Token); State(LiveRecordingState.Recording); }
            catch { State(LiveRecordingState.Failed); throw; }
            finally { _startup = null; }
        }
        public void CancelPendingStart() => _startup?.Cancel();
        public Task StopLiveAsync(CancellationToken token) { Stops++; State(LiveRecordingState.Completed); return Task.CompletedTask; }
        public Task<IDuplexPcmRecording> StartRecordingAsync(DuplexPcmRecordingRequest request, CancellationToken token = default) => throw new NotSupportedException();
        public async ValueTask DisposeAsync()
        { CancelPendingStart(); if (_status.State == LiveRecordingState.Recording) await StopLiveAsync(CancellationToken.None); await inner.DisposeAsync(); }
    }
}
