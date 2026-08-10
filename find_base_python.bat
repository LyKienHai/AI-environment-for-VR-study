@echo off
set OUT=C:\GenAI-3D-Virtual-Reality-Augmented\base_python_info.txt

echo === pyvenv.cfg (base Python for venv) === > "%OUT%"
type "E:\ai_generator\ai_generator\venv\pyvenv.cfg" >> "%OUT%" 2>&1

echo. >> "%OUT%"
echo === All conda envs === >> "%OUT%"
C:\Users\Van\anaconda3\Scripts\conda.exe env list >> "%OUT%" 2>&1

echo. >> "%OUT%"
echo === Windows Python in AppData\Local === >> "%OUT%"
dir /b "C:\Users\Van\AppData\Local\Programs\Python\" >> "%OUT%" 2>&1

echo. >> "%OUT%"
echo === Python in Program Files === >> "%OUT%"
dir /b "C:\Program Files\Python*" >> "%OUT%" 2>&1
dir /b "C:\Python*" >> "%OUT%" 2>&1

echo. >> "%OUT%"
echo === Check gradio in AppData Local Pythons === >> "%OUT%"
for /d %%P in ("C:\Users\Van\AppData\Local\Programs\Python\*") do (
  if exist "%%P\python.exe" (
    echo Checking %%P >> "%OUT%"
    "%%P\python.exe" -c "import gradio; print('FOUND gradio in %%P')" >> "%OUT%" 2>&1
  )
)

type "%OUT%"
pause
