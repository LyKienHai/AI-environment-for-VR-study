@echo off
set OUT=C:\GenAI-3D-Virtual-Reality-Augmented\all_python_versions.txt

echo === Python Launcher (py -0) === > "%OUT%"
py -0 >> "%OUT%" 2>&1

echo. >> "%OUT%"
echo === Python Launcher paths === >> "%OUT%"
py -0p >> "%OUT%" 2>&1

echo. >> "%OUT%"
echo === Checking known Python locations === >> "%OUT%"

for %%V in (310 311 312 313) do (
  for %%D in (
    "C:\Python3%%V\python.exe"
    "C:\Users\Van\AppData\Local\Programs\Python\Python3%%V\python.exe"
    "C:\Program Files\Python3%%V\python.exe"
  ) do (
    if exist %%D (
      echo FOUND: %%D >> "%OUT%"
      %%D -c "import gradio; print('  gradio OK:', gradio.__version__)" >> "%OUT%" 2>&1
      %%D -c "import torch; print('  torch OK:', torch.__version__)" >> "%OUT%" 2>&1
    )
  )
)

echo. >> "%OUT%"
echo === IDLE locations (to trace Python install) === >> "%OUT%"
where idle >> "%OUT%" 2>&1
where idle3 >> "%OUT%" 2>&1
where py >> "%OUT%" 2>&1
where python3 >> "%OUT%" 2>&1

type "%OUT%"
pause
