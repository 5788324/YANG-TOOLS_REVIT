# YangTools 4.0.0 个人可用版：安装与回退第一阶段（2026-11-18）

## 当前基线
- 源码：Exchange P5（653 文件），PR #9，Revit 2021–2026 六版本 BUILD GO，Revit 2025 C4 LIMITED GO。
- 仍有 29 个 HATCH、1 个 DIM 未转换，另 1 条曲线最终偏移 0.18755 mm 超出约定 0.15 mm 上限，不能宣称 100% 无损。
- 本批不改 CAD→Revit、Ribbon、MCP 或 YangCAD；只调整安装/回退流程。

## 本批实际修改
1. 安装/卸载操作不再自动删除 `%APPDATA%\YangTools` 个人数据或旧助手设置/历史。
2. 命令行安装与卸载必须显式指定年份；图形安装器仍由用户勾选版本。
3. 安装更新后保留上一版 `YangTools.backup` / `YangTools.Revit.addin.backup`，失败时按实际是否移动旧文件恢复，避免覆盖原有安装。
4. 图形安装器新增“恢复上一版”按钮，CLI 新增 `Rollback.ps1 -Version 2025`；每次只对一个年份做版本交换。
5. 不再在安装时自动移除 `RevitParameterDeduplicator` 等独立插件，只提示潜在重复入口。
6. 直接覆盖式 `Build-Deploy-2024-2025.ps1` 和 `Build-All.ps1 -Deploy` 明确禁用，避免在编译前就删除已有生产 Addins。

所有修改的 PowerShell 脚本继续使用 UTF-8 BOM，兼容 Windows PowerShell 5.1 对中文文本的读取。

## 下一阶段 Gemini Windows 实机（无需重新测试 Exchange 业务）
- 基于本分支对 Revit 2025 做 `dotnet build -c 2025` 的隔离构建；不得部署到 2022/2023。
- 先对安装脚本做 PowerShell Parser 检查，再使用**独立测试 Windows 账户/可丢弃配置目录**验证：安装前一版 → 更新新版本 → 备份保留 → 一键恢复 → 两次 DLL SHA 与 manifest 对照。
- 验证 `%APPDATA%\YangTools` 个人数据哈希前后不变；不能使用真实个人机数据作为测试删除样本。
- 仅在独立 QA 的 Revit 2025 上验证 Ribbon 能启动、已验收三项 2D 功能能正常调用、Exchange 转换可生成部分结果及 Undo。
- **禁止对生产 Revit 2022/2023 Addins 安装或回退。** 2027 仍未验，不打包为已支持版本。
- 该批是源码静态候选，尚未 Windows PowerShell 实测/正式安装。
