@echo off
echo ========================================
echo   Installing Whisper server dependencies
echo ========================================
echo.

:: Use the same Python as the Gradio app
set PYTHON=E:\Python\python.exe

if not exist "%PYTHON%" (
    echo E:\Python\python.exe not found — falling back to system python
    set PYTHON=python
)

echo Using: %PYTHON%
echo.

%PYTHON% -m pip install openai-whisper fastapi uvicorn python-multipart

echo.
echo ========================================
echo   Done! Run start_whisper.bat to launch.
echo ========================================
pause
