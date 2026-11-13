param([long]$WindowHandle, [int]$OwnerProcessId)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$element = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$WindowHandle)
if ($element.Current.ProcessId -ne $OwnerProcessId -or $element.Current.Name -ne "WawiCallPro - Native tray audit $OwnerProcessId") {
    throw 'Das erwartete eigene Prüffenster wurde nicht gefunden.'
}
$element.SetFocus()
