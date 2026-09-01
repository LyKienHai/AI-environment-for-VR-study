@echo off
echo Killing existing Ollama...
taskkill /F /IM ollama.exe /T 2>nul
timeout /t 2 /nobreak >nul
if not exist "E:\ollama_models" mkdir "E:\ollama_models"
start "Ollama" cmd /k "set OLLAMA_MODELS=E:\ollama_models && ollama serve"
timeout /t 3 /nobreak >nul
set OLLAMA_MODELS=E:\ollama_models
ollama pull llama3.2:1b
pause
