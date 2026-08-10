@echo off
set OUT=C:\GenAI-3D-Virtual-Reality-Augmented\epython_info.txt
set PY=E:\Python\python.exe

echo === E:\Python site-packages (gradio/torch/requests) === > "%OUT%"
dir /b "E:\Python\Lib\site-packages\" 2>&1 | findstr /i "gradio torch diffusers requests urllib3" >> "%OUT%"

echo. >> "%OUT%"
echo === User site-packages for Python310 === >> "%OUT%"
dir /b "C:\Users\Van\AppData\Roaming\Python\Python310\site-packages\" 2>&1 | findstr /i "gradio torch diffusers requests urllib3" >> "%OUT%"

echo. >> "%OUT%"
echo === Pip list for E:\Python === >> "%OUT%"
"%PY%" -m pip list 2>&1 | findstr /i "gradio torch requests urllib3 diffusers" >> "%OUT%"

echo. >> "%OUT%"
echo === E:\Python sys.path === >> "%OUT%"
"%PY%" -c "import sys; [print(p) for p in sys.path]" >> "%OUT%" 2>&1

type "%OUT%"
pause
