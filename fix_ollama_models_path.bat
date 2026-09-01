@echo off
echo ============================================
echo  Fixing Ollama - Redirecting to E: Drive
echo ============================================
echo.

echo [1/5] Killing ALL Ollama processes (including parent)...
taskkill /F /IM ollama.exe /T >nul 2>&1
taskkill /F /FI "IMAGENAME eq ollama*" /T >nul 2>&1
wmic process where "name like 'ollama%%'" delete >nul 2>&1
timeout /t 3 /nobreak >nul

echo [2/5] Verifying all Ollama processes are gone...
tasklist | findstr /i ollama
if %errorlevel%==0 (
    echo WARNING: Some Ollama processes still running, force killing...
    wmic process where "name like 'ollama%%'" delete >nul 2>&1
    timeout /t 2 /nobreak >nul
) else (
    echo All Ollama processes stopped.
)

echo [3/5] Creating E:\ollama_models folder...
mkdir "E:\ollama_models" 2>nul

echo [4/5] Starting fresh Ollama SERVER with E: drive path...
set OLLAMA_MODELS=E:\ollama_models
start "OllamaServer" /min cmd /c "set OLLAMA_MODELS=E:\ollama_models && ollama serve"
echo Waiting for server to start...
timeout /t 5 /nobreak >nul

echo [5/5] Pulling llama3.2:1b (saving to E:\ollama_models)...
set OLLAMA_MODELS=E:\ollama_models
ollama pull llama3.2:1b

echo.
echo ============================================
echo  Done! Check E:\ollama_models for the model
echo ============================================
dir E:\ollama_models
pause
