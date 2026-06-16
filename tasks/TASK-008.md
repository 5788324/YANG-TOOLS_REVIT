# TASK-008

## 任务名称

实现 `revit.parameter.set`。

## 执行者

```text
DeepSeek
```

## 目标

```text
批量修改当前选择元素指定参数，必须支持 dryRun，并在正式执行后支持 Ctrl+Z 撤销。
```

## 允许修改文件

```text
- src/YangTools.Revit/Tools/ParameterSetTool.cs
- src/YangTools.Revit/Utils/TransactionRunner.cs
- src/YangTools.Revit/Utils/OperationLogger.cs
- docs/TOOL_INDEX.md
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
- docs/CURRENT_STATE.md（如状态变化）
```

## 验收

1. `dryRun=true` 不修改模型。
2. `dryRun=false` 使用 Transaction。
3. 返回 changedItems / failedItems。
4. MCP 操作写日志。
5. Revit 2024 实机可 Ctrl+Z 撤销。
