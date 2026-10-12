## 2026-09-22｜Rebar Ribbon V1.0.3：生产入口/测试入口分离 + 图标重做
- `钢筋工具区` 收敛为两个一级 Pulldown：`创建钢筋` 与 `钢筋测试`。
- `创建钢筋` 只保留已完成 Runtime/GUI 验收的 `渐变钢筋集`；其余 12 个 R00–R10 钢筋研发/诊断命令全部移入 `钢筋测试`。
- 13 个钢筋功能全部换成独立 16/32 图标，新增稳定的 `rebar_test` 分组图标；不再复用 `project_inspector` 占位图。
- Ribbon 属性新增可选 GroupLargeIcon/GroupSmallIcon，使 Pulldown 图标不依赖组内第一条可见命令。
- 不修改 GR01 Core/Spacing/Interpolation/CreateFreeForm/Transaction/MCP。
- 静态：Rebar Ribbon 87/87；GR01 V1.0.2 40/40；全部 36 Test suites / 0 failure；UI 201 C#/43 XAML/44 Ribbon/0 error/0 warning；MCP py_compile PASS。
- 当前：`REBAR RIBBON V1.0.3 STATIC GO / REVIT 2025 RIBBON+ICON SMOKE REQUIRED / PROJECT-WIDE RELEASE NO-GO`。

## 2026-09-11｜Reliability Hardening R2 源码复审补丁

在首版 F01–F05 静态候选上进行独立源码复审，发现两类不能带入 Windows 终验的残留，并已做小范围修复：

- F03：`FamilyManagerWindow` 仍有 6 处直接 `Commit()` 后继续成功流程；`LevelModifierCommand` / `DimTextOverrideCommand` 调用公共提交 helper 后仍无条件返回 `Result.Succeeded`。R2 已统一要求 `TransactionStatus.Committed`，未提交即失败，不再把 UI/命令返回值当模型真值。
- F05：`WriteProductOperation` 文档声称支持 `OperationId/BatchRunId`，实际 payload 只有 `OperationId`；Batch 又把 runId 直接当 operationId。R2 新增独立 `BatchRunId` 字段，并为一次 Batch 用户动作分别生成 `operationId` 与 `batchRunId`，所有 Batch 产品日志显式携带二者。
- `Test-ReliabilityHardening.py` 从 38 项扩展到 **43/43 PASS**；全量 `tools/Test-*.py` 仍为 **19/19 suites PASS**；UI static 153 C# / 42 XAML / 31 Ribbon / 0 error / 0 warning。
- 当前环境仍无 `dotnet/csc/mcs`，因此状态仍是 **SOURCE STATIC PASS / WINDOWS BUILD+FAULT-HARNESS+REVIT RUNTIME REQUIRED / RELEASE NO-GO**。
- 未混入 UI 七分组、MCP SDK v2、全功能 MCP 或安装器发布模式改造。

详细复审：`docs/RELIABILITY_HARDENING_R2_SOURCE_REVIEW_20260911.md`。

## 0A. 2026-09-11 最新交接：可靠性加固候选

Drive 后续审查 28/29/31 发现原版权候选仍有源码阻塞，因此旧“STATIC GO”不能作为发布结论。本轮已实现 F01–F05 第一批修复：

1. Batch 文件 commit/restore 独立服务，恢复失败保留备份；
2. Execute 强制全批 preflight；
3. transaction/group 只有 Committed 才成功，Family 参数写入 readback；
4. BatchRunRequest 深快照 + Busy 锁定；
5. OperationId/BatchRunId 产品自然日志。

新增 `tools/BatchFileCommitFaultHarness` 供 Windows 真 .NET 故障注入。静态 `19/19 suites PASS`，其中 Reliability `38/38`。当前环境未编译 C#，所以状态仅：`RELIABILITY FIX STATIC PASS / WINDOWS BUILD+RUNTIME REQUIRED / RELEASE NO-GO`。

审查 31 的长期方向：不整体重做；渐进模块化单体，稳定 featureId/operationId，不可变 Request、结构化 Result，GUI/MCP 共用 Service。下一批才整理 Family/Batch UI，之后 Ribbon/配置和全功能 MCP。

# YangTools 3.1.38 交接

日期：2026-09-02

## 0. 2026-09-02 最新交接补充：版权/签名候选

在原 3.1.38 FIX 基础上新增**非业务发布层**：

- Assembly/File/Informational Version 固定 3.1.38；
- Authors/Company/Product/Copyright = Yang / YangTools / Copyright © 2026 Yang；
- SystemSettings “关于”页显示版本、作者、版权、许可与签名证书；
- 许可：免费使用/分发/修改/二次开发；必须保留 YangTools + Yang 来源；未经授权禁止商业使用；
- 新增 LICENSE/NOTICE/THIRD_PARTY_NOTICES；
- 新增 Authenticode 签名脚本，但源码包内**绝不包含私钥/PFX**；
- 当前没有作者受信任 CA 私钥，因此正式可信签名尚未完成。Gemini 可用本地非导出自签名做签名+Revit load smoke。
- 新专项 `Test-CopyrightSigning.py = 47/47 PASS`；全量 static = **18/18 suites PASS**。
- 不要把版权层改动当成 Batch/Family Runtime 已通过；剩余终验仍是双模型 Batch、真实可见 UI 截图、产品 Handler/Service 日志。


