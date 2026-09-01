@echo off
powershell -c "[console]::beep(880,150); Start-Sleep -Milliseconds 80; [console]::beep(1100,150); Start-Sleep -Milliseconds 80; [console]::beep(1320,400)"
