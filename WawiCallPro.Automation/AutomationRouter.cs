using System.Text.Json;
using WawiCallPro.Interfaces.Hosting;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Automation;

/// <summary>UI host endpoint; only the presentation adapter calls it, on the injected UI dispatcher.</summary>
public interface IAutomationViewHost
{
    Task ShowAsync(PresentationRequest request, CancellationToken cancellationToken);
}

public sealed class DispatchedAutomationPresentation(IUiDispatcher dispatcher, IAutomationViewHost host, IAutomationEnvironment environment) : IAutomationPresentation
{
    public async Task ShowAsync(PresentationRequest request, CancellationToken cancellationToken)
    {
        Task? work = null;
        await dispatcher.InvokeAsync(() =>
        {
            environment.RequireAccess(request.Company is null ? AutomationAccess.Presentation : AutomationAccess.Business, request.Company);
            work = host.ShowAsync(request, cancellationToken);
        }, cancellationToken).ConfigureAwait(false);
        await work!.ConfigureAwait(false);
    }
}

internal sealed class AutomationRouter(IAutomationEnvironment environment, IAutomationPresentation presentation,
    IAutomationTelephony telephony, NumberedPartyResolver parties)
{
    internal async Task<Guid?> ExecuteAsync(AutomationCommand command, CancellationToken token)
    {
        var access = command.Kind is "connect" or "dial" or "hangup" ? AutomationAccess.Telephony
            : command.Company is null ? AutomationAccess.Presentation : AutomationAccess.Business;
        environment.RequireAccess(access, command.Company);
        if (command.Kind == "connect") { await telephony.ConnectAsync(token).ConfigureAwait(false); return null; }
        if (command.Kind == "dial") return await telephony.DialAsync(command.Company!, command.Value, token).ConfigureAwait(false);
        if (command.Kind == "hangup") { await telephony.HangUpAsync(AutomationInput.Id(command.Value), token).ConfigureAwait(false); return null; }

        var request = new PresentationRequest(command.Kind == "show" ? "home" : command.Value, command.Company, ContextJson: command.Context);
        if (command.Kind == "show") request = request with { ActivateOnly = true };
        if (command.Kind is "customer" or "supplier")
        {
            var kind = command.Kind == "customer" ? PartyKind.Customer : PartyKind.Supplier;
            var key = await parties.ResolveAsync(command.Company!, kind, command.Value, token).ConfigureAwait(false);
            environment.RequireAccess(AutomationAccess.Business, key.Company);
            request = new(command.Kind, key.Company, key, Number: command.Value);
        }
        else if (command.Kind == "showdial") request = new("dial", command.Company, Number: command.Value);
        if (command.Kind == "view")
        {
            if (!environment.GetViews().Any(view => view.Id == command.Value)) throw new AutomationFault(AutomationFailure.NotSupported);
            if (command.Context.Length > 0)
            {
                using var json = JsonDocument.Parse(command.Context);
                if (json.RootElement.TryGetProperty("companyKey", out var key))
                {
                    var company = AutomationInput.Company(key.GetString()!, environment);
                    environment.RequireAccess(AutomationAccess.Business, company);
                    request = request with { Company = company };
                    var kind = json.RootElement.TryGetProperty("customerNumber", out var number) ? PartyKind.Customer
                        : json.RootElement.TryGetProperty("supplierNumber", out number) ? PartyKind.Supplier : (PartyKind?)null;
                    if (kind is { } role)
                    {
                        if (!command.Value.StartsWith("plugin:", StringComparison.Ordinal)) throw new AutomationFault(AutomationFailure.InvalidArgument);
                        var party = await parties.ResolveAsync(company, role, number.GetString()!, token).ConfigureAwait(false);
                        request = request with { Party = party, Number = number.GetString() };
                    }
                }
            }
        }
        await presentation.ShowAsync(request, token).ConfigureAwait(false);
        return null;
    }
}
