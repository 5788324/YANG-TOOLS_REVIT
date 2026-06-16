# CURRENT_TASK

当前任务只能有一个。DeepSeek / Gemini 必须严格按本文件执行。

## 任务编号

```text
TASK-001（待 Codex 正式下发）
```

## 任务名称

```text
统一 McpRequest / McpResponse 结构，并打通 /mcp/ 基础入口。
```

## 任务目标

```text
固定 v0.1 MCP 请求与返回结构，让后续 Tool 统一走一个入口。
```

## 执行者

```text
DeepSeek
```

## 前置条件

```text
TASK-000 已完成。
只有在 Codex 明确下发正式开工指令后，DeepSeek 才能开始本任务。
```

## 允许修改文件

```text
- src/YangTools.Revit/App.cs
- src/YangTools.Revit/Mcp/*
- src/YangTools.Revit/Utils/JsonUtils.cs
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
- docs/CURRENT_STATE.md（如状态变化）
```

## 禁止修改文件

```text
- docs/AGENT_RULES.md
- docs/PROJECT_START_PLAN.md
- tasks/TASK-002.md ~ tasks/TASK-009.md
- MCP 基础协议字段名
- 与本任务无关的 Tool 文件
```

## Tool 名称

```text
无
```

## 是否写模型

```text
否
```

## 是否需要 Transaction

```text
否
```

## 是否需要 dryRun

```text
否
```

## 测试要求

```text
1. 能解析 tool 与 args。
2. 空 tool 和未知 tool 返回标准错误。
3. 返回结构固定为 ok / tool / data / error / warnings。
4. 如果涉及后续 Revit API 调用规划，不能越过 ExternalEvent / 主线程边界。
```

## 完成后必须更新

```text
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
- docs/TOOL_INDEX.md，如本任务触发实现状态变化
- docs/CURRENT_STATE.md，如状态变化
- logs/conversations/YYYY-MM-DD.md
```
