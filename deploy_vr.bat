@echo off
:: ============================================================================
::  deploy_vr.bat  —  Build German VR app and deploy to Meta Quest via ADB
::
::  USAGE:
::    deploy_vr.bat                  <- full build + deploy
::    deploy_vr.bat --deploy-only    <- skip build, just install existing APK
::    deploy_vr.bat --build-only     <- build only, no ADB install
::
::  REQUIREMENTS:
::    • Android SDK / ADB on PATH  (or edit ADB_PATH below)
::    • Unity.exe on PATH          (or edit UNITY_EXE below)
::    • Meta Quest with Developer Mode + USB Debugging ON
::
::  OUTPUT APK:  Builds\GermanVR.apk  (relative to script folder)
:: ============================================================================
setlocal EnableDelayedExpansion

:: ── CONFIG — edit these to match your setup ─────────────────────────────────

:: Path to Unity executable (update Unity version number as needed)
set UNITY_EXE=C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe

:: Path to this Unity project
set PROJECT_PATH=%~dp0

:: Where the APK is built
set APK_PATH=%~dp0Builds\GermanVR.apk

:: Build log location
set BUILD_LOG=%~dp0Builds\build.log

:: ADB executable (just "adb" if it's on PATH, or full path like C:\adb\adb.exe)
set ADB_PATH=adb

:: Package name (must match Unity Player Settings → Package Name)
set PACKAGE=com.yourname.germanvr

:: ── PARSE ARGS ───────────────────────────────────────────────────────────────

set DO_BUILD=1
set DO_DEPLOY=1

if "%1"=="--deploy-only" set DO_BUILD=0
if "%1"=="--build-only"  set DO_DEPLOY=0

:: ── CHECK ADB ────────────────────────────────────────────────────────────────

echo.
echo [VR Deploy] Checking ADB...
%ADB_PATH% version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] ADB not found. Install Android SDK or add adb.exe to PATH.
    echo         Download: https://developer.android.com/studio/releases/platform-tools
    pause & exit /b 1
)

:: ── CHECK UNITY ──────────────────────────────────────────────────────────────

if %DO_BUILD%==1 (
    if not exist "%UNITY_EXE%" (
        echo [ERROR] Unity not found at:
        echo         %UNITY_EXE%
        echo         Edit UNITY_EXE at the top of this script.
        pause & exit /b 1
    )
)

:: ── CHECK QUEST CONNECTED ────────────────────────────────────────────────────

echo [VR Deploy] Looking for connected Meta Quest...
%ADB_PATH% devices > "%TEMP%\adb_devices.txt" 2>&1
set DEVICE_SERIAL=

for /f "skip=1 tokens=1,2" %%A in (%TEMP%\adb_devices.txt) do (
    if "%%B"=="device" (
        if "!DEVICE_SERIAL!"=="" set DEVICE_SERIAL=%%A
    )
)

if %DO_DEPLOY%==1 (
    if "!DEVICE_SERIAL!"=="" (
        echo.
        echo [ERROR] No Android device found via ADB.
        echo.
        echo  Steps to fix:
        echo   1. Enable Developer Mode on your Quest:
        echo      Meta app on phone → Device → Developer Mode ON
        echo   2. Connect Quest via USB-C cable
        echo   3. Put on the headset and click "Allow" on the USB Debugging prompt
        echo   4. Run this script again
        echo.
        pause & exit /b 1
    )
    echo [VR Deploy] Device found: !DEVICE_SERIAL!
)

:: ── BUILD ────────────────────────────────────────────────────────────────────

if %DO_BUILD%==1 (
    echo.
    echo [VR Deploy] Starting Unity build...
    echo             Project:  %PROJECT_PATH%
    echo             Output:   %APK_PATH%
    echo             Log:      %BUILD_LOG%
    echo.

    if not exist "%~dp0Builds" mkdir "%~dp0Builds"

    "%UNITY_EXE%" ^
        -batchmode ^
        -quit ^
        -projectPath "%PROJECT_PATH%" ^
        -executeMethod BuildScript.BuildAndroid ^
        -logFile "%BUILD_LOG%"

    if errorlevel 1 (
        echo.
        echo [ERROR] Unity build FAILED. Check log:
        echo         %BUILD_LOG%
        echo.
        :: Show last 30 lines of build log
        echo ── Build log (last 30 lines) ─────────────────────────────────────
        powershell -command "Get-Content '%BUILD_LOG%' -Tail 30"
        echo ──────────────────────────────────────────────────────────────────
        pause & exit /b 1
    )

    echo [VR Deploy] ✓ Build succeeded.
)

:: ── VERIFY APK ───────────────────────────────────────────────────────────────

if not exist "%APK_PATH%" (
    echo [ERROR] APK not found at:  %APK_PATH%
    if %DO_BUILD%==1 (
        echo         Build reported success but APK is missing — check log.
    ) else (
        echo         Run without --deploy-only to build first.
    )
    pause & exit /b 1
)

:: ── DEPLOY ───────────────────────────────────────────────────────────────────

if %DO_DEPLOY%==1 (
    echo.
    echo [VR Deploy] Installing APK on !DEVICE_SERIAL!...
    echo             APK: %APK_PATH%

    %ADB_PATH% -s !DEVICE_SERIAL! install -r "%APK_PATH%"

    if errorlevel 1 (
        echo.
        echo [ERROR] ADB install failed.
        echo         Make sure the Quest is awake, unlocked, and has USB Debugging allowed.
        pause & exit /b 1
    )

    echo.
    echo [VR Deploy] ✓ Install complete. Launching app...

    %ADB_PATH% -s !DEVICE_SERIAL! shell monkey -p %PACKAGE% -c android.intent.category.LAUNCHER 1 >nul 2>&1

    echo [VR Deploy] ✓ App launched — put on your headset!
    echo.
)

echo ══════════════════════════════════════════════
echo   DONE — German VR deployed to Meta Quest!
echo ══════════════════════════════════════════════
echo.
pause
endlocal
