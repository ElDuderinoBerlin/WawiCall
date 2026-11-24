@echo off
setlocal
pwsh -NoProfile -File "%~dp0bootstrap.ps1" -Update %*
exit /b %errorlevel%
