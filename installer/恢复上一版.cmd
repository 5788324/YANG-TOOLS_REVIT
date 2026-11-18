@echo off
chcp 65001 >nul
set /p RVERSION=请输入需要恢复的 Revit 年份（例如 2025）: 
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Rollback.ps1" -Version "%RVERSION%"
echo.
pause
