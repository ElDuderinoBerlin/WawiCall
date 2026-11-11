using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.Telephony.Sip;

/// <summary>One account and one live call per workstation. Observations are persisted and published in queue order.</summary>
public sealed class SipTelephonyService : ITelephonyService, ITelephonyConnection, IDtmfReceiver, ICallLineContextProvider, IAsyncDisposable
{
    private readonly ConfigurationStore _settings;
    private readonly ISecretProtector _secrets;
    private readonly ISipTransportFactory _transports;
    private readonly ISipAccountLeaseFactory _leases;
    private readonly ISipMediaSessionFactory _media;
    private readonly ISipCallPersistence _persistence;
    private readonly ICallStateSessionFactory _states;
    private readonly TimeProvider _clock;
    private readonly ILogger<SipTelephonyService> _log;
    private readonly SemaphoreSlim _commands = new(1);
    private readonly Lock _gate = new();
    private readonly Channel<Func<Task>> _events = Channel.CreateUnbounded<Func<Task>>(new() { SingleReader = true });
    private readonly Task _eventLoop;
    private readonly ConcurrentDictionary<Guid, CallSnapshot> _calls = new();
    private readonly ConcurrentDictionary<Guid, WawiCallPro.Interfaces.PhoneNumbers.PhoneLineContext> _callLines = new();
    private readonly ConcurrentDictionary<long, Task> _jobs = new();
    private long _jobId;
    private RegistrationSnapshot _registration = new(RegistrationState.Stopped, DateTimeOffset.UtcNow);
    private SIPTransport? _transport;
    private SIPRegistrationUserAgent? _registrar;
    private IDisposable? _lease;
    private SipAccountOptions? _account;
    private AudioDeviceSettings _devices = new();
    private Agent? _agent;
    private bool _stopping;
    private bool _disposed;
    private string? _failure;

    public SipTelephonyService(ConfigurationStore settings, ISecretProtector secrets, ISipTransportFactory transports,
        ISipAccountLeaseFactory leases, ISipMediaSessionFactory media, ISipCallPersistence persistence,
        ICallStateSessionFactory states, TimeProvider clock, ILogger<SipTelephonyService> log)
    {
        _settings = settings; _secrets = secrets; _transports = transports; _leases = leases; _media = media;
        _persistence = persistence; _states = states; _clock = clock; _log = log;
        _eventLoop = ConsumeAsync();
    }

