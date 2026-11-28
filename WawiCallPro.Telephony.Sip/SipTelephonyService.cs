using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Telephony.Sip;

/// <summary>One account and one live call per workstation. Observations are persisted and published in queue order.</summary>
public sealed partial class SipTelephonyService : ITelephonyService, ITelephonyConnection, IDtmfReceiver, ICallLineContextProvider, IAsyncDisposable
{
    private readonly ConfigurationStore _settings;
    private readonly ApplicationConfigurationService _configuration;
    private readonly ISecretProtector _secrets;
    private readonly ISipTransportFactory _transports;
    private readonly ISipAccountLeaseFactory _leases;
    private readonly ISipMediaSessionFactory _media;
    private readonly IIncomingRingtone? _ringtone;
    private readonly ISipCallPersistence _persistence;
    private readonly ISipCallOutbox _outbox;
    private readonly ICallOriginProvider _origins;
    private readonly ICallStateSessionFactory _states;
    private readonly TimeProvider _clock;
    private readonly ILogger<SipTelephonyService> _log;
    private readonly SemaphoreSlim _commands = new(1);
    private readonly Lock _gate = new();
    private readonly Channel<Func<Task>> _events = Channel.CreateUnbounded<Func<Task>>(new() { SingleReader = true });
    private readonly Task _eventLoop;
    // Accessed only by the event loop; retries must never overtake an earlier revision.
    private readonly Queue<CallSnapshot> _pendingWrites = new();
    private readonly HashSet<(Guid, long)> _journaled = new();
    private bool _outboxLoaded;
    private volatile bool _connecting;
    private CallOrigin? _origin;
    private readonly CancellationTokenSource _retryStopping = new();
    private readonly Task _retryLoop;
    private int _storageUnavailable;
    private readonly ConcurrentDictionary<Guid, CallSnapshot> _calls = new();
    private readonly ConcurrentDictionary<Guid, WawiCallPro.Interfaces.PhoneNumbers.PhoneLineContext> _callLines = new();
    private readonly ConcurrentDictionary<long, Task> _jobs = new();
    private long _jobId;
    private RegistrationSnapshot _registration = new(RegistrationState.Stopped, DateTimeOffset.UtcNow);
    private SIPTransport? _transport;
    private SIPRegistrationUserAgent? _registrar;
    private SIPEndPoint? _registeredPeer;
    private PeriodicTimer? _keepAliveTimer;
    private IDisposable? _lease;
    private SipAccountOptions? _account;
    private Guid? _sipProfileId;
    private AudioDeviceSettings _devices = new();
    private Agent? _agent;
    private bool _stopping;
    private bool _disposed;
    private string? _failure;

    public SipTelephonyService(ConfigurationStore settings, ISecretProtector secrets, ISipTransportFactory transports,
        ISipAccountLeaseFactory leases, ISipMediaSessionFactory media, ISipCallPersistence persistence,
        ICallStateSessionFactory states, TimeProvider clock, ILogger<SipTelephonyService> log,
        ISipCallOutbox outbox, ICallOriginProvider origins, ApplicationConfigurationService configuration)
        : this(settings, secrets, transports, leases, media, persistence, states, clock, log, outbox, origins, configuration, null)
    {
    }

    public SipTelephonyService(ConfigurationStore settings, ISecretProtector secrets, ISipTransportFactory transports,
        ISipAccountLeaseFactory leases, ISipMediaSessionFactory media, ISipCallPersistence persistence,
        ICallStateSessionFactory states, TimeProvider clock, ILogger<SipTelephonyService> log,
        ISipCallOutbox outbox, ICallOriginProvider origins, ApplicationConfigurationService configuration, IIncomingRingtone? ringtone)
    {
        _settings = settings; _secrets = secrets; _transports = transports; _leases = leases; _media = media;
        _persistence = persistence; _states = states; _clock = clock; _log = log;
        _outbox = outbox; _origins = origins;
        _configuration = configuration;
        _ringtone = ringtone;
        _eventLoop = ConsumeAsync();
        _retryLoop = RetryPersistenceAsync();
    }

