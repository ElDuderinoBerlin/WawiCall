param([long]$WindowHandle, [int]$OwnerProcessId)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$element = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$WindowHandle)
if ($element.Current.ProcessId -ne $OwnerProcessId -or !$element.Current.Name.StartsWith('WawiCallPro')) {
    throw 'The exact isolated application window was not found.'
}
$element.SetFocus()
