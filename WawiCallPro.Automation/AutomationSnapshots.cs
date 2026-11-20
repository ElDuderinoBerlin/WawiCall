using System.Runtime.InteropServices;
using WawiCallPro.Automation.Contracts;
using WawiCallPro.Interfaces.Telephony;

[assembly: ComVisible(false)]

namespace WawiCallPro.Automation;

[ComVisible(true), ClassInterface(ClassInterfaceType.None), ComDefaultInterface(typeof(IStatusV1))]
public sealed class StatusSnapshot : IStatusV1
{
    internal StatusSnapshot(AutomationStatus status)
    {
        ProductLine = status.ProductLine.ToString();
        State = status.State is "Starting" or "Ready" or "LicenseRequired" or "ConfigurationRequired" or "Failed" or "Stopping" ? status.State : "Failed";
        IsVisible = status.IsVisible;
        SipState = Enum.TryParse<RegistrationState>(status.SipState, out var sip) && Enum.IsDefined(sip) ? sip.ToString() : "Failed";
        ErrorCode = status.ErrorCode.Length == 0 ? ""
            : Enum.TryParse<AutomationFailure>(status.ErrorCode, out var error) && Enum.IsDefined(error) ? error.ToString() : "ProviderUnavailable";
    }
    public string ApiVersion => "1.0";
    public string ProductLine { get; }
    public string State { get; }
    public bool IsVisible { get; }
    public string SipState { get; }
    public string ErrorCode { get; }
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None), ComDefaultInterface(typeof(IOperationV1))]
public sealed class OperationSnapshot : IOperationV1
{
    internal OperationSnapshot(Guid id, string state, string errorCode = "", string message = "", Guid? callId = null)
    { Id = id.ToString("D"); State = state; ErrorCode = errorCode; Message = message; CallId = callId?.ToString("D") ?? ""; }
    public string Id { get; }
    public string State { get; }
    public string ErrorCode { get; }
    public string Message { get; }
    public string CallId { get; }
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None), ComDefaultInterface(typeof(ICallV1))]
public sealed class CallSnapshot : ICallV1
{
    internal CallSnapshot(AutomationCall call)
    {
        if (!Enum.TryParse<CallState>(call.State, out var state) || !Enum.IsDefined(state)) throw new AutomationFault(AutomationFailure.ProviderUnavailable);
        Id = call.Id.ToString("D"); State = state.ToString();
    }
    public string Id { get; }
    public string State { get; }
}
