# TASK-003

## 任务名称

实现 `OperationLogger`，固定 MCP 操作 JSONL 日志。

## 执行者

```text
DeepSeek
```

## 目标

```text
每次 MCP 调用都写入 logs/operations/YYYY-MM-DD.jsonl，成功失败都要记。
```

## 允许修改文件

```text
- src/YangTools.Revit/Mcp/*
- src/YangTools.Revit/Utils/OperationLogger.cs
- logs/operations/README.md（如字段有必要微调）
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
```

## 验收

1. 成功请求写日志。
2. 失败请求写日志。
3. 写操作包含 dryRun 字段。
