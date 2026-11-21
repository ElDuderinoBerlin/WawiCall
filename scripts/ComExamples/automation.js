// Run with cscript.exe //nologo automation.js Pro|FinOffice action [arguments].
var args = WScript.Arguments;
var edition = args.length > 0 ? String(args.Item(0)) : "";
var action = args.length > 1 ? String(args.Item(1)).toLowerCase() : "";
var progId = edition == "Pro" ? "WawiCallPro.Pro.Application.1"
    : edition == "FinOffice" ? "WawiCallPro.FinOffice.Application.1" : "";
var lengths = {status:2, companies:2, show:2, connect:2, customer:4, supplier:4, dial:5, hangup:3, call:3};
if (!progId || !lengths[action] || args.length != lengths[action]) {
    WScript.StdErr.WriteLine("automation.js Pro|FinOffice status|companies|show|connect|customer|supplier|dial|hangup|call [Argumente laut Anleitung]");
    WScript.Quit(2);
}
function initialize(app) {
    var until = new Date().getTime() + 40000;
    while (new Date().getTime() < until) {
        var state = app.GetStatus();
        if (state.State != "Starting") return;
        state = null; CollectGarbage(); WScript.Sleep(100);
    }
    throw new Error("Anwendungsstart dauert noch an. Status pr\u00fcfen.");
}
function completed(app, id) {
    var until = new Date().getTime() + 40000;
    WScript.Echo("Auftrag: " + id);
    while (new Date().getTime() < until) {
        var result = app.GetOperation(id);
        if (result.State == "Succeeded" || result.State == "Failed" || result.State == "Canceled") return result;
        result = null; CollectGarbage(); WScript.Sleep(100);
    }
    throw new Error("Auftrag noch offen: " + id + ". Vor einem erneuten W\u00e4hlen den vorhandenen Auftrag pr\u00fcfen.");
}
var app = null, status = null, operation = null, call = null, exitCode = 0;
try {
    app = new ActiveXObject(progId);
    status = app.GetStatus();
    if (status.ProductLine != edition) throw new Error("Die COM-Edition stimmt nicht \u00fcberein.");
    if (action != "status") initialize(app);
    var id = "";
    switch (action) {
        case "status": WScript.Echo(status.ProductLine + " / " + status.State + " / SIP: " + status.SipState + " / sichtbar: " + status.IsVisible); break;
        case "companies": WScript.Echo(app.GetCompaniesJson()); break;
        case "call": call = app.GetCall(String(args.Item(2))); WScript.Echo(call.Id + " / " + call.State); break;
        case "show": id = app.Show(); break;
        case "connect": id = app.ConnectTelephony(); break;
        case "customer": id = app.ShowCustomer(String(args.Item(2)), String(args.Item(3))); break;
        case "supplier": id = app.ShowSupplier(String(args.Item(2)), String(args.Item(3))); break;
        case "dial": id = app.Dial(String(args.Item(3)), String(args.Item(2)), String(args.Item(4))); break;
        case "hangup": id = app.HangUp(String(args.Item(2))); break;
    }
    if (id) {
        operation = completed(app, id);
        WScript.Echo(operation.State + (operation.ErrorCode ? " / " + operation.ErrorCode + " / " + operation.Message : ""));
        if (operation.CallId) WScript.Echo("Gespr\u00e4ch: " + operation.CallId);
        if (operation.State != "Succeeded") exitCode = 1;
    }
} catch (error) {
    exitCode = 1;
    var code = error.number ? ("00000000" + (error.number >>> 0).toString(16)).slice(-8) : "00000000";
    WScript.StdErr.WriteLine("0x" + code + " / " + (error.description || error.message));
} finally {
    call = null; operation = null; status = null; app = null;
    CollectGarbage(); // Releases this client's COM references; never forces GC in the app server.
}
WScript.Quit(exitCode);
