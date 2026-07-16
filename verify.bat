@echo off
setlocal EnableExtensions

powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify.ps1" %*
exit /b %ERRORLEVEL%
