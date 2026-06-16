# TASK-001

## 任务名称

统一 `McpRequest` / `McpResponse` 结构，并打通 `/mcp/` 基础入口

## 执行者

```text
DeepSeek
```

## 目标

```text
在不扩展协议的前提下，固定 v0.1 的 MCP 请求与返回结构，让后续 Tool 都走同一入口。
```

## 允许修改文件

```text
- src/YangTools.Revit/App.cs
- src/YangTools.Revit/Mcp/*
- src/YangTools.Revit/Utils/JsonUtils.cs
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
- MCP 基础协议字段名
- 与本任务无关的 Tool 文件
```

## 验收

1. 能解析 `tool` 与 `args`。
2. 空 `tool` 和未知 `tool` 返回标准错误。
3. 返回结构固定为 `ok/tool/data/error/warnings`。
4. 已更新 `docs/WORKLOG.md`。
5. 已更新 `tasks/HANDOFF_TO_CODEX.md`。
6. 已更新 `logs/conversations/YYYY-MM-DD.md`。
7. 如状态变化，已更新 `docs/CURRENT_STATE.md`。
8. 如 Tool 状态变化，已更新 `docs/TOOL_INDEX.md`。

## 备注

```text
本任务不允许顺手做 HTTP 之外的新通信方式。
如果涉及后续 Revit API 调用规划，不能越过 ExternalEvent / 主线程边界。
```
