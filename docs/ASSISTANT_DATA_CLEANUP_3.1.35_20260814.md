# YangTools 3.1.35：已删除 AI/摸鱼助手旧数据清理

日期：2026-08-14

## 目标

AI/摸鱼助手运行功能和源码已永久删除。用户随后明确要求旧历史、设置、提示词和 API Key 也全部清理，不再保留。

## 删除范围

只删除以下退役助手数据：

- `%APPDATA%\YangTools\Assistant\`；
- `%APPDATA%\YangTools\copilot_settings.json`；
- `%APPDATA%\YangTools\copilot_history.json`；
- `%APPDATA%\YangTools\moyu_settings.json`；
- `%APPDATA%\YangTools\moyu_history.json`。

旧 `copilot_settings.json` / `moyu_settings.json` 曾可能包含明文或可解码的历史 API Key，所以和新 `Assistant/*/api_key.dat` 一并清理。

## 不删除

- `%APPDATA%\YangTools\MCP`；
- 当前主题、Ribbon、族管理、项目资产等仍使用的配置；
- `%LOCALAPPDATA%\YangTools\Logs`。

## 清理入口

1. Revit 加载当前 YangTools 时，`LegacyUserDataCleanup.RemoveRetiredAssistantData()` 幂等清理；
2. `installer/Install.ps1` 安装/更新前清理；
3. `installer/Uninstall.ps1` 卸载时清理；
4. `installer/YangTools安装器.ps1` GUI 安装/更新/卸载均清理；
5. `tools/Build-All.ps1 -Deploy` 清理；
6. `tools/Build-Deploy-2024-2025.ps1` 清理。

这样既覆盖用户日常 DeepSeek 直接部署，也覆盖以后分享给他人的自包含安装包。

## 失败策略

Revit 启动中的清理失败只记录诊断日志，不阻断插件启动；安装/部署脚本使用严格删除，失败会明确报错，避免误以为已清理完成。
