using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.Utilities.Persistence;

/// <summary>Local, read-only diagnostics. Filtering scans bounded repository pages on a worker, without new SQL dialects.</summary>
public sealed class PhoneNumberDiagnostics(ICompanyRepository companies, INumberParseLogRepository errors,
    ApplicationDescriptor application) : IPhoneNumberDiagnostics
{
    public Task<ImmutableArray<Company>> GetCompaniesAsync(CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        var result = ImmutableArray.CreateBuilder<Company>();
        for (var offset = 0; ; offset = checked(offset + 500))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await companies.GetPageAsync(new(new(offset, 500), Enum.Parse<LexwareProductLine>(application.ProductLine)), cancellationToken).ConfigureAwait(false);
            result.AddRange(page.Items.Select(item => item.Company));
            if (!page.HasMore) return result.ToImmutable();
        }
    }, cancellationToken);

    public Task<Page<PhoneNumberDiagnostic>> GetPageAsync(CompanyKey company, PageRequest page, string? search = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(page);
        if (company.ProductLine.ToString() != application.ProductLine) throw new ArgumentException("Company belongs to another edition.", nameof(company));
        return Task.Run(() => ReadAsync(company, page, search?.Trim(), cancellationToken), cancellationToken);
    }

    private async Task<Page<PhoneNumberDiagnostic>> ReadAsync(CompanyKey company, PageRequest request, string? search, CancellationToken token)
    {
        if (string.IsNullOrEmpty(search))
        {
            var result = await errors.GetPageAsync(new(company, request), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return new(result.Items.Select(Convert).ToImmutableArray(), result.HasMore);
        }
        var matches = ImmutableArray.CreateBuilder<PhoneNumberDiagnostic>();
        var skipped = 0;
        for (var offset = 0; ; offset = checked(offset + 500))
        {
            token.ThrowIfCancellationRequested();
            var source = await errors.GetPageAsync(new(company, new(offset, 500)), token).ConfigureAwait(false);
            foreach (var failure in source.Items)
            {
                token.ThrowIfCancellationRequested();
                var item = Convert(failure);
                if (!Matches(item, search)) continue;
                if (skipped < request.Offset) { skipped++; continue; }
                if (matches.Count == request.Limit) return new(matches.ToImmutable(), true);
                matches.Add(item);
            }
            if (!source.HasMore) return new(matches.ToImmutable(), false);
        }
    }

    private static PhoneNumberDiagnostic Convert(NumberParseFailure failure) => new(failure.Id, failure.Party, failure.Source,
        failure.Kind, failure.OriginalValue, failure.Code, Reason(failure.Code), failure.OccurredAtUtc);

    private static bool Matches(PhoneNumberDiagnostic item, string search) => new[]
    {
        item.OriginalValue, item.Code, item.Reason, item.Source.RecordType, item.Source.RecordId, item.Source.FieldName,
        item.Party.ExternalId, item.Party.OwnerExternalId, item.Party.Kind.ToString(), item.Kind.ToString()
    }.Any(value => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);

    private static string Reason(string code) => code switch
    {
        "number.empty" => "Das Quellfeld ist leer.",
        "number.not-a-number" => "Der Quellwert enthält keine auswertbare Rufnummer.",
        "number.invalid" => "Die Rufnummer ist laut Nummernplan ungültig.",
        "number.possible-only" => "Die Länge ist möglich, die Rufnummer aber nicht als gültig bestätigt.",
        "number.too-short" => "Die Rufnummer ist zu kurz.",
        "number.too-long" or "number.e164-length" => "Die Rufnummer ist zu lang.",
        "number.invalid-country-or-region" => "Ländervorwahl fehlt oder passt nicht zum eingestellten Land.",
        "number.unsupported-region" => "Das eingestellte Land wird nicht unterstützt.",
        "number.sip-address" or "number.unsupported-address" => "Eine SIP-Adresse ist keine öffentliche Telefonnummer.",
        "number.internal" => "Interne Nebenstelle; keine öffentliche Telefonnummer.",
        "number.internal-context-required" => "Interne Nebenstelle ohne zugeordnete Telefonanlage.",
        "number.invalid-internal" => "Die interne Nebenstelle enthält ungültige Zeichen.",
        "number.non-public-code" => "Steuer- oder Dienstcode; keine öffentliche Telefonnummer.",
        "number.invalid-extension" => "Die explizite Durchwahl ist ungültig.",
        "number.extension-conflict" => "Durchwahl im Quellwert und explizite Durchwahl widersprechen sich.",
        "number.local-context-required" => "Für die lokale Rufnummer fehlt ein gültiger Orts-/Landeskontext.",
        "number.invalid-area-code" => "Die Ortsvorwahl fehlt oder passt nicht zum Rufnummernkontext.",
        "number.input-kind-conflict" => "Die Schreibweise passt nicht zur angegebenen Nummernart.",
        "number.withheld" => "Die Rufnummer ist unterdrückt.",
        "number.unknown" => "Die Rufnummer ist unbekannt.",
        _ => "Die Quelle konnte nicht als öffentliche Rufnummer aufgelöst werden."
    };
}
