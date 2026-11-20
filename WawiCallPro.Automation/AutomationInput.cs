using System.Text;
using System.Text.Json;
using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Automation;

internal static class AutomationInput
{
    internal static string Text(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl)) throw new AutomationFault(AutomationFailure.InvalidArgument);
        return value.Trim();
    }
    internal static Guid Id(string value)
    { if (!Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty) throw new AutomationFault(AutomationFailure.InvalidArgument); return id; }
    internal static string Encode(CompanyKey key) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(key)));
    internal static CompanyKey Company(string token, IAutomationEnvironment environment)
    {
        Text(token, 2048);
        // Resolve only tokens issued by the current authorized catalog. No user-controlled company deserialization.
        var matches = Companies(environment).Where(company => Encode(company.Key) == token).Take(2).ToArray();
        if (matches.Length != 1 || matches[0].Key.ProductLine != environment.GetStatus().ProductLine) throw new AutomationFault(AutomationFailure.AccessDenied);
        return matches[0].Key;
    }
    internal static ImmutableArray<Company> Companies(IAutomationEnvironment environment)
    {
        var companies = environment.GetCompanies();
        var product = environment.GetStatus().ProductLine;
        if (companies.IsDefault || companies.Length > 1000 || companies.Any(company => company.Key.ProductLine != product
            || company.DisplayName.Length > 256 || Encode(company.Key).Length > 2048)
            || companies.Select(company => company.Key).Distinct().Count() != companies.Length)
            throw new AutomationFault(AutomationFailure.ProviderUnavailable);
        return companies;
    }
    internal static string Context(string? json)
    {
        if (string.IsNullOrEmpty(json)) return "";
        if (json.Length > 8192) throw new AutomationFault(AutomationFailure.InvalidArgument);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4, AllowDuplicateProperties = false });
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name is not ("companyKey" or "callId") || property.Value.ValueKind != JsonValueKind.String) throw new JsonException();
                Text(property.Value.GetString(), 2048);
                if (property.Name == "callId") Id(property.Value.GetString()!);
            }
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch (JsonException) { throw new AutomationFault(AutomationFailure.InvalidArgument); }
    }
}
