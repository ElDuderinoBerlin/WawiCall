@echo off
setlocal
pwsh -NoProfile -File "%~dp0scripts\Invoke-VelopackRelease.ps1" -ProductLine FinOffice %*
exit /b %errorlevel%