## 1. 当前事实

正式稳定基线仍为：

`YangTools_Project_3.1.36_Source_CAD_TO_REVIT_FINAL_E2E_20260901.zip`

SHA-256：`D126F1A65F0DD4A74A1F97AD6F1CD3302309F77212E30BD7752666DE740CDBA4`

当前统一开发候选：**3.1.38**，合并三项：

1. PasteCAD Shared；
2. Family Management V2.1；
3. Batch Publisher V2。

状态：`STATIC GO / WINDOWS REVIT UNIFIED RETEST REQUIRED / RELEASE NO-GO`。

## 2. PasteCAD Shared

- 定位模式：Origin / Centered / Shared；
- Shared 使用 `ImportPlacement.Shared`；
- 不 Acquire/Publish，不改项目共享坐标；
- Shared API 成功与真实坐标正确必须分开验收；
- 工作副本、Transaction rollback、finally 清临时文件、成功选择 ImportInstance、一 Ctrl+Z 保持。

## 3. Family Management V2.1

### Family Manager
- Family/Type 使用计数；
- 未使用过滤；
- Trial Delete 安全清理；
- Family→Instance Manager；
- 多族 RFA；
- StableParameterKey。

### Family Instance Manager
- Scope Project / ActiveView / Selection；
- Type / Level filter；
- Level / Host / Pinned；
- batch ChangeTypeId；
- batch same-parameter + only-empty；
- 动态系统类别；
- StableParameterKey。

UI 硬门禁：Family 1280×780 Min1080×650；Instance 1360×800 Min1160×660；Grid Row30/Header36；固定列/动态列有 Width/Min/Max，最小窗口不能出现重叠、单字列或按钮多行挤压。

## 4. Batch Publisher V2

用户目标：多模型/云模型批量输出 NWC/IFC/DWG/PDF，并兼容 Revit 2021 PDF 打印。

### 模型来源
- 本地/网络/Desktop Connector RVT；
- 当前已打开模型；
- 当前会话已打开的 native Autodesk Docs/BIM 360 cloud model；
- cloud 保存 region/project/model GUID，并通过 Cloud ModelPath 重新打开；
- 不实现假的 ACC 云端浏览器。

### 跨模型资源
所有 View/SheetSet/DWG/PDF/Print setup 由每个目标 Document 重新解析，不再套当前模型对象；设置名输入允许公司统一命名，缺失在预检阶段返回。

### NWC
- Whole model / auto3D / current3D / named3D；
- shared/internal coords；
- links/properties/ElementId/room attrs/room geometry/divide levels；
- `IsNavisworksExporterAvailable`；
- staging + non-empty output gate。

### IFC
- Whole model / auto3D / named3D；
- version/base quantities/space boundary/wall-column splitting；
- `IsIFCAvailable`；
- Transaction 包裹；
- staging + non-empty gate。

### DWG
- all sheets / all printable views / current / named ViewSheetSet；
- target-document `ExportDWGSettings`；
- per-view export for deterministic primary filename；
- optional coordinate override / MergedViews；
- `IsDWGExportAvailable`；
- exact expected main DWG + non-empty gate；伴随文件/XRef 一起 commit。

### PDF Export
- only Revit 2022+；
- target-document ExportPDFSettings；
- combined: one PDF；separate: one-view `Combine=true` calls for deterministic names；
- Revit 2025+ `SetExportInBackground(false)`；
- output non-empty gate。

### PDF Print
- Revit 2021+；
- PrintManager / Select / InSession / PrintToFileName / SubmitPrint；
- combined/separate deterministic naming；
- wait actual file; safe replace；
- snapshot/restore print state；
- interactive drivers must be NOT_TESTED/USER_TEST_REQUIRED/FAIL, never fake PASS。

### Naming / output safety
Tokens：`{模型名} {模型文件名} {图纸号} {图纸名} {视图名} {序号} {日期}`。

Preflight predicts final primary paths across the entire batch. Same-batch collision is always forbidden even when Overwrite=true. Existing old files can be overwritten only when enabled and only after new output has succeeded. `UseDocumentSubfolders` follows user checkbox exactly.

## 5. 静态门禁

- Batch Publisher V2：99/99 PASS
- Batch Print PDF：32/32 PASS
- Family Management V2：110/110 PASS
- PasteCAD：19/19 PASS
- CAD→Revit：76/76 PASS
- all `Test-*.py`：17/17 suites PASS
- UI：151 C# / 42 XAML / 37 roots / 4 themes / 31 Ribbon / 0 error / 0 warning
- C# lexical：151/151 PASS

## 6. 实机必须验收

统一验收任务见：`docs/DEEPSEEK_GEMINI_3.1.38_UNIFIED_ACCEPTANCE_20260902.md`。

Gemini只能写 `TEST EXECUTION COMPLETE`，最终 GO/NO-GO 由 ChatGPT 审查 Drive 原始证据后决定。
