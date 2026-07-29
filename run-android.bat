@echo off
setlocal EnableExtensions

pushd "%~dp0"
title KillerMudClient Android

set "AVD_NAME=%~1"
if "%AVD_NAME%"=="" set "AVD_NAME=Pixel_8"

set "ANDROID_SDK=%ANDROID_SDK_ROOT%"
if "%ANDROID_SDK%"=="" set "ANDROID_SDK=%ANDROID_HOME%"
if "%ANDROID_SDK%"=="" set "ANDROID_SDK=%LOCALAPPDATA%\Android\Sdk"

set "JAVA_SDK=%JAVA_HOME%"
if not exist "%JAVA_SDK%\bin\java.exe" set "JAVA_SDK=%LOCALAPPDATA%\Android\Jdk"
if not exist "%JAVA_SDK%\bin\java.exe" set "JAVA_SDK=%ProgramFiles%\Android\Android Studio\jbr"

set "ADB=%ANDROID_SDK%\platform-tools\adb.exe"
set "EMULATOR=%ANDROID_SDK%\emulator\emulator.exe"
set "PROJECT=src\MudClient.Android\MudClient.Android.csproj"
set "ARTIFACTS=%CD%\.artifacts\android-run"
set "APK=%ARTIFACTS%\bin\MudClient.Android\debug\pl.killermud.client-Signed.apk"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: Nie znaleziono dotnet w PATH.
  goto :fail
)

if not exist "%ADB%" (
  echo ERROR: Nie znaleziono adb:
  echo   %ADB%
  goto :fail
)

if not exist "%EMULATOR%" (
  echo ERROR: Nie znaleziono emulatora Android:
  echo   %EMULATOR%
  goto :fail
)

if not exist "%JAVA_SDK%\bin\java.exe" (
  echo ERROR: Nie znaleziono JDK.
  echo Ustaw JAVA_HOME albo zainstaluj JDK w Android Studio.
  goto :fail
)

if not exist "%PROJECT%" (
  echo ERROR: Nie znaleziono projektu:
  echo   %PROJECT%
  goto :fail
)

"%EMULATOR%" -list-avds | findstr /x /i /c:"%AVD_NAME%" >nul
if errorlevel 1 (
  echo ERROR: Nie znaleziono AVD "%AVD_NAME%".
  echo Dostepne emulatory:
  "%EMULATOR%" -list-avds
  goto :fail
)

echo ============================================================
echo  KillerMudClient Android
echo  Emulator: %AVD_NAME%
echo ============================================================
echo.
echo [1/4] Budowanie Core, App i Android...
dotnet build "%PROJECT%" ^
  -c Debug ^
  -m:1 ^
  --artifacts-path "%ARTIFACTS%" ^
  -p:AndroidSdkDirectory="%ANDROID_SDK%" ^
  -p:JavaSdkDirectory="%JAVA_SDK%"
if errorlevel 1 (
  echo.
  echo ERROR: Build Androida nie powiodl sie.
  goto :fail
)

if not exist "%APK%" (
  echo ERROR: Build zakonczyl sie bez oczekiwanego APK:
  echo   %APK%
  goto :fail
)

echo.
echo [2/4] Przygotowywanie widocznego emulatora...
"%ADB%" start-server >nul

rem Ukryty emulator nie moze zostac pokazany po starcie. Zamykamy tylko taki
rem proces i uruchamiamy ten sam AVD ponownie z normalnym oknem.
powershell -NoLogo -NoProfile -Command ^
  "$p = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'qemu-system*.exe' -and $_.CommandLine -match '(^|\s)-no-window(\s|$)' }; if ($p) { exit 0 } else { exit 1 }"
if not errorlevel 1 (
  echo Wykryto emulator bez okna. Ponowne uruchamianie w trybie widocznym...
  "%ADB%" -e emu kill >nul 2>nul
  timeout /t 3 /nobreak >nul
)

"%ADB%" -e get-state >nul 2>nul
if errorlevel 1 (
  start "Android Emulator - %AVD_NAME%" "%EMULATOR%" -avd "%AVD_NAME%"
)

set "DEVICE_READY="
for /l %%I in (1,1,120) do (
  "%ADB%" -e get-state 2>nul | findstr /x /c:"device" >nul
  if not errorlevel 1 (
    set "DEVICE_READY=1"
    goto :device_ready
  )
  timeout /t 1 /nobreak >nul
)

:device_ready
if not defined DEVICE_READY (
  echo ERROR: Emulator nie pojawil sie w adb w ciagu 120 sekund.
  goto :fail
)

echo Oczekiwanie na pelne uruchomienie Androida...
set "BOOT_READY="
for /l %%I in (1,1,180) do (
  for /f "usebackq delims=" %%B in (`"%ADB%" -e shell getprop sys.boot_completed 2^>nul`) do (
    if "%%B"=="1" set "BOOT_READY=1"
  )
  if defined BOOT_READY goto :boot_ready
  timeout /t 1 /nobreak >nul
)

:boot_ready
if not defined BOOT_READY (
  echo ERROR: Android nie zakonczyl startu w ciagu 180 sekund.
  goto :fail
)

echo.
echo [3/4] Instalowanie APK...
"%ADB%" -e install -r "%APK%"
if errorlevel 1 (
  echo ERROR: Instalacja APK nie powiodla sie.
  goto :fail
)

echo.
echo [4/4] Uruchamianie KillerMudClient...
"%ADB%" -e shell am force-stop pl.killermud.client >nul
"%ADB%" -e shell monkey -p pl.killermud.client -c android.intent.category.LAUNCHER 1 >nul
if errorlevel 1 (
  echo ERROR: Nie udalo sie uruchomic aplikacji.
  goto :fail
)

echo.
echo Gotowe. Emulator i KillerMudClient sa uruchomione.
echo APK:
echo   %APK%
echo.
pause
popd
exit /b 0

:fail
echo.
echo Operacja przerwana.
echo.
pause
popd
exit /b 1
