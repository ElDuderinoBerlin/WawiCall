using System.Runtime.InteropServices;

[assembly: ComVisible(false)]
[assembly: Guid(WawiCallPro.Automation.Contracts.AutomationIds.Library)]
[assembly: TypeLibVersion(1, 0)]

namespace WawiCallPro.Automation.Contracts;

/// <summary>Published version-1 identities. Breaking changes require new interface IDs and a new API version.</summary>
public static class AutomationIds
{
    public const string Library = "7155BD3A-6580-4A46-A034-1E4F55CFA047";
    public const string Application = "710E28F8-6286-4F6E-8898-C88A3F872FC1";
    public const string Status = "9A897EBA-795D-4FDD-9417-97B156287D22";
    public const string Operation = "643E9245-55FF-466E-A7E7-57B18E9D3FC6";
    public const string Call = "68DA705A-6F68-49F7-8C6A-B6CC3F29A4FE";
    public const string ProClass = "B24140A1-859F-4A8F-97F6-962668F0D20D";
    public const string FinOfficeClass = "F078BC10-F918-49FF-99C7-C5A2B44CFD5E";
    public const string ProProgId = "WawiCallPro.Pro.Application.1";
    public const string FinOfficeProgId = "WawiCallPro.FinOffice.Application.1";
}

/// <summary>Late-bound version-1 API. Mutating calls return an operation ID, never a Task or a UI object.</summary>
[ComVisible(true), Guid(AutomationIds.Application), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IApplicationV1
{
    [DispId(1)] [return: MarshalAs(UnmanagedType.IDispatch)] IStatusV1 GetStatus();
    [DispId(2)] [return: MarshalAs(UnmanagedType.BStr)] string Show();
    [DispId(3)] [return: MarshalAs(UnmanagedType.BStr)] string ShowView([MarshalAs(UnmanagedType.BStr)] string viewId, [MarshalAs(UnmanagedType.BStr)] string contextJson);
    [DispId(4)] [return: MarshalAs(UnmanagedType.BStr)] string ShowDial([MarshalAs(UnmanagedType.BStr)] string number, [MarshalAs(UnmanagedType.BStr)] string companyKey);
    [DispId(5)] [return: MarshalAs(UnmanagedType.BStr)] string ShowCustomer([MarshalAs(UnmanagedType.BStr)] string companyKey, [MarshalAs(UnmanagedType.BStr)] string customerNumber);
    [DispId(6)] [return: MarshalAs(UnmanagedType.BStr)] string ShowSupplier([MarshalAs(UnmanagedType.BStr)] string companyKey, [MarshalAs(UnmanagedType.BStr)] string supplierNumber);
    [DispId(7)] [return: MarshalAs(UnmanagedType.BStr)] string ConnectTelephony();
    [DispId(8)] [return: MarshalAs(UnmanagedType.BStr)] string Dial([MarshalAs(UnmanagedType.BStr)] string number, [MarshalAs(UnmanagedType.BStr)] string companyKey, [MarshalAs(UnmanagedType.BStr)] string requestId);
    [DispId(9)] [return: MarshalAs(UnmanagedType.IDispatch)] IOperationV1 GetOperation([MarshalAs(UnmanagedType.BStr)] string operationId);
    [DispId(10)] [return: MarshalAs(UnmanagedType.IDispatch)] ICallV1 GetCall([MarshalAs(UnmanagedType.BStr)] string callId);
    [DispId(11)] [return: MarshalAs(UnmanagedType.BStr)] string HangUp([MarshalAs(UnmanagedType.BStr)] string callId);
    [DispId(12)] [return: MarshalAs(UnmanagedType.BStr)] string GetCompaniesJson();
    [DispId(13)] [return: MarshalAs(UnmanagedType.BStr)] string GetViewsJson();
}

/// <summary>Detached safe application status; properties perform no IO.</summary>
[ComVisible(true), Guid(AutomationIds.Status), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IStatusV1
{
    [DispId(1)] string ApiVersion { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(2)] string ProductLine { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(3)] string State { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(4)] bool IsVisible { [return: MarshalAs(UnmanagedType.VariantBool)] get; }
    [DispId(5)] string SipState { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(6)] string ErrorCode { [return: MarshalAs(UnmanagedType.BStr)] get; }
}

/// <summary>Immutable observation of one operation. A successful Dial means accepted call setup, not an answered call.</summary>
[ComVisible(true), Guid(AutomationIds.Operation), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface IOperationV1
{
    [DispId(1)] string Id { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(2)] string State { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(3)] string ErrorCode { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(4)] string Message { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(5)] string CallId { [return: MarshalAs(UnmanagedType.BStr)] get; }
}

/// <summary>Safe detached call status without numbers or customer data.</summary>
[ComVisible(true), Guid(AutomationIds.Call), InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
public interface ICallV1
{
    [DispId(1)] string Id { [return: MarshalAs(UnmanagedType.BStr)] get; }
    [DispId(2)] string State { [return: MarshalAs(UnmanagedType.BStr)] get; }
}
