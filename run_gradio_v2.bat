@echo off
echo Stopping any existing Gradio on port 7863...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":7863"') do taskkill /F /PID %%a 2>nul
timeout /t 1 /nobreak >nul
echo Clearing conflicting Python paths...
set PYTHONPATH=
set PYTHONUSERBASE=
set PYTHONNOUSERSITE=1
echo Starting German Medical VR Room Generator v2...
"E:\Python\python.exe" "C:\GenAI-3D-Virtual-Reality-Augmented\gradio_app_v2.py"
pause
