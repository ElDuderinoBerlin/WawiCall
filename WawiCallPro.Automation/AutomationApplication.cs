using System.Runtime.InteropServices;
using System.Text.Json;
using WawiCallPro.Automation.Contracts;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Automation;

/// <summary>Versioned COM adapter; constructor injection is supplied by the host's class factory.</summary>
[ComVisible(false)]
public abstract class AutomationApplication(AutomationOperations operations, IAutomationEnvironment environment) : IApplicationV1, ISupportErrorInfo
{
    public IStatusV1 GetStatus() => Boundary<IStatusV1>(() => new StatusSnapshot(environment.GetStatus()));
    public string Show() => Submit(new("show"), AutomationAccess.Presentation);
    public string ShowView(string viewId, string contextJson) => Boundary(() => Submit(new("view", Value: AutomationInput.Text(viewId, 128), Context: AutomationInput.Context(contextJson)), AutomationAccess.Presentation));
    public string ShowDial(string number, string companyKey) => WithCompany("showdial", companyKey, number, AutomationAccess.Business);
    public string ShowCustomer(string companyKey, string customerNumber) => WithCompany("customer", companyKey, customerNumber, AutomationAccess.Business);
    public string ShowSupplier(string companyKey, string supplierNumber) => WithCompany("supplier", companyKey, supplierNumber, AutomationAccess.Business);
    public string ConnectTelephony() => Submit(new("connect"), AutomationAccess.Telephony);
    public string Dial(string number, string companyKey, string requestId) => Boundary(() =>
    {
        environment.RequireAccess(AutomationAccess.Telephony);
        return Submit(new("dial", AutomationInput.Company(companyKey, environment), AutomationInput.Text(number, 200), RequestId: AutomationInput.Id(requestId)), AutomationAccess.Telephony);
    });
    public IOperationV1 GetOperation(string operationId) => Boundary<IOperationV1>(() => operations.Get(operationId));
    public abstract ICallV1 GetCall(string callId);
    public string HangUp(string callId) => Boundary(() => Submit(new("hangup", Value: AutomationInput.Id(callId).ToString("D")), AutomationAccess.Telephony));
    public string GetCompaniesJson() => Boundary(() =>
    {
        environment.RequireAccess(AutomationAccess.Business);
        var companies = AutomationInput.Companies(environment);
        return CatalogJson(companies.Select(company => new { Key = AutomationInput.Encode(company.Key), Name = company.DisplayName }));
    });
    public string GetViewsJson() => Boundary(() =>
    {
        environment.RequireAccess(AutomationAccess.Presentation);
        var views = environment.GetViews();
        if (views.IsDefault || views.Length > 256 || views.Any(view => string.IsNullOrWhiteSpace(view.Id) || view.Id.Length > 128 || view.Caption.Length > 256)
            || views.Select(view => view.Id).Distinct(StringComparer.Ordinal).Count() != views.Length) throw new AutomationFault(AutomationFailure.ProviderUnavailable);
        return CatalogJson(views);
    });
    public int InterfaceSupportsErrorInfo(ref Guid riid) => riid == typeof(IApplicationV1).GUID ? 0 : 1;

    private string WithCompany(string kind, string key, string number, AutomationAccess access) => Boundary(() =>
    {
        environment.RequireAccess(access);
        return Submit(new(kind, AutomationInput.Company(key, environment), AutomationInput.Text(number, 200)), access);
    });
    private string Submit(AutomationCommand command, AutomationAccess access) => Boundary(() =>
    { environment.RequireAccess(access, command.Company); return operations.Submit(command); });
    private static string CatalogJson<T>(T items)
    {
        var json = JsonSerializer.Serialize(new { SchemaVersion = 1, Items = items });
        if (json.Length > 262144) throw new AutomationFault(AutomationFailure.Busy);
        return json;
    }
    protected static T Boundary<T>(Func<T> action)
    {
        try { return action(); }
        catch (SafeComException) { throw; }
        catch (AutomationFault fault)
        {
            var code = fault.Failure switch { AutomationFailure.InvalidArgument => unchecked((int)0x80070057), AutomationFailure.AccessDenied => unchecked((int)0x80070005), _ => unchecked((int)0x8004A100) + (int)fault.Failure };
            throw new SafeComException(fault.Message, code);
        }
        catch (Exception) { throw new SafeComException(AutomationFault.MessageFor(AutomationFailure.ProviderUnavailable), unchecked((int)0x8004A108)); }
    }
    private sealed class SafeComException(string message, int code) : COMException(message, code);
}

[ComVisible(true), Guid(AutomationIds.ProClass), ProgId(AutomationIds.ProProgId), ClassInterface(ClassInterfaceType.None), ComDefaultInterface(typeof(IApplicationV1))]
public sealed class ProApplication : AutomationApplication
{
    private readonly IAutomationTelephony _telephony;
    private readonly IAutomationEnvironment _environment;
    public ProApplication(AutomationOperations operations, IAutomationEnvironment environment, IAutomationTelephony telephony) : base(operations, environment)
    { if (environment.GetStatus().ProductLine != LexwareProductLine.Pro) throw new ArgumentException("Wrong product line."); _telephony = telephony; _environment = environment; }
    public override ICallV1 GetCall(string callId) => Boundary<ICallV1>(() => { _environment.RequireAccess(AutomationAccess.Telephony); var id = AutomationInput.Id(callId); var call = _telephony.GetCall(id); if (call.Id != id) throw new AutomationFault(AutomationFailure.ProviderUnavailable); return new CallSnapshot(call); });
}

[ComVisible(true), Guid(AutomationIds.FinOfficeClass), ProgId(AutomationIds.FinOfficeProgId), ClassInterface(ClassInterfaceType.None), ComDefaultInterface(typeof(IApplicationV1))]
public sealed class FinOfficeApplication : AutomationApplication
{
    private readonly IAutomationTelephony _telephony;
    private readonly IAutomationEnvironment _environment;
    public FinOfficeApplication(AutomationOperations operations, IAutomationEnvironment environment, IAutomationTelephony telephony) : base(operations, environment)
    { if (environment.GetStatus().ProductLine != LexwareProductLine.FinOffice) throw new ArgumentException("Wrong product line."); _telephony = telephony; _environment = environment; }
    public override ICallV1 GetCall(string callId) => Boundary<ICallV1>(() => { _environment.RequireAccess(AutomationAccess.Telephony); var id = AutomationInput.Id(callId); var call = _telephony.GetCall(id); if (call.Id != id) throw new AutomationFault(AutomationFailure.ProviderUnavailable); return new CallSnapshot(call); });
}
