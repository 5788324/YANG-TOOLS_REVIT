# YangTools 4.0.0 / PersonalPilot — 新对话交接

> 日期：2026-11-18。此文档是后续开发和个人试用交付的接手入口，不是正式 Release 证明。实时状态优先以 GitHub PR Head、源码和最新 QA 证据为准。

## 1. 项目目标、基线及约束

- 项目：个人使用的 Revit 二次开发插件 YangTools（关联 YangCAD、CAD→Revit Exchange）。项目仓库：<https://github.com/5788324/YANG-TOOLS_REVIT>。
- **最新源码主线：Draft PR #11** <https://github.com/5788324/YANG-TOOLS_REVIT/pull/11>；分支 `feat/personal-pilot-version-scoped-revit-gate-20261118`。交接写入前 Head 为 `1eca1cf28b0c50cc21fa5cd0bfaed898e13c9a62`；本交接提交后请读取 PR 的新 Head。
- `main` 交接核查时仍为 `bd589ed87d76ebc7b0595072e98fcaa419f74d2c`。**PR #4–#11 全部 Open Draft、未合并；直接拉 main 不是最新完整源码。** PR 链按父分支逐级延续，PR #11 含前序开发内容。
- 用户默认中文，项目 UI 中文；个人项目快速迭代，优先完成正常路径、节约时间；不要无需求扩展、复杂抽象或过度测试。保留生产文件、个人数据和回退的基本保护。
- Windows 生产 Revit 2022（历史实测 PID 17776，不应假定永久不变）继续工作，不关闭、不更改其 Addins；历史还涉及生产 2023。只在隔离的 **Revit 2025.4 QA + 可丢弃模型/APPDATA/Addins** 测试。不得修改原 DWG/MAP/RVT、YangCAD 仓库及个人设置。
- Git 默认只更新现有 Draft PR，**未经明确授权不合并 main、不发布正式 4.0.0、不强推历史分支**。

## 2. 当前已完成与未完成

| 模块 | 已验证结论 | 限制 |
| --- | --- | --- |
| Revit 插件框架/Ribbon | Revit 2025.4 QA 加载正常；YangTools 中文 Ribbon **10 个面板**，图纸管理窗口、CAD→Revit 窗口打开关闭正常；RC3J/HF2 历史有 **36 按钮**的 Ribbon UX 静态验收 | 不是全部按钮都做过实际建模操作测试 |
| HF2 2D 整理 | 历史验收已覆盖 **直线合并、重叠清理、填充隐藏边界** 三项 | 不代表全部其它功能验收完成 |
| Exchange P5 | Revit **2021–2026 六版本 BUILD GO**（原始 DLL/哈希已有证据）；Revit **2025 C4 转换成功、原生一次 Undo 后清空** | **LIMITED GO**，非 100% 无损；其他年份运行时未验，YangCAD 全链路未验 |
| PersonalPilot CLI 安装器 | Windows PowerShell 5.1/BOM、隔离 **安装→更新→备份→回退→卸载 PASS**；目标年份进程门禁正反向 PASS；个人设置 SHA 保持不变 | 中文 GUI 安装/回退按钮完整点击链路未实测；不同 DLL 内容的版本回退未实测 |
| 2025 打包/插件加载 | Gemini Windows 2025 隔离编译和 Package-Installer 打包 ExitCode 0，QA Revit 2025 Ribbon/窗口加载 PASS | QA 测试 != 生产安装许可；包名包含 4.0.0 不等于正式 Release |
| YangTools 4.0.0 正式发布 | 尚未通过完整系统验收 | **RELEASE NO-GO**；不要把个人试用和正式发布混为一谈 |

## 3. Exchange P5 实际结果与已知问题

独立验收：[`docs/YANGTOOLS_EXCHANGE_P5_INDEPENDENT_ACCEPTANCE_20261118.md`](YANGTOOLS_EXCHANGE_P5_INDEPENDENT_ACCEPTANCE_20261118.md)。P5 PR：<https://github.com/5788324/YANG-TOOLS_REVIT/pull/9>。

