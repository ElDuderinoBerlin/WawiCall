@echo off
setlocal
chcp 65001 >nul

rem Azure Translator configuration
set "AZURE_TRANSLATOR_ENDPOINT=https://api.cognitive.microsofttranslator.com/"
set "AZURE_TRANSLATOR_REGION=germanywestcentral"
set "GIT_UPDATE_TITLE=Updates und Fixes von WawiCallPro {YEAR}"

rem Optional: Replace the placeholder with a new key.
rem For better security, leave this empty. The PowerShell script then asks for it.
set "AZURE_TRANSLATOR_KEY="
rem set "AZURE_TRANSLATOR_KEY=HIER_NEUEN_AZURE_TRANSLATOR_KEY_EINTRAGEN"

where pwsh.exe >nul 2>&1
if errorlevel 1 (
    set "POWERSHELL_EXE=powershell.exe"
) else (
    set "POWERSHELL_EXE=pwsh.exe"
)

"%POWERSHELL_EXE%" -NoProfile -ExecutionPolicy Bypass ^
    -File "%~dp0generate-git-update-list.ps1" ^
    -UseAzureTranslator ^
    -AzureTranslatorEndpoint "%AZURE_TRANSLATOR_ENDPOINT%" ^
    -AzureTranslatorRegion "%AZURE_TRANSLATOR_REGION%" ^
    -DocumentTitle "%GIT_UPDATE_TITLE%"

set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
    echo.
    echo Die Git-Update-Liste konnte nicht erstellt werden.
)

endlocal & exit /b %EXIT_CODE%
