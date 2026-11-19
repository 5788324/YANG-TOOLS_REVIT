# Gemini 实机任务｜YangTools PersonalPilot TEST · Revit 2025

## 测试基线

- 仓库 `5788324/YANG-TOOLS_REVIT`，[Draft PR #11](https://github.com/5788324/YANG-TOOLS_REVIT/pull/11)，产品源码固定到 `ec9a89cc875accc6d3f0291660039e3122c81f81`。用新 QA 工作目录检出，避免混用旧 main 或残留构建。
- 本轮只做一次中文 GUI 的首装 → 更新 → 恢复上一版 → 卸载，并核验不同 DLL 的版本回退。复用已验收的 CLI 结果、Ribbon 加载和 Exchange P5 证据。
- 仅使用 Windows PowerShell 5.1、Revit 2025.4 QA 和临时 APPDATA。保持生产 Revit 2022/2023 和其他生产进程运行状态，保护其 Addins、真实个人设置、原始 DWG/RVT 及 YangCAD。若阻断的是隔离 QA Revit 2025，只在确认进程身份后关闭该 QA 实例。安装器从专用子进程继承临时 APPDATA，启动前用 `Resolve-Path` 确认 QA 根目录。切勿全局改 APPDATA。
- 创建两个独立的纯解包目录。两套安装器脚本都来自固定基线，目录只含 2025 payload，旁边没有源码 `bin` 或 `artifacts` 可抢占 GUI 的 payload 来源。

## 准备两份 DLL

- **A（上一版）**：优先用已有可追溯、能提供完整 payload 的实际 Revit 2025 构建。若现有 A 与 B 的 DLL SHA-256 相同，或缺少 A 的完整产物，在临时副本中仅将 `YangTools.Revit.csproj` 的 `FileVersion` 设为 `4.0.0.1`，用现有构建脚本只编译 2025，作为 QA 用旧版样本。保持 AssemblyVersion，记录 A 的来源或元数据差异。A 样本不提交、不打入个人试用包。
- **B（试用候选）**：使用固定提交的干净源码及可追溯的 2025 构建。需要补编译时，仅运行 `tools/Build-All.ps1 -Versions 2025`；随后用 `tools/Package-Installer.ps1 -SkipBuild -Versions 2025` 生成候选。A 与 B 的 SHA-256 必须不同，不能用不同 marker 代替不同 DLL。任何构建均不启用部署。
- 保留 A/B 原始 DLL 的 SHA-256、FileVersion、大小和来源，并记录各包的原始 `.addin` SHA-256。两份 manifest 可以相同，此时按各自源哈希核对，不以它区分 DLL。

## 用现有按钮完成一轮

1. 在临时 APPDATA 的 `YangTools` 下创建一份 QA 个人数据样本并记录 SHA-256。四步使用同一个临时 APPDATA。可从专用 Windows PowerShell 5.1 窗口启动解包目录中的 `YangTools安装器.cmd`，每次只勾选 2025。生产 Revit 2022 如已在运行，记录实际 PID 并保持运行。
2. 从 A 解包目录启动，点「安装 / 更新」。2025 插件 DLL 和 manifest 应匹配 A，首次安装无上一版备份。关闭窗口。
3. 从 B 解包目录启动，点「安装 / 更新」。2025 当前 DLL 和 manifest 应匹配 B，`YangTools.backup` 内 DLL 和 `YangTools.Revit.addin.backup` 应匹配 A。
4. 在 B 窗口点「恢复上一版」并确认。2025 当前 DLL 和 manifest 应匹配 A，备份应匹配 B。个人数据 SHA-256 与初始值一致。
5. 点「卸载」并确认。2025 当前及备份的插件目录和 manifest 应移除，QA 个人数据文件及 SHA-256 保持一致。每步截图记录 GUI 结果，附操作后的路径与哈希。遇到真实失败，保存截图、日志和文件状态并报告，勿用 CLI 绕过或自行改安装器。

## 通过后制作待交付包

- 使用已测的 **B** 原始打包输出。仅在独立打包目录把说明换为 **YangTools 4.0.0 PersonalPilot TEST · Revit 2025**，写明先关闭目标 Revit 2025、双击 `YangTools安装器.cmd` 并勾选 2025，以及有备份时用「恢复上一版」。注明恢复会保留刚替换的版本作为备份、卸载会移除插件及其备份但保留个人设置。个人试用包仅面向隔离的 Revit 2025。Exchange P5 已知遗留为 29 个 HATCH、1 个标注和一处 0.18755 mm 偏移。
- 重新生成包内 `SHA256SUMS.txt`，覆盖说明、全部脚本和 2025 payload，清单排除自身。把包根目录内容压缩为 `YangTools-4.0.0-PersonalPilot-TEST-Revit2025.zip`，另生成整包 SHA-256。解包复核清单，唯一 payload 年份为 2025，DLL SHA-256 与刚实测的 B 一致，安装器脚本也与实测文件逐一一致。
- 回传 QA 证据 ZIP（四步截图、每步当前/备份 DLL 和 manifest 及个人文件状态与哈希、A/B 来源、基线提交、QA APPDATA 和进程记录）。测试通过时另附 B 试用 ZIP 及整包 SHA-256。Codex 核对证据后在 Draft PR #11 记录验收与冻结结果。
