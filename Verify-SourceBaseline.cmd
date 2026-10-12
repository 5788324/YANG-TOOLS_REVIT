@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\Verify-SourceBaseline.ps1"
if errorlevel 1 (
  echo.
  echo Source baseline verification failed. Do not build this directory.
  pause
  exit /b 1
)
echo.
pause
