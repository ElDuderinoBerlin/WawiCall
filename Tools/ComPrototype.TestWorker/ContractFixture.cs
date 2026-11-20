using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using WawiCallPro.Automation;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.ComPrototype;

// Synthetic source/presentation only. No product host, database, installation registry or network is used.
internal sealed class ContractFixture(PrototypeOptions options) : IAutomationEnvironment, IPartySearch, IAutomationViewHost, IAutomationTelephony
{
    private int _visible;
    private int _dialCount;
    private readonly ConcurrentDictionary<Guid, AutomationCall> _calls = new();
#if LX_PRO
    private const LexwareProductLine Product = LexwareProductLine.Pro;
#else
    private const LexwareProductLine Product = LexwareProductLine.FinOffice;
#endif
    private static readonly CompanyKey CompanyA = new(Product, "fixture-install", "0001");
    private static readonly CompanyKey CompanyB = new(Product, "fixture-install", "0002");
    public AutomationStatus GetStatus() => new(Product, "Ready", Volatile.Read(ref _visible) != 0, "Registered");
    public ImmutableArray<Company> GetCompanies() => [new(CompanyA, "Müller 東京"), new(CompanyB, "Zweiter Testmandant")];
    public ImmutableArray<AutomationView> GetViews() => [new("home", "Startseite"), new("history", "Historie")];
    public void RequireAccess(AutomationAccess access, CompanyKey? company = null)
    { if (company is not null && company != CompanyA && company != CompanyB) throw new AutomationFault(AutomationFailure.AccessDenied); }
    public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (query.Text == "00888") throw new InvalidOperationException("synthetic-private-secret");
        var parties = new[]
        {
            new Party { Key = new(query.Company, PartyKind.Customer, query.Company == CompanyA ? "customer-a" : "customer-b"), Number = "00123", DisplayName = "Müller 東京" },
            new Party { Key = new(query.Company, PartyKind.Supplier, "supplier"), Number = "00123", DisplayName = "Lieferant" },
            new Party { Key = new(query.Company, PartyKind.Customer, "unicode"), Number = "Ä-東京", DisplayName = "Unicode" },
            new Party { Key = new(query.Company, PartyKind.Customer, "duplicate-one"), Number = "00777", DisplayName = "Doppelt" },
            new Party { Key = new(query.Company, PartyKind.Customer, "duplicate-two"), Number = "00777", DisplayName = "Doppelt" }
        };
        return Task.FromResult(new PartySearchResult(query, parties.Where(party => party.Key.Kind == query.Kind && party.Number!.Contains(query.Text, StringComparison.Ordinal)).ToImmutableArray()));
    }
    public Task ShowAsync(PresentationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _visible, 1);
        File.WriteAllText(Path.Combine(options.Profile, "presentation.json"), JsonSerializer.Serialize(new
        { request.ViewId, Company = request.Company?.ExternalId, Kind = request.Party?.Kind.ToString(), ExternalId = request.Party?.ExternalId, Thread = Environment.CurrentManagedThreadId }));
        return Task.CompletedTask;
    }
    public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = Guid.NewGuid(); _calls[id] = new(id, "Dialing");
        File.WriteAllText(Path.Combine(options.Profile, "dial-count.json"), JsonSerializer.Serialize(new { Count = Interlocked.Increment(ref _dialCount) }));
        return Task.FromResult(id);
    }
    public Task HangUpAsync(Guid callId, CancellationToken cancellationToken)
    { if (!_calls.TryGetValue(callId, out _)) throw new AutomationFault(AutomationFailure.NotFound); _calls[callId] = new(callId, "Ended"); return Task.CompletedTask; }
    public AutomationCall GetCall(Guid callId) => _calls.TryGetValue(callId, out var call) ? call : throw new AutomationFault(AutomationFailure.NotFound);
}

internal sealed class ContractDispatcher(Quantarix.Interop.Interfaces.IUiDispatcher dispatcher) : IUiDispatcher
{
    public ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dispatcher.Invoke(() => { cancellationToken.ThrowIfCancellationRequested(); callback(); });
        return ValueTask.CompletedTask;
    }
}
