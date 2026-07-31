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
set "PACKAGE_ID=pl.killermud.client"
set "ARTIFACTS=%CD%\.artifacts\android-run"

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
echo [1/5] Zimny start widocznego emulatora...
"%ADB%" start-server >nul

rem Quick Boot potrafi przywrocic zawieszony SystemUI/GPU. Taki emulator pokazuje
rem czarne okno aplikacji albo przechwytuje focus pol tekstowych. Kazdy run zaczyna
rem wiec od zimnego startu AVD, ale nie czysci danych samego emulatora.
"%ADB%" -e get-state >nul 2>nul
if not errorlevel 1 (
  echo Zamykanie poprzedniej instancji emulatora...
  "%ADB%" -e emu kill >nul 2>nul
  for /l %%I in (1,1,30) do (
    "%ADB%" -e get-state >nul 2>nul
    if errorlevel 1 goto :emulator_stopped
    powershell -NoLogo -NoProfile -Command "Start-Sleep -Seconds 1"
  )
  echo ERROR: Poprzedni emulator nie zamknal sie w ciagu 30 sekund.
  goto :fail
)

:emulator_stopped
start "Android Emulator - %AVD_NAME%" "%EMULATOR%" -avd "%AVD_NAME%" -no-snapshot-load

set "DEVICE_READY="
for /l %%I in (1,1,120) do (
  "%ADB%" -e get-state 2>nul | findstr /x /c:"device" >nul
  if not errorlevel 1 (
    set "DEVICE_READY=1"
    goto :device_ready
  )
  powershell -NoLogo -NoProfile -Command "Start-Sleep -Seconds 1"
)

:device_ready
if not defined DEVICE_READY (
  echo ERROR: Emulator nie pojawil sie w adb w ciagu 120 sekund.
  goto :fail
)

echo Oczekiwanie na pelne uruchomienie Androida...
set "BOOT_READY="
for /l %%I in (1,1,180) do (
  "%ADB%" -e shell getprop sys.boot_completed 2>nul | findstr /x /c:"1" >nul
  if not errorlevel 1 (
    "%ADB%" -e shell getprop init.svc.bootanim 2>nul | findstr /x /c:"stopped" >nul
    if not errorlevel 1 (
      set "BOOT_READY=1"
      goto :boot_ready
    )
  )
  powershell -NoLogo -NoProfile -Command "Start-Sleep -Seconds 1"
)

:boot_ready
if not defined BOOT_READY (
  echo ERROR: Android nie zakonczyl startu w ciagu 180 sekund.
  goto :fail
)

echo.
echo [2/5] Usuwanie poprzedniej instalacji...
"%ADB%" -e shell pm path "%PACKAGE_ID%" 2>nul | findstr /b /c:"package:" >nul
if not errorlevel 1 (
  "%ADB%" -e uninstall "%PACKAGE_ID%"
  if errorlevel 1 (
    echo ERROR: Nie udalo sie usunac poprzedniej instalacji.
    goto :fail
  )
) else (
  echo Aplikacja nie byla jeszcze zainstalowana.
)

set "ANDROID_ABI="
for /f "usebackq delims=" %%A in (`"%ADB%" -e shell getprop ro.product.cpu.abi 2^>nul`) do set "ANDROID_ABI=%%A"
if /i "%ANDROID_ABI%"=="x86_64" (
  set "ANDROID_RID=android-x64"
) else if /i "%ANDROID_ABI%"=="arm64-v8a" (
  set "ANDROID_RID=android-arm64"
) else (
  echo ERROR: Nieobslugiwana architektura emulatora: %ANDROID_ABI%
  goto :fail
)

echo.
echo [3/5] Szybkie budowanie i instalowanie dla %ANDROID_ABI%...
dotnet build "%PROJECT%" ^
  -t:Install ^
  -c Debug ^
  -r "%ANDROID_RID%" ^
  -m:1 ^
  --artifacts-path "%ARTIFACTS%" ^
  -p:EmbedAssembliesIntoApk=false ^
  -p:AdbTarget=-e ^
  -p:AndroidSdkDirectory="%ANDROID_SDK%" ^
  -p:JavaSdkDirectory="%JAVA_SDK%"
if errorlevel 1 (
  echo.
  echo ERROR: Build lub instalacja Androida nie powiodly sie.
  goto :fail
)

echo.
echo [4/5] Uruchamianie KillerMudClient...
"%ADB%" -e shell am force-stop "%PACKAGE_ID%" >nul
"%ADB%" -e shell monkey -p "%PACKAGE_ID%" -c android.intent.category.LAUNCHER 1 >nul
if errorlevel 1 (
  echo ERROR: Nie udalo sie uruchomic aplikacji.
  goto :fail
)

echo.
echo [5/5] Sprawdzanie okna aplikacji...
set "APP_READY="
for /l %%I in (1,1,30) do (
  "%ADB%" -e shell dumpsys window 2>nul | findstr /i /c:"mCurrentFocus" | findstr /i /c:"%PACKAGE_ID%" >nul
  if not errorlevel 1 (
    set "APP_READY=1"
    goto :app_ready
  )
  powershell -NoLogo -NoProfile -Command "Start-Sleep -Seconds 1"
)

:app_ready
if not defined APP_READY (
  echo ERROR: Okno aplikacji nie przejelo focusu w ciagu 30 sekund.
  echo Sprawdz emulator i logcat; skrypt nie uznaje czarnego ekranu za udany start.
  goto :fail
)

echo.
echo Gotowe. Emulator i KillerMudClient sa uruchomione.
echo.
if not "%KMC_NO_PAUSE%"=="1" pause
popd
exit /b 0

:fail
echo.
echo Operacja przerwana.
echo.
if not "%KMC_NO_PAUSE%"=="1" pause
popd
exit /b 1
