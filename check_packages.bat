@echo off
set OUT=C:\GenAI-3D-Virtual-Reality-Augmented\packages_found.txt
echo Checking where gradio is installed... > "%OUT%"
echo. >> "%OUT%"

REM Check venv site-packages
echo VENV site-packages: >> "%OUT%"
dir /b "E:\ai_generator\ai_generator\venv\Lib\site-packages\" 2>&1 | findstr /i "gradio torch requests urllib3 diffusers" >> "%OUT%"

echo. >> "%OUT%"
echo User Python311 site-packages: >> "%OUT%"
dir /b "C:\Users\Van\AppData\Roaming\Python\Python311\site-packages\" 2>&1 | findstr /i "gradio torch requests urllib3 diffusers" >> "%OUT%"

echo. >> "%OUT%"
echo Anaconda base site-packages: >> "%OUT%"
dir /b "C:\Users\Van\anaconda3\Lib\site-packages\" 2>&1 | findstr /i "gradio torch requests urllib3 diffusers" >> "%OUT%"

echo. >> "%OUT%"
echo Bug-detection env site-packages: >> "%OUT%"
dir /b "C:\Users\Van\anaconda3\envs\bug-detection\Lib\site-packages\" 2>&1 | findstr /i "gradio torch requests urllib3 diffusers" >> "%OUT%"

type "%OUT%"
pause
