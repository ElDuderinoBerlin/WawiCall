namespace WawiCallPro.Interfaces.Companies;

/// <summary>An external company, independent of its optional local database ID.</summary>
public sealed record Company(CompanyKey Key, string DisplayName);
