# Active Documentation Index

This file is the working index. Historical acceptance and iteration records remain available but are not the first-read path.

## 当前项目状态｜YangTools 4.0.0 / PersonalPilot · 2026-11-18

- **开发基线**：[Draft PR #11](https://github.com/5788324/YANG-TOOLS_REVIT/pull/11)，以其最新 Head 作为工作入口。本次审查基线 `f434792ed99c20b202bc4be787596cb7325953f6`，`main` 为 `bd589ed87d76ebc7b0595072e98fcaa419f74d2c`。提交留在 Draft 分支，合并 main 需用户授权。
- **试用范围**：Revit 2025.4、PersonalPilot TEST、CLI。现有证据支持 Ribbon、HF2 三项及隔离 CLI 生命周期与目标年份进程门禁。GUI 点击和不同 DLL 内容回退按用户安排暂缓。正式 4.0.0 暂未达到发布条件。
- **源码审查结果**：执行仓库原有静态入口，Draft 基线 63/72 套通过，main 69/69 套通过。Draft 的失败项涉及 7 套旧 Ribbon 名称、提示或示例断言，以及 2 套要求旧助手清理、自动部署或自动删除旧 Addin 的断言。保持现有安装行为，相关模块改动时一并更新对应断言。专项安装器 25/25、HF2 命令 17/17、Exchange P5 31/31。此次审查未运行 Windows/Revit。
- **2025 CLI TEST 已打包**：`YangTools-4.0.0-PersonalPilot-TEST-Revit2025-CLI.zip`，来源 `428643cd608a8a1b36a686328da19cf13608e23e`，使用干净工作目录及 .NET SDK 8.0.422 交叉编译 Windows x64 目标。编译 391 个警告、0 个错误。包仅含 2025 payload，检查了 DLL 架构、WPF 资源、manifest、依赖、原版 CLI 脚本和全部 118 项文件哈希。含中文安装和回退说明。ZIP SHA-256 `8719F902230D6BEF455E14AB48D236F0BA73942C2E20CD16F824EDC6ED04AE90`。本 ZIP 的 DLL 尚待隔离 Revit 2025.4 首次加载确认。
- **已知试用行为**：Revit 插件启动时仍会清理此前已弃用的 AI/摸鱼助手目录和配置、历史文件。CLI 安装器本身保留用户数据。试用使用隔离 Windows/Revit 2025 配置，保护生产 Revit 2022/2023、生产 Addins、原始 DWG/RVT 和 YangCAD。
- **后续顺序**：先用已交付的 CLI TEST 在隔离 Revit 2025.4 确认 Ribbon 加载，再按实际需求推进单个模块。Exchange P5 保持 LIMITED GO，记录 29 个 HATCH、1 个标注和 0.18755 mm 几何偏移。其余年份、MCP、YangCAD 联动按明确需求排期。

交接与证据：[PersonalPilot 交接](HANDOFF_20261118_YANGTOOLS_PERSONAL_PILOT.md) · [CLI 安装器验收](YANGTOOLS_PERSONAL_PILOT_VERSIONGATE_REGEX_ACCEPTANCE_20261118.md) · [Exchange P5 验收](YANGTOOLS_EXCHANGE_P5_INDEPENDENT_ACCEPTANCE_20261118.md)

## 历史设计和迭代资料
- `../README.md`
- `../PROJECT.md`
- `ARCHITECTURE_SLIMMING_AUDIT_20260923.md`
- `RELIABILITY_HARDENING_3.1.38_20260911.md`
- `REBAR_MODULE_PLAN_R00_20260911.md`
- `GR01_GRADIENT_REBAR_REQUIREMENTS_V0.3_20260918.md`
- `GR01_V1_EXPLICIT_MANAGED_FREEFORM_DESIGN_20260920.md`
- `GR01_V1_0_4_SPACING_POLICY_20260923.md`
- `REBAR_RIBBON_V1_0_3_CLEANUP_20260922.md`
- `REVIT_2021_2027_COMPATIBILITY_3.1.38.md`
- `MCP_2026_07_28_SDK_V2_EVALUATION_20260814.md`
- `ICON_DESIGN_GUIDE.md`
- `RIBBON_FEATURE_DEVELOPMENT_RULES.md`（新功能命名/说明/图标硬门禁）
- `RIBBON_FEATURE_CATALOG_RC3J.md`（36 项功能短名与操作方式）
- `RC3J_GEOMETRY_FIX_CANDIDATE_20261022.md`（2D 几何修复候选、边界与验收）

## Rule
New engineering decisions should update one current design/status document rather than adding another near-duplicate status file unless the file is an acceptance/evidence artifact.
