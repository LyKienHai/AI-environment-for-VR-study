@echo off
set PY=E:\Python\python.exe
echo ============================================
echo Installing AI Room Generator dependencies
echo into E:\Python (Python 3.10)
echo ============================================
echo.

echo [1/3] Installing torch (CPU) + torchvision...
"%PY%" -m pip install torch torchvision --index-url https://download.pytorch.org/whl/cpu
echo.

echo [2/3] Installing gradio, diffusers, transformers, Pillow, requests, accelerate...
"%PY%" -m pip install gradio diffusers transformers accelerate Pillow requests
echo.

echo [3/3] Verifying key packages...
"%PY%" -c "import gradio, torch, diffusers, PIL, requests; print('ALL OK - gradio:', gradio.__version__, '| torch:', torch.__version__)"
echo.
echo ============================================
echo Done! Run run_gradio.bat to launch the app.
echo ============================================
pause
