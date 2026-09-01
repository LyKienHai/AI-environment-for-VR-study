@echo off
echo [1/4] Killing all Ollama processes...
taskkill /F /IM ollama.exe /T 2>nul
timeout /t 3 /nobreak >nul

echo [2/4] Starting Ollama with E drive model path...
start "Ollama Server" cmd /k "set OLLAMA_MODELS=E:\ollama_models && ollama serve"
timeout /t 5 /nobreak >nul

echo [3/4] Pulling llama3.2:1b to E:\ollama_models ...
set OLLAMA_MODELS=E:\ollama_models
ollama pull llama3.2:1b

echo.
echo [4/4] Done! Model saved to E:\ollama_models
pause
