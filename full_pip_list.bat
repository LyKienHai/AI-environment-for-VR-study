@echo off
set OUT=C:\GenAI-3D-Virtual-Reality-Augmented\full_pip_list.txt
set PY=E:\Python\python.exe

echo === Full pip list for E:\Python === > "%OUT%"
"%PY%" -m pip list >> "%OUT%" 2>&1

echo. >> "%OUT%"
echo === E:\Python\Lib\site-packages (first 60 entries) === >> "%OUT%"
dir /b "E:\Python\Lib\site-packages\" 2>&1 | findstr /v "^$" | more /E +1 >> "%OUT%"

echo. >> "%OUT%"
echo === Check requirements.txt === >> "%OUT%"
if exist "E:\ai_generator\ai_generator\requirements.txt" (
  type "E:\ai_generator\ai_generator\requirements.txt" >> "%OUT%"
) else (
  echo No requirements.txt found >> "%OUT%"
)

type "%OUT%"
pause
