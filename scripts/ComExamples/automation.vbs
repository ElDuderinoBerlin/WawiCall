Option Explicit
' cscript.exe //nologo automation.vbs Pro|FinOffice smoke|status|views|show|view|companies|customer|supplier|connect|dial|hangup|call [arguments]
' Default smoke only reads status/views. Only explicit actions open windows or perform telephony.
Dim args, edition, action, progId, app, status, operation, callInfo, id, exitCode, errorCode, i, ready, count
Set args = WScript.Arguments
Set app = Nothing : Set status = Nothing : Set operation = Nothing : Set callInfo = Nothing
exitCode = 0
If args.Count < 1 Then Usage
edition = CStr(args(0))
If edition = "Pro" Then
    progId = "WawiCallPro.Pro.Application.1"
ElseIf edition = "FinOffice" Then
    progId = "WawiCallPro.FinOffice.Application.1"
Else
    Usage
End If
action = "smoke"
If args.Count > 1 Then action = LCase(CStr(args(1)))
count = args.Count
Select Case action
    Case "smoke": If count > 2 Then Usage
    Case "status", "views", "companies", "show", "connect": If count <> 2 Then Usage
    Case "view": If count < 3 Or count > 4 Then Usage
    Case "customer", "supplier": If count <> 4 Then Usage
    Case "dial": If count <> 5 Then Usage
    Case "hangup", "call": If count <> 3 Then Usage
    Case Else: Usage
End Select
For i = 0 To count - 1
    If Len(CStr(args(i))) > 2048 Or InStr(CStr(args(i)), vbCr) > 0 Or InStr(CStr(args(i)), vbLf) > 0 Or InStr(CStr(args(i)), Chr(0)) > 0 Then Usage
Next
On Error Resume Next
Set app = CreateObject(progId)
CheckError "activation"
Set status = app.GetStatus()
CheckError "status"
If status.ProductLine <> edition Then Fail "EditionMismatch", 1
If action <> "status" Then
    ready = False
    For i = 1 To 400
        If status.State <> "Starting" Then ready = True : Exit For
        Set status = Nothing
        WScript.Sleep 100
        Set status = app.GetStatus()
        CheckError "startup-status"
    Next
    If Not ready Then Fail "StartupTimeout", 1
    If status.State <> "Ready" Then WScript.Echo "State=" & status.State : Fail "NotReady", 1
End If
id = ""
Select Case action
    Case "smoke", "status"
        WScript.Echo "ProductLine=" & status.ProductLine & "; State=" & status.State & "; SIP=" & status.SipState & "; Visible=" & status.IsVisible
        If action = "smoke" Then WScript.Echo app.GetViewsJson()
    Case "views": WScript.Echo app.GetViewsJson()
    Case "companies": WScript.Echo app.GetCompaniesJson()
    Case "show": id = app.Show()
    Case "view"
        Dim context : context = ""
        If count = 4 Then context = CStr(args(3))
        If Left(context, 1) = "@" Then
            Dim fileSystem, contextFile
            Set fileSystem = CreateObject("Scripting.FileSystemObject")
            Set contextFile = fileSystem.OpenTextFile(Mid(context, 2), 1, False, -1)
            CheckError "context-open"
            context = contextFile.Read(16385)
            contextFile.Close : Set contextFile = Nothing : Set fileSystem = Nothing
            CheckError "context-read"
        End If
        If Len(context) > 16384 Then Fail "ContextTooLarge", 2
        id = app.ShowView(CStr(args(2)), context)
    Case "customer": id = app.ShowCustomer(CStr(args(2)), CStr(args(3)))
    Case "supplier": id = app.ShowSupplier(CStr(args(2)), CStr(args(3)))
    Case "connect": id = app.ConnectTelephony()
    Case "dial": id = app.Dial(CStr(args(3)), CStr(args(2)), CStr(args(4)))
    Case "hangup": id = app.HangUp(CStr(args(2)))
    Case "call"
        Set callInfo = app.GetCall(CStr(args(2)))
        CheckError "call"
        WScript.Echo "Call=" & callInfo.Id & "; State=" & callInfo.State
End Select
CheckError "invoke"
If Len(id) > 0 Then
    WScript.Echo "Operation=" & id
    ready = False
    For i = 1 To 400
        Set operation = app.GetOperation(id)
        CheckError "operation-status"
        If operation.State = "Succeeded" Or operation.State = "Failed" Or operation.State = "Canceled" Then ready = True : Exit For
        Set operation = Nothing
        WScript.Sleep 100
    Next
    If Not ready Then Fail "OperationTimeout (do not repeat dial; inspect the existing Operation ID)", 1
    WScript.Echo "Result=" & operation.State & "; ErrorCode=" & operation.ErrorCode
    If Len(operation.CallId) > 0 Then WScript.Echo "Call=" & operation.CallId
    If operation.State <> "Succeeded" Then exitCode = 1
End If
CheckError "result"
ReleaseObjects
WScript.Quit exitCode

Sub Usage
    WScript.StdErr.WriteLine "automation.vbs Pro|FinOffice [smoke|status|views|show|view|companies|customer|supplier|connect|dial|hangup|call] [arguments according to COM-Automation-Anleitung.md]"
    WScript.Quit 2
End Sub
Sub CheckError(stage)
    If Err.Number <> 0 Then
        errorCode = Err.Number : Err.Clear
        Fail stage & ": HRESULT=0x" & Right("00000000" & Hex(errorCode), 8), 1
    End If
End Sub
Sub Fail(message, code)
    WScript.StdErr.WriteLine message
    ReleaseObjects
    WScript.Quit code
End Sub
Sub ReleaseObjects
    Set callInfo = Nothing : Set operation = Nothing : Set status = Nothing : Set app = Nothing
End Sub
