using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using SIPSorcery.Media;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Telephony.Sip;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Telephony.Tests;

[TestClass, DoNotParallelize]
public sealed class SipRuntimeTests
{
    [TestMethod]
    public async Task RealUdpRegistrationAndUnregistrationUseConfiguredPortsAndSafeStatus()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        Assert.IsTrue(fixture.Registers > 0);
        Assert.AreEqual(fixture.LocalPort, fixture.LastRegistrationPort);
        Assert.AreEqual(1, fixture.Persistence.Recoveries);
        Assert.IsFalse(fixture.Media.Created > 0, "Registration must not open an audio endpoint.");
        await fixture.Runtime.DisconnectAsync();
        Assert.AreEqual(RegistrationState.Stopped, fixture.Runtime.Registration.State);
        Assert.IsTrue(fixture.Unregisters > 0);
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, fixture.LocalPort));
    }

    [TestMethod]
    public async Task RealTcpRegistrationKeepsRegistrarAndContactTransportConsistent()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.UseTcpAsync();
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        Assert.AreEqual(SIPProtocolsEnum.tcp, fixture.LastContactProtocol);
        Assert.AreEqual(fixture.LocalPort, fixture.LastRegistrationPort);
        await fixture.Runtime.DisconnectAsync();
        Assert.IsTrue(fixture.Unregisters > 0);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LostRegistrarRecoversAutomaticallyWithoutReacquiringAccountOrOpeningAudio(bool tcp)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (tcp) await fixture.UseTcpAsync();
        fixture.DropRegistrations = true;
        var states = new ConcurrentQueue<RegistrationSnapshot>();
        fixture.Runtime.RegistrationChanged += (_, change) => states.Enqueue(change.Registration);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Reconnecting);
        Assert.AreEqual("RegistrationRetry", fixture.Runtime.Registration.ErrorCode);
        Assert.IsTrue(fixture.Leases.Active);
        var attempts = Volatile.Read(ref fixture.Registers);
        await UntilAsync(() => Volatile.Read(ref fixture.Registers) > attempts);
        fixture.DropRegistrations = false;
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        Assert.IsNull(fixture.Runtime.LastFailureCode);
        Assert.AreEqual(1, fixture.Persistence.Recoveries);

        // The registrar grants a shorter lifetime than requested. A later refresh loses its response.
        fixture.DropRegistrations = true;
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Reconnecting);
        var notReady = await Assert.ThrowsAsync<TelephonyException>(() => fixture.Runtime.DialAsync(
            new(LexwareProductLine.Pro, "isolated-sip", "1"), "123"));
        Assert.AreEqual("NotRegistered", notReady.Code);
        fixture.DropRegistrations = false;
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        Assert.IsGreaterThanOrEqualTo(2, states.Count(state => state.State == RegistrationState.Reconnecting));
        Assert.AreEqual(1, fixture.Persistence.Recoveries);
        Assert.AreEqual(0, fixture.Media.Created);

        await fixture.Runtime.DisconnectAsync();
        Assert.AreEqual(RegistrationState.Stopped, fixture.Runtime.Registration.State);
        Assert.IsFalse(fixture.Leases.Active);
        var stoppedAttempts = Volatile.Read(ref fixture.Registers);
        await Task.Delay(2200);
        Assert.AreEqual(stoppedAttempts, Volatile.Read(ref fixture.Registers), "Shutdown must stop registration retries.");
    }

    [TestMethod]
    public async Task PermanentRegistrationFailureRequiresExplicitRetry()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.RejectRegistration = true;
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Failed);
        var rejectedAttempts = Volatile.Read(ref fixture.Registers);
        fixture.RejectRegistration = false;
        await Task.Delay(2200);
        Assert.AreEqual(rejectedAttempts, Volatile.Read(ref fixture.Registers), "A permanent account error must not create a login retry loop.");
        Assert.AreEqual(RegistrationState.Failed, fixture.Runtime.Registration.State);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ShutdownDuringRegistrarOutageReleasesPortAndIgnoresLateResponses(bool tcp)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (tcp) await fixture.UseTcpAsync();
        fixture.DropRegistrations = true;
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Reconnecting);
        await fixture.Runtime.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(4));
        Assert.IsFalse(fixture.Leases.Active);
        Assert.AreEqual(RegistrationState.Stopped, fixture.Runtime.Registration.State);
        await fixture.ReplyToLastRegistrationAsync();
        await Task.Delay(1200);
        Assert.AreEqual(RegistrationState.Stopped, fixture.Runtime.Registration.State);
        fixture.DropRegistrations = false;
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        await fixture.Runtime.DisconnectAsync();
        Assert.AreEqual(0, fixture.Media.Created);
        if (tcp)
        {
            var probe = new TcpListener(IPAddress.Loopback, fixture.LocalPort);
            try { probe.Start(); }
            finally { probe.Stop(); }
        }
        else
        {
            using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, fixture.LocalPort));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ShutdownClosesRingingOrConnectedCallBeforeReleasingAccount(bool answer)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peer = new SIPUserAgent();
        using var media = new TestMedia();
        var outgoing = peer.Call(fixture.Destination, "caller", "synthetic", media.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing));
        if (answer)
        {
            await fixture.Runtime.AnswerAsync(fixture.Runtime.GetCalls().Single().Id);
            Assert.IsTrue(await outgoing.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        await fixture.Runtime.DisconnectAsync().WaitAsync(TimeSpan.FromSeconds(4));
        if (!answer) Assert.IsFalse(await outgoing.WaitAsync(TimeSpan.FromSeconds(5)));
        var call = fixture.Runtime.GetCalls().Single();
        Assert.AreEqual(CallState.Failed, call.State);
        Assert.AreEqual(CallEndReason.Interrupted, call.EndReason);
        Assert.AreEqual(answer, call.AnsweredAtUtc.HasValue);
        Assert.IsNotNull(call.EndedAtUtc);
        Assert.AreEqual(call, fixture.Persistence.Snapshots.Last());
        Assert.AreEqual(fixture.Media.Created, fixture.Media.Closed);
        Assert.IsFalse(fixture.Leases.Active);
        Assert.AreEqual(RegistrationState.Stopped, fixture.Runtime.Registration.State);
    }

    [TestMethod]
    public void IndependentHostsCannotLeaseTheSameAccountUntilItsOwnerReleasesIt()
    {
        var first = new SipAccountLeaseFactory();
        var second = new SipAccountLeaseFactory();
        var account = new SipAccountOptions { Server = "isolated.invalid", Port = 5060, Username = "test-" + Guid.NewGuid().ToString("N") };
        using (first.Acquire(account))
        {
            var conflict = Assert.Throws<TelephonyException>(() => second.Acquire(new()
            {
                Server = "ISOLATED.INVALID", Port = account.Port, Username = account.Username, Transport = SipTransport.Tcp
            }));
            Assert.AreEqual("AccountInUse", conflict.Code);
        }
        using var nextOwner = second.Acquire(account);
    }

    [TestMethod]
    public async Task RealIncomingCallRequiresAnswerRejectsSecondCallAndClosesMediaAfterRemoteBye()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peer = new SIPUserAgent();
        using var media = new TestMedia();
        var outgoing = peer.Call(fixture.Destination, "caller", "synthetic", media.Session, 5);
        await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing));
        var incoming = fixture.Runtime.GetCalls().Single();
        Assert.AreEqual(CallDirection.Incoming, incoming.Direction);
        Assert.IsNotNull(fixture.Runtime.GetLineContext(incoming.Id));
        Assert.AreEqual(incoming.LocalAddress.OriginalValue, fixture.Runtime.GetLineContext(incoming.Id)!.LineId);
        Assert.AreEqual(0, fixture.Media.Created);
        Assert.IsNull(incoming.AnsweredAtUtc);
        using var second = new SIPUserAgent();
        using var secondMedia = new TestMedia();
        Assert.IsFalse(await second.Call(fixture.Destination, "second", "synthetic", secondMedia.Session, 3).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.HasCount(1, fixture.Runtime.GetCalls());
        await fixture.Runtime.AnswerAsync(incoming.Id);
        Assert.IsTrue(await outgoing.WaitAsync(TimeSpan.FromSeconds(5)));
        await UntilAsync(() => fixture.Runtime.GetCalls().Single().State == CallState.Connected);
        var connected = fixture.Runtime.GetCalls().Single();
        Assert.IsNotNull(connected.AnsweredAtUtc);
        Assert.IsTrue(connected.AnsweredAtUtc >= connected.StartedAtUtc);
        peer.Hangup();
        await UntilAsync(() => fixture.Runtime.GetCalls().Single().State == CallState.Ended);
        await UntilAsync(() => fixture.Media.Closed == fixture.Media.Created);
        Assert.AreEqual(CallEndReason.RemoteHangup, fixture.Runtime.GetCalls().Single().EndReason);
        Assert.IsTrue(fixture.Persistence.Snapshots.Select(call => call.Revision).SequenceEqual(new long[] { 0, 1, 2, 3 }));
    }

    [TestMethod]
    public async Task RemoteCancelAndExplicitRejectHaveDifferentEndReasonsAndNeverCreateAudio()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using (var peer = new SIPUserAgent())
        using (var media = new TestMedia())
        {
            var outgoing = peer.Call(fixture.Destination, "caller", "synthetic", media.Session, 5);
            await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing));
            peer.Cancel();
            Assert.IsFalse(await outgoing.WaitAsync(TimeSpan.FromSeconds(5)));
            await UntilAsync(() => fixture.Runtime.GetCalls().Single().EndReason == CallEndReason.Missed);
        }
        using (var peer = new SIPUserAgent())
        using (var media = new TestMedia())
        {
            var outgoing = peer.Call(fixture.Destination, "caller", "synthetic", media.Session, 5);
            await UntilAsync(() => fixture.Runtime.GetCalls().Any(call => call.State == CallState.Ringing));
            await fixture.Runtime.RejectAsync(fixture.Runtime.GetCalls().Single(call => call.State == CallState.Ringing).Id);
            Assert.IsFalse(await outgoing.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(fixture.Runtime.GetCalls().Any(call => call.EndReason == CallEndReason.Rejected));
        }
        Assert.AreEqual(0, fixture.Media.Created);
    }

    [TestMethod]
    public async Task FailedRegistrationAndPortConflictsReleaseOwnershipAndCanBeRetried()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.RejectRegistration = true;
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Failed);
        Assert.AreEqual("RegistrationFailed", fixture.Runtime.Registration.ErrorCode);
        fixture.RejectRegistration = false;
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        await fixture.Runtime.DisconnectAsync();
        using (var blocker = new UdpClient(new IPEndPoint(IPAddress.Loopback, fixture.LocalPort)))
        {
            await Assert.ThrowsAsync<TelephonyException>(() => fixture.Runtime.ConnectAsync());
            Assert.AreEqual(RegistrationState.Failed, fixture.Runtime.Registration.State);
        }
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        Assert.IsTrue(fixture.Leases.Active);
        await fixture.Runtime.DisconnectAsync();
        Assert.IsFalse(fixture.Leases.Active);
    }

    [TestMethod]
    public async Task AudioUnavailableFailsExplicitlyAndDoesNotPretendTheCallConnected()
    {
        await using var fixture = await Fixture.CreateAsync(unavailableAudio: true);
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        var company = new CompanyKey(LexwareProductLine.Pro, "isolated-sip", "1");
        var id = await fixture.Runtime.DialAsync(company, "123");
        await UntilAsync(() => fixture.Runtime.GetCalls().Single().State == CallState.Failed);
        Assert.AreEqual(id, fixture.Runtime.GetCalls().Single().Id);
        Assert.AreEqual("AudioUnavailable", fixture.Runtime.LastFailureCode);
        Assert.IsNull(fixture.Runtime.GetCalls().Single().AnsweredAtUtc);
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsTrue(condition(), "Timed out waiting for an observed SIP state.");
    }

    [TestMethod]
    public async Task RealOutgoingCallSupportsConfirmedHoldResumeAndBidirectionalRtpDtmf()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peerTransport = new SIPTransport();
        var peerChannel = new SIPUDPChannel(new IPEndPoint(IPAddress.Loopback, 0));
        peerTransport.AddSIPChannel(peerChannel);
        using var peer = new SIPUserAgent(peerTransport, null, true);
        using var peerMedia = new TestMedia();
        var peerDigits = new ConcurrentQueue<byte>();
        var localDigits = new ConcurrentQueue<DtmfReceivedEventArgs>();
        peer.OnDtmfTone += (tone, _) => peerDigits.Enqueue(tone);
        fixture.Runtime.DtmfReceived += (_, tone) => localDigits.Enqueue(tone);
        peer.OnIncomingCall += async (agent, request) => await agent.Answer(agent.AcceptCall(request), peerMedia.Session);
        var company = new CompanyKey(LexwareProductLine.Pro, "isolated-sip", "1");
        var id = await fixture.Runtime.DialAsync(company, "sip:peer@127.0.0.1:" + peerChannel.Port);
        await UntilAsync(() => fixture.Runtime.GetCalls().Single().State == CallState.Connected);
        Assert.AreEqual(company, fixture.Runtime.GetCalls().Single().Company);
        await fixture.Runtime.HoldAsync(id);
        Assert.AreEqual(CallState.Held, fixture.Runtime.GetCalls().Single().State);
        await fixture.Runtime.ResumeAsync(id);
        Assert.AreEqual(CallState.Connected, fixture.Runtime.GetCalls().Single().State);
        await fixture.Runtime.SendDtmfAsync(id, '#');
        await UntilAsync(() => peerDigits.Contains((byte)11));
        await peer.SendDtmf(5);
        await UntilAsync(() => localDigits.Any(tone => tone.CallId == id && tone.Digit == '5'));
        Assert.AreEqual(1, localDigits.Count(tone => tone.Digit == '5'));
        await fixture.Runtime.HangUpAsync(id);
        Assert.AreEqual(CallState.Ended, fixture.Runtime.GetCalls().Single().State);
        await UntilAsync(() => fixture.Media.Created == fixture.Media.Closed);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "wawi-sip-" + Guid.NewGuid().ToString("N"));
        private readonly SIPTransport _registrar = new();
        internal SipTelephonyService Runtime = null!;
        internal readonly MemoryPersistence Persistence = new();
        internal readonly MediaFactory Media = new();
        internal readonly LeaseFactory Leases = new();
        internal int LocalPort;
        internal int Registers;
        internal int Unregisters;
        internal int LastRegistrationPort;
        internal volatile bool RejectRegistration;
        internal volatile bool DropRegistrations;
        private SIPRequest? _lastRegistration;
        internal SIPProtocolsEnum LastContactProtocol;
        private ConfigurationStore _settings = null!;
        internal string Destination => "sip:workstation@127.0.0.1:" + LocalPort;

        internal static async Task<Fixture> CreateAsync(bool unavailableAudio = false)
        {
            var fixture = new Fixture();
            var channel = new SIPUDPChannel(new IPEndPoint(IPAddress.Loopback, 0));
            fixture._registrar.AddSIPChannel(channel);
            fixture._registrar.SIPTransportRequestReceived += async (_, remote, request) =>
            {
                if (request.Method != SIPMethodsEnum.REGISTER) return;
                Volatile.Write(ref fixture._lastRegistration, request);
                fixture.LastRegistrationPort = remote.Port;
                fixture.LastContactProtocol = request.Header.Contact[0].ContactURI.Protocol;
                if (request.Header.Expires == 0) Interlocked.Increment(ref fixture.Unregisters); else Interlocked.Increment(ref fixture.Registers);
                if (!fixture.DropRegistrations) await fixture.ReplyAsync(request);
            };
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) fixture.LocalPort = ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
            var protector = new WindowsUserSecretProtector();
            var settings = new ConfigurationStore(new ConfigurationLocation(fixture._root), new ApplicationOptionsValidator(protector), null);
            fixture._settings = settings;
            await settings.SaveAsync(new()
            {
                SipAccount = new()
                {
                    Server = "127.0.0.1", Port = channel.Port, Username = "workstation",
                    Password = protector.Protect("isolated-secret", SipAccountOptions.PasswordPurpose),
                    Connection = new() { BindAddress = "127.0.0.1", LocalPort = fixture.LocalPort, RegistrationTimeoutSeconds = 1, RetryIntervalSeconds = 1, RingTimeoutSeconds = 5 }
                }
            });
            fixture.Runtime = new(settings, protector, new SipTransportFactory(), fixture.Leases,
                unavailableAudio ? new UnavailableSipMediaSessionFactory() : fixture.Media, fixture.Persistence,
                new CallStateSessionFactory(), TimeProvider.System, NullLogger<SipTelephonyService>.Instance);
            return fixture;
        }
        internal Task ReplyToLastRegistrationAsync() => ReplyAsync(Volatile.Read(ref _lastRegistration)!);

        private async Task ReplyAsync(SIPRequest request)
        {
            var response = SIPResponse.GetResponse(request, RejectRegistration ? SIPResponseStatusCodesEnum.Forbidden : SIPResponseStatusCodesEnum.Ok, null);
            response.Header.Contact = request.Header.Contact;
            response.Header.Expires = request.Header.Expires == 0 ? 0 : 10;
            response.Header.Contact[0].Expires = response.Header.Expires;
            await _registrar.SendResponseAsync(response);
        }
        internal async Task UseTcpAsync()
        {
            var snapshot = await _settings.LoadAsync();
            var account = snapshot.Options.SipAccount!;
            _registrar.AddSIPChannel(new SIPTCPChannel(new IPEndPoint(IPAddress.Loopback, account.Port)));
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(); LocalPort = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            await _settings.SaveAsync(snapshot.Options with { SipAccount = new() { Server = account.Server, Port = account.Port,
                Username = account.Username, Password = account.Password, Transport = SipTransport.Tcp,
                Connection = account.Connection with { LocalPort = LocalPort } } }, snapshot);
        }
        public async ValueTask DisposeAsync()
        {
            try { await Runtime.DisposeAsync(); }
            finally { _registrar.Shutdown(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        }
    }

    [TestMethod]
    public async Task RealBlindTransferPreservesRejectedCallAndRecordsAcceptedHandoff()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Runtime.ConnectAsync();
        await UntilAsync(() => fixture.Runtime.Registration.State == RegistrationState.Registered);
        using var peerTransport = new SIPTransport();
        var channel = new SIPUDPChannel(new IPEndPoint(IPAddress.Loopback, 0));
        peerTransport.AddSIPChannel(channel);
        using var peer = new SIPUserAgent(peerTransport, null, true);
        using var peerMedia = new TestMedia();
        peer.OnIncomingCall += async (agent, request) => await agent.Answer(agent.AcceptCall(request), peerMedia.Session);
        var id = await fixture.Runtime.DialAsync(new(LexwareProductLine.Pro, "isolated-sip", "1"), "sip:peer@127.0.0.1:" + channel.Port);
        await UntilAsync(() => fixture.Runtime.GetCalls().Single().State == CallState.Connected);
        var rejection = await Assert.ThrowsAsync<TelephonyException>(() => fixture.Runtime.TransferAsync(id, "123"));
        Assert.AreEqual("TransferRejected", rejection.Code);
        Assert.AreEqual(CallState.Connected, fixture.Runtime.GetCalls().Single().State);
        using var targetTransport = new SIPTransport();
        var targetChannel = new SIPUDPChannel(new IPEndPoint(IPAddress.Loopback, 0));
        targetTransport.AddSIPChannel(targetChannel);
        using var target = new SIPUserAgent(targetTransport, null, true);
        using var targetMedia = new TestMedia();
        target.OnIncomingCall += async (agent, request) => await agent.Answer(agent.AcceptCall(request), targetMedia.Session);
        peer.OnTransferRequested += (_, _) => true;
        await fixture.Runtime.TransferAsync(id, "sip:target@127.0.0.1:" + targetChannel.Port);
        await UntilAsync(() => fixture.Runtime.GetCalls().Single().State == CallState.Ended);
        Assert.AreEqual(CallEndReason.Transferred, fixture.Runtime.GetCalls().Single().EndReason);
        await UntilAsync(() => target.IsCallActive);
        target.Hangup();
    }

    private sealed class MemoryPersistence : ISipCallPersistence
    {
        internal readonly ConcurrentQueue<CallSnapshot> Snapshots = new();
        internal int Recoveries;
        public Task<CallSnapshot> SaveAsync(CallSnapshot snapshot, CancellationToken cancellationToken = default)
        { Snapshots.Enqueue(snapshot); return Task.FromResult(snapshot); }
        public Task RecoverAsync(CancellationToken cancellationToken = default) { Recoveries++; return Task.CompletedTask; }
    }
    private sealed class LeaseFactory : ISipAccountLeaseFactory
    {
        internal bool Active;
        public IDisposable Acquire(SipAccountOptions account) { Assert.IsFalse(Active); Active = true; return new Lease(this); }
        private sealed class Lease(LeaseFactory owner) : IDisposable { public void Dispose() => owner.Active = false; }
    }
    private sealed class MediaFactory : ISipMediaSessionFactory
    {
        internal int Created;
        internal int Closed;
        public ISipMediaSession Create(SipAccountOptions account, AudioDeviceSettings devices)
        { Interlocked.Increment(ref Created); return new TestMedia(() => Interlocked.Increment(ref Closed)); }
    }
    private sealed class TestMedia(Action? closed = null) : ISipMediaSession, IDisposable
    {
        private readonly VoIPMediaSession _session = new(format => format.Codec is SIPSorceryMedia.Abstractions.AudioCodecsEnum.PCMA or SIPSorceryMedia.Abstractions.AudioCodecsEnum.PCMU);
        private int _disposed;
        public IMediaSession Session => _session;
        public void ApplyPlaybackSettings() { }
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) { _session.Close("test"); closed?.Invoke(); } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
