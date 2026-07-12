@echo off
setlocal

set "TOOL_DIR=%~dp0"
pushd "%TOOL_DIR%..\.."

dotnet run --project "%TOOL_DIR%MudClient.MapImageCalibrator.csproj"
set "EXIT_CODE=%ERRORLEVEL%"

popd
exit /b %EXIT_CODE%
