# TASK-003

## 任务名称

实现 `OperationLogger`，为每次 MCP 调用写入 `logs/operations/YYYY-MM-DD.jsonl`

## 执行者

```text
DeepSeek
```

## 目标

```text
无论请求成功还是失败，都统一记录 MCP 操作日志，为后续审计、排障和 dryRun / apply 区分提供基础。
```

## 允许修改文件

```text
- src/YangTools.Revit/Mcp/*
- src/YangTools.Revit/Utils/OperationLogger.cs
- logs/operations/README.md（如字段说明需要微调）
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
- logs/conversations/YYYY-MM-DD.md
- docs/CURRENT_STATE.md（如状态变化）
- docs/TOOL_INDEX.md（如 Tool 状态变化）
```

## 禁止修改文件

```text
- docs/AGENT_RULES.md
- docs/PROJECT_START_PLAN.md
- MCP 基础请求/返回协议
- 与本任务无关的 Tool 实现
```

## 验收

1. 成功请求会写入 `logs/operations/YYYY-MM-DD.jsonl`。
2. 失败请求会写入 `logs/operations/YYYY-MM-DD.jsonl`。
3. 日志至少包含：时间、tool、ok、error、warnings、dryRun、请求摘要。
4. 不把整个大型 body 原样无脑写入日志。
5. 不修改 MCP 协议。
6. 已更新 `docs/WORKLOG.md`。
7. 已更新 `tasks/HANDOFF_TO_CODEX.md`。
8. 已更新 `logs/conversations/YYYY-MM-DD.md`。
9. 如状态变化，已更新 `docs/CURRENT_STATE.md`。
10. 如 Tool 状态变化，已更新 `docs/TOOL_INDEX.md`。

## 备注

```text
本任务先做最小闭环日志能力，不扩展到复杂审计系统，不做云端上传，不做数据库。
```
