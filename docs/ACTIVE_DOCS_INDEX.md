# Active Documentation Index

This file is the working index. Historical acceptance and iteration records remain available but are not the first-read path.

## 当前项目状态｜YangTools 4.0.0 / PersonalPilot · 2026-11-18

- **开发基线**：[Draft PR #11](https://github.com/5788324/YANG-TOOLS_REVIT/pull/11)，以其最新 Head 作为工作入口。本次审查基线 `f434792ed99c20b202bc4be787596cb7325953f6`，`main` 为 `bd589ed87d76ebc7b0595072e98fcaa419f74d2c`。提交留在 Draft 分支，合并 main 需用户授权。
- **试用范围**：Revit 2025.4、PersonalPilot TEST、CLI。现有证据支持 Ribbon、HF2 三项及隔离 CLI 生命周期与目标年份进程门禁。GUI 点击和不同 DLL 内容回退按用户安排暂缓。正式 4.0.0 暂未达到发布条件。
- **源码审查结果**：执行仓库原有静态入口，Draft 基线 63/72 套通过，main 69/69 套通过。Draft 的失败项涉及 7 套旧 Ribbon 名称、提示或示例断言，以及 2 套要求旧助手清理、自动部署或自动删除旧 Addin 的断言。保持现有安装行为，相关模块改动时一并更新对应断言。专项安装器 25/25、HF2 命令 17/17、Exchange P5 31/31。此次审查未运行 Windows/Revit。
- **2025 CLI TEST 已打包**：`YangTools-4.0.0-PersonalPilot-TEST-Revit2025-CLI.zip`，来源 `428643cd608a8a1b36a686328da19cf13608e23e`，使用干净工作目录及 .NET SDK 8.0.422 交叉编译 Windows x64 目标。编译 391 个警告、0 个错误。包仅含 2025 payload，检查了 DLL 架构、WPF 资源、manifest、依赖、原版 CLI 脚本和全部 118 项文件哈希。含中文安装和回退说明。ZIP SHA-256 `8719F902230D6BEF455E14AB48D236F0BA73942C2E20CD16F824EDC6ED04AE90`。2026-11-18 用户反馈安装成功并提供 Revit 截图，截图可见中文 YangTools Ribbon 及工具按钮，记录为试用环境首次加载通过。截图未显示具体 Revit 补丁号、实际加载 DLL 的路径或哈希，也未验证各工具执行。
- **已知试用行为**：Revit 插件启动时仍会清理此前已弃用的 AI/摸鱼助手目录和配置、历史文件。CLI 安装器本身保留用户数据。试用使用隔离 Windows/Revit 2025 配置，保护生产 Revit 2022/2023、生产 Addins、原始 DWG/RVT 和 YangCAD。
- **后续顺序**：进入 Revit 2025 个人试用，用图纸副本按实际需求使用工具，遇到问题再定点处理。Exchange P5 保持 LIMITED GO，记录 29 个 HATCH、1 个标注和 0.18755 mm 几何偏移。其余年份、MCP、YangCAD 联动按明确需求排期。

交接与证据：[PersonalPilot 交接](HANDOFF_20261118_YANGTOOLS_PERSONAL_PILOT.md) · [CLI 安装器验收](YANGTOOLS_PERSONAL_PILOT_VERSIONGATE_REGEX_ACCEPTANCE_20261118.md) · [Exchange P5 验收](YANGTOOLS_EXCHANGE_P5_INDEPENDENT_ACCEPTANCE_20261118.md)

## 代码与界面审查｜2026-11-18

审查基线 `ebf248b36c6b0748fbf7c1c24e36f7c97eb2c0c0`。Revit 2025 PersonalPilot 进入个人试用。现有分模块服务、WPF 共享主题、Revit 事务与撤销能力可继续使用。维护压力集中在资产管理（约 1,900 行代码后置）、族实例（约 1,600 行代码后置）和批量任务引擎（约 1,800 行）。优先在修改具体功能时减少重复校验和异常包装，保留防止写错文档、级联删除、覆盖文件和批量写入一半的保护。

| 优先级 | 发现 | 证据与处理范围 |
| --- | --- | --- |
| P0 · 2027 编译 | 用 .NET SDK 10.0.100 构建 2027，默认解析到 API 2027.4.0，显式使用 API 2027.0.0 复核，两次均 10 个错误。钢筋创建仍用 `RebarHookOrientation`，自由形钢筋相关调用使用旧 `CreateFreeForm` 签名。 | [渐变钢筋](../src/YangTools.Revit/Core/Rebar/RebarGradientSetService.cs#L183)、[形状驱动钢筋](../src/YangTools.Revit/Core/Rebar/RebarShapeDrivenCreateService.cs#L76)。按目标版本适配调用并固定 API 版本，保留 2021–2026 路径。 |
| P1 · CAD 转图 | 「重新扫描」会用已保存配置重建表格，丢弃当前未保存的映射。只转换非标注对象时，清空尺寸界线样式也会阻止转换，服务实际只在处理标注时才用这个设置。 | [重新扫描](../src/YangTools.Revit/UI/CadToRevitWindow.xaml.cs#L63)、[开始转换](../src/YangTools.Revit/UI/CadToRevitWindow.xaml.cs#L246)。只改界面状态和条件，不改 P5 几何。 |
| P1 · 族实例 | 「选择图元」先隐藏窗口，再提交 Revit 外部事件。事件未获接受且没有待处理请求接手，或因文档切换被跳过时，没有恢复窗口的路径。 | [选择图元](../src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml.cs#L1121)、[图框换型已有处理](../src/YangTools.Revit/UI/SheetManagerWindow.xaml.cs#L808)。这是代码路径审查，实际触发频率待试用确认。 |
| P2 · 窗口布局 | 族实例窗口最小宽度 1160 WPF DIP 且使用自绘标题栏，小屏幕或高缩放下会超出工作区。CAD 转图将 `DetailCurve`、`Reference` 等 API 实现术语直接放进操作界面。 | [族实例布局](../src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml#L1)、[CAD 转图文案](../src/YangTools.Revit/UI/CadToRevitWindow.xaml#L149)。优先修布局和主操作反馈，复用现有主题。 |

兼容范围按实际 Revit 版本记录。2021–2026 有历史编译证据，2025.4 有当前试用加载记录。2025.5、2026.5 已转入 .NET 10，当前项目的 2025/2026 配置仍固定 .NET 8 API，后续须单独验证这些更新版。[Autodesk .NET 10 迁移说明](https://help.autodesk.com/view/NAV/2026/ENU/?caas=caas/sfdcarticles/sfdcarticles/Revit--Requirements-for-products-affected-by-the-Microsoft--NET-10-transition.html)

后续需求按一个功能一个行为改动交付，优先跑现有相关检查与目标版本编译。单元格修改的输入错误放在原位提示，常规成功用状态行反馈，删除、覆盖或影响整批时保留确认。此次只记录审查，未改功能代码，也未运行 Windows 窗口交互测试。

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
