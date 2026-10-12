@echo off
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0YangTools安装器.ps1"
if errorlevel 1 (
  echo.
  echo 安装器启动失败，请查看上方错误。
  pause
)
