@echo off
setlocal EnableExtensions

pushd "%~dp0"
title KillerMUD Klient

echo Uruchamianie Killer MUD Client...
echo.

where powershell >nul 2>nul
if errorlevel 1 (
  echo Nie znaleziono PowerShell.
  echo Upewnij sie, ze Windows PowerShell 5.1 jest dostepny w PATH.
  echo.
  pause
  popd
  exit /b 1
)

powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1"
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if "%EXIT_CODE%"=="0" (
  echo Aplikacja zakonczona poprawnie.
) else (
  echo Uruchomienie nie powiodlo sie. Kod bledu: %EXIT_CODE%
)

echo.
pause
popd
exit /b %EXIT_CODE%
