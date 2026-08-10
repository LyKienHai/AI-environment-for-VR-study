@echo off
set OUT=C:\GenAI-3D-Virtual-Reality-Augmented\python_found.txt
echo Searching for Python with gradio... > "%OUT%"

REM Try common python locations
for %%P in (
  "C:\ProgramData\Anaconda3\python.exe"
  "C:\Users\Van\anaconda3\python.exe"
  "C:\Users\Van\Anaconda3\python.exe"
  "C:\Users\Van\AppData\Local\anaconda3\python.exe"
  "C:\Users\Van\miniconda3\python.exe"
  "E:\ai_generator\ai_generator\venv\Scripts\python.exe"
) do (
  if exist %%P (
    echo Found: %%P >> "%OUT%"
    %%P -c "import gradio; print('gradio OK')" >> "%OUT%" 2>&1
  )
)

REM Also try 'where python'
echo. >> "%OUT%"
echo All pythons in PATH: >> "%OUT%"
where python >> "%OUT%" 2>&1

echo Done. Check C:\GenAI-3D-Virtual-Reality-Augmented\python_found.txt
type "%OUT%"
pause
