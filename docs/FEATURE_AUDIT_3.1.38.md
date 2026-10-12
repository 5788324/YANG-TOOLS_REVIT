## 2026-09-11｜Reliability Hardening R2 源码复审补丁

在首版 F01–F05 静态候选上进行独立源码复审，发现两类不能带入 Windows 终验的残留，并已做小范围修复：

- F03：`FamilyManagerWindow` 仍有 6 处直接 `Commit()` 后继续成功流程；`LevelModifierCommand` / `DimTextOverrideCommand` 调用公共提交 helper 后仍无条件返回 `Result.Succeeded`。R2 已统一要求 `TransactionStatus.Committed`，未提交即失败，不再把 UI/命令返回值当模型真值。
- F05：`WriteProductOperation` 文档声称支持 `OperationId/BatchRunId`，实际 payload 只有 `OperationId`；Batch 又把 runId 直接当 operationId。R2 新增独立 `BatchRunId` 字段，并为一次 Batch 用户动作分别生成 `operationId` 与 `batchRunId`，所有 Batch 产品日志显式携带二者。
- `Test-ReliabilityHardening.py` 从 38 项扩展到 **43/43 PASS**；全量 `tools/Test-*.py` 仍为 **19/19 suites PASS**；UI static 153 C# / 42 XAML / 31 Ribbon / 0 error / 0 warning。
- 当前环境仍无 `dotnet/csc/mcs`，因此状态仍是 **SOURCE STATIC PASS / WINDOWS BUILD+FAULT-HARNESS+REVIT RUNTIME REQUIRED / RELEASE NO-GO**。
- 未混入 UI 七分组、MCP SDK v2、全功能 MCP 或安装器发布模式改造。

详细复审：`docs/RELIABILITY_HARDENING_R2_SOURCE_REVIEW_20260911.md`。

# 2026-09-11 Reliability Audit Update

| 审查项 | 修复状态 | 静态 | Runtime |
|---|---|---|---|
| F01 文件提交恢复 | 已抽 `BatchFileCommitService`；恢复失败保留 backup | Reliability 38/38 | REQUIRED |
| F02 全批预检 | Execute 与 Preflight 共用全批逻辑 | PASS | REQUIRED |
| F03 transaction 真值 | 仅 Committed 成功 + Family readback | PASS | REQUIRED |
| F04 请求漂移 | BatchRunRequest 快照 + UI Busy lock | PASS | REQUIRED |
| F05 产品 provenance | OperationId/RunId + DLL SHA + doc/result | PASS | REQUIRED |

当前结论：`SOURCE FIX STATIC PASS / RELEASE NO-GO`。下一阶段 UI/Ribbon/MCP 不与本轮混包。

# YangTools 3.1.38 功能审查

## 功能矩阵

| 功能 | 3.1.38 | 静态 | 当前实机 |
|---|---|---|---|
| 从 CAD 粘贴 Shared | 新增 Shared placement | 19/19 | RETEST REQUIRED |
| CAD→Revit | 无业务改动 | 76/76 | 3.1.36 Revit 2024 GO |
| 族文档管理 V2.1 | 大幅增强 | Family V2 110/110 | RETEST REQUIRED |
| 族实例管理 V2.1 | 大幅增强 | Family V2 110/110 | RETEST REQUIRED |
| 批处理与云链接 V2 | 重做 | Batch V2 99/99 | RETEST REQUIRED |
| PDF Print legacy contracts | 兼容保留 | 32/32 | RETEST REQUIRED |
| 其它既有功能 | 无业务改动 | all tests PASS | 不扩大既有状态 |

## 核心审查

### StableParameterKey
动态参数写入优先 GUID/BIP/PID，名称仅 fallback；重复同名歧义拒绝写。

### Family usage / destructive actions
FamilyUsageService 集中统计；清理未使用 Type 前重新扫描 + Trial Delete，级联异常则阻止。

### Batch Publisher V2
旧设计不适合多模型：当前 Document 的 View/Setup 被错误复用于其它模型、命名不可控、云模型语义不足、无全批预检。3.1.38 已改为逐 Document 资源解析 + 全批 output plan + staging/safe replace。

PDF Export 2022+ 与 Print PDF 2021+ 明确分离；NWC/IFC/DWG 用 OptionalFunctionalityUtils 检查能力。

### UI
Family 两窗口和 Batch Publisher 都有默认/最小尺寸、Grid row/header、关键列宽/MinWidth 硬契约；实机必须在四主题 + 最小尺寸截图验证。

## 禁止误判

- `Document.Export` 返回 true ≠ 文件可用：必须检查实际文件存在、非空、名称与 SHA。
- NWC Exporter 缺失 = NOT_INSTALLED，不是 FAIL 也不是 PASS。
- 2021 无原生 PDF Export；只能实际 Print PDF。
- 打印驱动弹 Save As = 无人值守未通过。
- 2025 Build/Deploy ≠ Runtime PASS。
- Cloud 只拿 Desktop Connector 本地路径 ≠ native cloud model runtime。
- 窗口打开 ≠ UI layout PASS。

## 版权 / 版本 / 发布签名

- 版本元数据：3.1.38 / 3.1.38.0；Authors/Company=Yang；Product=YangTools；Copyright © 2026 Yang。
- About 页明确允许非商业使用/分发/修改/二次开发，同时要求保留原项目/原作者/许可，禁止未经授权商用。
- Authenticode：源码提供签名和验证脚本；正式 Release 只有在 `Get-AuthenticodeSignature.Status == Valid` 时才能写“受信任签名 PASS”。
- 本地 self-signed 可以用于开发/加载 smoke，但不能冒充公共可信签名。
- 私钥/PFX/密码不得进入源码、Drive 公开交付或安装包。
