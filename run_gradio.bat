@echo off
echo Clearing conflicting Python paths...
set PYTHONPATH=
set PYTHONUSERBASE=
set PYTHONNOUSERSITE=1
echo Starting Gradio app with E:\Python\python.exe...
"E:\Python\python.exe" E:\ai_generator\ai_generator\gradio_app_depth.py
pause
