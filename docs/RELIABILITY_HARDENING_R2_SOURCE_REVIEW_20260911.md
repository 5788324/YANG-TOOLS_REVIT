# YangTools 3.1.38 Reliability Hardening R2｜独立源码复审

日期：2026-09-11
父候选：`YangTools_Project_3.1.38_Source_RELIABILITY_HARDENING_STATIC_20260911.zip`
父 SHA-256：`7F343F4CF70DEE248CA43FE3B2878A8A575101653E4AC4D8C8CED9D0500263FB`

## 结论

F01、F02、F04 首版实现的关键控制流与报告一致；F03、F05 仍有两个源码级残留，已在 R2 小范围修复。未进入下一阶段 UI/MCP 架构迁移。

## R2-01｜F05 日志字段与实际实现不一致

首版 `DiagnosticLog.WriteProductOperation` 注释/设计写 `OperationId/BatchRunId`，实际 payload 只有 `OperationId`。Batch 只有一个 `runId`，并把它作为 `operationId` 传入，因此 Windows 日志无法同时证明操作与批次两个身份。

修复：
- `WriteProductOperation(..., string? batchRunId = null)`；
- payload 显式输出 `OperationId:` 与 `BatchRunId:`；
- Batch 启动时分别生成 `operationId` 和 `batchRunId`；
- START / PREFLIGHT_FAILED / LINKS / EXPORT / FINAL / EXCEPTION 均携带同一 BatchRunId。

## R2-02｜F03 transaction truth 尚未覆盖 FamilyManager 旧路径

首版已修公共 helper 和 Family 参数单元格 readback，但 `FamilyManagerWindow` 仍有六处直接 `t.Commit()` 后继续成功路径；`LevelModifierCommand`、`DimTextOverrideCommand` 也会忽略 helper 返回状态。

修复：
- 六处 FamilyManager Transaction 都要求 `Commit() == TransactionStatus.Committed`，否则抛出失败；
- LevelModifier / DimTextOverride 检查 helper 返回值，非 Committed 直接 `Result.Failed`。

## 静态复核

- `Test-ReliabilityHardening.py`：43/43 PASS；
- 全部 `tools/Test-*.py`：19/19 suites PASS；
- `Test-UiPanelsStatic.py`：153 C# / 42 XAML / 31 Ribbon / 0 error / 0 warning。

当前容器没有 `dotnet/csc/mcs`，所以没有 C# Build 和 `.NET BatchFileCommitFaultHarness` 结果；Revit Runtime 也未执行。

## 状态

`SOURCE STATIC PASS / WINDOWS BUILD + .NET HARNESS + REVIT RUNTIME REQUIRED / RELEASE NO-GO`
