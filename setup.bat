@echo off
setlocal EnableDelayedExpansion
title AI_HDRI_Unity — Setup Prerequisites
color 0A

echo ============================================================
echo   AI_HDRI_Unity — Auto Setup
echo   This will install: Git, Python 3.10, Unity Hub, Unity 6
echo ============================================================
echo.

:: ── Check winget ────────────────────────────────────────────
winget --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] winget not found.
    echo Please update Windows 10/11 from the Microsoft Store,
    echo or install "App Installer" from:
    echo https://aka.ms/getwinget
    pause
    exit /b 1
)
echo [OK] winget found.
echo.

:: ── Git ─────────────────────────────────────────────────────
echo [1/4] Installing Git...
winget install --id Git.Git -e --silent --accept-package-agreements --accept-source-agreements
echo.

:: ── Python 3.10 ──────────────────────────────────────────────
echo [2/4] Installing Python 3.10...
winget install --id Python.Python.3.10 -e --silent --accept-package-agreements --accept-source-agreements
echo.

:: Refresh PATH so python is available immediately
set "PATH=%PATH%;%LOCALAPPDATA%\Programs\Python\Python310;%LOCALAPPDATA%\Programs\Python\Python310\Scripts"

:: ── Unity Hub ────────────────────────────────────────────────
echo [3/4] Installing Unity Hub...
winget install --id Unity.UnityHub -e --silent --accept-package-agreements --accept-source-agreements
echo.

:: ── Unity 6000.4.3f1 via Hub CLI ────────────────────────────
echo [4/4] Installing Unity 6000.4.3f1 (this may take 10-20 min)...
set UNITY_HUB="C:\Program Files\Unity Hub\Unity Hub.exe"
if exist %UNITY_HUB% (
    %UNITY_HUB% -- --headless install --version 6000.4.3f1 --changeset 4de9589e0db0 ^
        --module android-build-support ^
        --module android-sdk-ndk-tools ^
        --childModules
    echo [OK] Unity 6000.4.3f1 install started.
) else (
    echo [WARN] Unity Hub not found at default path.
    echo Open Unity Hub manually and install version 6000.4.3f1
)
echo.

:: ── Python packages ──────────────────────────────────────────
echo [5/5] Installing Python packages...
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
echo.

:: ── Done ─────────────────────────────────────────────────────
echo ============================================================
echo   Setup complete!
echo.
echo   Next steps:
echo   1. Open Unity Hub and add this project folder
echo   2. Run the web app:  python gradio_app_v2.py
echo   3. Open browser:     http://localhost:7860
echo ============================================================
echo.
pause
