using WawiCallPro.Automation;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;

namespace WawiCallPro.App.Hosting;

internal sealed class AppAutomationPresentation(AppAutomationState state) : IAutomationViewHost
{
    public Task ShowAsync(PresentationRequest request, CancellationToken cancellationToken)
    {
        return state.PresentRequested?.Invoke(request, cancellationToken)
            ?? (request.ViewId == "home" ? state.ShowRequested?.Invoke(cancellationToken) : null)
            ?? throw new AutomationFault(AutomationFailure.NotReady);
    }
}

/// <summary>Phase-5 boundary: unsupported telephone commands never touch a SIP account.</summary>
internal sealed class PendingAutomationAdapters : IPartySearch, IAutomationTelephony
{
    public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default) => throw new AutomationFault(AutomationFailure.NotSupported);
    public Task ConnectAsync(CancellationToken cancellationToken) => throw new AutomationFault(AutomationFailure.NotSupported);
    public Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken cancellationToken) => throw new AutomationFault(AutomationFailure.NotSupported);
    public Task HangUpAsync(Guid callId, CancellationToken cancellationToken) => throw new AutomationFault(AutomationFailure.NotSupported);
    public AutomationCall GetCall(Guid callId) => throw new AutomationFault(AutomationFailure.NotSupported);
}
