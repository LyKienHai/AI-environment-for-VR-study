@echo off
title Stop All Services
color 0C
echo.
echo  ============================================================
echo    German Medical VR  —  Stopping all services
echo  ============================================================
echo.

REM ── Kill windows by title ─────────────────────────────────────────────────
echo  Closing terminal windows...
taskkill /FI "WINDOWTITLE eq Ollama" /F /T 2>nul
taskkill /FI "WINDOWTITLE eq Whisper :7864" /F /T 2>nul
taskkill /FI "WINDOWTITLE eq Gradio :7863" /F /T 2>nul
taskkill /FI "WINDOWTITLE eq Room AI :7865" /F /T 2>nul

REM ── Kill by port (belt-and-suspenders) ───────────────────────────────────
echo  Freeing ports...
for /f "tokens=5" %%a in ('netstat -aon 2^>nul ^| findstr ":7863 "') do taskkill /F /PID %%a 2>nul
for /f "tokens=5" %%a in ('netstat -aon 2^>nul ^| findstr ":7864 "') do taskkill /F /PID %%a 2>nul
for /f "tokens=5" %%a in ('netstat -aon 2^>nul ^| findstr ":7865 "') do taskkill /F /PID %%a 2>nul
for /f "tokens=5" %%a in ('netstat -aon 2^>nul ^| findstr ":11434 "') do taskkill /F /PID %%a 2>nul

echo.
echo  All services stopped.
echo.
timeout /t 2 /nobreak >nul
