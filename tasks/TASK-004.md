# TASK-004

## 任务名称

实现 `TransactionRunner`，统一写操作事务边界。

## 执行者

```text
DeepSeek
```

## 目标

```text
统一封装 Transaction / TransactionGroup，保证失败 RollBack，成功后可 Ctrl+Z 撤销。
```

## 允许修改文件

```text
- src/YangTools.Revit/Utils/TransactionRunner.cs
- src/YangTools.Revit/Tools/ParameterSetTool.cs
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
- docs/CURRENT_STATE.md（如状态变化）
```

## 验收

1. 正式写操作进入 Transaction。
2. 异常时 RollBack。
3. Revit 2024 实机可 Ctrl+Z 撤销。