- 真实 C4 重导出 MAP（QA 报告 SHA `1D506D76C1AFF58C819C652482924E7634B5343B558E2BF6EE73D22B17F2E794`）源对象 **841**：`810 created + 20 failed + 11 rejected`。MAP 原文件未收入 P5 证据 ZIP，不能凭该 ZIP 独立复算 MAP SHA。
- 新建图元 **1,131**：108 TextNote、810 普通 DetailCurve（来自 603 曲线源）、42 FilledRegion、57 Dimension、114 尺寸参考线。一次原生 Undo 后目标视图归零；本次 Journal 未出现 666 次连接循环。
- 未恢复：**19 个短边 HATCH + 10 个弧形边界 HATCH = 29 个填充**；**1 个短参考线标注**；另 1 个空白文字 Reader 拒绝。
- **810 条最终普通曲线**：771 strict、38 accepted 微调、1 rejected（指最终几何判定，图元其实已创建）。唯一超限为 `SourceId=1ED` / `ElementId=1060243`：线长由 100.00000 mm 变 100.18755 mm，约 **0.18755 mm > P5 0.15 mm 上限**。在后续创建标注参考线时 Autojoin 再调整，**原审计 JSON 未警告这个最终超限**。
- P5 只对新建且发生合法 DetachElements 的有界 Line/Arc 允许有限微调：`min(0.15mm, 0.20*ShortCurveTolerance, 0.20*源线长)`；整圆严格核验。**冻结 P5，不主动继续 P6/P7 或放宽容差。** 真正影响用户日常使用时才做单点修复或补最终阶段告警。
- 原证据 `P5_EVIDENCE.zip`：4,066,561 字节，SHA256 `63129CD5C9A6B9B0325F44919DE1F943895DF82D0D3D9BA4EBFFC4DF804DFFF8`；全部六版本 DLL 样本在该证据包中曾独立核验。

## 4. PersonalPilot 安装器状态与证据

文档：[`docs/YANGTOOLS_PERSONAL_PILOT_INSTALL_ROLLBACK_20261118.md`](YANGTOOLS_PERSONAL_PILOT_INSTALL_ROLLBACK_20261118.md)、[`docs/YANGTOOLS_PERSONAL_PILOT_VERSIONGATE_REGEX_ACCEPTANCE_20261118.md`](YANGTOOLS_PERSONAL_PILOT_VERSIONGATE_REGEX_ACCEPTANCE_20261118.md)。代码目录 `installer/`。