    public RegistrationSnapshot Registration { get { lock (_gate) return _registration; } }
    public string? LastFailureCode => Volatile.Read(ref _failure);
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
            _account = account; _devices = snapshot.Options.Devices; _stopping = false;
            _lease = _leases.Acquire(account);
            // A second host must never mark the first host's live calls as interrupted.
            try { await _persistence.RecoverAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch { throw new TelephonyException("DatabaseNotReady"); }
            _transport = _transports.Create(account);
            var activeTransport = _transport;
            _transport.SIPTransportRequestReceived += async (_, _, request) =>
            {
                if (_stopping || !ReferenceEquals(_transport, activeTransport)) return;
                if (request.Method == SIPMethodsEnum.INVITE && string.IsNullOrEmpty(request.Header.To.ToTag))
                {
                    Agent? incoming;
                    lock (_gate) incoming = _agent;
                    if (incoming is not null) Track(() => IncomingAsync(incoming, request));
                }
                else if (request.Method == SIPMethodsEnum.OPTIONS && _transport is { } transport)
                    await transport.SendResponseAsync(SIPResponse.GetResponse(request, SIPResponseStatusCodesEnum.Ok, null)).ConfigureAwait(false);
            };
            _agent = CreateAgent();
            using var password = _secrets.Unprotect(account.Password!, SipAccountOptions.PasswordPurpose);
            var contact = new SIPURI(SIPSchemesEnum.sip, System.Net.IPAddress.Any, 0)
            {
                User = account.Username,
                Protocol = Enum.Parse<SIPProtocolsEnum>(account.Transport.ToString(), true)
            };
            // Transient failures retry; a rejected account needs an explicit new login after correction.
            var registrar = new SIPRegistrationUserAgent(_transport, null, Address(account.Username!),
                account.AuthenticationUsername ?? account.Username, password.Reveal(), null, Address(null).ToString(), contact,
                account.Connection.RegistrationExpirySeconds, null, account.Connection.RegistrationTimeoutSeconds,
                account.Connection.RetryIntervalSeconds, 3, true);
            _registrar = registrar;
            registrar.RegistrationSuccessful += (_, _) => { if (ReferenceEquals(_registrar, registrar) && !_stopping) SetRegistration(RegistrationState.Registered); };
            registrar.RegistrationTemporaryFailure += (_, _, _) => { if (ReferenceEquals(_registrar, registrar) && !_stopping) SetRegistration(RegistrationState.Reconnecting, "RegistrationRetry"); };
            registrar.RegistrationFailed += (_, _, _) => { if (ReferenceEquals(_registrar, registrar) && !_stopping) SetRegistration(RegistrationState.Failed, "RegistrationFailed"); };
            registrar.RegistrationRemoved += (_, _) => { if (ReferenceEquals(_registrar, registrar) && !_stopping) SetRegistration(RegistrationState.Stopped); };
            SetRegistration(RegistrationState.Registering);
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
        finally { _commands.Release(); }
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
                if (agent.UserAgent.IsCallActive) agent.UserAgent.Hangup();
                else if (agent.Incoming is not null) agent.Incoming.Reject(SIPResponseStatusCodesEnum.TemporarilyUnavailable, null);
                else agent.UserAgent.Cancel();
                await EnqueueAsync(() => ChangeAsync(agent, CallState.Failed, CallEndReason.Interrupted)).ConfigureAwait(false);
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
            await EnqueueAsync(() => Task.CompletedTask).ConfigureAwait(false);
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
            agent = GetAgent(callId, CallState.Ringing);
            if (agent.Incoming is null) throw new TelephonyException("NotIncoming");
            agent.Media = _media.Create(_account!, _devices);
            using var registration = cancellationToken.Register(() => agent.UserAgent.Hangup());
            if (!await agent.UserAgent.Answer(agent.Incoming, agent.Media.Session).ConfigureAwait(false)) throw new TelephonyException("AnswerFailed");
            agent.Media.ApplyPlaybackSettings();
            await EnqueueAsync(() => ChangeAsync(agent, CallState.Connected)).ConfigureAwait(false);
        }
        catch
        {
            if (agent is not null)
            {
                agent.Incoming?.Reject(SIPResponseStatusCodesEnum.TemporarilyUnavailable, null);
                await EnqueueAsync(() => ChangeAsync(agent, CallState.Failed, CallEndReason.Error)).ConfigureAwait(false);
            }
            throw;
        }
        finally { _commands.Release(); }
    }

    public Task RejectAsync(Guid callId, CancellationToken cancellationToken = default) => CommandAsync(callId, async agent =>
    {
        if (agent.Incoming is null || agent.State!.Current.State != CallState.Ringing) throw new TelephonyException("NotIncoming");
        agent.Incoming.Reject(SIPResponseStatusCodesEnum.BusyHere, null);
        await EnqueueAsync(() => ChangeAsync(agent, CallState.Ended, CallEndReason.Rejected)).ConfigureAwait(false);
    }, cancellationToken);

    public Task HangUpAsync(Guid callId, CancellationToken cancellationToken = default) => CommandAsync(callId, async agent =>
    {
        var current = agent.State!.Current;
        var reason = current.AnsweredAtUtc.HasValue ? CallEndReason.Completed : current.Direction == CallDirection.Outgoing ? CallEndReason.Cancelled : CallEndReason.Rejected;
        if (agent.UserAgent.IsCallActive) agent.UserAgent.Hangup();
        else if (agent.Incoming is not null) agent.Incoming.Reject(SIPResponseStatusCodesEnum.Decline, null);
        else agent.UserAgent.Cancel();
        await EnqueueAsync(() => ChangeAsync(agent, CallState.Ended, reason)).ConfigureAwait(false);
    }, cancellationToken);

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
        await agent.UserAgent.SendDtmf((byte)code).WaitAsync(cancellationToken).ConfigureAwait(false);
    }, cancellationToken);

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
        agent.UserAgent.OnCallHungup += _ => Queue(() => ChangeAsync(agent,
            agent.ShutdownRequested ? CallState.Failed : CallState.Ended,
            agent.ShutdownRequested ? CallEndReason.Interrupted : agent.TransferAccepted ? CallEndReason.Transferred : CallEndReason.RemoteHangup));
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
            var number = request.Header.From.FromURI.User;
            var withheld = string.Equals(number, "anonymous", StringComparison.OrdinalIgnoreCase);
            if (withheld) number = null;
            Begin(agent, CallDirection.Incoming, number, null);
            if (withheld) agent.State = _states.Create(agent.State!.Current with { RemoteAddress = new(NumberPresentation.Withheld) });
            Queue(() => PublishAsync(agent));
            agent.Incoming = agent.UserAgent.AcceptCall(request);
        }
        await EnqueueAsync(() => ChangeAsync(agent, CallState.Ringing)).ConfigureAwait(false);
        agent.Incoming.Progress(SIPResponseStatusCodesEnum.Ringing, null, null, null, null);
        Track(async () =>
        {
            try { await Task.Delay(TimeSpan.FromSeconds(_account!.Connection.RingTimeoutSeconds), agent.Stopping.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            if (agent.State?.Current.State != CallState.Ringing) return;
            agent.Incoming.Reject(SIPResponseStatusCodesEnum.TemporarilyUnavailable, null);
            await EnqueueAsync(() => ChangeAsync(agent, CallState.Ended, CallEndReason.Missed)).ConfigureAwait(false);
        });
    }

    private async Task PlaceCallAsync(Agent agent, string target)
    {
        try
        {
            agent.Media = _media.Create(_account!, _devices);
            using var password = _secrets.Unprotect(_account!.Password!, SipAccountOptions.PasswordPurpose);
            var descriptor = new SIPCallDescriptor(_account.Username, password.Reveal(), target, Address(_account.Username!).ToString(), target,
                null, null, _account.AuthenticationUsername ?? _account.Username, SIPCallDirection.Out, null, null, null);
            var connected = await agent.UserAgent.Call(descriptor, agent.Media.Session, _account.Connection.RingTimeoutSeconds).ConfigureAwait(false);
            if (connected)
            {
                agent.Media.ApplyPlaybackSettings();
                await EnqueueAsync(() => ChangeAsync(agent, CallState.Connected)).ConfigureAwait(false);
            }
            else
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
        agent.State = _states.Create(new() { Id = agent.Id, Revision = 0, Company = company, Direction = direction, State = CallState.Created,
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
        await PublishAsync(agent).ConfigureAwait(false);
        if (state is not (CallState.Ended or CallState.Failed)) return;
        agent.Stopping.Cancel();
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
        try
        {
            var saved = await _persistence.SaveAsync(snapshot).ConfigureAwait(false);
            if (snapshot.Origin is null) agent.State = _states.Create(saved);
            snapshot = agent.State.Current;
        }
        catch { Failure("CallStorageUnavailable"); }
        _calls[snapshot.Id] = snapshot;
        if (_calls.Count > 128)
            foreach (var old in _calls.Values.Where(call => call.State is CallState.Ended or CallState.Failed).OrderBy(call => call.StartedAtUtc).Take(_calls.Count - 128))
            { _calls.TryRemove(old.Id, out _); _callLines.TryRemove(old.Id, out _); }
        Publish(CallChanged, new(snapshot));
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
        if (state == RegistrationState.Registered) Volatile.Write(ref _failure, null);
        if (code is not null) Failure(code);
        lock (_gate) _registration = new(state, _clock.GetUtcNow(), code);
        Publish(RegistrationChanged, new(Registration));
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
        _registrar?.Stop(false);
        _registrar = null;
        _agent?.UserAgent.Dispose(); _agent = null;
        _transport?.Shutdown(); _transport = null;
        _lease?.Dispose(); _lease = null;
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisconnectAsync().ConfigureAwait(false);
        _disposed = true; _events.Writer.TryComplete(); await _eventLoop.ConfigureAwait(false);
        _commands.Dispose();
    }

    private sealed class Agent(SIPUserAgent userAgent)
    {
        internal SIPUserAgent UserAgent { get; } = userAgent;
        internal Guid Id;
        internal ICallStateSession? State;
        internal SIPServerUserAgent? Incoming;
        internal ISipMediaSession? Media;
        internal SIPResponseStatusCodesEnum? FailureStatus;
        internal volatile bool TransferAccepted;
        internal volatile bool ShutdownRequested;
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
