// Windowless native clients. CollectGarbage affects this JScript process only, never the app server.
var fs = new ActiveXObject("Scripting.FileSystemObject");
var prefix = WScript.Arguments.Item(1);
function quote(value) { return '"' + String(value).replace(/\\/g, "\\\\").replace(/"/g, '\\"').replace(/\r/g, "\\r").replace(/\n/g, "\\n") + '"'; }
function write(name, text) { var file = fs.CreateTextFile(prefix + name, true, true); file.Write(text); file.Close(); }
try {
    var app = new ActiveXObject(WScript.Arguments.Item(0));
    var status;
    var deadline = new Date().getTime() + 45000;
    do {
        status = app.GetStatus();
        if (status.State != "Starting") break;
        status = null; CollectGarbage(); WScript.Sleep(30);
    } while (new Date().getTime() < deadline);
    write("ready.json", '{"State":' + quote(status.State) + '}');
    deadline = new Date().getTime() + 210000;
    var dropped = false;
    while (!fs.FileExists(prefix + "release") && new Date().getTime() < deadline) {
        if (!dropped && fs.FileExists(prefix + "drop-root")) {
            app = null; CollectGarbage(); dropped = true;
            write("dropped.json", '{"Released":true}');
        }
        if (fs.FileExists(prefix + "read-child") && !fs.FileExists(prefix + "child.json"))
            write("child.json", '{"State":' + quote(status.State) + '}');
        WScript.Sleep(50);
    }
    app = null; status = null; CollectGarbage();
} catch (error) {
    write("failure.json", '{"Failure":' + quote(error.description || error.message) + ',"Number":' + (error.number || 0) + '}');
    WScript.Quit(1);
}
