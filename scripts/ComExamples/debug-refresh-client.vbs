Option Explicit

Dim files, app, status, prefix, started, activated, observed, result
Set files = CreateObject("Scripting.FileSystemObject")
prefix = WScript.Arguments(1)

started = Timer
Set app = CreateObject(WScript.Arguments(0))
activated = Timer
Do
    Set status = app.GetStatus()
    observed = Timer
    If status.State <> "Starting" Or Elapsed(started, observed) >= 45000 Then Exit Do
    WScript.Sleep 10
Loop

Set result = files.CreateTextFile(prefix & "pending", True)
result.WriteLine Elapsed(started, activated)
result.WriteLine Elapsed(started, observed)
result.WriteLine status.State
result.WriteLine status.IsVisible
result.Close
files.MoveFile prefix & "pending", prefix & "status"

Do Until files.FileExists(prefix & "release") Or Elapsed(started, Timer) >= 180000
    If files.FileExists(prefix & "ping") And Not files.FileExists(prefix & "pong") Then
        Set status = app.GetStatus()
        Set result = files.CreateTextFile(prefix & "pending", True)
        result.WriteLine status.State
        result.Close
        files.MoveFile prefix & "pending", prefix & "pong"
    End If
    WScript.Sleep 20
Loop

Function Elapsed(fromTime, toTime)
    If toTime < fromTime Then toTime = toTime + 86400
    Elapsed = CLng((toTime - fromTime) * 1000)
End Function
