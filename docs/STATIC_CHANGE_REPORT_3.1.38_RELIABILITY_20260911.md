# 3.1.38 Reliability Hardening｜Static Change Report

日期：2026-09-11
输入：`YangTools_Project_3.1.38_Source_COPYRIGHT_SIGNING_HANDOFF_STATIC_20260902.zip`
输入 SHA：`ECC53143B29047C3FB7B169A3FB2CFA0B6390F805B5BD050E7959DF8AACDB43E`

## 业务源码变更

- `src/YangTools.Revit/Core/BatchTasks/BatchFileCommitService.cs`（新增）
- `src/YangTools.Revit/Core/BatchTasks/BatchTaskEngine.cs`
- `src/YangTools.Revit/Models/BatchTask/BatchRunRequest.cs`（新增）
- `src/YangTools.Revit/Models/BatchTask/BatchTaskViewModel.cs`
- `src/YangTools.Revit/UI/BatchTaskWindow.xaml`
- `src/YangTools.Revit/UI/BatchTaskWindow.xaml.cs`
- `src/YangTools.Revit/Core/TransactionHelper.cs`
- `src/YangTools.Revit/Core/DiagnosticLog.cs`
- `src/YangTools.Revit/Core/RevitEventHandler.cs`
- `src/YangTools.Revit/Commands/PasteCadCommand.cs`
- `src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml.cs`
- `src/YangTools.Revit/UI/FamilyManagerWindow.xaml.cs`

## 测试新增

- `tools/Test-ReliabilityHardening.py`
- `tools/BatchFileCommitFaultHarness/BatchFileCommitFaultHarness.csproj`
- `tools/BatchFileCommitFaultHarness/Program.cs`

## 范围边界

本轮只修 F01–F05。未执行：
- Family/Batch 全面 UI 重布局；
- Ribbon 七面板迁移；
- FeatureDescriptor/OperationDescriptor 全量导入；
- MCP SDK v2 迁移；
- 全功能 MCP coverage 实现；
- 安装器发布模式修复。

这些保留到后续独立批次，避免可靠性修复与大 UI/架构改造混在一个候选中。

## R2 独立复审补丁

在上述首版可靠性候选上继续源码审查，新增修改：

- `Core/DiagnosticLog.cs`：产品日志新增独立 `BatchRunId` 字段；
- `UI/BatchTaskWindow.xaml.cs`：Batch 用户动作使用独立 `operationId` + `batchRunId`，所有产品日志同时传递；
- `UI/FamilyManagerWindow.xaml.cs`：六处剩余 `Transaction.Commit()` 检查 `Committed`；
- `Commands/LevelModifierCommand.cs`：公共 helper 非 `Committed` 时返回 `Result.Failed`；
- `Commands/DimTextOverrideCommand.cs`：同上；
- `tools/Test-ReliabilityHardening.py`：38 → 43 项，增加上述残留的静态回归门禁。

R2 全量静态 19/19 suites PASS；尚无 C# Build / .NET harness / Revit Runtime PASS。
