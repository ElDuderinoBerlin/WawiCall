// Native late-bound UI acceptance against the real app, using an explicit Dummy profile only.
var fs = new ActiveXObject("Scripting.FileSystemObject");
var prefix = WScript.Arguments.Item(1), root = fs.GetParentFolderName(prefix), checks = [];
function quote(value) { return '"' + String(value).replace(/\\/g, "\\\\").replace(/"/g, '\\"').replace(/\r/g, "\\r").replace(/\n/g, "\\n") + '"'; }
function write(path, content) { var file=fs.CreateTextFile(path,true,true); file.Write(content); file.Close(); }
function read(path) { var file=fs.OpenTextFile(path,1); var text=file.ReadAll(); file.Close(); return text; }
function check(value,name) { if (!value) throw new Error(name); checks.push(quote(name)); }
function operation(id) {
    var limit=new Date().getTime()+40000;
    while(new Date().getTime()<limit) {
        var item=app.GetOperation(id);
        if(item.State=="Succeeded" || item.State=="Failed" || item.State=="Canceled") return item;
        WScript.Sleep(40);
    }
    throw new Error("View operation timed out.");
}
function closeDialogs() {
    write(fs.BuildPath(root,"close-dialog.request"),"");
    var limit=new Date().getTime()+15000;
    while(new Date().getTime()<limit) {
        if(!fs.FileExists(fs.BuildPath(root,"close-dialog.request")) && read(fs.BuildPath(root,"com-runtime.json")).indexOf('"OwnedViews":[]')>=0) return;
        WScript.Sleep(100);
    }
    throw new Error("Dialogs did not close.");
}
try {
    var app=new ActiveXObject(WScript.Arguments.Item(0));
    var limit=new Date().getTime()+40000;
    while(app.GetStatus().State=="Starting" && new Date().getTime()<limit) WScript.Sleep(40);
    check(app.GetStatus().State=="Ready", "App catalog initialization completed without showing UI");
    var companies=app.GetCompaniesJson(), company=/"Key":"([^"]+)"/.exec(companies)[1];
    check(company.length>0, "Authorized company token is available before Show");
    var catalog=app.GetViewsJson(), matcher=/"Id":"([^"]+)"/g, ids=[], match;
    while((match=matcher.exec(catalog))!=null) ids.push(match[1]);
    check(ids.length>=25, "Available view catalog includes settings tabs and source workspace");
    check(operation(app.ShowCustomer(company,"TEST-001")).State=="Succeeded", "ShowCustomer opens the real read-only master data workspace");
    check(operation(app.ShowSupplier(company,"TEST-001")).ErrorCode=="NotFound", "Supplier lookup cannot open a same-number customer");
    check(operation(app.ShowCustomer(company,"001")).ErrorCode=="NotFound", "Business numbers are compared exactly");
    check(operation(app.ShowCustomer(company,"TEST-001")).State=="Succeeded", "Repeated customer request reuses its existing view");
    WScript.Sleep(1200);
    var runtime=read(fs.BuildPath(root,"com-runtime.json"));
    check(runtime.indexOf('"PartyViews":1')>=0 && runtime.indexOf('"GlobalCompanySelected":false')>=0, "Customer view preserves global company selection and has one document");
    for(var index=0;index<ids.length;index++) {
        var id=ids[index];
        if(id=="call" || id.indexOf("plugin:")==0) continue;
        var context=id=="dial" ? '{"companyKey":'+quote(company)+'}' : "";
        var result=operation(app.ShowView(id,context));
        check(result.State=="Succeeded", "Open available view: "+id);
        if(id=="settings") {
            check(operation(app.ShowView("history","")).ErrorCode=="Busy", "Modal settings prevent conflicting navigation without closing edits");
            check(operation(app.ShowView("settings-mail","")).State=="Succeeded", "An existing settings dialog accepts explicit tab selection");
        }
        if(id=="database" || id=="settings" || id=="plugin" || id=="number-diagnostics" || id.indexOf("settings-")==0 || id=="caller" || id=="dial") closeDialogs();
    }
    check(operation(app.ShowView("not-an-available-view","")).ErrorCode=="NotSupported", "Unknown view is rejected explicitly");
    check(operation(app.ShowView("call","")).ErrorCode=="InvalidArgument", "Call view requires an exact call ID");
    write(prefix+"ready.json", '{"Checks":['+checks.join(",")+'],"Status":{"State":"Ready","Visible":true,"SipState":'+quote(app.GetStatus().SipState)+'}}');
    var limit=new Date().getTime()+120000;
    while(!fs.FileExists(prefix+"release") && new Date().getTime()<limit) WScript.Sleep(50);
    app=null; CollectGarbage();
} catch(error) {
    write(prefix+"failure.json", '{"Failure":'+quote(error.description||error.message)+',"Number":'+(error.number||0)+',"Checks":['+checks.join(",")+']}');
    WScript.Quit(1);
}
