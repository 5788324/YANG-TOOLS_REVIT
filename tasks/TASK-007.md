# TASK-007

## 任务名称

实现 `revit.parameter.get`。

## 执行者

```text
DeepSeek
```

## 目标

```text
读取当前选择元素的参数名、参数值、StorageType、只读状态。
```

## 允许修改文件

```text
- src/YangTools.Revit/Tools/ParameterGetTool.cs
- docs/TOOL_INDEX.md
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
```

## 验收

1. 常见参数可返回。
2. 参数缺失时能返回 failedItems 或标准 error，不崩溃。
3. 返回统一格式。
