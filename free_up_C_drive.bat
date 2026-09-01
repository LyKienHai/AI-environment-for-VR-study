@echo off
title Free up C: drive space
echo ============================================
echo  Freeing up C: drive space for Varjo install
echo ============================================
echo.

echo [1/4] Clearing Windows Temp files...
del /q /f /s "%TEMP%\*" 2>nul
del /q /f /s "C:\Windows\Temp\*" 2>nul

echo [2/4] Clearing old Varjo install cache (if any)...
if exist "C:\ProgramData\Varjo" (
    echo   Found old Varjo data - skipping (may be needed)
)

echo [3/4] Clearing Windows Update cache (safe to delete)...
net stop wuauserv >nul 2>&1
del /q /f /s "C:\Windows\SoftwareDistribution\Download\*" 2>nul
net start wuauserv >nul 2>&1

echo [4/4] Launching Disk Cleanup for C: (manual step)...
cleanmgr /d C:

echo.
echo ============================================
echo  Current C: drive space:
echo ============================================
wmic logicaldisk where "DeviceID='C:'" get FreeSpace,Size /format:list
echo.
echo If C: now has at least 4 GB free, re-run the Varjo installer.
echo Varjo installer: E:\Warcraft III\trunk\VarjoBaseSetup_4.16.0.15.exe
echo.
pause
