using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Hosting;

/// <summary>Checks new telephone commands without prompting for activation or changing the selected company.</summary>
internal sealed class AppTelephonyAccess(IProductLicenseService licenses, LicenseProductIdentity product,
    ILexwareInstallation installation, IProductPluginRuntime runtime, ApplicationDescriptor application, RuntimeFailureState failures)
{
    internal async Task CheckLicenseAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (failures.Failure is not null) throw new UnauthorizedAccessException();
        if (!product.Required) return;
        var license = await licenses.ValidateAsync(token).ConfigureAwait(false);
        if (license.Status != LicenseStatus.Valid) throw new LicenseAccessException(license.Status);
    }

    internal async Task<Guid> DialAsync(CompanyKey company, Func<CancellationToken, Task<Guid>> dial, CancellationToken token)
    {
        await CheckLicenseAsync(token).ConfigureAwait(false);
        CheckSource();
        return await runtime.UseAsync(async (plugin, cancellation) =>
        {
            if (plugin.ProductLine != company.ProductLine || company.ProductLine.ToString() != application.ProductLine)
                throw new UnauthorizedAccessException();
            var companies = await plugin.Directory.GetCompaniesAsync(cancellation).ConfigureAwait(false);
            if (companies.IsDefault || companies.Length > 1000 || companies.Any(item => item?.Key is null)
                || companies.Select(item => item.Key).Distinct().Count() != companies.Length
                || !companies.Any(item => item.Key == company)) throw new UnauthorizedAccessException();
            // Keep the plugin lease until the backend has accepted the call. A source switch cannot overtake this check.
            await CheckLicenseAsync(cancellation).ConfigureAwait(false);
            CheckSource(); cancellation.ThrowIfCancellationRequested();
            return await dial(cancellation).ConfigureAwait(false);
        }, token).ConfigureAwait(false);
    }
    internal void CheckBusinessSource() => CheckSource();
    private void CheckSource()
    {
        var installed = installation.Check();
        var testSource = (runtime as IProductPluginSelection)?.ActivePluginId == ProductPluginIds.JtlWawi;
        if (!testSource && !installed.IsAvailable) throw new LexwareUnavailableException();
        if (!runtime.IsLoaded || runtime is IProductPluginAvailability availability && !availability.CanUseActivePlugin)
            throw new UnauthorizedAccessException();
    }
}

