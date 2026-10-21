# YangTools 4.0.0 RC3E | Phase A V4 编译修复与取证门禁

日期：2026-10-14

## 源码链
- 唯一输入 RC3D ZIP 的 SHA-256：`15AC5FEB189A4B349CFC2147E12F7020EE59646A402A9BCA018D962B7E0B372A`。
- RC3D Gemini Phase A V3：Windows/Revit 2024、2025 配置均 `dotnet build exit 1`；CS0104 在 `Core/ProjectAssets/ProjectAssetLineDuplicateService.cs:106`；未生成 DLL；B/C 未测。
- RC3E 是**新的候选源码**，不是 RELEASE GO；新 ZIP SHA 见外部独立验收报告。

## 源码改动（最小闭环）
1. `ProjectAssetLineDuplicateService.cs:67`：实例化使用 `new Autodesk.Revit.DB.Color(...)`。
2. `ProjectAssetLineDuplicateService.cs:106`：`Matches` 参数类型使用 `Autodesk.Revit.DB.Color`。
3. 更新 `tools/Test-RC3A-LineMicroUI.py` 中脆弱的 `color_copy` 字符串断言（从不安全的 `new Color` 改为明确检测 `new Autodesk.Revit.DB.Color`）；新增 `tools/Test-RC3E-CompileGuard.py` 检测同类 Color 绑定回归、事务读回/回滚与部署仍为 opt-in。
4. 本文件及 WORKLOG 文档记录，其他模块保持二进制源文件内容不变。

**刻意不动**：Ribbon、MCP 27 wire、钢筋、Rebar、Loft、实例参数、用户数据、打包安装器、Revit 版本框架、自动部署目标。`MSB3052 /define:2024/2025` 警告不属于当前阻断错误，先记录后在 Windows 编译日志检查，不以更改配置为代价消除警告。

## 验收层级
- 本地：原 ZIP SHA、CRC、全部现有静态测试、增加的编译防回归测试；43/43 XAML XML parse；所有 JSON parse；源码差异/保护路径审计。
- **真正 Phase A 必须在 Windows**：2024 `net48`、2025 `net8.0-windows` 独立全新目录完整编译（含还原），记录原始命令、ExitCode、DLL 文件状态/SHA/长度、环境、原始日志、源码变化情况。
- `ExitCode==0 AND DLL exists AND hash recorded` 才可标记 해당配置 `BUILD PASS`。其中任一失败则记录 `BUILD FAIL` 并停止 B/C。
- Phase A PASS 不等于实际 Revit 加载通过。仅 ChatGPT 独立核对 evidence 后才能安排 B/C 隔离 Revit 实测。

## 禁止
绝不能使用 `Build-All.ps1` 或任何包含部署的脚本；不可写 `%APPDATA%/Autodesk/Revit/Addins`；不可触碰用户生产 `.rvt`、生产插件，不改 RC3E 源码，不推 Git。Gemini 执行证据获取，不负责修复代码、不自行判 RELEASE GO。
