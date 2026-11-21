using System.Collections.Immutable;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Tests;

/// <summary>Only synthetic identities and an in-memory telephone backend; never opens a SIP transport or audio device.</summary>
internal sealed class ComTelephonyFixture : IAsyncDisposable
{
    internal readonly ApplicationDescriptor Application = ApplicationDescriptor.FromAssembly(typeof(MainForm).Assembly);
    internal readonly LicenseProbe License = new();
    internal readonly InstallationProbe Installation = new();
    internal readonly Backend Phone = new();
    internal readonly RuntimeFailureState Failures = new();
    internal readonly Runtime Source;
    internal readonly CompanyKey A, B;
    internal readonly AppTelephonyService Service;
    internal ComTelephonyFixture()
    {
        var line = Enum.Parse<LexwareProductLine>(Application.ProductLine);
        A = new(line, "installation-A", "1"); B = new(line, "installation-B", "1");
        Source = new(line, [new(A, "Firma A"), new(B, "Firma B")]);
        Service = new(Phone, Phone, new(License, new("fixture", Guid.NewGuid(), "1.0", true), Installation, Source, Application, Failures));
    }
    public ValueTask DisposeAsync() => Service.DisposeAsync();
    internal sealed class LicenseProbe : IProductLicenseService
    {
        internal LicenseStatus Status = LicenseStatus.Valid;
        internal int Checks;
        public Task<ProductLicenseSnapshot> ValidateAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); Checks++; return Task.FromResult(new ProductLicenseSnapshot(Status)); }
        public Task<ProductLicenseSnapshot> ActivateAsync(string key, CancellationToken token) => throw new AssertFailedException("No activation expected.");
        public Task<ProductLicenseSnapshot> RefreshAsync(CancellationToken token) => throw new AssertFailedException("No online request expected.");
        public string GetMachineId() => "synthetic";
        public Task<CrashAuthentication?> GetCrashAuthenticationAsync(CancellationToken token) => Task.FromResult<CrashAuthentication?>(null);
    }
    internal sealed class InstallationProbe : ILexwareInstallation
    {
        internal LexwareInstallationState State = new(true, true, false);
        internal int Checks;
        public LexwareInstallationState Current => State;
        public LexwareInstallationState Check() { Checks++; return State; }
        public void EnsureAvailable() { if (!Check().IsAvailable) throw new LexwareUnavailableException(); }
    }
    internal sealed class Runtime(LexwareProductLine line, ImmutableArray<Company> companies)
        : IProductPluginRuntime, IProductPluginSelection, IProductPluginAvailability, IBusinessDirectory, IWawiCallPlugin
    {
        private readonly SemaphoreSlim _gate = new(1);
        internal bool Available = true;
        internal Func<CancellationToken, Task<ImmutableArray<Company>>>? Catalog;
        public bool IsLoaded => true;
        public bool CanUseActivePlugin => Available;
        public bool CanActivate(string id) => Available;
        public string? ActivePluginId { get; private set; } = "Synthetic.Lexware";
        public IReadOnlyList<ProductPluginChoice> GetAvailablePlugins() => [];
        public async Task SwitchAsync(string id, CancellationToken token = default) { await _gate.WaitAsync(token); try { ActivePluginId = id; } finally { _gate.Release(); } }
        public async Task<T> UseAsync<T>(Func<IWawiCallPlugin, CancellationToken, Task<T>> action, CancellationToken token = default)
        { await _gate.WaitAsync(token); try { return await action(this, token); } finally { _gate.Release(); } }
        public string Name => "Synthetic telephone source";
        public Version Version { get; } = new(1, 0);
        public LexwareProductLine ProductLine => line;
        public IBusinessDirectory Directory => this;
        public void Initialize(Quantarix.Plugin.Abstractions.IPluginHost host) { }
        public void Configure(IWawiCallHostServices services) { }
        public void Execute() => throw new NotSupportedException();
        public void Shutdown() { }
        public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken token = default) => Catalog?.Invoke(token) ?? Task.FromResult(companies);
        public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken token = default) => throw new NotSupportedException();
        public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken token = default) => throw new NotSupportedException();
        public Task<Party?> GetPartyAsync(PartyKey party, CancellationToken token = default) => throw new NotSupportedException();
    }
    internal sealed class Backend : ITelephonyService, ITelephonyConnection
    {
        private readonly object _gate = new();
        private ImmutableArray<CallSnapshot> _calls = [];
        internal int Attempts, Connects, Disconnects, Hangups;
        internal bool RegisterOnConnect = true;
        internal Func<CancellationToken, Task>? BeforeDial;
        internal Func<CallSnapshot, Task>? Persist = null;
        internal Exception? Failure = null;
        internal CancellationToken LastDialToken;
        public RegistrationSnapshot Registration { get; private set; } = new(RegistrationState.Stopped, DateTimeOffset.UtcNow);
        public string? LastFailureCode => null;
        public event EventHandler<RegistrationChangedEventArgs>? RegistrationChanged;
        public event EventHandler<CallChangedEventArgs>? CallChanged;
        internal int RegistrationSubscribers => RegistrationChanged?.GetInvocationList().Length ?? 0;
        public ImmutableArray<CallSnapshot> GetCalls() { lock (_gate) return _calls; }
        internal void Register(RegistrationState state) { Registration = new(state, DateTimeOffset.UtcNow); RegistrationChanged?.Invoke(this, new(Registration)); }
        public Task ConnectAsync(CancellationToken token = default) { token.ThrowIfCancellationRequested(); Connects++; Register(RegisterOnConnect ? RegistrationState.Registered : RegistrationState.Registering); return Task.CompletedTask; }
        public Task DisconnectAsync(CancellationToken token = default) { Disconnects++; Register(RegistrationState.Stopped); return Task.CompletedTask; }
        internal async Task PublishAsync(CallSnapshot call)
        {
            if (Persist is not null) await Persist(call);
            lock (_gate) _calls = _calls.Where(old => old.Id != call.Id).Append(call).ToImmutableArray();
            CallChanged?.Invoke(this, new(call));
        }
        public async Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken token = default)
        {
            Attempts++; LastDialToken = token;
            if (BeforeDial is not null) await BeforeDial(token);
            if (Failure is not null) throw Failure;
            var now = DateTimeOffset.UtcNow;
            var call = new CallSnapshot { Id = Guid.NewGuid(), Revision = 1, Company = company, Direction = CallDirection.Outgoing,
                State = CallState.Dialing, LocalAddress = new(NumberPresentation.Unknown), RemoteAddress = new(NumberPresentation.Available, destination),
                StartedAtUtc = now, StateChangedAtUtc = now };
            await PublishAsync(call); return call.Id;
        }
        public async Task HangUpAsync(Guid id, CancellationToken token = default)
        {
            var call = GetCalls().SingleOrDefault(call => call.Id == id && AppTelephonyService.IsActive(call)) ?? throw new TelephonyException("CallNotActive");
            Hangups++;
            await PublishAsync(call with { Revision = call.Revision + 1, State = CallState.Ended, StateChangedAtUtc = DateTimeOffset.UtcNow,
                EndedAtUtc = DateTimeOffset.UtcNow, EndReason = CallEndReason.Cancelled });
        }
        public Task AnswerAsync(Guid id, CancellationToken token = default) => Task.CompletedTask;
        public Task RejectAsync(Guid id, CancellationToken token = default) => HangUpAsync(id, token);
        public Task HoldAsync(Guid id, CancellationToken token = default) => Task.CompletedTask;
        public Task ResumeAsync(Guid id, CancellationToken token = default) => Task.CompletedTask;
        public Task TransferAsync(Guid id, string destination, CancellationToken token = default) => Task.CompletedTask;
        public Task SendDtmfAsync(Guid id, char digit, CancellationToken token = default) => Task.CompletedTask;
    }
}
