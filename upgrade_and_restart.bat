@echo off
echo Pulling llama3.2:3b (this may take a few minutes)...
set OLLAMA_MODELS=E:\ollama_models
ollama pull llama3.2:3b
echo Done! Restarting Gradio...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr ":7863"') do taskkill /F /PID %%a 2>nul
timeout /t 2 /nobreak >nul
set PYTHONPATH=
set PYTHONUSERBASE=
set PYTHONNOUSERSITE=1
start "Gradio App" cmd /k ""E:\Python\python.exe" "C:\GenAI-3D-Virtual-Reality-Augmented\gradio_app_v2.py""
timeout /t 5 /nobreak >nul
start http://localhost:7863
