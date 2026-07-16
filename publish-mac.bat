@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

rem ============================================================================
rem  Builds the macOS version of the client (cross-compiled from Windows).
rem  Usage:  publish-mac.bat [beta|release]
rem
rem  Output (release):  publish\mac-arm64\KillerMudClient-{version}
rem                      publish\mac-x64\KillerMudClient-{version}
rem  Output (beta):     publish\mac-arm64\KillerMudClient-{version}-beta
rem                      publish\mac-x64\KillerMudClient-{version}-beta
rem
rem  On the Mac, before the first run:
rem      chmod +x KillerMudClient-{version}
rem      xattr -dr com.apple.quarantine .
rem  (the binary is not signed/notarized)
rem ============================================================================

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

set PROJECT=src\MudClient.App\MudClient.App.csproj
set APP_NAME=KillerMudClient-%VERSION%%SUFFIX%

echo ============================================================
echo  MudClient.App  %VERSION%  ^(%FLAVOR%^)
echo  macOS cross-compile (Apple Silicon + Intel)
echo ============================================================

for %%R in (osx-arm64 osx-x64) do (
    set "RID=%%R"
    set "ARCH=!RID:osx-=!"
    set "OUTDIR=publish\mac-!ARCH!\%FLAVOR%"

    echo.
    echo === macOS ^(!ARCH!, !RID!^) ===

    rem ---- Czysty katalog wyjsciowy zapobiega pozostawieniu plikow starego release ----
    if exist "!OUTDIR!" (
        echo Cleaning: !OUTDIR!
        rmdir /s /q "!OUTDIR!"
        if exist "!OUTDIR!" (
            echo ERROR: Nie mozna wyczyscic katalogu: !OUTDIR!
            exit /b 1
        )
    )

    mkdir "!OUTDIR!"
    if errorlevel 1 (
        echo ERROR: Nie mozna utworzyc katalogu: !OUTDIR!
        exit /b 1
    )

    dotnet publish "%PROJECT%" -c Release -r "!RID!" --self-contained true ^
        -p:PublishSingleFile=true ^
        -p:IncludeNativeLibrariesForSelfExtract=true ^
        -o "!OUTDIR!" ^
        -p:Version=%VERSION%
    if errorlevel 1 (
        echo ERROR: Publikacja !RID! nie powiodla sie.
        exit /b 1
    )

    if exist "!OUTDIR!\%APP_NAME%" del "!OUTDIR!\%APP_NAME%"
    ren "!OUTDIR!\MudClient.App" "%APP_NAME%"
    if errorlevel 1 exit /b 1

    rem ---- Pozostaw tylko koncowy plik wykonywalny ----
    for %%F in ("!OUTDIR!\*") do (
        if /i not "%%~nxF"=="%APP_NAME%" del /q "%%~fF"
    )
    for /d %%D in ("!OUTDIR!\*") do rmdir /s /q "%%~fD"
)

echo.
echo ============================================================
echo  Gotowe!
echo    Apple Silicon:  publish\mac-arm64\%FLAVOR%\%APP_NAME%
echo    Intel:          publish\mac-x64\%FLAVOR%\%APP_NAME%
echo.
echo  Na Macu przed pierwszym uruchomieniem:
echo    chmod +x %APP_NAME%
echo    xattr -dr com.apple.quarantine .
echo ============================================================
exit /b 0
