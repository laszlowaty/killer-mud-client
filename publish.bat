@echo off
setlocal

REM Determine the repository root (directory containing this script).
set "ROOT=%~dp0"
cd /d "%ROOT%"

echo ============================================================
echo  Publishing MudClient.App (self-contained win-x64 Release)
echo ============================================================

set "PROJECT=src\MudClient.App\MudClient.App.csproj"
set "OUTDIR=%ROOT%publish\win-x64"

if not exist "%PROJECT%" (
    echo ERROR: Project not found at %PROJECT%
    exit /b 1
)

echo.
echo Project : %PROJECT%
echo Output  : %OUTDIR%
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
    /p:NativeDebugSymbols=false

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: dotnet publish failed with exit code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo.
echo ============================================================
echo  Publish successful.
echo  Executable: %OUTDIR%\MudClient.App.exe
echo ============================================================

endlocal