- CLI 安装/卸载必须指定 `-Versions 2025`，回退 `Rollback.ps1 -Version 2025`；中文图形安装器按勾选年份操作，包含“恢复上一版”。
- 安装/卸载不自动删除 `%APPDATA%\YangTools` 个人设置历史；升级保留 `YangTools.backup` 与 `YangTools.Revit.addin.backup`；不自动删除独立插件；`Build-All.ps1 -Deploy` 和危险直接部署脚本已禁用。
- `Assert-TargetRevitClosed.ps1` 使用 `Win32_Process.ExecutablePath` 区分运行中的 Revit 年份；**正则字符类必须为 `[\\/]`**（Windows 反斜杠与正斜杠均匹配）。运行生产 Revit 2022 不阻止隔离 2025 安装；QA Revit 2025 正在运行时必须阻止覆盖 2025；路径无法识别时保守拒绝。曾发生 `[\/]` 误匹配 Bug，已修复并经 Windows 路径回归 25/25 静态通过。
- 最新补证：`VERSIONGATE_REGEX_FIX_EVIDENCE(1).zip`，9,724 字节，SHA256 `55418B30C07D5D650981AFAC5AB9BD24A6354BF77CA2382739A8B80E0E8AAC8E`，CRC 10/10、内部 SHA 9/9。独立 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File` 子进程实测 Revit 2025（PID 8476）运行中：Install/Uninstall 的 ExitCode **都为 1**，明确提示目标版本正在运行，DLL 和 `.addin` SHA 不变；生产 Revit 2022 未干预。
- 老 QA `install_blocked=false`、`uninstall_blocked=false` 由于测试脚本没有捕捉内部 `exit 1`；**原始 JSON 保留历史记录**，后续子进程证据已补正。回退门禁旧 QA 为 PASS。
- **版本回退的 v1/v2 测试 DLL 哈希相同**，已证明 manifest/备份/目录交换，不等于不同 DLL 二进制回退实测通过。
- **CLI 阶段冻结**，不继续改产品代码。GUI 安装/更新/恢复/卸载按钮实际点击仍需一次隔离实测；没有需要时不再进行其它重编译。

## 5. 当前 GitHub PR 链（交接写入前）

| Draft PR | 功能 | Head |
| --- | --- | --- |
| #4 | RC3J-HF2 Ribbon / 2D 几何工具 | `31a091dabd4b67cf1de5293357b7c75555fa6b68` |
| #5 | Exchange P1 源对象/ElementId 审计 | `99d493b99473ddcaebefc412ec031bf80655ad82` |
| #6 | Exchange P2 Hatch polyline | `a828c8e93fcb6e57885dc3caca5bbcc58e00ab00` |
| #7 | Exchange P3 Join 失败修复 | `259776749934feb64df2e9381670d19f17b450f0` |
| #8 | Exchange P4 非绑定整圆 | `aef69c78f89a1df67a86b745117598b9bfe3aad6` |
| #9 | Exchange P5 Autojoin + 独立报告 | `134a77a3d0d387212df0b29e40e68ecaf6c055a7` |
| #10 | PersonalPilot 数据保留、备份、回退 | `b291597a2e47b35bc3e63891b868972489ffaefc` |
| #11 | VersionGate 目标年份识别、Regex 修复、CLI 验收 | `1eca1cf28b0c50cc21fa5cd0bfaed898e13c9a62` |

这些 PR 彼此串联，**从最新 PR #11 Head 检出完整源码**，不要按旧 `README.md`、`PROJECT.md` 的早期 RC 文案误判当前状态，也不要把旧 `main` 当成最新开发基线。

## 6. 下一步明确安排（按优先级）

1. **P0 新对话核对 Git**：查看 PR #11 最新 Head、Draft 和未合并状态，读本交接和两份验收文件；不重复问已经知道的问题。
2. **P1 GUI 最短实测**：Gemini 在隔离 Revit 2025 QA 与测试 APPDATA 中实际点击中文安装器的安装→更新→恢复上一版→卸载。检查备份、manifest、DLL、个人数据哈希，必要时用**两个真正不同 SHA 的测试 DLL**核对版本恢复。生产 Revit 2022 继续运行且不受影响。只需一轮、只修真实失败点。
3. **P2 个人试用版打包**：通过 GUI QA 后复用现有 `tools/Package-Installer.ps1`，明确标记为 **PersonalPilot / TEST / Revit 2025**，整理安装/回退说明、文件 SHA 与限制列表。不得因为安装包名里含 4.0.0 就声称正式发布；不建议马上合并 main。
4. **P3 日常使用**：优先用户实测最常用的 HF2 和已完成工具，有故障再定点修。Exchange 29 个 HATCH/1 DIM/1ED 漂移按已知问题保留。
5. **P4 后续正式版**：其它模块逐项运行验收、2021–2026 真实运行兼容性、YangCAD 端到端联调、全功能发布清单，再讨论正式 4.0.0。**目前 FORMAL RELEASE NO-GO**。

验收标签要分开写 `STATIC PASS`、`BUILD GO`、`2025 LIMITED GO`、`CLI INSTALL GO`、`GUI NOT_TESTED`、`FORMAL RELEASE NO-GO`。不要重新开启 Exchange 无限补丁或重新跑所有已通过的六版本编译。

## 7. 新对话提示词（可直接复制）

请接手我的个人项目 **YangTools 4.0.0 / PersonalPilot**，中文交流，尽量用已有代码快速完成可用版本。仓库是 https://github.com/5788324/YANG-TOOLS_REVIT ，当前**唯一最新开发主线**是 Draft PR #11：https://github.com/5788324/YANG-TOOLS_REVIT/pull/11 。

**第一步请直接用 GitHub 读取** `docs/HANDOFF_20261118_YANGTOOLS_PERSONAL_PILOT.md`，再阅读 `docs/YANGTOOLS_EXCHANGE_P5_INDEPENDENT_ACCEPTANCE_20261118.md`、`docs/YANGTOOLS_PERSONAL_PILOT_VERSIONGATE_REGEX_ACCEPTANCE_20261118.md`，核对 PR Head 与 main，简短确认项目现状。旧 main/README 不代表最新源码。

已完成：Revit 2025 Ribbon 10 面板可加载；HF2 三项 2D 工具已有验收；Exchange P5 六版本编译 GO、2025 真实 C4 部分转换成功并 Undo，但仍有 29 个 HATCH、1 个 DIM 未恢复及 `1ED` 最终偏移 0.18755 mm，暂时冻结，不再主动 P6/P7。PersonalPilot CLI 安装→更新→回退→卸载及目标年份进程保护在隔离 Windows QA 已通过，生产 Revit 2022 正运行时也能单独测试 Revit 2025。**尚未实测中文 GUI 安装/回退按钮，以及不同 DLL 哈希的回退。** 正式 4.0.0 仍 NO-GO。

**接下来优先：安排最短的中文 GUI 2025 隔离实测，通过后整理 Revit 2025 PersonalPilot TEST 个人试用安装包。** 不要主动重写架构、扩展功能、反复测试已通过的 Exchange 或无条件合并 main。务必保护生产 Revit 2022/2023 Addins、原 DWG/RVT、个人设置和 YangCAD。你负责 GitHub 开发与验收审查，Gemini 可负责 Windows/Revit 真机执行。对无法在本会话直接访问的旧 E: 路径和 ZIP，不要臆测已经读到，需要时让我上传证据。
