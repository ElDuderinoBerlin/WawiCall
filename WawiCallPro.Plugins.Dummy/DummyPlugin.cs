using System.Collections.Immutable;
using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugins.Dummy;

/// <summary>Explicit test source without ERP, network or database access.</summary>
public sealed class DummyPlugin : IWawiCallPlugin, IBusinessDirectory, IWawiCallPluginWorkspaces
{
    private readonly CompanyKey _company;
    public DummyPlugin() => _company = new(ProductLine, "dummy-test-installation", "demo");
    private bool _initialized;
    private bool _configured;
    public string Name => "Dummy-Plugin (Testbetrieb)";
    public Version Version { get; } = new(1, 0);
#if LX_PRO
    public LexwareProductLine ProductLine => LexwareProductLine.Pro;
#else
    public LexwareProductLine ProductLine => LexwareProductLine.FinOffice;
#endif
    private CompanyKey Company => _company;
    public IBusinessDirectory Directory => _configured ? this : throw new InvalidOperationException("Plugin is not configured.");
    public void Initialize(IPluginHost host) { ArgumentNullException.ThrowIfNull(host); _initialized = true; }
    public void Configure(IWawiCallHostServices services) { if (!_initialized) throw new InvalidOperationException(); _configured = true; }
    public void Shutdown() { _configured = false; _initialized = false; }
    public IPluginLifetimeResource CreatePartySearch(IPartySearchWorkspaceFactory factory) => _configured
        ? factory.Create() : throw new InvalidOperationException("Plugin is not configured.");
    public void Execute() => throw new NotSupportedException("Explizite Testquelle über den Datenadapter verwenden.");
    public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    { Check(cancellationToken); return Task.FromResult(ImmutableArray.Create(new Company(Company, "Dummy – Testmandant"))); }
    public Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default)
    {
        CheckCompany(company, cancellationToken);
        var entries = Enum.GetValues<BusinessFeature>().ToImmutableDictionary(feature => feature,
            feature => new CapabilitySupport(feature is BusinessFeature.Customers or BusinessFeature.MasterData ? FeatureAvailability.Partial : FeatureAvailability.Unsupported,
                "Explizite Dummy-Testdaten; keine Verbindung zu einer Warenwirtschaft."));
        var report = new CapabilitySet(entries);
        return Task.FromResult(new CapabilitySet(company, report, new CapabilitySet(ImmutableDictionary<BusinessFeature, CapabilitySupport>.Empty)));
    }
    public Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default)
    {
        CheckCompany(query.Company, cancellationToken);
        var party = CreateParty();
        var matches = (query.Kind is null or PartyKind.Customer) && (string.IsNullOrEmpty(query.SearchText)
            || party.DisplayName.Contains(query.SearchText, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(new PartyPage(query.Company, new Page<Party>(matches && query.Page.Offset == 0 ? [party] : [], false)));
    }
    public Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default)
    { CheckCompany(key.Company, cancellationToken); return Task.FromResult<Party?>(key == CreateParty().Key ? CreateParty() : null); }
    private Party CreateParty() => new() { Key = new(Company, PartyKind.Customer, "dummy-customer"), DisplayName = "Dummy – Beispielkunde", Number = "TEST-001" };
    private void Check(CancellationToken token) { token.ThrowIfCancellationRequested(); if (!_configured) throw new InvalidOperationException("Plugin is not configured."); }
    private void CheckCompany(CompanyKey company, CancellationToken token) { Check(token); if (company != Company) throw new InvalidOperationException("Fremder Dummy-Mandant."); }
}
