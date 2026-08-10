@echo off
set OUT=C:\GenAI-3D-Virtual-Reality-Augmented\python311_packages.txt
set PY=C:\Python311\python.exe

echo === Checking C:\Python311 for gradio === > "%OUT%"

if exist "%PY%" (
  echo Python311 FOUND at %PY% >> "%OUT%"
  "%PY%" --version >> "%OUT%" 2>&1
  echo. >> "%OUT%"
  echo Checking for gradio: >> "%OUT%"
  "%PY%" -c "import gradio; print('GRADIO OK:', gradio.__version__)" >> "%OUT%" 2>&1
  echo. >> "%OUT%"
  echo Checking for torch: >> "%OUT%"
  "%PY%" -c "import torch; print('TORCH OK:', torch.__version__)" >> "%OUT%" 2>&1
  echo. >> "%OUT%"
  echo Checking for diffusers: >> "%OUT%"
  "%PY%" -c "import diffusers; print('DIFFUSERS OK:', diffusers.__version__)" >> "%OUT%" 2>&1
  echo. >> "%OUT%"
  echo Checking for requests: >> "%OUT%"
  "%PY%" -c "import requests; print('REQUESTS OK:', requests.__version__)" >> "%OUT%" 2>&1
) else (
  echo Python311 NOT found at %PY% >> "%OUT%"
)

echo. >> "%OUT%"
echo === Listing C:\Python311\Lib\site-packages (filtered) === >> "%OUT%"
dir /b "C:\Python311\Lib\site-packages\" 2>&1 | findstr /i "gradio torch diffusers requests urllib3 transformers" >> "%OUT%"

type "%OUT%"
pause
