using WawiCallPro.Automation;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;

namespace WawiCallPro.App.Hosting;

internal sealed class AppAutomationPresentation(AppAutomationState state) : IAutomationViewHost
{
    public Task ShowAsync(PresentationRequest request, CancellationToken cancellationToken)
    {
        if (request.ViewId != "home") throw new AutomationFault(AutomationFailure.NotSupported);
        return state.ShowRequested?.Invoke(cancellationToken) ?? throw new AutomationFault(AutomationFailure.NotReady);
    }
}

/// <summary>Explicit phase boundary: real views/party and telephony adapters are connected in phases 4 and 5.</summary>
internal sealed class PendingAutomationAdapters : IPartySearch, IAutomationTelephony
{
    public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default) => throw new AutomationFault(AutomationFailure.NotSupported);
    public Task ConnectAsync(CancellationToken cancellationToken) => throw new AutomationFault(AutomationFailure.NotSupported);
    public Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken cancellationToken) => throw new AutomationFault(AutomationFailure.NotSupported);
    public Task HangUpAsync(Guid callId, CancellationToken cancellationToken) => throw new AutomationFault(AutomationFailure.NotSupported);
    public AutomationCall GetCall(Guid callId) => throw new AutomationFault(AutomationFailure.NotSupported);
}
