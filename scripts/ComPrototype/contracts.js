// Version-1 contract proof: no TLB registration, product configuration, real records or telephone calls.
var fs = new ActiveXObject("Scripting.FileSystemObject");
var progId = WScript.Arguments.Item(0);
var resultPath = WScript.Arguments.Item(2);
var profile = fs.GetParentFolderName(resultPath);
var checks = [];
function quote(value) { return '"' + String(value).replace(/\\/g, "\\\\").replace(/"/g, '\\"').replace(/\r/g, "\\r").replace(/\n/g, "\\n") + '"'; }
function write(value) { var file = fs.CreateTextFile(resultPath, true, true); file.Write(value); file.Close(); }
function check(value, name) { if (!value) throw new Error(name); checks.push(quote(name)); }
function read(name) { var file = fs.OpenTextFile(fs.BuildPath(profile, name), 1); var text = file.ReadAll(); file.Close(); return text; }
function completed(id) {
    var end = new Date().getTime() + 10000;
    while (new Date().getTime() < end) {
        var operation = app.GetOperation(id);
        if (operation.State == "Succeeded" || operation.State == "Failed" || operation.State == "Canceled") return operation;
        WScript.Sleep(20);
    }
    throw new Error("Contract operation timed out.");
}
function fails(action, name) {
    var failed = false;
    try { action(); } catch (error) { failed = error.number != 0 && error.description.length > 0; }
    check(failed, name);
}
try {
    var app = new ActiveXObject(progId);
    var status = app.GetStatus();
    check(status.ApiVersion == "1.0" && status.State == "Ready", "Versioned status IDispatch");
    check(status.ProductLine == "Pro" || status.ProductLine == "FinOffice", "Edition-specific class");
    var catalog = app.GetCompaniesJson();
    var matcher = /"Key":"([^"]+)"/g;
    var first = matcher.exec(catalog)[1];
    var second = matcher.exec(catalog)[1];
    check(catalog.indexOf('"SchemaVersion":1') >= 0, "Versioned company catalog");
    check(app.GetViewsJson().indexOf('"history"') >= 0, "View catalog through BSTR");
    var operation = completed(app.ShowCustomer(first, "00123"));
    check(operation.State == "Succeeded" && read("presentation.json").indexOf('"Kind":"Customer"') >= 0, "Customer number resolves correct role");
    operation = completed(app.ShowSupplier(first, "00123"));
    check(operation.State == "Succeeded" && read("presentation.json").indexOf('"Kind":"Supplier"') >= 0, "Same number resolves supplier independently");
    operation = completed(app.ShowCustomer(second, "00123"));
    check(operation.State == "Succeeded" && read("presentation.json").indexOf('"ExternalId":"customer-b"') >= 0, "Same number in another company stays isolated");
    check(completed(app.ShowCustomer(first, "123")).ErrorCode == "NotFound", "Leading zeros remain significant");
    check(completed(app.ShowCustomer(first, "\u00c4-\u6771\u4eac")).State == "Succeeded", "Unicode BSTR number reaches source unchanged");
    check(completed(app.ShowCustomer(first, "00777")).ErrorCode == "Ambiguous", "Ambiguous number is not selected arbitrarily");
    operation = completed(app.ShowCustomer(first, "00888"));
    check(operation.ErrorCode == "ProviderUnavailable" && operation.Message.indexOf("synthetic-private-secret") < 0, "Provider exception is redacted");
    fails(function() { app.ShowCustomer(first, ""); }, "Empty number reports IErrorInfo");
    fails(function() { app.ShowCustomer("foreign-company", "00123"); }, "Unknown company is denied");
    fails(function() { app.GetOperation("bad-id"); }, "Invalid GUID is rejected");
    fails(function() { app.ShowView("history", '{"secret":"no"}'); }, "Unknown context fields rejected");
    fails(function() { app.ShowView("history", '{"callId":"bad"}'); }, "Invalid call context rejected");
    check(completed(app.ShowView("unknown", "")).ErrorCode == "NotSupported", "Unknown view produces explicit failed operation");
    check(completed(app.Show()).State == "Succeeded", "Show is an asynchronous operation");
    check(status.IsVisible == false && app.GetStatus().IsVisible == true, "Detached status snapshot and VARIANT_BOOL");
    check(completed(app.ShowView("history", "")).State == "Succeeded", "View request reaches presentation adapter");
    check(completed(app.ShowDial("+4930000000", first)).State == "Succeeded" && !fs.FileExists(fs.BuildPath(profile, "dial-count.json")), "ShowDial does not dial");
    check(completed(app.ConnectTelephony()).State == "Succeeded", "Explicit connect adapter");
    var raw = progId.substr(5);
    var request = raw.substr(0, 8) + "-" + raw.substr(8, 4) + "-" + raw.substr(12, 4) + "-" + raw.substr(16, 4) + "-" + raw.substr(20);
    var id = app.Dial("+4930000000", first, request);
    check(app.Dial("+4930000000", first, request) == id, "Repeated request returns same operation");
    fails(function() { app.Dial("+4930000001", first, request); }, "Conflicting reuse of request GUID rejected");
    operation = completed(id);
    check(operation.State == "Succeeded" && operation.CallId != id && operation.CallId.length == 36, "Operation ID differs from call ID");
    check(app.GetCall(operation.CallId).State == "Dialing", "Detached call IDispatch");
    check(read("dial-count.json").indexOf('"Count":1') >= 0, "Deduplicated request has one simulated side effect");
    check(completed(app.HangUp(operation.CallId)).State == "Succeeded" && app.GetCall(operation.CallId).State == "Ended", "HangUp adapter targets exact call");
    var output = read("presentation.json");
    var factory = read("factory.json");
    check(/"Thread":(\d+)/.exec(output)[1] == /"UiThread":(\d+)/.exec(factory)[1], "Presentation runs on injected UI dispatcher");
    write('{"Checks":[' + checks.join(",") + '],"ProductLine":' + quote(status.ProductLine) + '}');
    app = null; CollectGarbage();
} catch (error) {
    write('{"Failure":' + quote(error.description || error.message) + ',"Number":' + (error.number || 0) + ',"Checks":[' + checks.join(",") + ']}');
    WScript.Quit(1);
}
