# TASK-002

## 任务名称

实现 `ToolRouter` 与 `revit.status` 最小接入

## 执行者

```text
DeepSeek
```

## 目标

```text
根据 request.tool 分发到对应 IRevitTool，所有功能只允许通过 ToolRouter 进入。
```

## 允许修改文件

```text
- src/YangTools.Revit/Mcp/*
- src/YangTools.Revit/Tools/*
- docs/WORKLOG.md
- tasks/HANDOFF_TO_CODEX.md
- logs/conversations/YYYY-MM-DD.md
- docs/CURRENT_STATE.md（如状态变化）
- docs/TOOL_INDEX.md（如 Tool 状态变化）
```

## 验收

1. `revit.status` 能经 ToolRouter 调用。
2. 未注册 tool 返回标准错误。
3. 不新增独立 endpoint。
4. 已更新 `docs/WORKLOG.md`。
5. 已更新 `tasks/HANDOFF_TO_CODEX.md`。
6. 已更新 `logs/conversations/YYYY-MM-DD.md`。
7. 如状态变化，已更新 `docs/CURRENT_STATE.md`。
8. 如 Tool 状态变化，已更新 `docs/TOOL_INDEX.md`。
