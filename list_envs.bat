@echo off
set OUT=C:\GenAI-3D-Virtual-Reality-Augmented\all_envs.txt
echo All conda environments: > "%OUT%"
C:\Users\Van\anaconda3\Scripts\conda.exe env list >> "%OUT%" 2>&1
echo. >> "%OUT%"
echo Checking each env for gradio... >> "%OUT%"
C:\Users\Van\anaconda3\Scripts\conda.exe run --no-capture-output -n base python -c "import sys; print(sys.path)" >> "%OUT%" 2>&1
type "%OUT%"
pause
