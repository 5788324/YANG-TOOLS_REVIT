@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Build-Deploy-2024-2025.ps1"
if errorlevel 1 (
  echo.
  echo Build/deploy failed. Review the error above.
  pause
  exit /b 1
)
echo.
echo Revit 2024 and Revit 2025 build/deploy completed.
pause
