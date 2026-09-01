@echo off
title Uploading to GitHub — AI VR Study
color 0A
cd /d "C:\GenAI-3D-Virtual-Reality-Augmented"
echo.
echo  ============================================================
echo    GitHub Upload — AI-environment-for-VR-study
echo    https://github.com/LyKienHai/AI-environment-for-VR-study
echo  ============================================================
echo.

echo  [1/5] Removing all previously tracked files...
git rm -rf --cached . > git_push_log.txt 2>&1
echo  Done.

echo  [2/5] Staging all current files...
git add . >> git_push_log.txt 2>&1
echo  Done.

echo  [3/5] Committing...
git commit -m "2026-08-17: Fresh upload — complete project reset and update" >> git_push_log.txt 2>&1
echo  Done.

echo  [4/5] Force pushing to GitHub (master branch)...
git push --force origin master >> git_push_log.txt 2>&1
echo  Done.

echo  [5/5] Verifying...
git log --oneline -3 >> git_push_log.txt 2>&1

echo.
echo  ============================================================
echo    Upload complete!
echo    Repo: https://github.com/LyKienHai/AI-environment-for-VR-study
echo.
echo    If you see an auth prompt — sign in to GitHub in your browser.
echo    Check git_push_log.txt in this folder for full details.
echo  ============================================================
echo.
pause
