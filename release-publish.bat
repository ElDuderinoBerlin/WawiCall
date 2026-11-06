@echo off
setlocal
pwsh -NoProfile -File "%~dp0scripts\publish-release.ps1" %*
exit /b %errorlevel%
