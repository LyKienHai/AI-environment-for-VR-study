@echo off
set OUT=C:\GenAI-3D-Virtual-Reality-Augmented\env_found.txt
echo Checking conda environments for gradio... > "%OUT%"

REM Check each subfolder in anaconda3\envs
for /d %%E in ("C:\Users\Van\anaconda3\envs\*") do (
  if exist "%%E\python.exe" (
    echo. >> "%OUT%"
    echo ENV: %%E >> "%OUT%"
    "%%E\python.exe" -c "import gradio; print('  gradio OK in %%E')" >> "%OUT%" 2>&1
  )
)

REM Also check .conda\envs
for /d %%E in ("C:\Users\Van\.conda\envs\*") do (
  if exist "%%E\python.exe" (
    echo. >> "%OUT%"
    echo ENV: %%E >> "%OUT%"
    "%%E\python.exe" -c "import gradio; print('  gradio OK in %%E')" >> "%OUT%" 2>&1
  )
)

type "%OUT%"
pause
