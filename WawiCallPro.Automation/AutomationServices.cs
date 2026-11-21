using System.Collections.Immutable;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Automation;

public enum AutomationAccess { Presentation, Business, Telephony }
public sealed record AutomationStatus(LexwareProductLine ProductLine, string State, bool IsVisible, string SipState, string ErrorCode = "");
public sealed record AutomationView(string Id, string Caption)
{
    public string Context { get; init; } = "";
}
public sealed record AutomationCall(Guid Id, string State);
public sealed record PresentationRequest(string ViewId, CompanyKey? Company = null, PartyKey? Party = null, string? Number = null, string ContextJson = "")
{
    public bool ActivateOnly { get; init; }
}

/// <summary>Host-owned detached catalogs and gates. Reads use already loaded snapshots, never synchronous source IO.</summary>
public interface IAutomationEnvironment
{
    AutomationStatus GetStatus();
    ImmutableArray<Company> GetCompanies();
    ImmutableArray<AutomationView> GetViews();
    void RequireAccess(AutomationAccess access, CompanyKey? company = null);
}

/// <summary>Presentation adapter supplied by the UI host. It must preserve ownership, modal dialogs and dirty drafts.</summary>
public interface IAutomationPresentation
{
    Task ShowAsync(PresentationRequest request, CancellationToken cancellationToken);
}

/// <summary>Telephony adapter supplied by the host; applies the same readiness and single-call rules as its UI.</summary>
public interface IAutomationTelephony
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken cancellationToken);
    Task HangUpAsync(Guid callId, CancellationToken cancellationToken);
    AutomationCall GetCall(Guid callId);
}

/// <summary>Bounded in-memory requests. Completed entries and their deduplication keys remain until Retention expires.</summary>
public sealed record AutomationOptions
{
    public int MaximumOperations { get; init; } = 128;
    public TimeSpan Retention { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(30);
    internal void Validate()
    {
        if (MaximumOperations is < 1 or > 4096 || Retention <= TimeSpan.Zero || Retention > TimeSpan.FromDays(1)
            || CommandTimeout <= TimeSpan.Zero || CommandTimeout > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(AutomationOptions));
    }
}

public enum AutomationFailure
{
    InvalidArgument = 0, NotFound = 1, Ambiguous = 2, AccessDenied = 3, NotReady = 4, NotSupported = 5,
    Conflict = 6, Busy = 7, ProviderUnavailable = 8, Timeout = 9, Stopping = 10
}

/// <summary>Only fixed safe messages cross the automation boundary. Provider exceptions are never sent to clients.</summary>
public sealed class AutomationFault(AutomationFailure failure) : Exception(MessageFor(failure))
{
    public AutomationFailure Failure { get; } = failure;
    public static string MessageFor(AutomationFailure failure) => failure switch
    {
        AutomationFailure.InvalidArgument => "Ungültige Automationsparameter.",
        AutomationFailure.NotFound => "Datensatz oder Auftrag nicht gefunden.",
        AutomationFailure.Ambiguous => "Die Datensatznummer ist im Mandanten nicht eindeutig.",
        AutomationFailure.AccessDenied => "Zugriff auf diese Funktion oder diesen Mandanten ist gesperrt.",
        AutomationFailure.NotReady => "Die Anwendung oder Verbindung ist noch nicht bereit.",
        AutomationFailure.NotSupported => "Diese Ansicht oder Funktion wird nicht angeboten.",
        AutomationFailure.Conflict => "Die Request-ID gehört zu einem anderen Auftrag.",
        AutomationFailure.Busy => "Die begrenzte Auftragsverwaltung ist ausgelastet.",
        AutomationFailure.Timeout => "Die Bearbeitungsfrist ist abgelaufen. Den vorhandenen Auftrag prüfen, bevor erneut gewählt wird.",
        AutomationFailure.Stopping => "Die Anwendung wird beendet.",
        _ => "Die Datenquelle oder Funktion ist derzeit nicht verfügbar."
    };
}
