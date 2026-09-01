@echo off
title Sync local changes to GitHub
color 0A
cd /d "%~dp0"

echo ============================================================
echo  Syncing AI-environment-for-VR-study to GitHub
echo ============================================================
echo.

:: ── Remove junk/diagnostic files ─────────────────────────────
echo [1/4] Removing junk and diagnostic files...
git rm --cached --ignore-unmatch ".~lock.AI_VR_Environment_Presentation.pptx#"
git rm --cached --ignore-unmatch all_python_versions.txt
git rm --cached --ignore-unmatch base_python_info.txt
git rm --cached --ignore-unmatch check_e_python.bat
git rm --cached --ignore-unmatch check_output.txt
git rm --cached --ignore-unmatch check_packages.bat
git rm --cached --ignore-unmatch check_python311.bat
git rm --cached --ignore-unmatch env_found.txt
git rm --cached --ignore-unmatch epython_info.txt
git rm --cached --ignore-unmatch find_all_python.bat
git rm --cached --ignore-unmatch find_base_python.bat
git rm --cached --ignore-unmatch find_env.bat
git rm --cached --ignore-unmatch find_python.bat
git rm --cached --ignore-unmatch full_pip_list.bat
git rm --cached --ignore-unmatch full_pip_list.txt
git rm --cached --ignore-unmatch install_packages.bat
git rm --cached --ignore-unmatch list_envs.bat
git rm --cached --ignore-unmatch packages_found.txt
git rm --cached --ignore-unmatch python311_packages.txt
git rm --cached --ignore-unmatch python_found.txt

:: Also remove the physical files from disk
del /q ".~lock.AI_VR_Environment_Presentation.pptx#" 2>nul
del /q all_python_versions.txt 2>nul
del /q base_python_info.txt 2>nul
del /q check_e_python.bat 2>nul
del /q check_output.txt 2>nul
del /q check_packages.bat 2>nul
del /q check_python311.bat 2>nul
del /q env_found.txt 2>nul
del /q epython_info.txt 2>nul
del /q find_all_python.bat 2>nul
del /q find_base_python.bat 2>nul
del /q find_env.bat 2>nul
del /q find_python.bat 2>nul
del /q full_pip_list.bat 2>nul
del /q full_pip_list.txt 2>nul
del /q install_packages.bat 2>nul
del /q list_envs.bat 2>nul
del /q packages_found.txt 2>nul
del /q python311_packages.txt 2>nul
del /q python_found.txt 2>nul
echo Done.
echo.

:: ── Stage new and modified files ─────────────────────────────
echo [2/4] Staging new and modified files...
git add QuizManager.cs
git add DoctorRoomDecorator.cs
git add NPCChatUI.cs
git add WallImageQuiz.cs
git add setup.bat
git add requirements.txt
git add README.md
git add copy_scripts_to_unity.bat
git add free_up_C_drive.bat
echo Done.
echo.

:: ── Commit ───────────────────────────────────────────────────
echo [3/4] Committing...
git commit -m "Add image quiz, setup scripts, double VR fonts; remove diagnostic junk"
echo.

:: ── Push ─────────────────────────────────────────────────────
echo [4/4] Pushing to GitHub...
git push origin main
echo.

echo ============================================================
echo  Done! Repo is up to date.
echo ============================================================
pause
