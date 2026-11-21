using WawiCallPro.Automation;

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