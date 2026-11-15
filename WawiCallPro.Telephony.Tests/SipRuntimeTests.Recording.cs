using SIPSorcery.SIP.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    [DataRow("host")]
    public async Task CallEndWaitsForItsRecordingBeforeReleasingMediaAndAccount(string end)
    {
        await using var fixture = await Fixture.CreateAsync(); RecordingMedia? recordingMedia = null;
        fixture.Media.Wrap = inner => recordingMedia = new(inner);
        await fixture.Runtime.ConnectAsync(); await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peer = new SIPUserAgent(); using var peerMedia = new TestMedia();
        var outgoing = peer.Call(fixture.Destination, "caller", "synthetic", peerMedia.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing)); var id = fixture.Runtime.GetCalls().Single().Id;
        var request = RecordingRequest(id);
        var ringing = await Assert.ThrowsExactlyAsync<TelephonyException>(() => fixture.Runtime.StartRecordingAsync(request)); Assert.AreEqual("CallNotConnected", ringing.Code);
        await fixture.Runtime.AnswerAsync(id); Assert.IsTrue(await outgoing.WaitAsync(TimeSpan.FromSeconds(5))); Assert.IsNotNull(recordingMedia);
        var wrongCall = await Assert.ThrowsExactlyAsync<TelephonyException>(() => fixture.Runtime.StartRecordingAsync(RecordingRequest(Guid.NewGuid())));
        Assert.AreEqual("CallNotActive", wrongCall.Code); Assert.AreEqual(0, recordingMedia.Starts);
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Runtime.StartRecordingAsync(request, new CancellationToken(true)));
        var recording = await fixture.Runtime.StartRecordingAsync(request); Assert.AreSame(recordingMedia.Recording, recording); Assert.AreSame(request, recordingMedia.Request);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddSipTelephony(false);
        builder.Services.AddSingleton(fixture.Runtime);
        builder.Services.AddSingleton(fixture.Configuration);
        var host = builder.Build();
        await using var hostOwner = (IAsyncDisposable)host;
        await host.StartAsync();
        recordingMedia.PauseCleanup = true;
        Task closing;
        if (end == "remote") { peer.Hangup(); closing = Task.CompletedTask; }
        else closing = end == "local" ? fixture.Runtime.HangUpAsync(id) : end == "host" ? host.StopAsync() : fixture.Runtime.DisconnectAsync();
        try
        {
            await recordingMedia.Disposing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0, fixture.Media.Closed); Assert.IsTrue(fixture.Leases.Active); Assert.IsFalse(recordingMedia.Recording.Disposed);
        }
        finally { recordingMedia.Release.TrySetResult(); }
        await closing.WaitAsync(TimeSpan.FromSeconds(5));
        await UntilAsync(() => fixture.Media.Closed == 1);
        Assert.IsTrue(recordingMedia.Recording.Disposed); Assert.AreEqual(PcmRecordingState.Aborted, recording.State);
        await fixture.Runtime.DisconnectAsync(); Assert.IsFalse(fixture.Leases.Active);
        var ended = await Assert.ThrowsExactlyAsync<TelephonyException>(() => fixture.Runtime.StartRecordingAsync(request)); Assert.AreEqual("CallNotActive", ended.Code);
    }

    [TestMethod]
    public async Task RemoteByeDuringRecordingStartWaitsForTheNewOwnerBeforeCleanup()
    {
        await using var fixture = await Fixture.CreateAsync(); RecordingMedia? media = null;
        fixture.Media.Wrap = inner => media = new(inner);
        await fixture.Runtime.ConnectAsync(); await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peer = new SIPUserAgent(); using var peerMedia = new TestMedia();
        var outgoing = peer.Call(fixture.Destination, "caller", "synthetic", peerMedia.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing)); var id = fixture.Runtime.GetCalls().Single().Id;
        await fixture.Runtime.AnswerAsync(id); Assert.IsTrue(await outgoing.WaitAsync(TimeSpan.FromSeconds(5))); Assert.IsNotNull(media);
        media.StartRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = fixture.Runtime.StartRecordingAsync(RecordingRequest(id));
        try
        {
            await media.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)); peer.Hangup();
            Assert.IsFalse(start.IsCompleted); Assert.AreEqual(0, fixture.Media.Closed);
        }
        finally { media.StartRelease.TrySetResult(); }
        Assert.AreSame(media.Recording, await start.WaitAsync(TimeSpan.FromSeconds(5)));
        await UntilAsync(() => fixture.Media.Closed == 1); Assert.IsTrue(media.Recording.Disposed);
        Assert.AreEqual(CallEndReason.RemoteHangup, fixture.Runtime.GetCalls().Single().EndReason);
    }

    [TestMethod]
    public async Task ConnectedCallWithoutRecordingCapabilityIsRejectedWithoutEndingTheCall()
    {
        await using var fixture = await Fixture.CreateAsync(); await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peer = new SIPUserAgent(); using var peerMedia = new TestMedia();
        var outgoing = peer.Call(fixture.Destination, "caller", "synthetic", peerMedia.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing)); var id = fixture.Runtime.GetCalls().Single().Id;
        await fixture.Runtime.AnswerAsync(id); Assert.IsTrue(await outgoing.WaitAsync(TimeSpan.FromSeconds(5)));
        var error = await Assert.ThrowsExactlyAsync<TelephonyException>(() => fixture.Runtime.StartRecordingAsync(RecordingRequest(id)));
        Assert.AreEqual("RecordingUnavailable", error.Code); Assert.AreEqual(CallState.Connected, fixture.Runtime.GetCalls().Single().State);
        await fixture.Runtime.HangUpAsync(id);
    }

    private static DuplexPcmRecordingRequest RecordingRequest(Guid callId) => new(new(callId, Guid.NewGuid(), "unused.wav"),
        new(1, 0, 8000, TimeSpan.Zero), new(2, 0, 0, 8000, TimeSpan.Zero));

    private sealed class RecordingMedia(ISipMediaSession inner) : ISipRecordingMediaSession
    {
        internal readonly RuntimeRecording Recording = new();
        internal readonly TaskCompletionSource Disposing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource? StartRelease;
        internal DuplexPcmRecordingRequest? Request;
        internal int Starts;
        internal bool PauseCleanup;
        public IMediaSession Session => inner.Session;
        public IConversationPcmSource PcmSource => throw new NotSupportedException();
        public void ApplyPlaybackSettings() => inner.ApplyPlaybackSettings();
        public async Task<IDuplexPcmRecording> StartRecordingAsync(DuplexPcmRecordingRequest request, CancellationToken cancellationToken = default)
        { Starts++; Request = request; Started.TrySetResult(); if (StartRelease is not null) await StartRelease.Task; return Recording; }
        public async ValueTask DisposeAsync()
        { Disposing.TrySetResult(); if (PauseCleanup) await Release.Task; await Recording.DisposeAsync(); await inner.DisposeAsync(); }
    }
    private sealed class RuntimeRecording : IDuplexPcmRecording
    {
        internal bool Disposed;
        public PcmRecordingState State => Disposed ? PcmRecordingState.Aborted : PcmRecordingState.Capturing;
        public PcmRecordingFailure? Failure => null;
        public Task InputCompletion => Task.CompletedTask;
        public Task<PcmRecordingResult> StopAsync(TimeSpan duration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