    public RegistrationSnapshot Registration { get { lock (_gate) return _registration; } }
    public string? LastFailureCode => Volatile.Read(ref _storageUnavailable) != 0 ? "CallStorageUnavailable" : Volatile.Read(ref _failure);
    public ImmutableArray<CallSnapshot> GetCalls() => _calls.Values.OrderByDescending(call => call.StartedAtUtc).ToImmutableArray();
    public WawiCallPro.Interfaces.PhoneNumbers.PhoneLineContext? GetLineContext(Guid callId) => _callLines.GetValueOrDefault(callId);
    public event EventHandler<RegistrationChangedEventArgs>? RegistrationChanged;
    public event EventHandler<CallChangedEventArgs>? CallChanged;
    public event EventHandler<DtmfReceivedEventArgs>? DtmfReceived;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_transport is not null)
            {
                if (Registration.State == RegistrationState.Registered) return;
                if (_agent?.State is not null) throw new TelephonyException("Busy");
                CleanupTransport();
            }
            var snapshot = await _settings.LoadAsync(cancellationToken).ConfigureAwait(false);
            var account = snapshot.Options.SipAccount ?? throw new TelephonyException("AccountMissing");
            _sipProfileId = snapshot.Options.ActiveSipProfileId;
            _account = account; _devices = snapshot.Options.Devices; _stopping = false;
            _lease = _leases.Acquire(account);
            _connecting = true;
            // A second host must never mark the first host's live calls as interrupted.
            try
            {
                await EnqueueAsync(async () =>
                {
                    // Repositories use the startup target; database edits take effect only after restarting the host.
                    var recovered = await _outbox.OpenAsync(_configuration.Current.Options.Database, cancellationToken).ConfigureAwait(false);
                    if (!_outboxLoaded)
                    {
                        foreach (var call in recovered)
                        {
                            _pendingWrites.Enqueue(call);
                            _journaled.Add((call.Id, call.Revision));
                        }
                        _outboxLoaded = true;
                    }
                    _origin = await _origins.CaptureAsync(cancellationToken).ConfigureAwait(false);
                    await FlushPendingAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
                if (Volatile.Read(ref _storageUnavailable) != 0) throw new TelephonyException("DatabaseNotReady");
                await _persistence.RecoverAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch { throw new TelephonyException("DatabaseNotReady"); }
            _transport = _transports.Create(account);
            var activeTransport = _transport;
            activeTransport.SIPRequestInTraceEvent += (_, _, request) =>
            {
                if (request.Method is SIPMethodsEnum.BYE or SIPMethodsEnum.CANCEL)
                    _log.LogDebug("SIP-Abbruchsignal empfangen ({Method}).", request.Method);
            };
            activeTransport.SIPResponseOutTraceEvent += (_, _, response) => ObserveByeResponse(activeTransport, response);
            _transport.SIPTransportRequestReceived += async (_, _, request) =>
            {
                if (_stopping || !ReferenceEquals(_transport, activeTransport)) return;
                if (request.Method == SIPMethodsEnum.INVITE && string.IsNullOrEmpty(request.Header.To.ToTag))
                {
                    // Scanners on the network must not ring the workstation or create call history.
                    if (!account.Connection.AcceptInvitesFromAnySource
                        && !SipCallerIdentity.IsRegisteredPeer(request.RemoteSIPEndPoint, Volatile.Read(ref _registeredPeer)))
                    {
                        _log.LogDebug("SIP-INVITE einer nicht registrierten Quelle verworfen.");
                        return;
                    }
                    Agent? incoming;
                    lock (_gate) incoming = _agent;
                    if (incoming is not null)
                    {
                        // Register the INVITE transaction before the transport can process a following CANCEL.
                        var processing = IncomingAsync(incoming, request);
                        Track(() => processing);
                    }
                }
                else if (request.Method == SIPMethodsEnum.OPTIONS && _transport is { } transport)
                    await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null)).ConfigureAwait(false);
            };
            _agent = CreateAgent();
            using var password = _secrets.Unprotect(account.Password!, SipAccountOptions.PasswordPurpose);
            var registrarAddress = Address(null);
            var contact = new SIPURI(SIPSchemesEnum.sip, System.Net.IPAddress.Any, 0)
            {
                User = account.Username,
                Protocol = Enum.Parse<SIPProtocolsEnum>(account.Transport.ToString(), true)
            };
            // Transient failures retry; a rejected account needs an explicit new login after correction.
            var registrar = new SIPRegistrationUserAgent(_transport, null, Address(account.Username!),
                account.AuthenticationUsername ?? account.Username, password.Reveal(), null, registrarAddress.ToString(), contact,
                account.Connection.RegistrationExpirySeconds, null, account.Connection.RegistrationTimeoutSeconds,
                account.Connection.RetryIntervalSeconds, 3, true);
            _registrar = registrar;
            registrar.RegistrationSuccessful += (_, response) =>
            {
                if (!ReferenceEquals(_registrar, registrar) || _stopping) return;
                Volatile.Write(ref _registeredPeer, response.RemoteSIPEndPoint);
                SetRegistration(RegistrationState.Registered);
            };
            registrar.RegistrationTemporaryFailure += (_, _, _) => { if (ReferenceEquals(_registrar, registrar) && !_stopping) SetRegistration(RegistrationState.Reconnecting, "RegistrationRetry"); };
            registrar.RegistrationFailed += (_, _, _) => { if (ReferenceEquals(_registrar, registrar) && !_stopping) SetRegistration(RegistrationState.Failed, "RegistrationFailed"); };
            registrar.RegistrationRemoved += (_, _) => { if (ReferenceEquals(_registrar, registrar) && !_stopping) SetRegistration(RegistrationState.Stopped); };
            SetRegistration(RegistrationState.Registering);
            if (account.Connection.KeepAliveIntervalSeconds > 0)
            {
                var timer = new PeriodicTimer(TimeSpan.FromSeconds(account.Connection.KeepAliveIntervalSeconds), _clock);
                _keepAliveTimer = timer;
                _ = SendKeepAlivesAsync(activeTransport, registrarAddress, timer);
            }
            registrar.Start();
        }
        catch (OperationCanceledException) { CleanupTransport(); SetRegistration(RegistrationState.Stopped); throw; }
        catch (Exception exception)
        {
            CleanupTransport();
            var code = exception is TelephonyException failure ? failure.Code : "ConnectionFailed";
            SetRegistration(RegistrationState.Failed, code);
            throw new TelephonyException(code);
        }
        finally { _connecting = false; _commands.Release(); }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _stopping = true;
            Agent? agent;
            lock (_gate) agent = _agent;
            if (agent?.State is not null)
            {
                // SIPUserAgent raises OnCallHungup synchronously for our own Hangup, too.
                agent.ShutdownRequested = true;
                agent.Stopping.Cancel();
                (agent.Media as ISipLiveRecordingMediaSession)?.CancelPendingStart();
                try
                {
                    if (agent.UserAgent.IsCallActive) agent.UserAgent.Hangup();
                    else if (agent.Incoming is not null) agent.Incoming.Reject(SIPResponseStatusCodesEnum.TemporarilyUnavailable, null);
                    else agent.UserAgent.Cancel();
                }
                finally { await EnqueueAsync(() => ChangeAsync(agent, CallState.Failed, CallEndReason.Interrupted)).ConfigureAwait(false); }
            }
            if (_registrar is { } registrar)
            {
                var removed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                registrar.RegistrationRemoved += (_, _) => removed.TrySetResult();
                registrar.Stop(true);
                try { await removed.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false); }
                catch (TimeoutException) { }
            }
            CleanupTransport();
            await Task.WhenAll(_jobs.Values).ConfigureAwait(false);
            await EnqueueAsync(FlushPendingAsync).ConfigureAwait(false);
            SetRegistration(RegistrationState.Stopped);
        }
        finally { CleanupTransport(); SetRegistration(RegistrationState.Stopped); _commands.Release(); }
    }

    public async Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);
        var target = Destination(destination);
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Agent agent;
            lock (_gate)
            {
                agent = RequireReadyAgent();
                if (agent.State is not null) throw new TelephonyException("Busy");
                Begin(agent, CallDirection.Outgoing, destination, company);
            }
            await EnqueueAsync(() => PublishAsync(agent)).ConfigureAwait(false);
            await EnqueueAsync(() => ChangeAsync(agent, CallState.Dialing)).ConfigureAwait(false);
            Track(() => PlaceCallAsync(agent, target));
            return agent.Id;
        }
        finally { _commands.Release(); }
    }

    public async Task AnswerAsync(Guid callId, CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        Agent? agent = null;
        try
        {
            var ringing = GetAgent(callId, CallState.Ringing);
            if (ringing.Incoming is null) throw new TelephonyException("NotIncoming");
            await EnqueueAsync(() => StopRingtoneAsync(ringing)).ConfigureAwait(false);
            agent = GetAgent(callId, CallState.Ringing);
            agent.Media = _media.Create(_account!, _devices);
            using var registration = cancellationToken.Register(() => agent.UserAgent.Hangup());
            if (!await agent.UserAgent.Answer(agent.Incoming, agent.Media.Session).ConfigureAwait(false)) throw new TelephonyException("AnswerFailed");
            agent.Media.ApplyPlaybackSettings();
            await EnqueueAsync(() => CompleteMediaConnectionAsync(agent)).ConfigureAwait(false);
        }
        catch
        {
            if (agent is not null)
            {
                try { agent.Incoming?.Reject(SIPResponseStatusCodesEnum.TemporarilyUnavailable, null); }
                finally { await EnqueueAsync(() => ChangeAsync(agent, CallState.Failed, CallEndReason.Error)).ConfigureAwait(false); }
            }
            throw;
        }
        finally { _commands.Release(); }
    }

    public Task RejectAsync(Guid callId, CancellationToken cancellationToken = default) => CommandAsync(callId, async agent =>
    {
        if (agent.Incoming is null || agent.State!.Current.State != CallState.Ringing) throw new TelephonyException("NotIncoming");
        try { agent.Incoming.Reject(SIPResponseStatusCodesEnum.Decline, null); }
        finally { await EnqueueAsync(() => ChangeAsync(agent, CallState.Ended, CallEndReason.Rejected)).ConfigureAwait(false); }
    }, cancellationToken);

    public Task HangUpAsync(Guid callId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancelRecordingStartup(callId);
        return CommandAsync(callId, async agent =>
        {
            var current = agent.State!.Current;
            var reason = current.AnsweredAtUtc.HasValue ? CallEndReason.Completed : current.Direction == CallDirection.Outgoing ? CallEndReason.Cancelled : CallEndReason.Rejected;
            agent.LocalHangupRequested = true;
            try
            {
                if (agent.UserAgent.IsCallActive) agent.UserAgent.Hangup();
                else if (agent.Incoming is not null) agent.Incoming.Reject(SIPResponseStatusCodesEnum.Decline, null);
                else agent.UserAgent.Cancel();
            }
            finally { await EnqueueAsync(() => ChangeAsync(agent, CallState.Ended, reason)).ConfigureAwait(false); }
        }, cancellationToken);
    }

    public Task HoldAsync(Guid callId, CancellationToken cancellationToken = default) => SetHoldAsync(callId, true, cancellationToken);
    public Task ResumeAsync(Guid callId, CancellationToken cancellationToken = default) => SetHoldAsync(callId, false, cancellationToken);
    private Task SetHoldAsync(Guid callId, bool hold, CancellationToken token) => CommandAsync(callId, async agent =>
    {
        if (agent.State!.Current.State != (hold ? CallState.Connected : CallState.Held)) throw new TelephonyException("InvalidState");
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expectedSequence = agent.UserAgent.Dialogue.CSeq + 1;
        var dialogId = agent.UserAgent.Dialogue.CallId;
        var transport = _transport ?? throw new TelephonyException("NotRegistered");
        void Observe(SIPEndPoint local, SIPEndPoint remote, SIPResponse response)
        {
            // SIPUserAgent.OnTransactionStateChange does not include its re-INVITE transactions.
            if (response.Header.CallId != dialogId || response.Header.CSeqMethod != SIPMethodsEnum.INVITE
                || response.Header.CSeq < expectedSequence || (int)response.Status < 200
                || response.Status is SIPResponseStatusCodesEnum.Unauthorised or SIPResponseStatusCodesEnum.ProxyAuthenticationRequired) return;
            completion.TrySetResult((int)response.Status < 300);
        }
        transport.SIPResponseInTraceEvent += Observe;
        try
        {
            if (hold) agent.UserAgent.PutOnHold(); else agent.UserAgent.TakeOffHold();
            if (!await completion.Task.WaitAsync(TimeSpan.FromSeconds(_account!.Connection.RegistrationTimeoutSeconds), token).ConfigureAwait(false))
                throw new TelephonyException("HoldRejected");
            await EnqueueAsync(() => ChangeAsync(agent, hold ? CallState.Held : CallState.Connected)).ConfigureAwait(false);
        }
        catch (TimeoutException) { throw new TelephonyException("HoldTimedOut"); }
        finally { transport.SIPResponseInTraceEvent -= Observe; }
    }, token);

    public Task TransferAsync(Guid callId, string destination, CancellationToken cancellationToken = default) => CommandAsync(callId, async agent =>
    {
        if (agent.State!.Current.AnsweredAtUtc is null) throw new TelephonyException("NotConnected");
        var transport = _transport ?? throw new TelephonyException("NotRegistered");
        var dialogId = agent.UserAgent.Dialogue.CallId;
        void Observe(SIPEndPoint local, SIPEndPoint remote, SIPResponse response)
        {
            if (response.Header.CallId == dialogId && response.Header.CSeqMethod == SIPMethodsEnum.REFER
                && response.Status == SIPResponseStatusCodesEnum.Accepted) agent.TransferAccepted = true;
        }
        transport.SIPResponseInTraceEvent += Observe;
        try
        {
            using var password = _secrets.Unprotect(_account!.Password!, SipAccountOptions.PasswordPurpose);
            if (!await agent.UserAgent.BlindTransfer(SIPURI.ParseSIPURI(Destination(destination)), TimeSpan.FromSeconds(15), cancellationToken,
                    username: _account.AuthenticationUsername ?? _account.Username, password: password.Reveal()).ConfigureAwait(false))
                throw new TelephonyException("TransferRejected");
            // 202 only accepts the REFER request. Keep the original leg until BYE or a successful NOTIFY.
        }
        finally { transport.SIPResponseInTraceEvent -= Observe; }
    }, cancellationToken);

    public Task SendDtmfAsync(Guid callId, char digit, CancellationToken cancellationToken = default) => CommandAsync(callId, async agent =>
    {
        if (agent.State!.Current.State is not (CallState.Connected or CallState.Held)) throw new TelephonyException("NotConnected");
        const string digits = "0123456789*#ABCD";
        var code = digits.IndexOf(char.ToUpperInvariant(digit));
        if (code < 0) throw new TelephonyException("InvalidDtmf");
        if (_account!.Connection.DtmfMethod == SipDtmfMethod.SipInfo
            || agent.UserAgent.MediaSession is RTPSession rtp
                && (rtp.AudioStream is not { } audio || audio.RemoteTrack?.Capabilities.Any(format => format.ID == audio.NegotiatedRtpEventPayloadID
                    && string.Equals(format.Name(), SDP.TELEPHONE_EVENT_ATTRIBUTE, StringComparison.OrdinalIgnoreCase)) != true))
            await SendDtmfInfoAsync(agent, digits[code], cancellationToken).ConfigureAwait(false);
        else
        {
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, agent.Stopping.Token);
            try { await agent.UserAgent.MediaSession.SendDtmf((byte)code, stopping.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (agent.Stopping.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            { throw new TelephonyException("NotConnected"); }
            cancellationToken.ThrowIfCancellationRequested();
            if (agent.Stopping.IsCancellationRequested) throw new TelephonyException("NotConnected");
        }
    }, cancellationToken);

    private async Task SendDtmfInfoAsync(Agent agent, char digit, CancellationToken cancellationToken)
    {
        var transport = _transport ?? throw new TelephonyException("NotRegistered");
        var dialog = agent.UserAgent.Dialogue ?? throw new TelephonyException("NotConnected");
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, agent.Stopping.Token);
        cancellationToken.ThrowIfCancellationRequested();
        if (agent.Stopping.IsCancellationRequested) throw new TelephonyException("NotConnected");

        var request = dialog.GetInDialogRequest(SIPMethodsEnum.INFO);
        request.Header.ContentType = "application/dtmf-relay";
        request.Body = $"Signal={digit}\r\nDuration=250\r\n";
        request.Header.ContentLength = request.Body.Length; // Only ASCII digits and fixed text are used.
        var transaction = new SIPNonInviteTransaction(transport, request, SIPEndPoint.ParseSIPEndPoint(dialog.ProxySendFrom));
        var finalResponse = new TaskCompletionSource<SIPResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        transaction.NonInviteTransactionFinalResponseReceived += (_, _, _, response) =>
        {
            finalResponse.TrySetResult(response);
            return Task.FromResult(System.Net.Sockets.SocketError.Success);
        };
        transaction.SendRequest();
        try
        {
            var response = await finalResponse.Task.WaitAsync(TimeSpan.FromSeconds(5), stopping.Token).ConfigureAwait(false);
            if ((int)response.Status is < 200 or >= 300) throw new TelephonyException("DtmfInfoRejected");
        }
        catch (TimeoutException) { throw new TelephonyException("DtmfInfoTimedOut"); }
        catch (OperationCanceledException) when (agent.Stopping.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        { throw new TelephonyException("NotConnected"); }
    }

    private Agent CreateAgent()
    {
        // The account owns the transport. Retiring a call must not shut down registration or the next call.
        using var password = _secrets.Unprotect(_account!.Password!, SipAccountOptions.PasswordPurpose);
        var credentials = new AnswerCredentials(_account.AuthenticationUsername ?? _account.Username!, password.Reveal(), _account.Server!);
        var agent = new Agent(new SIPUserAgent(_transport!, null, false, credentials));
        agent.UserAgent.ClientCallRinging += (_, _) => Queue(() => ChangeAsync(agent, CallState.Ringing));
        agent.UserAgent.ClientCallAnswered += (_, response) =>
        {
            if ((int)response.Status is >= 200 and < 300) Queue(() => ChangeAsync(agent, CallState.Connected));
        };
        agent.UserAgent.ClientCallFailed += (_, _, response) => agent.FailureStatus = response?.Status;
        agent.UserAgent.OnCallHungup += _ =>
        {
            agent.Stopping.Cancel();
            (agent.Media as ISipLiveRecordingMediaSession)?.CancelPendingStart();
            if (agent.LocalHangupRequested) return;
            Queue(() => ChangeAsync(agent,
                agent.ShutdownRequested ? CallState.Failed : CallState.Ended,
                agent.ShutdownRequested ? CallEndReason.Interrupted : agent.TransferAccepted ? CallEndReason.Transferred : CallEndReason.RemoteHangup));
        };
        agent.UserAgent.OnTransferNotify += body =>
        {
            if (!agent.TransferAccepted) return;
            if (body.StartsWith("SIP/2.0 200 ", StringComparison.Ordinal))
            {
                agent.UserAgent.Hangup();
                Queue(() => ChangeAsync(agent, CallState.Ended, CallEndReason.Transferred));
            }
            else if (body.StartsWith("SIP/2.0 ", StringComparison.Ordinal) && body.Length >= 11
                && int.TryParse(body.AsSpan(8, 3), out var status) && status >= 300)
            { agent.TransferAccepted = false; Failure("TransferFailed"); }
        };
        agent.UserAgent.ServerCallCancelled += (_, _) => Queue(() => ChangeAsync(agent,
            agent.ShutdownRequested ? CallState.Failed : CallState.Ended,
            agent.ShutdownRequested ? CallEndReason.Interrupted : CallEndReason.Missed));
        agent.UserAgent.ServerCallRingTimeout += _ => Queue(() => ChangeAsync(agent, CallState.Failed, CallEndReason.NetworkFailure));
        agent.UserAgent.RemotePutOnHold += () => Queue(() => ChangeAsync(agent, CallState.Held));
        agent.UserAgent.RemoteTookOffHold += () => Queue(() => ChangeAsync(agent, CallState.Connected));
        agent.UserAgent.OnDtmfTone += (tone, duration) => Queue(() =>
        {
            if (tone < 16 && agent.State?.Current.State is CallState.Connected or CallState.Held)
                Publish(DtmfReceived, new(agent.Id, "0123456789*#ABCD"[tone], duration));
            return Task.CompletedTask;
        });
        return agent;
    }

    private async Task IncomingAsync(Agent agent, SIPRequest request)
    {
        lock (_gate)
        {
            if (_stopping || !ReferenceEquals(_agent, agent) || agent.State is not null)
            {
                var transport = _transport;
                if (transport is not null) Track(async () => await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.BusyHere, null)).ConfigureAwait(false));
                return;
            }
            var caller = SipCallerIdentity.Read(request, Volatile.Read(ref _registeredPeer));
            if (caller.Presentation != NumberPresentation.Available)
                _log.LogDebug("SIP-Rufnummer {Presentation} ({Transport}, P-Asserted-Identity vorhanden: {HasAssertedIdentity}).",
                    caller.Presentation, request.RemoteSIPEndPoint?.Protocol, request.Header.PassertedIdentity.Count > 0);
            Begin(agent, CallDirection.Incoming, caller.OriginalValue, null);
            if (caller.Presentation == NumberPresentation.Withheld)
                agent.State = _states.Create(agent.State!.Current with { RemoteAddress = caller });
            Queue(() => PublishAsync(agent));
            agent.Incoming = agent.UserAgent.AcceptCall(request);
        }
        await EnqueueAsync(() => ChangeAsync(agent, CallState.Ringing)).ConfigureAwait(false);
        // AcceptCall has already sent the provisional responses and owns the transaction.
        if (agent.Stopping.IsCancellationRequested) return;
        Track(async () =>
        {
            try { await Task.Delay(TimeSpan.FromSeconds(_account!.Connection.RingTimeoutSeconds), agent.Stopping.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            if (agent.State?.Current.State != CallState.Ringing) return;
            try { agent.Incoming.Reject(SIPResponseStatusCodesEnum.TemporarilyUnavailable, null); }
            finally { await EnqueueAsync(() => ChangeAsync(agent, CallState.Ended, CallEndReason.Missed)).ConfigureAwait(false); }
        });
    }

    private async Task PlaceCallAsync(Agent agent, string target)
    {
        try
        {
            agent.Media = _media.Create(_account!, _devices);
            using var password = _secrets.Unprotect(_account!.Password!, SipAccountOptions.PasswordPurpose);
            var descriptor = new SIPCallDescriptor(_account.Username, password.Reveal(), target, Address(_account.Username!).ToString(), target,
                null, null, _account.AuthenticationUsername ?? _account.Username, SIPCallDirection.Out, SDP.SDP_MIME_CONTENTTYPE, null, null);
            var connected = await agent.UserAgent.Call(descriptor, agent.Media.Session, _account.Connection.RingTimeoutSeconds).ConfigureAwait(false);
            if (connected)
            {
                agent.Media.ApplyPlaybackSettings();
                await EnqueueAsync(() => CompleteMediaConnectionAsync(agent)).ConfigureAwait(false);
            }
            else if (!agent.LocalHangupRequested)
            {
                var reason = agent.FailureStatus switch
                {
                    SIPResponseStatusCodesEnum.BusyHere or SIPResponseStatusCodesEnum.BusyEverywhere => CallEndReason.Busy,
                    SIPResponseStatusCodesEnum.Decline or SIPResponseStatusCodesEnum.Forbidden => CallEndReason.Rejected,
                    _ => CallEndReason.NetworkFailure
                };
                await EnqueueAsync(() => ChangeAsync(agent, reason == CallEndReason.NetworkFailure ? CallState.Failed : CallState.Ended, reason)).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            Failure(exception is TelephonyException failure ? failure.Code : "CallFailed");
            await EnqueueAsync(() => ChangeAsync(agent, CallState.Failed, CallEndReason.Error)).ConfigureAwait(false);
        }
        finally
        {
            if (agent.State?.Current.State is CallState.Ended or CallState.Failed)
            {
                var media = Interlocked.Exchange(ref agent.Media, null);
                if (media is not null) await media.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private void Begin(Agent agent, CallDirection direction, string? number, CompanyKey? company)
    {
        agent.Id = Guid.NewGuid();
        _callLines[agent.Id] = CallerLineConfiguration.ContextFor(_account!);
        var now = _clock.GetUtcNow();
        agent.State = _states.Create(new() { Id = agent.Id, Revision = 0, Company = company, Origin = _origin, Direction = direction, State = CallState.Created,
            LocalAddress = new(NumberPresentation.Available, _account!.Username),
            RemoteAddress = new(string.IsNullOrEmpty(number) ? NumberPresentation.Unknown : NumberPresentation.Available, number),
            StartedAtUtc = now, StateChangedAtUtc = now });
    }

    private async Task ChangeAsync(Agent agent, CallState state, CallEndReason? reason = null)
    {
        if (agent.State is null) return;
        var current = agent.State.Current;
        var now = _clock.GetUtcNow();
        if (now < current.StateChangedAtUtc) now = current.StateChangedAtUtc;
        if (agent.State.Apply(new(current.Revision + 1, state, now, reason)) != CallChangeResult.Applied) return;
        var terminal = state is CallState.Ended or CallState.Failed;
        if (terminal)
        {
            agent.Stopping.Cancel();
            await StopRingtoneAsync(agent).ConfigureAwait(false);
        }
        else if (state == CallState.Ringing && current.Direction == CallDirection.Incoming) StartRingtone(agent);
        await PublishAsync(agent).ConfigureAwait(false);
        if (!terminal) return;
        var media = Interlocked.Exchange(ref agent.Media, null);
        try
        {
            if (media is not null) await media.DisposeAsync().ConfigureAwait(false);
        }
        catch { Failure("AudioCleanupFailed"); }
        finally
        {
            try { agent.UserAgent.Dispose(); }
            finally
            {
                lock (_gate)
                    if (ReferenceEquals(_agent, agent)) _agent = !_stopping && _transport is not null ? CreateAgent() : null;
            }
        }
    }

    private async Task PublishAsync(Agent agent)
    {
        var snapshot = agent.State!.Current;
        _pendingWrites.Enqueue(snapshot);
        await FlushPendingAsync().ConfigureAwait(false);
        snapshot = agent.State.Current;
        _calls[snapshot.Id] = snapshot;
        if (_calls.Count > 128)
            foreach (var old in _calls.Values.Where(call => call.State is CallState.Ended or CallState.Failed).OrderBy(call => call.StartedAtUtc).Take(_calls.Count - 128))
            { _calls.TryRemove(old.Id, out _); _callLines.TryRemove(old.Id, out _); }
        Publish(CallChanged, new(snapshot));
    }

    private async Task FlushPendingAsync()
    {
        if (!_outboxLoaded) return;
        try
        {
            // Journal all observations first, even when the first database delivery keeps failing.
            foreach (var call in _pendingWrites)
            {
                if (_journaled.Contains((call.Id, call.Revision))) continue;
                await _outbox.AppendAsync(call, _retryStopping.Token).ConfigureAwait(false);
                _journaled.Add((call.Id, call.Revision));
            }
        }
        catch { StorageFailed(); return; }
        while (_pendingWrites.TryPeek(out var snapshot))
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5), _clock);
                using var stopping = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, _retryStopping.Token);
                var saved = await _persistence.SaveAsync(snapshot, stopping.Token).ConfigureAwait(false);
                _outbox.Acknowledge(snapshot);
                _pendingWrites.Dequeue();
                _journaled.Remove((snapshot.Id, snapshot.Revision));
                if (_calls.TryGetValue(saved.Id, out var current) && current.Revision == saved.Revision && current != saved)
                {
                    _calls[saved.Id] = saved;
                    Publish(CallChanged, new(saved));
                }
                lock (_gate)
                    if (_agent?.Id == saved.Id && _agent.State?.Current.Revision == saved.Revision)
                        _agent.State = _states.Create(saved);
            }
            catch (Exception error) when (IsPermanentRejection(error))
            {
                // Retrying cannot succeed. Retire only this observation so later calls are still persisted.
                try { _outbox.Quarantine(snapshot); }
                catch { StorageFailed(); return; }
                _pendingWrites.Dequeue();
                _journaled.Remove((snapshot.Id, snapshot.Revision));
                Failure("CallSnapshotRejected");
            }
            catch
            {
                StorageFailed();
                return;
            }
        }
        Interlocked.Exchange(ref _storageUnavailable, 0);
        Interlocked.CompareExchange(ref _failure, null, "CallStorageUnavailable");
        if (!_connecting && _transport is null)
        {
            _outbox.Close();
            _outboxLoaded = false;
        }
    }

    private static bool IsPermanentRejection(Exception error) => error is ArgumentException
        || error is WawiCallPro.Database.Contracts.RepositoryConflictException { Kind: not WawiCallPro.Database.Contracts.RepositoryConflictKind.VersionMismatch };

    private void StorageFailed()
    {
        if (Interlocked.Exchange(ref _storageUnavailable, 1) == 0) Failure("CallStorageUnavailable");
    }

    private async Task RetryPersistenceAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), _clock);
            while (await timer.WaitForNextTickAsync(_retryStopping.Token).ConfigureAwait(false))
                await EnqueueAsync(FlushPendingAsync).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_retryStopping.IsCancellationRequested) { }
    }

    private Agent RequireReadyAgent()
    {
        if (_stopping || Registration.State != RegistrationState.Registered || _agent is null) throw new TelephonyException("NotRegistered");
        return _agent;
    }
    private Agent GetAgent(Guid id, params CallState[] states)
    {
        lock (_gate)
        {
            var agent = _agent;
            if (agent?.State is null || agent.Id != id) throw new TelephonyException("CallNotActive");
            if (states.Length > 0 && !states.Contains(agent.State.Current.State)) throw new TelephonyException("InvalidState");
            return agent;
        }
    }
    private async Task CommandAsync(Guid id, Func<Agent, Task> action, CancellationToken token)
    {
        await _commands.WaitAsync(token).ConfigureAwait(false);
        try { token.ThrowIfCancellationRequested(); await action(GetAgent(id)).ConfigureAwait(false); }
        finally { _commands.Release(); }
    }
    private SIPURI Address(string? user)
    {
        var account = _account ?? throw new TelephonyException("AccountMissing");
        var protocol = account.Transport.ToString().ToLowerInvariant();
        return new SIPURI(user, account.Server + ":" + account.Port, "transport=" + protocol,
            account.Transport == SipTransport.Tls ? SIPSchemesEnum.sips : SIPSchemesEnum.sip);
    }
    private string Destination(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
            throw new TelephonyException("InvalidDestination");
        if (value.StartsWith("sip:", StringComparison.OrdinalIgnoreCase) || value.StartsWith("sips:", StringComparison.OrdinalIgnoreCase))
            return SIPURI.ParseSIPURI(value).ToString();
        if (value.Any(character => !char.IsAsciiDigit(character) && character is not ('+' or '*' or '#')))
            throw new TelephonyException("InvalidDestination");
        return Address(value).ToString();
    }
    private void SetRegistration(RegistrationState state, string? code = null)
    {
        RegistrationState previous;
        RegistrationSnapshot snapshot;
        lock (_gate)
        {
            previous = _registration.State;
            if (state == RegistrationState.Registered) Volatile.Write(ref _failure, null);
            if (code is not null) Volatile.Write(ref _failure, code);
            snapshot = _registration = new(state, _clock.GetUtcNow(), code)
                { SipProfileId = state == RegistrationState.Stopped ? null : _sipProfileId };
        }
        if (code is not null)
        {
            OperationEvents.Write(_log, SipRegistrationMessages.ForCode(code));
        }
        else if (state != previous)
        {
            var operation = state switch
            {
                RegistrationState.Registering => OperationEvent.SipRegistrationStarted,
                RegistrationState.Registered => OperationEvent.SipRegistrationSucceeded,
                RegistrationState.Stopped => OperationEvent.SipRegistrationStopped,
                _ => (OperationEvent?)null
            };
            if (operation is { } notice) OperationEvents.Write(_log, notice);
        }
        Publish(RegistrationChanged, new(snapshot));
    }
    private void Failure(string code) { Volatile.Write(ref _failure, code); _log.LogWarning("SIP operation: {Code}", code); }
    private void Publish<T>(EventHandler<T>? handler, T args) where T : EventArgs
    {
        if (handler is null) return;
        foreach (EventHandler<T> subscriber in handler.GetInvocationList())
            try { subscriber(this, args); } catch { Failure("SubscriberFailed"); }
    }
    private void Queue(Func<Task> callback) { if (!_disposed) _events.Writer.TryWrite(callback); }
    private Task EnqueueAsync(Func<Task> callback)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_events.Writer.TryWrite(async () => { try { await callback().ConfigureAwait(false); done.TrySetResult(); } catch (Exception e) { done.TrySetException(e); } }))
            done.TrySetException(new ObjectDisposedException(nameof(SipTelephonyService)));
        return done.Task;
    }
    private async Task ConsumeAsync()
    {
        await foreach (var callback in _events.Reader.ReadAllAsync().ConfigureAwait(false))
            try { await callback().ConfigureAwait(false); } catch { Failure("EventFailed"); }
    }
    private void Track(Func<Task> action)
    {
        var id = Interlocked.Increment(ref _jobId);
        var task = Task.Run(async () => { try { await action().ConfigureAwait(false); } catch { Failure("CallFailed"); } });
        _jobs[id] = task;
        _ = task.ContinueWith(completed => _jobs.TryRemove(id, out _), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
    private void CleanupTransport()
    {
        _keepAliveTimer?.Dispose();
        _keepAliveTimer = null;
        _registrar?.Stop(false);
        _registrar = null;
        Volatile.Write(ref _registeredPeer, null);
        _agent?.UserAgent.Dispose(); _agent = null;
        _transport?.Shutdown(); _transport = null;
        _lease?.Dispose(); _lease = null;
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisconnectAsync().ConfigureAwait(false);
        await _retryStopping.CancelAsync().ConfigureAwait(false);
        await _retryLoop.ConfigureAwait(false);
        _disposed = true; _events.Writer.TryComplete(); await _eventLoop.ConfigureAwait(false);
        _outbox.Close();
        _retryStopping.Dispose();
        _commands.Dispose();
    }

    private sealed class Agent(SIPUserAgent userAgent)
    {
        internal SIPUserAgent UserAgent { get; } = userAgent;
        internal Guid Id;
        internal ICallStateSession? State;
        internal SIPServerUserAgent? Incoming;
        internal ISipMediaSession? Media;
        internal (CancellationTokenSource Cancellation, Task Playback)? Ringtone;
        internal Task? AutomaticRecordingStart;
        internal SIPResponseStatusCodesEnum? FailureStatus;
        internal volatile bool TransferAccepted;
        internal volatile bool ShutdownRequested;
        internal volatile bool LocalHangupRequested;
        internal CancellationTokenSource Stopping { get; } = new();
    }

    private sealed class AnswerCredentials(string username, string password, string domain) : ISIPAccount
    {
        public string ID => username + "@" + domain;
        public string SIPUsername => username;
        public string SIPPassword => password;
        public string? HA1Digest => null;
        public string SIPDomain => domain;
        public bool IsDisabled => false;
        public override string ToString() => "[SIP credentials]";
    }
}
