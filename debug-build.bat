@echo off
setlocal
pwsh -NoProfile -File "%~dp0scripts\build-debug.ps1" %*
exit /b %errorlevel%
