# TASK-005

## 任务名称

实现 `revit.status`。

## 执行者

```text
DeepSeek
```

## 目标

```text
返回 Revit 版本、当前文档状态、MCP 状态和已注册 Tool 数量。
```

## 允许修改文件

```text
- src/YangTools.Revit/Tools/RevitStatusTool.cs
- src/YangTools.Revit/Mcp/*
- docs/TOOL_INDEX.md
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
```

## 验收

1. 有活动文档和无活动文档都能返回。
2. 不依赖 Python。
3. 返回统一格式。
