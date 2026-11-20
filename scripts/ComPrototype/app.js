// Exercises the actual application host through an isolated, randomly registered CLSID.
var fs = new ActiveXObject("Scripting.FileSystemObject");
var prefix = WScript.Arguments.Item(1);
function quote(value) { return '"' + String(value).replace(/\\/g, "\\\\").replace(/"/g, '\\"').replace(/\r/g, "\\r").replace(/\n/g, "\\n") + '"'; }
function write(name, text) { var file = fs.CreateTextFile(prefix + name, true, true); file.Write(text); file.Close(); }
function statusText(status) { return '{"State":' + quote(status.State) + ',"Visible":' + status.IsVisible + ',"SipState":' + quote(status.SipState) + '}'; }
try {
    var app = new ActiveXObject(WScript.Arguments.Item(0));
    var initial = app.GetStatus().State;
    var deadline = new Date().getTime() + 45000;
    var status;
    do {
        status = app.GetStatus();
        if (status.State != "Starting") break;
        WScript.Sleep(30);
    } while (new Date().getTime() < deadline);
    write("ready.json", '{"Initial":' + quote(initial) + ',"Status":' + statusText(status) + '}');
    var shown = false;
    deadline = new Date().getTime() + 150000;
    while (!fs.FileExists(prefix + "release") && new Date().getTime() < deadline) {
        if (fs.FileExists(prefix + "show") && !shown) {
            var id = app.Show();
            var until = new Date().getTime() + 45000;
            var operation;
            do {
                operation = app.GetOperation(id);
                if (operation.State == "Succeeded" || operation.State == "Failed" || operation.State == "Canceled") break;
                WScript.Sleep(30);
            } while (new Date().getTime() < until);
            write("shown.json", '{"Operation":' + quote(operation.State) + ',"ErrorCode":' + quote(operation.ErrorCode) + ',"Status":' + statusText(app.GetStatus()) + '}');
            shown = true;
        }
        if (fs.FileExists(prefix + "snapshot") && !fs.FileExists(prefix + "snapshot.json"))
            write("snapshot.json", statusText(app.GetStatus()));
        WScript.Sleep(50);
    }
    app = null; status = null; CollectGarbage();
} catch (error) {
    write("failure.json", '{"Failure":' + quote(error.description || error.message) + ',"Number":' + (error.number || 0) + '}');
    WScript.Quit(1);
}
