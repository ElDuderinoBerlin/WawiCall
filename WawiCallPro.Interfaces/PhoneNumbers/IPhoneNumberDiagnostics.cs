using System.Collections.Immutable;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Interfaces.PhoneNumbers;

/// <summary>A detached source-data observation, never an operational log message.</summary>
public sealed record PhoneNumberDiagnostic(Guid Id, PartyKey Party, SourceField Source, PhoneKind Kind,
    string OriginalValue, string Code, string Reason, DateTimeOffset OccurredAtUtc)
{
    public override string ToString() => nameof(PhoneNumberDiagnostic);
}

/// <summary>Reads historical source errors from local storage, without changing the selected company or contacting ERP.</summary>
public interface IPhoneNumberDiagnostics
{
    Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default);

    /// <summary>Literal, case-insensitive substring search across all stored pages, including source identities and the reason.</summary>
    Task<Page<PhoneNumberDiagnostic>> GetPageAsync(CompanyKey company, PageRequest page, string? search = null,
        CancellationToken cancellationToken = default);
}
