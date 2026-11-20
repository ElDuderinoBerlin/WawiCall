// Fixture client for both native Windows Script Host bitnesses. No product API or credentials.
var fs = new ActiveXObject("Scripting.FileSystemObject");
var progId = WScript.Arguments.Item(0);
var mode = WScript.Arguments.Item(1);
var resultPath = WScript.Arguments.Item(2);
var hold = parseInt(WScript.Arguments.Item(3), 10);
function quote(value) { return '"' + String(value).replace(/\\/g, "\\\\").replace(/"/g, '\\"').replace(/\r/g, "\\r").replace(/\n/g, "\\n") + '"'; }
function write(value) { var file = fs.CreateTextFile(resultPath, true, false); file.Write(value); file.Close(); }
try {
    if (WScript.Arguments.length > 4) {
        var barrier = WScript.Arguments.Item(4);
        var limit = new Date().getTime() + 15000;
        while (!fs.FileExists(barrier)) {
            if (new Date().getTime() > limit) throw new Error("Client barrier timed out.");
            WScript.Sleep(20);
        }
    }
    var app = new ActiveXObject(progId);
    var initial = app.State;
    if (mode == "ready") {
        var limit = new Date().getTime() + 15000;
        while (app.State != "Ready") {
            if (new Date().getTime() > limit) throw new Error("Host readiness timed out.");
            WScript.Sleep(50);
        }
    }
    if (mode == "user") app.SetSimulatedUserOwnership(true);
    if (mode == "call") app.SetSimulatedCall(true);
    var errorNumber = 0;
    var errorDescription = "";
    try { app.Fail(); }
    catch (error) { errorNumber = error.number; errorDescription = error.description; }
    write('{"ProcessId":' + app.ProcessId + ',"DependencyId":' + quote(app.DependencyId)
        + ',"InitialState":' + quote(initial) + ',"State":' + quote(app.State)
        + ',"ThreadId":' + app.ThreadId + ',"VisibleWindows":' + app.VisibleWindows
        + ',"ErrorNumber":' + errorNumber + ',"ErrorDescription":' + quote(errorDescription) + '}');
    if (hold > 0) WScript.Sleep(hold);
    if (mode != "abandon") app.Close();
    app = null;
    CollectGarbage();
} catch (error) {
    write('{"Failure":' + quote(error.description || error.message) + ',"Number":' + (error.number || 0) + '}');
    WScript.Quit(1);
}
