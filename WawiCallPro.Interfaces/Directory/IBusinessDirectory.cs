using System.Collections.Immutable;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Interfaces.Directory;

public sealed record PartyQuery(CompanyKey Company, PageRequest Page, string? SearchText = null, PartyKind? Kind = null);

/// <summary>The source company accompanies the result even when the result is empty.</summary>
public sealed record PartyPage(CompanyKey Company, Page<Party> Page);

/// <summary>
/// Product-neutral access to external master data. Each request carries its company explicitly.
/// Unsupported operations throw NotSupportedException; failures throw and cancellation uses
/// OperationCanceledException. Empty results mean a successful query without matches only.
/// </summary>
public interface IBusinessDirectory
{
    Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default);
    Task<CapabilitySet> GetCapabilitiesAsync(CompanyKey company, CancellationToken cancellationToken = default);
    Task<PartyPage> GetPartiesAsync(PartyQuery query, CancellationToken cancellationToken = default);

    /// <summary>Returns null only when the source record does not exist in the specified company.</summary>
    Task<Party?> GetPartyAsync(PartyKey key, CancellationToken cancellationToken = default);
}
