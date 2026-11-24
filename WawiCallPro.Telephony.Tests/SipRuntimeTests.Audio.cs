using SIPSorcery.SIP.App;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;

namespace WawiCallPro.Telephony.Tests;

public sealed partial class SipRuntimeTests
{
    [TestMethod]
    public async Task AudioCommandsTargetOnlyTheConnectedOrHeldCall()
    {
        await using var fixture = await Fixture.CreateAsync();
        var audio = new TestConversationAudio();
        fixture.Media.Wrap = inner => new MediaWithAudio(inner, audio);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peer = new SIPUserAgent();
        using var media = new TestMedia();
        var calling = peer.Call(fixture.Destination, "caller", "synthetic", media.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().SingleOrDefault()?.State == CallState.Ringing);
        var id = fixture.Runtime.GetCalls().Single().Id;
        Assert.IsNull(fixture.Runtime.GetAudio(id));
        await Assert.ThrowsAsync<TelephonyException>(() => fixture.Runtime.SetMicrophoneMutedAsync(id, true));
        await fixture.Runtime.AnswerAsync(id);
        Assert.IsTrue(await calling.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<TelephonyException>(() => fixture.Runtime.SetPlaybackVolumeAsync(Guid.NewGuid(), 70));
        await fixture.Runtime.SetPlaybackVolumeAsync(id, 70);
        await fixture.Runtime.HoldAsync(id);
        await fixture.Runtime.SetMicrophoneMutedAsync(id, true);
        Assert.AreEqual(new ConversationAudioSnapshot(70, true, 0, 0), fixture.Runtime.GetAudio(id));
        await fixture.Runtime.ResumeAsync(id);
        await fixture.Runtime.HangUpAsync(id);
        Assert.IsNull(fixture.Runtime.GetAudio(id));
        await Assert.ThrowsAsync<TelephonyException>(() => fixture.Runtime.SetMicrophoneMutedAsync(id, false));
    }

    private sealed class MediaWithAudio(ISipMediaSession inner, ISipConversationAudio audio) : ISipMediaSession
    {
        public IMediaSession Session => inner.Session;
        public ISipConversationAudio ConversationAudio => audio;
        public void ApplyPlaybackSettings() => inner.ApplyPlaybackSettings();
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class TestConversationAudio : ISipConversationAudio
    {
        private ConversationAudioSnapshot _state = new(100, false, 0, 0);
        public ConversationAudioSnapshot GetAudio() => _state;
        public void SetPlaybackVolume(int percent) => _state = _state with { PlaybackVolumePercent = percent };
        public void SetMicrophoneMuted(bool muted) => _state = _state with { MicrophoneMuted = muted };
    }
}
