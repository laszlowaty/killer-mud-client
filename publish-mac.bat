@echo off
setlocal
cd /d "%~dp0"

rem ============================================================================
rem  Builds the macOS version of the client (cross-compiled from Windows).
rem  Output: publish\mac-arm64 (Apple Silicon) and publish\mac-x64 (Intel).
rem  The executable is named "macMudClient".
rem
rem  On the Mac, before the first run:
rem      chmod +x macMudClient
rem      xattr -dr com.apple.quarantine .
rem  (the binary is not signed/notarized)
rem ============================================================================

set PROJECT=src\MudClient.App\MudClient.App.csproj
set APP_NAME=macMudClient

call :publish osx-arm64 "Apple Silicon" || goto :error
call :publish osx-x64 "Intel" || goto :error

echo.
echo ============================================================
echo  Gotowe!
echo    Apple Silicon:  publish\mac-arm64\%APP_NAME%
echo    Intel:          publish\mac-x64\%APP_NAME%
echo.
echo  Na Macu przed pierwszym uruchomieniem:
echo    chmod +x %APP_NAME%
echo    xattr -dr com.apple.quarantine .
echo ============================================================
exit /b 0

:publish
setlocal
set RID=%~1
set ARCH=%RID:osx-=%
set OUTDIR=publish\mac-%ARCH%

echo.
echo === Publikacja macOS (%~2, %RID%) ===
dotnet publish %PROJECT% -c Release -r %RID% --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o %OUTDIR%
if errorlevel 1 (endlocal & exit /b 1)

if exist "%OUTDIR%\%APP_NAME%" del "%OUTDIR%\%APP_NAME%"
ren "%OUTDIR%\MudClient.App" "%APP_NAME%"
if errorlevel 1 (endlocal & exit /b 1)
endlocal
exit /b 0

:error
echo.
echo Publikacja nie powiodla sie.
exit /b 1
