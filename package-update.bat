@echo off
setlocal
pwsh -NoProfile -File "%~dp0update-nuget-packages.ps1" %*
exit /b %errorlevel%
