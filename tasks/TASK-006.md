# TASK-006

## 任务名称

实现 `revit.selection.read`。

## 执行者

```text
DeepSeek
```

## 目标

```text
读取当前选择元素基础信息，用作后续 parameter.get / set 的前置能力。
```

## 允许修改文件

```text
- src/YangTools.Revit/Tools/SelectionReadTool.cs
- docs/TOOL_INDEX.md
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
```

## 验收

1. 返回 ElementId、Category、Name、FamilyName、TypeName、DocumentName。
2. 无选中元素时返回标准空结果或标准错误，不崩溃。
