@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0"
cd /d "%ROOT%"

rem ---- Odczyt wersji z Directory.Build.props ----
for /f "tokens=*" %%A in ('findstr "<Version>" Directory.Build.props') do (
    set "LINE=%%A"
)
set "VERSION=!LINE:*<Version>=!"
set "VERSION=!VERSION:</Version>=!"
set "VERSION=!VERSION: =!"
if "%VERSION%"=="" set "VERSION=0.1.0"

rem ---- Parametr: beta / release (domyslnie release) ----
set "FLAVOR=%~1"
if "%FLAVOR%"=="" set "FLAVOR=release"
if /i "%FLAVOR%"=="beta" (
    set "SUFFIX=-beta"
) else (
    set "FLAVOR=release"
    set "SUFFIX="
)

set "PROJECT=src\MudClient.App\MudClient.App.csproj"
set "BASE_OUTDIR=%ROOT%publish\win-x64"
set "OUTDIR=%BASE_OUTDIR%\%FLAVOR%"
set "APP_NAME=KillerMudClient-%VERSION%%SUFFIX%"

if not exist "%PROJECT%" (
    echo ERROR: Project not found at %PROJECT%
    exit /b 1
)

echo ============================================================
echo  MudClient.App  %VERSION%  ^(%FLAVOR%^)
echo  Self-contained win-x64, single-file
echo ============================================================
echo.
echo Project : %PROJECT%
echo Output  : %OUTDIR%
echo App     : %APP_NAME%.exe
echo.

dotnet publish "%PROJECT%" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output "%OUTDIR%" ^
    /p:PublishSingleFile=true ^
    /p:IncludeAllContentForSelfExtract=true ^
    /p:DebugType=None ^
    /p:DebugSymbols=false ^
    /p:NativeDebugSymbols=false ^
    /p:Version=%VERSION%

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: dotnet publish failed with exit code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

rem ---- Zmiana nazwy pliku na wersjonowana ----
if exist "%OUTDIR%\MudClient.App.exe" (
    ren "%OUTDIR%\MudClient.App.exe" "%APP_NAME%.exe"
)

echo.
echo ============================================================
echo  Publish successful.
echo  Executable: %OUTDIR%\%APP_NAME%.exe
echo ============================================================
echo.
echo  Uzycie: publish.bat [beta^|release]
echo.

endlocal
