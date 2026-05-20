@echo off
title Config Table Export
echo ============================================
echo  Config Table Export Tool
echo ============================================
echo.
cd /d "%~dp0"
echo Running export...
python export_config.py
echo.
echo ============================================
echo Done. You can close this window.
pause >nul
