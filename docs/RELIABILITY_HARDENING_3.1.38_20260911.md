# YangTools 3.1.38｜可靠性加固 F01–F05

日期：2026-09-11
状态：SOURCE STATIC PASS / WINDOWS BUILD+RUNTIME REQUIRED / RELEASE NO-GO
依据：Drive `28_CHATGPT_CODE_STRUCTURE_UI_AUDIT_20260907.md`、`29_YANGTOOLS_OPTIMIZATION_PLAN_20260907.md`、`31_CONTINUOUS_FEATURE_ARCHITECTURE_REVIEW_20260908.md`。

## 目标

本轮只处理源码审查新增的 F01–F05 可靠性阻塞，不混入 Family/Batch 全面 UI 重构、Ribbon 七面板迁移或全功能 MCP 迁移。版本仍为 3.1.38；正式稳定版仍为 3.1.36 FINAL_E2E。

## F01｜批量输出提交与恢复

新增 `Core/BatchTasks/BatchFileCommitService.cs`，把最终文件提交/旧文件恢复从 `BatchTaskEngine` 抽成不依赖 Revit API 的文件服务。

行为：
- staged 输出先映射最终目标；
- 检查同批 reservation 与 overwrite；
- 已有旧输出先移动到唯一 backupFolder；
- 新输出全部成功后才清理 backup；
- 新输出任一移动失败：删除本轮已提交的新文件并逐项恢复旧文件；
- 恢复失败或目标被占用：**保留 backupFolder**，返回逐文件恢复消息与路径；
- 不再出现“恢复失败但 finally 无条件删除最后备份”的路径。

新增 Windows/.NET 故障注入 harness：
`tools/BatchFileCommitFaultHarness/`

Harness 直接链接真实 `BatchFileCommitService.cs`，使用真实临时目录/文件，并用代理文件系统注入：
1. 第二个 staged 文件移动失败，旧输出全部恢复且 SHA 不变；
2. staged 失败 + 第一个旧文件恢复失败，backupFolder 必须保留且包含可恢复数据。

该 harness 必须由 Windows 执行器 `dotnet run -c Release` 真运行，Python 模拟不能替代。

## F02｜执行入口强制全批预检

`BatchTaskWindow` 新增 `RunFullExportPreflight(BatchRunRequest)`。

- “预检”按钮与“执行批处理”调用同一全批逻辑；
- 共享 `plannedOutputReservations`；
- 执行入口在任何 Link/Export 前先完成所有已选目标模型的路径/设置/冲突检查；
- 确定性重名冲突必须 0 新输出，不依赖用户先手动点“预检”。

## F03｜事务结果与 UI 真值

`TransactionHelper.ShowSuccessAndCommit` 两个重载改为返回 `TransactionStatus`：
- `Transaction.Commit()` 只有 `Committed` 才成功提示；
- `TransactionGroup.Assimilate()` 只有 `Committed` 才成功提示；
- 异常回滚后不再继续成功提示。

Family 类型参数/实例参数旧单元格编辑：
- 检查 `Commit() == TransactionStatus.Committed`；
- 成功后从实际 Parameter readback 更新 UI；
- 未提交/回滚不能把输入值当模型真值。

## F04｜批处理请求快照与运行锁定

新增：
- `IBatchTaskOptions`
- `BatchRunOptions`
- `BatchRunRequest.Capture(...)`

执行时：
- 深复制批处理选项、目标模型条目与 Link 条目；
- `BatchTaskEngine` 只依赖 `IBatchTaskOptions`，不直接读取活的 WPF ViewModel；
- `RootLayout.IsHitTestVisible=false` 锁定运行期输入；
- 当前运行不会因 Dispatcher 处理其它 UI 事件而让后续模型读取到不同设置。

本轮不实现强制中断正在进行的 Autodesk Export；未来取消只在安全边界停止下一项。

## F05｜产品执行 provenance

`DiagnosticLog` 新增 `WriteProductOperation`：
- OperationId / BatchRunId
- state
- ProcessId
- executing assembly SHA-256
- Document title/path/cloud
- detail/result

记录真实链：
- Batch：`IExternalCommand → modal ShowDialog → BtnExecute_Click → BatchTaskEngine`，**不伪造 ExternalEvent**；
- PasteCAD：Command → Window → Transaction → `Document.Import`，记录 ImportInstanceId / placement / source；
- Family modeless：记录 ExternalEvent operationId、事务组结果、批量换型/批量参数/安全清理输入与结果。

日志是产品自然执行输出，不允许事后手写 PASS 摘要替代。

## 与 31 号架构复查的关系

本轮不整体重做项目。后续遵循“按功能组织的模块化单体”：
- 新功能逐步采用稳定 featureId/operationId；
- GUI/MCP 共用 Request/Result/Service；
- Service 不依赖 WPF Window/ViewModel/TaskDialog/交互 Pick；
- Ribbon/MCP/覆盖矩阵最终从同一 OperationDescriptor 派生；
- 现有成熟功能渐进迁移，不一次性 MVVM 重写或拆几十个 DLL。

## 静态结果

新增 `Test-ReliabilityHardening.py`：43/43 PASS（R2 增加 F03/F05 残留回归门禁）。
全项目 `tools/Test-*.py`：19/19 suites PASS。
UI 静态：153 C# / 42 XAML / 31 Ribbon / 0 error / 0 warning。

当前 Linux 环境没有 dotnet/csc/mcs，因此以上不是 C# Build 或 Revit Runtime 证明。

## R2 源码复审补充

首版实现复审后补齐：
- `FamilyManagerWindow` 六处剩余 Transaction 必须确认 `Committed`；
- `LevelModifierCommand` / `DimTextOverrideCommand` 必须把 helper 返回的非 `Committed` 状态传播为 `Result.Failed`；
- `DiagnosticLog.WriteProductOperation` payload 必须有独立 `BatchRunId`；
- Batch 一次运行同时生成并记录独立 `operationId` 与 `batchRunId`。

静态专项更新为 43/43 PASS；这些修改仍需 Windows C# Build、.NET fault harness 与 Revit 产品链实测。