/// <summary>One shared UI/COM command gate around the existing SIP runtime; events and snapshots retain their original ownership.</summary>
internal sealed class AppTelephonyService(ITelephonyService backend, ITelephonyConnection connection, AppTelephonyAccess access)
    : ITelephonyService, ITelephonyConnection, ICallLineContextProvider, IDtmfReceiver, ICallRecordingService, ILiveCallRecordingService,
        IConversationAudioService, IAsyncDisposable
{
    private readonly SemaphoreSlim _commands = new(1);
    private readonly CancellationTokenSource _stopping = new();
    private int _disposed;
    public RegistrationSnapshot Registration => backend.Registration;
    public string? LastFailureCode => connection.LastFailureCode;
    public ImmutableArray<CallSnapshot> GetCalls() => backend.GetCalls();
    public PhoneLineContext? GetLineContext(Guid callId) => (backend as ICallLineContextProvider)?.GetLineContext(callId);
    public event EventHandler<RegistrationChangedEventArgs>? RegistrationChanged
    { add => backend.RegistrationChanged += value; remove => backend.RegistrationChanged -= value; }
    public event EventHandler<CallChangedEventArgs>? CallChanged
    { add => backend.CallChanged += value; remove => backend.CallChanged -= value; }
    public event EventHandler<DtmfReceivedEventArgs>? DtmfReceived
    { add { if (backend is IDtmfReceiver receiver) receiver.DtmfReceived += value; }
        remove { if (backend is IDtmfReceiver receiver) receiver.DtmfReceived -= value; } }
    public Task<IDuplexPcmRecording> StartRecordingAsync(DuplexPcmRecordingRequest request, CancellationToken token = default) =>
        backend is ICallRecordingService recordings ? recordings.StartRecordingAsync(request, token) : throw new TelephonyException("AudioUnavailable");
    public LiveRecordingSnapshot GetRecording(Guid callId) => (backend as ILiveCallRecordingService)?.GetRecording(callId)
        ?? new(callId, null, LiveRecordingState.Idle);
    public event EventHandler<LiveRecordingChangedEventArgs>? RecordingChanged
    { add { if (backend is ILiveCallRecordingService recordings) recordings.RecordingChanged += value; }
        remove { if (backend is ILiveCallRecordingService recordings) recordings.RecordingChanged -= value; } }
    public async Task StartLiveRecordingAsync(Guid callId, CancellationToken cancellationToken = default)
    {
        await access.CheckLicenseAsync(cancellationToken).ConfigureAwait(false);
        if (backend is not ILiveCallRecordingService recordings) throw new TelephonyException("RecordingUnavailable");
        await recordings.StartLiveRecordingAsync(callId, cancellationToken).ConfigureAwait(false);
    }
    public Task StopLiveRecordingAsync(Guid callId, CancellationToken cancellationToken = default) =>
        backend is ILiveCallRecordingService recordings ? recordings.StopLiveRecordingAsync(callId, cancellationToken)
            : throw new TelephonyException("RecordingUnavailable");
    public ConversationAudioSnapshot? GetAudio(Guid callId) => (backend as IConversationAudioService)?.GetAudio(callId);
    public Task SetPlaybackVolumeAsync(Guid callId, int percent, CancellationToken cancellationToken = default) =>
        backend is IConversationAudioService audio ? audio.SetPlaybackVolumeAsync(callId, percent, cancellationToken)
            : throw new TelephonyException("AudioUnavailable");
    public Task SetMicrophoneMutedAsync(Guid callId, bool muted, CancellationToken cancellationToken = default) =>
        backend is IConversationAudioService audio ? audio.SetMicrophoneMutedAsync(callId, muted, cancellationToken)
            : throw new TelephonyException("AudioUnavailable");

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
        var token = linked.Token;
        await _commands.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await access.CheckLicenseAsync(token).ConfigureAwait(false);
            if (Registration.State is RegistrationState.Registered or RegistrationState.Registering or RegistrationState.Reconnecting) return;
            if (GetCalls().Any(IsActive)) throw new TelephonyException("Busy");
            token.ThrowIfCancellationRequested();
            await connection.ConnectAsync(token).ConfigureAwait(false);
        }
        finally { _commands.Release(); }
    }
    public async Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);
        if (string.IsNullOrWhiteSpace(destination) || destination.Length > 256 || destination.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
            throw new TelephonyException("InvalidDestination");
        if (!destination.StartsWith("sip:", StringComparison.OrdinalIgnoreCase) && !destination.StartsWith("sips:", StringComparison.OrdinalIgnoreCase)
            && destination.Any(character => !char.IsAsciiDigit(character) && character is not ('+' or '*' or '#')))
            throw new TelephonyException("InvalidDestination");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
        var token = linked.Token;
        await _commands.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await access.DialAsync(company, async checkedToken =>
            {
                if (Registration.State != RegistrationState.Registered) throw new TelephonyException("NotRegistered");
                if (GetCalls().Any(IsActive)) throw new TelephonyException("Busy");
                return await backend.DialAsync(company, destination, checkedToken).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }
        finally { _commands.Release(); }
    }
    internal static bool IsActive(CallSnapshot call) => call.State is not (CallState.Ended or CallState.Failed);
    // Ending an owned call remains available after license/source revocation.
    public Task HangUpAsync(Guid callId, CancellationToken cancellationToken = default) => backend.HangUpAsync(callId, cancellationToken);
    public Task AnswerAsync(Guid callId, CancellationToken cancellationToken = default) => backend.AnswerAsync(callId, cancellationToken);
    public Task RejectAsync(Guid callId, CancellationToken cancellationToken = default) => backend.RejectAsync(callId, cancellationToken);
    public Task HoldAsync(Guid callId, CancellationToken cancellationToken = default) => backend.HoldAsync(callId, cancellationToken);
    public Task ResumeAsync(Guid callId, CancellationToken cancellationToken = default) => backend.ResumeAsync(callId, cancellationToken);
    public Task TransferAsync(Guid callId, string destination, CancellationToken cancellationToken = default) => backend.TransferAsync(callId, destination, cancellationToken);
    public Task SendDtmfAsync(Guid callId, char digit, CancellationToken cancellationToken = default) => backend.SendDtmfAsync(callId, digit, cancellationToken);
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await connection.DisconnectAsync(cancellationToken).ConfigureAwait(false); }
        finally { _commands.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _stopping.CancelAsync().ConfigureAwait(false);
        await _commands.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        _commands.Release(); _commands.Dispose(); _stopping.Dispose();
    }
}
